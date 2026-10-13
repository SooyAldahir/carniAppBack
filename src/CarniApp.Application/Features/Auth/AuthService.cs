using System.Security.Cryptography;
using System.Text;
using CarniApp.Application.Common.Interfaces;
using CarniApp.Domain.Entities;

namespace CarniApp.Application.Features.Auth;

/// <summary>
/// Orquesta login, refresh y bootstrap del Super Administrador inicial
/// (RF-AUTH-001, 002, 005). No depende de EF Core ni ASP.NET: solo de
/// las abstracciones definidas en Common/Interfaces (D-04).
/// </summary>
public class AuthService : IAuthService
{
    private const int MaxIntentosFallidos = 5;
    private const int MinutosBloqueo = 15;
    private const string NombreRolSuperAdministrador = "Super Administrador";

    private readonly IUsuarioRepository _usuarioRepository;
    private readonly IRefreshTokenRepository _refreshTokenRepository;
    private readonly IRepository<Rol> _rolRepository;
    private readonly IPasswordHasher _passwordHasher;
    private readonly ITokenService _tokenService;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IDateTimeProvider _dateTimeProvider;

    public AuthService(
        IUsuarioRepository usuarioRepository,
        IRefreshTokenRepository refreshTokenRepository,
        IRepository<Rol> rolRepository,
        IPasswordHasher passwordHasher,
        ITokenService tokenService,
        IUnitOfWork unitOfWork,
        IDateTimeProvider dateTimeProvider)
    {
        _usuarioRepository = usuarioRepository;
        _refreshTokenRepository = refreshTokenRepository;
        _rolRepository = rolRepository;
        _passwordHasher = passwordHasher;
        _tokenService = tokenService;
        _unitOfWork = unitOfWork;
        _dateTimeProvider = dateTimeProvider;
    }

    public async Task<AuthResultDto> LoginAsync(LoginRequest request, CancellationToken cancellationToken = default)
    {
        var usuario = await _usuarioRepository.GetByNombreUsuarioConRolYPermisosAsync(request.NombreUsuario, cancellationToken);

        if (usuario is null)
        {
            // Mensaje identico al de password incorrecta: no revelar si el usuario existe.
            return AuthResultDto.Fail(AuthResultCode.CredencialesInvalidas, "Usuario o contraseña incorrectos.");
        }

        if (!usuario.Activo)
        {
            return AuthResultDto.Fail(AuthResultCode.CuentaInactiva, "Esta cuenta esta desactivada. Contacta al administrador.");
        }

        var ahora = _dateTimeProvider.UtcNow;

        if (usuario.BloqueadoHasta.HasValue && usuario.BloqueadoHasta.Value > ahora)
        {
            var minutosRestantes = Math.Ceiling((usuario.BloqueadoHasta.Value - ahora).TotalMinutes);
            return AuthResultDto.Fail(
                AuthResultCode.CuentaBloqueada,
                $"Cuenta bloqueada por intentos fallidos. Intenta de nuevo en {minutosRestantes} minuto(s).");
        }

        if (!_passwordHasher.Verify(request.Password, usuario.PasswordHash))
        {
            usuario.IntentosFallidos++;

            if (usuario.IntentosFallidos >= MaxIntentosFallidos)
            {
                usuario.BloqueadoHasta = ahora.AddMinutes(MinutosBloqueo);
                usuario.IntentosFallidos = 0;
            }

            _usuarioRepository.Update(usuario);
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            return AuthResultDto.Fail(AuthResultCode.CredencialesInvalidas, "Usuario o contraseña incorrectos.");
        }

        usuario.IntentosFallidos = 0;
        usuario.BloqueadoHasta = null;
        _usuarioRepository.Update(usuario);

        var tokens = await EmitirTokensAsync(usuario, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return AuthResultDto.Ok(tokens);
    }

    public async Task<AuthResultDto> RefreshAsync(string refreshTokenPlano, CancellationToken cancellationToken = default)
    {
        var hash = CalcularHash(refreshTokenPlano);
        var tokenGuardado = await _refreshTokenRepository.GetByTokenHashAsync(hash, cancellationToken);
        var ahora = _dateTimeProvider.UtcNow;

        if (tokenGuardado is null)
        {
            return AuthResultDto.Fail(AuthResultCode.TokenInvalido, "Refresh token invalido.");
        }

        if (tokenGuardado.RevocadoUtc is not null)
        {
            // Un token ya revocado se vuelve a presentar: posible robo/reuso.
            // Por seguridad, se revocan TODAS las sesiones del usuario (RF-AUTH-007).
            await _refreshTokenRepository.RevocarTodosDelUsuarioAsync(tokenGuardado.UsuarioId, ahora, cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
            return AuthResultDto.Fail(AuthResultCode.TokenInvalido, "Refresh token invalido. Por seguridad, cerramos todas tus sesiones; vuelve a iniciar sesion.");
        }

        if (tokenGuardado.ExpiraUtc <= ahora)
        {
            return AuthResultDto.Fail(AuthResultCode.TokenInvalido, "Refresh token expirado. Vuelve a iniciar sesion.");
        }

        var usuario = await _usuarioRepository.GetByNombreUsuarioConRolYPermisosAsync(tokenGuardado.Usuario.NombreUsuario, cancellationToken);

        if (usuario is null || !usuario.Activo)
        {
            return AuthResultDto.Fail(AuthResultCode.CuentaInactiva, "Esta cuenta ya no esta activa.");
        }

        // Rotacion: se revoca el token usado y se emite un par nuevo.
        tokenGuardado.RevocadoUtc = ahora;
        _refreshTokenRepository.Update(tokenGuardado);

        var tokens = await EmitirTokensAsync(usuario, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return AuthResultDto.Ok(tokens);
    }

    public async Task<AuthResultDto> BootstrapAdminAsync(BootstrapAdminRequest request, CancellationToken cancellationToken = default)
    {
        // Solo funciona en instalacion nueva (sin ningun usuario todavia), para
        // no dejar un endpoint de "crear super admin" abierto indefinidamente.
        if (await _usuarioRepository.ExisteAlgunUsuarioAsync(cancellationToken))
        {
            return AuthResultDto.Fail(
                AuthResultCode.YaExisteAlMenosUnUsuario,
                "Ya existe al menos un usuario en el sistema. Pide a un Super Administrador que te de de alta.");
        }

        var roles = await _rolRepository.GetAllAsync(cancellationToken);
        var rolSuperAdmin = roles.FirstOrDefault(r => r.Nombre == NombreRolSuperAdministrador);

        if (rolSuperAdmin is null)
        {
            return AuthResultDto.Fail(
                AuthResultCode.RolSuperAdministradorNoConfigurado,
                "No existe el rol 'Super Administrador'. Aplica primero el script 003_seed_data.sql.");
        }

        var usuario = new Usuario
        {
            NombreCompleto = request.NombreCompleto,
            NombreUsuario = request.NombreUsuario,
            Correo = request.Correo,
            PasswordHash = _passwordHasher.Hash(request.Password),
            RolId = rolSuperAdmin.Id
        };

        await _usuarioRepository.AddAsync(usuario, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        // Releer con Rol+Permisos cargados para armar el JWT.
        var usuarioCompleto = await _usuarioRepository.GetByNombreUsuarioConRolYPermisosAsync(usuario.NombreUsuario, cancellationToken)
            ?? usuario;

        var tokens = await EmitirTokensAsync(usuarioCompleto, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return AuthResultDto.Ok(tokens);
    }

    public async Task LogoutAsync(string refreshTokenPlano, CancellationToken cancellationToken = default)
    {
        var hash = CalcularHash(refreshTokenPlano);
        var tokenGuardado = await _refreshTokenRepository.GetByTokenHashAsync(hash, cancellationToken);

        if (tokenGuardado is not null && tokenGuardado.RevocadoUtc is null)
        {
            tokenGuardado.RevocadoUtc = _dateTimeProvider.UtcNow;
            _refreshTokenRepository.Update(tokenGuardado);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }
    }

    private async Task<AuthTokensDto> EmitirTokensAsync(Usuario usuario, CancellationToken cancellationToken)
    {
        var permisos = usuario.Rol.RolPermisos.Select(rp => rp.Permiso.Clave).ToList();

        var accessToken = _tokenService.GenerarAccessToken(usuario, permisos);
        var refreshPlano = _tokenService.GenerarRefreshTokenPlano();

        var refreshToken = new RefreshToken
        {
            UsuarioId = usuario.Id,
            TokenHash = CalcularHash(refreshPlano),
            ExpiraUtc = _tokenService.CalcularExpiracionRefreshToken()
        };

        await _refreshTokenRepository.AddAsync(refreshToken, cancellationToken);

        var usuarioDto = new UsuarioResumenDto(
            usuario.Id,
            usuario.NombreCompleto,
            usuario.NombreUsuario,
            usuario.Correo,
            usuario.Rol.Nombre,
            permisos);

        return new AuthTokensDto(accessToken.Token, accessToken.ExpiraUtc, refreshPlano, usuarioDto);
    }

    private static string CalcularHash(string valor)
    {
        var bytes = Encoding.UTF8.GetBytes(valor);
        var hash = SHA256.HashData(bytes);
        return Convert.ToHexString(hash);
    }
}
