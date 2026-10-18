using CarniApp.Application.Features.Auth;
using CarniApp.Domain.Entities;
using CarniApp.Infrastructure.Persistence;
using CarniApp.Infrastructure.Security;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace CarniApp.Tests;

public class AuthServiceTests : IDisposable
{
    private const string Password = "Secreta#123";

    private readonly TestDb _db = new();
    private readonly FakeClock _clock = new();
    private readonly PasswordHasher _hasher = new();
    private readonly IConfiguration _config = new ConfigurationBuilder()
        .AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Jwt:Key"] = "clave-de-pruebas-con-mas-de-32-bytes-0123456789",
            ["Jwt:Issuer"] = "CarniApp.Tests",
            ["Jwt:Audience"] = "CarniApp.Tests",
            ["Jwt:AccessTokenMinutes"] = "20",
            ["Jwt:RefreshTokenDays"] = "14"
        })
        .Build();

    public void Dispose() => _db.Dispose();

    private AuthService CrearServicio(CarniAppDbContext ctx) => new(
        new UsuarioRepository(ctx),
        new RefreshTokenRepository(ctx),
        new Repository<Rol>(ctx),
        _hasher,
        new TokenService(_config),
        new UnitOfWork(ctx),
        _clock);

    private void SembrarRolSuperAdmin()
    {
        using var ctx = _db.NuevoContexto();
        var rol = new Rol { Nombre = "Super Administrador" };
        var permiso = new Permiso { Clave = "catalogo.administrar" };
        ctx.Roles.Add(rol);
        ctx.Permisos.Add(permiso);
        ctx.RolPermisos.Add(new RolPermiso { RolId = rol.Id, PermisoId = permiso.Id });
        ctx.SaveChanges();
    }

    private async Task<AuthResultDto> Bootstrap(string usuario = "admin")
    {
        using var ctx = _db.NuevoContexto();
        return await CrearServicio(ctx).BootstrapAdminAsync(new BootstrapAdminRequest("Admin", usuario, null, Password));
    }

    private async Task<AuthResultDto> Login(string usuario, string password)
    {
        using var ctx = _db.NuevoContexto();
        return await CrearServicio(ctx).LoginAsync(new LoginRequest(usuario, password));
    }

    private async Task<AuthResultDto> Refresh(string token)
    {
        using var ctx = _db.NuevoContexto();
        return await CrearServicio(ctx).RefreshAsync(token);
    }

    [Fact]
    public async Task Bootstrap_SinRol_FallaConCodigoClaro()
    {
        var r = await Bootstrap();
        Assert.Equal(AuthResultCode.RolSuperAdministradorNoConfigurado, r.Code);
    }

    [Fact]
    public async Task Bootstrap_CreaSuperAdmin_ConPermisosEnElToken_YSoloUnaVez()
    {
        SembrarRolSuperAdmin();

        var r = await Bootstrap();
        Assert.True(r.Exitoso, r.Mensaje);
        Assert.Equal("Super Administrador", r.Datos!.Usuario.Rol);
        Assert.Contains("catalogo.administrar", r.Datos.Usuario.Permisos);
        Assert.False(string.IsNullOrEmpty(r.Datos.AccessToken));

        var segundo = await Bootstrap("otro");
        Assert.Equal(AuthResultCode.YaExisteAlMenosUnUsuario, segundo.Code);
    }

    [Fact]
    public async Task Login_Correcto_DevuelveTokens_YGuardaSoloElHashDelRefresh()
    {
        SembrarRolSuperAdmin();
        await Bootstrap();

        var r = await Login("admin", Password);

        Assert.True(r.Exitoso, r.Mensaje);
        using var ctx = _db.NuevoContexto();
        var guardados = await ctx.RefreshTokens.ToListAsync();
        Assert.DoesNotContain(guardados, t => t.TokenHash == r.Datos!.RefreshToken);
    }

    [Fact]
    public async Task Login_UsuarioInexistente_YPasswordMala_DanElMismoMensaje()
    {
        SembrarRolSuperAdmin();
        await Bootstrap();

        var noExiste = await Login("fantasma", Password);
        var mala = await Login("admin", "incorrecta");

        Assert.Equal(AuthResultCode.CredencialesInvalidas, noExiste.Code);
        Assert.Equal(AuthResultCode.CredencialesInvalidas, mala.Code);
        Assert.Equal(noExiste.Mensaje, mala.Mensaje);
    }

    [Fact]
    public async Task CincoFallos_BloqueanLaCuenta_AunConPasswordCorrecta_Y15MinutosDespuesDesbloquea()
    {
        SembrarRolSuperAdmin();
        await Bootstrap();

        for (var i = 0; i < 5; i++)
            Assert.Equal(AuthResultCode.CredencialesInvalidas, (await Login("admin", "mala")).Code);

        Assert.Equal(AuthResultCode.CuentaBloqueada, (await Login("admin", Password)).Code);

        _clock.Avanzar(TimeSpan.FromMinutes(14));
        Assert.Equal(AuthResultCode.CuentaBloqueada, (await Login("admin", Password)).Code);

        _clock.Avanzar(TimeSpan.FromMinutes(2));
        Assert.True((await Login("admin", Password)).Exitoso);
    }

    [Fact]
    public async Task LoginExitoso_ReiniciaElContadorDeFallos()
    {
        SembrarRolSuperAdmin();
        await Bootstrap();

        for (var i = 0; i < 4; i++) await Login("admin", "mala");
        Assert.True((await Login("admin", Password)).Exitoso);
        for (var i = 0; i < 4; i++) await Login("admin", "mala");

        // 4 + (reinicio) + 4 fallos: nunca llego a 5 seguidos, no debe estar bloqueada.
        Assert.True((await Login("admin", Password)).Exitoso);
    }

    [Fact]
    public async Task CuentaInactiva_NoPuedeEntrar()
    {
        SembrarRolSuperAdmin();
        await Bootstrap();
        using (var ctx = _db.NuevoContexto())
        {
            var u = await ctx.Usuarios.SingleAsync();
            u.Activo = false;
            await ctx.SaveChangesAsync();
        }

        Assert.Equal(AuthResultCode.CuentaInactiva, (await Login("admin", Password)).Code);
    }

    [Fact]
    public async Task Refresh_RotaElToken_ElViejoYaNoSirve()
    {
        SembrarRolSuperAdmin();
        var inicial = (await Bootstrap()).Datos!;

        var rotado = await Refresh(inicial.RefreshToken);

        Assert.True(rotado.Exitoso, rotado.Mensaje);
        Assert.NotEqual(inicial.RefreshToken, rotado.Datos!.RefreshToken);
        Assert.True((await Refresh(rotado.Datos.RefreshToken)).Exitoso);
    }

    [Fact]
    public async Task Refresh_ReusarUnTokenYaRevocado_RevocaTodasLasSesiones()
    {
        SembrarRolSuperAdmin();
        var inicial = (await Bootstrap()).Datos!;
        var rotado = (await Refresh(inicial.RefreshToken)).Datos!;

        var reuso = await Refresh(inicial.RefreshToken);   // replay del token viejo
        Assert.Equal(AuthResultCode.TokenInvalido, reuso.Code);

        // El token "bueno" mas reciente tambien queda invalidado.
        Assert.Equal(AuthResultCode.TokenInvalido, (await Refresh(rotado.RefreshToken)).Code);
    }

    [Fact]
    public async Task Refresh_TokenDesconocidoOExpirado_Falla()
    {
        SembrarRolSuperAdmin();
        var inicial = (await Bootstrap()).Datos!;

        Assert.Equal(AuthResultCode.TokenInvalido, (await Refresh("no-existe")).Code);

        _clock.Avanzar(TimeSpan.FromDays(15));
        Assert.Equal(AuthResultCode.TokenInvalido, (await Refresh(inicial.RefreshToken)).Code);
    }

    [Fact]
    public async Task Logout_RevocaElRefreshToken()
    {
        SembrarRolSuperAdmin();
        var inicial = (await Bootstrap()).Datos!;

        using (var ctx = _db.NuevoContexto())
            await CrearServicio(ctx).LogoutAsync(inicial.RefreshToken);

        Assert.Equal(AuthResultCode.TokenInvalido, (await Refresh(inicial.RefreshToken)).Code);
    }
}
