using System.Text.RegularExpressions;
using CarniApp.Application.Common.Interfaces;
using CarniApp.Domain.Entities;

namespace CarniApp.Application.Features.Usuarios;

/// <summary>
/// Administracion del personal (cajeros y demas): alta, edicion, baja logica, rol, contrasena.
/// Reglas de seguridad: nadie se desactiva ni se cambia el rol a si mismo, y siempre debe quedar
/// al menos un Super Administrador activo. Al desactivar, cambiar de rol o cambiar la contrasena
/// se cierran las sesiones del usuario (sus refresh tokens), asi no sigue entrando con lo anterior.
/// </summary>
public class UsuarioService : IUsuarioService
{
    public const string NombreRolSuperAdministrador = "Super Administrador";
    public const int PasswordMinimo = 8;
    private const int PasswordMaximo = 100;
    private static readonly Regex NombreUsuarioValido = new("^[A-Za-z0-9._-]{3,50}$", RegexOptions.Compiled);

    private readonly IUsuarioRepository _usuarios;
    private readonly IRefreshTokenRepository _refreshTokens;
    private readonly IPasswordHasher _hasher;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IDateTimeProvider _reloj;

    public UsuarioService(
        IUsuarioRepository usuarios,
        IRefreshTokenRepository refreshTokens,
        IPasswordHasher hasher,
        IUnitOfWork unitOfWork,
        IDateTimeProvider reloj)
    {
        _usuarios = usuarios;
        _refreshTokens = refreshTokens;
        _hasher = hasher;
        _unitOfWork = unitOfWork;
        _reloj = reloj;
    }

    public async Task<IReadOnlyList<UsuarioAdminDto>> ListarAsync(CancellationToken cancellationToken = default) =>
        (await _usuarios.ListarConRolAsync(cancellationToken)).Select(Mapear).ToList();

    public async Task<IReadOnlyList<RolAdminDto>> ListarRolesAsync(CancellationToken cancellationToken = default) =>
        (await _usuarios.ListarRolesConPermisosAsync(cancellationToken))
            .Select(r => new RolAdminDto(
                r.Id,
                r.Nombre,
                r.Descripcion,
                r.RolPermisos.Select(rp => rp.Permiso.Clave).OrderBy(c => c).ToList()))
            .ToList();

    public async Task<UsuarioResultDto<UsuarioAdminDto>> CrearAsync(CrearUsuarioRequest request, CancellationToken cancellationToken = default)
    {
        var error = ValidarDatos(request.NombreCompleto, request.Correo) ?? ValidarNombreUsuario(request.NombreUsuario) ?? ValidarPassword(request.Password);
        if (error is not null) return Fallo<UsuarioAdminDto>(UsuarioResultCode.Invalido, error);

        var rol = await _usuarios.GetRolAsync(request.RolId, cancellationToken);
        if (rol is null) return Fallo<UsuarioAdminDto>(UsuarioResultCode.Invalido, "Elige un rol valido.");

        var nombreUsuario = request.NombreUsuario.Trim();
        if (await _usuarios.ExisteNombreUsuarioAsync(nombreUsuario, null, cancellationToken))
            return Fallo<UsuarioAdminDto>(UsuarioResultCode.NombreUsuarioDuplicado, $"El usuario \"{nombreUsuario}\" ya existe. Elige otro.");

        var usuario = new Usuario
        {
            NombreCompleto = request.NombreCompleto.Trim(),
            NombreUsuario = nombreUsuario,
            Correo = Limpiar(request.Correo),
            PasswordHash = _hasher.Hash(request.Password),
            RolId = rol.Id,
            FechaCreacion = _reloj.UtcNow
        };
        await _usuarios.AddAsync(usuario, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        usuario.Rol = rol;
        return UsuarioResultDto<UsuarioAdminDto>.Ok(Mapear(usuario));
    }

    public async Task<UsuarioResultDto<UsuarioAdminDto>> ActualizarAsync(Guid id, ActualizarUsuarioRequest request, Guid actorId, CancellationToken cancellationToken = default)
    {
        var usuario = await _usuarios.GetByIdConRolAsync(id, cancellationToken);
        if (usuario is null) return Fallo<UsuarioAdminDto>(UsuarioResultCode.NoEncontrado, "No se encontro el usuario.");

        var error = ValidarDatos(request.NombreCompleto, request.Correo);
        if (error is not null) return Fallo<UsuarioAdminDto>(UsuarioResultCode.Invalido, error);

        var rol = await _usuarios.GetRolAsync(request.RolId, cancellationToken);
        if (rol is null) return Fallo<UsuarioAdminDto>(UsuarioResultCode.Invalido, "Elige un rol valido.");

        var esUnoMismo = usuario.Id == actorId;
        var cambiaRol = usuario.RolId != rol.Id;
        var seDesactiva = usuario.Activo && !request.Activo;

        if (esUnoMismo && seDesactiva)
            return Fallo<UsuarioAdminDto>(UsuarioResultCode.NoPermitido, "No puedes desactivar tu propia cuenta.");
        if (esUnoMismo && cambiaRol)
            return Fallo<UsuarioAdminDto>(UsuarioResultCode.NoPermitido, "No puedes cambiar tu propio rol. Pidele a otro administrador que lo haga.");

        // Dejaria al sistema sin Super Administrador activo?
        var eraSuperAdminActivo = usuario.Activo && usuario.Rol.Nombre == NombreRolSuperAdministrador;
        var seguiraSiendoSuperAdminActivo = request.Activo && rol.Nombre == NombreRolSuperAdministrador;
        if (eraSuperAdminActivo && !seguiraSiendoSuperAdminActivo &&
            await _usuarios.ContarActivosPorRolAsync(usuario.RolId, usuario.Id, cancellationToken) == 0)
        {
            return Fallo<UsuarioAdminDto>(UsuarioResultCode.NoPermitido, "Debe quedar al menos un Super Administrador activo.");
        }

        usuario.NombreCompleto = request.NombreCompleto.Trim();
        usuario.Correo = Limpiar(request.Correo);
        usuario.RolId = rol.Id;
        usuario.Rol = rol;
        usuario.Activo = request.Activo;
        usuario.FechaActualizacion = _reloj.UtcNow;

        if (seDesactiva || cambiaRol)
            await _refreshTokens.RevocarTodosDelUsuarioAsync(usuario.Id, _reloj.UtcNow, cancellationToken);

        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return UsuarioResultDto<UsuarioAdminDto>.Ok(Mapear(usuario));
    }

    public async Task<UsuarioResultDto<bool>> RestablecerPasswordAsync(Guid id, string nuevaPassword, CancellationToken cancellationToken = default)
    {
        var usuario = await _usuarios.GetByIdAsync(id, cancellationToken);
        if (usuario is null) return Fallo<bool>(UsuarioResultCode.NoEncontrado, "No se encontro el usuario.");

        var error = ValidarPassword(nuevaPassword);
        if (error is not null) return Fallo<bool>(UsuarioResultCode.Invalido, error);

        usuario.PasswordHash = _hasher.Hash(nuevaPassword);
        usuario.IntentosFallidos = 0;
        usuario.BloqueadoHasta = null;
        usuario.FechaActualizacion = _reloj.UtcNow;
        await _refreshTokens.RevocarTodosDelUsuarioAsync(usuario.Id, _reloj.UtcNow, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return UsuarioResultDto<bool>.Ok(true);
    }

    public async Task<UsuarioResultDto<UsuarioAdminDto>> DesbloquearAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var usuario = await _usuarios.GetByIdConRolAsync(id, cancellationToken);
        if (usuario is null) return Fallo<UsuarioAdminDto>(UsuarioResultCode.NoEncontrado, "No se encontro el usuario.");

        usuario.IntentosFallidos = 0;
        usuario.BloqueadoHasta = null;
        usuario.FechaActualizacion = _reloj.UtcNow;
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return UsuarioResultDto<UsuarioAdminDto>.Ok(Mapear(usuario));
    }

    public async Task<UsuarioResultDto<bool>> CambiarPasswordPropiaAsync(Guid usuarioId, CambiarPasswordRequest request, CancellationToken cancellationToken = default)
    {
        var usuario = await _usuarios.GetByIdAsync(usuarioId, cancellationToken);
        if (usuario is null || !usuario.Activo) return Fallo<bool>(UsuarioResultCode.NoEncontrado, "No se encontro el usuario.");

        if (string.IsNullOrEmpty(request.PasswordActual) || !_hasher.Verify(request.PasswordActual, usuario.PasswordHash))
            return Fallo<bool>(UsuarioResultCode.PasswordIncorrecta, "La contrasena actual no es correcta.");

        var error = ValidarPassword(request.PasswordNueva);
        if (error is not null) return Fallo<bool>(UsuarioResultCode.Invalido, error);
        if (request.PasswordNueva == request.PasswordActual)
            return Fallo<bool>(UsuarioResultCode.Invalido, "La contrasena nueva debe ser distinta de la actual.");

        usuario.PasswordHash = _hasher.Hash(request.PasswordNueva);
        usuario.FechaActualizacion = _reloj.UtcNow;
        await _refreshTokens.RevocarTodosDelUsuarioAsync(usuario.Id, _reloj.UtcNow, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return UsuarioResultDto<bool>.Ok(true);
    }

    // ---- Validaciones

    private static string? ValidarDatos(string nombreCompleto, string? correo)
    {
        if (string.IsNullOrWhiteSpace(nombreCompleto)) return "El nombre completo es obligatorio.";
        if (nombreCompleto.Trim().Length > 150) return "El nombre completo no puede pasar de 150 caracteres.";
        var c = Limpiar(correo);
        if (c is not null && (c.Length > 150 || !c.Contains('@') || c.Contains(' '))) return "El correo no es valido.";
        return null;
    }

    private static string? ValidarNombreUsuario(string nombreUsuario) =>
        NombreUsuarioValido.IsMatch((nombreUsuario ?? string.Empty).Trim())
            ? null
            : "El usuario debe tener de 3 a 50 caracteres: letras, numeros, punto, guion o guion bajo (sin espacios).";

    private static string? ValidarPassword(string? password)
    {
        if (string.IsNullOrEmpty(password) || password.Length < PasswordMinimo)
            return $"La contrasena debe tener al menos {PasswordMinimo} caracteres.";
        if (password.Length > PasswordMaximo) return $"La contrasena no puede pasar de {PasswordMaximo} caracteres.";
        return null;
    }

    private static string? Limpiar(string? texto) => string.IsNullOrWhiteSpace(texto) ? null : texto.Trim();

    private static UsuarioResultDto<T> Fallo<T>(UsuarioResultCode code, string mensaje) => UsuarioResultDto<T>.Fail(code, mensaje);

    private UsuarioAdminDto Mapear(Usuario u) => new(
        u.Id,
        u.NombreCompleto,
        u.NombreUsuario,
        u.Correo,
        u.RolId,
        u.Rol?.Nombre ?? string.Empty,
        u.Activo,
        u.BloqueadoHasta.HasValue && u.BloqueadoHasta.Value > _reloj.UtcNow,
        u.FechaCreacion);
}
