using CarniApp.Application.Features.Auth;
using CarniApp.Application.Features.Usuarios;
using CarniApp.Domain.Entities;
using CarniApp.Infrastructure.Persistence;
using CarniApp.Infrastructure.Security;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace CarniApp.Tests;

/// <summary>Alta, edicion, baja y contrasenas del personal (cajeros incluidos).</summary>
public class UsuarioServiceTests : IDisposable
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

    private Guid _rolSuperAdmin;
    private Guid _rolCajera;
    private Guid _admin;

    public UsuarioServiceTests()
    {
        using var ctx = _db.NuevoContexto();
        var superAdmin = new Rol { Nombre = "Super Administrador", Descripcion = "Control total" };
        var cajera = new Rol { Nombre = "Cajera", Descripcion = "Ventas y caja" };
        var pos = new Permiso { Clave = "pos.vender" };
        var usuarios = new Permiso { Clave = "usuarios.administrar" };
        ctx.Roles.AddRange(superAdmin, cajera);
        ctx.Permisos.AddRange(pos, usuarios);
        ctx.RolPermisos.AddRange(
            new RolPermiso { RolId = superAdmin.Id, PermisoId = pos.Id },
            new RolPermiso { RolId = superAdmin.Id, PermisoId = usuarios.Id },
            new RolPermiso { RolId = cajera.Id, PermisoId = pos.Id });
        var admin = new Usuario
        {
            NombreCompleto = "Dueno", NombreUsuario = "dueno", PasswordHash = _hasher.Hash(Password), RolId = superAdmin.Id
        };
        ctx.Usuarios.Add(admin);
        ctx.SaveChanges();
        _rolSuperAdmin = superAdmin.Id;
        _rolCajera = cajera.Id;
        _admin = admin.Id;
    }

    public void Dispose() => _db.Dispose();

    private UsuarioService Servicio(CarniAppDbContext ctx) =>
        new(new UsuarioRepository(ctx), new RefreshTokenRepository(ctx), _hasher, new UnitOfWork(ctx), _clock);

    private AuthService Auth(CarniAppDbContext ctx) => new(
        new UsuarioRepository(ctx), new RefreshTokenRepository(ctx), new Repository<Rol>(ctx),
        _hasher, new TokenService(_config), new UnitOfWork(ctx), _clock);

    private async Task<UsuarioResultDto<UsuarioAdminDto>> Crear(string nombreUsuario = "ana", Guid? rol = null, string password = Password)
    {
        using var ctx = _db.NuevoContexto();
        return await Servicio(ctx).CrearAsync(new CrearUsuarioRequest("Ana Perez", nombreUsuario, "ana@correo.com", password, rol ?? _rolCajera));
    }

    private async Task<AuthResultDto> Login(string usuario, string password)
    {
        using var ctx = _db.NuevoContexto();
        return await Auth(ctx).LoginAsync(new LoginRequest(usuario, password));
    }

    private async Task<UsuarioResultDto<UsuarioAdminDto>> Actualizar(Guid id, string nombre, Guid rol, bool activo, Guid? actor = null)
    {
        using var ctx = _db.NuevoContexto();
        return await Servicio(ctx).ActualizarAsync(id, new ActualizarUsuarioRequest(nombre, null, rol, activo), actor ?? _admin);
    }

    [Fact]
    public async Task Crear_UnaCajera_PuedeIniciarSesion_YSuClaveQuedaCifrada()
    {
        var r = await Crear();

        Assert.True(r.Exitoso, r.Mensaje);
        Assert.Equal("Cajera", r.Datos!.Rol);
        Assert.True(r.Datos.Activo);
        Assert.False(r.Datos.Bloqueado);

        using (var ctx = _db.NuevoContexto())
        {
            var guardado = await ctx.Usuarios.SingleAsync(u => u.NombreUsuario == "ana");
            Assert.NotEqual(Password, guardado.PasswordHash);
        }

        var login = await Login("ana", Password);
        Assert.True(login.Exitoso);
        Assert.Contains("pos.vender", login.Datos!.Usuario.Permisos);
        Assert.DoesNotContain("usuarios.administrar", login.Datos.Usuario.Permisos);
    }

    [Fact]
    public async Task Crear_NoPermiteRepetirElUsuario_AunqueCambieLaCapitalizacion()
    {
        Assert.True((await Crear("ana")).Exitoso);

        var repetido = await Crear("ANA");

        Assert.Equal(UsuarioResultCode.NombreUsuarioDuplicado, repetido.Code);
    }

    [Theory]
    [InlineData("ab", Password)]            // muy corto
    [InlineData("ana perez", Password)]     // espacios
    [InlineData("ana", "corta")]            // contrasena corta
    [InlineData("ana", "")]                 // contrasena vacia
    public async Task Crear_ConDatosInvalidos_SeRechaza(string usuario, string password)
    {
        var r = await Crear(usuario, password: password);
        Assert.Equal(UsuarioResultCode.Invalido, r.Code);
    }

    [Fact]
    public async Task Crear_ConRolInexistenteONombreVacio_SeRechaza()
    {
        Assert.Equal(UsuarioResultCode.Invalido, (await Crear("ana", Guid.NewGuid())).Code);

        using var ctx = _db.NuevoContexto();
        var sinNombre = await Servicio(ctx).CrearAsync(new CrearUsuarioRequest("  ", "ana", null, Password, _rolCajera));
        Assert.Equal(UsuarioResultCode.Invalido, sinNombre.Code);
    }

    [Fact]
    public async Task Listar_IncluyeInactivos_YNuncaLaContrasena()
    {
        var ana = (await Crear("ana")).Datos!;
        await Actualizar(ana.Id, "Ana Perez", _rolCajera, activo: false);

        using var ctx = _db.NuevoContexto();
        var lista = await Servicio(ctx).ListarAsync();

        Assert.Equal(2, lista.Count);
        Assert.False(lista.Single(u => u.NombreUsuario == "ana").Activo);
        Assert.DoesNotContain(typeof(UsuarioAdminDto).GetProperties(), p => p.Name.Contains("Password", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ListarRoles_TraeCadaRolConSusPermisos()
    {
        using var ctx = _db.NuevoContexto();
        var roles = await Servicio(ctx).ListarRolesAsync();

        Assert.Equal(new[] { "Cajera", "Super Administrador" }, roles.Select(r => r.Nombre).ToArray());
        Assert.Equal(new[] { "pos.vender" }, roles.Single(r => r.Nombre == "Cajera").Permisos);
        Assert.Contains("usuarios.administrar", roles.Single(r => r.Nombre == "Super Administrador").Permisos);
    }

    [Fact]
    public async Task Actualizar_CambiaNombreYRol()
    {
        var ana = (await Crear("ana")).Datos!;

        var r = await Actualizar(ana.Id, "Ana Maria Perez", _rolSuperAdmin, activo: true);

        Assert.True(r.Exitoso, r.Mensaje);
        Assert.Equal("Ana Maria Perez", r.Datos!.NombreCompleto);
        Assert.Equal("Super Administrador", r.Datos.Rol);
    }

    [Fact]
    public async Task Desactivar_UnaCajera_LeImpideEntrar_YCierraSusSesiones()
    {
        var ana = (await Crear("ana")).Datos!;
        var sesion = (await Login("ana", Password)).Datos!;

        var r = await Actualizar(ana.Id, "Ana Perez", _rolCajera, activo: false);
        Assert.True(r.Exitoso, r.Mensaje);

        Assert.Equal(AuthResultCode.CuentaInactiva, (await Login("ana", Password)).Code);
        using var ctx = _db.NuevoContexto();
        var renovar = await Auth(ctx).RefreshAsync(sesion.RefreshToken);
        Assert.False(renovar.Exitoso, "el refresh token debio quedar revocado");
    }

    [Fact]
    public async Task Reactivar_LaDejaEntrarOtraVez()
    {
        var ana = (await Crear("ana")).Datos!;
        await Actualizar(ana.Id, "Ana Perez", _rolCajera, activo: false);

        await Actualizar(ana.Id, "Ana Perez", _rolCajera, activo: true);

        Assert.True((await Login("ana", Password)).Exitoso);
    }

    [Fact]
    public async Task Nadie_PuedeDesactivarseNiCambiarseElRolASiMismo()
    {
        var aMismo = await Actualizar(_admin, "Dueno", _rolSuperAdmin, activo: false, actor: _admin);
        Assert.Equal(UsuarioResultCode.NoPermitido, aMismo.Code);

        var rol = await Actualizar(_admin, "Dueno", _rolCajera, activo: true, actor: _admin);
        Assert.Equal(UsuarioResultCode.NoPermitido, rol.Code);
    }

    [Fact]
    public async Task Siempre_QuedaAlMenosUnSuperAdministradorActivo()
    {
        // Un solo Super Administrador (el dueno): otro actor no puede quitarlo.
        var otroActor = Guid.NewGuid();
        var r1 = await Actualizar(_admin, "Dueno", _rolSuperAdmin, activo: false, actor: otroActor);
        var r2 = await Actualizar(_admin, "Dueno", _rolCajera, activo: true, actor: otroActor);
        Assert.Equal(UsuarioResultCode.NoPermitido, r1.Code);
        Assert.Equal(UsuarioResultCode.NoPermitido, r2.Code);

        // Con un segundo Super Administrador ya se puede quitar al primero, pero no al ultimo.
        var segundo = (await Crear("socio", _rolSuperAdmin)).Datos!;
        Assert.True((await Actualizar(_admin, "Dueno", _rolCajera, activo: true, actor: segundo.Id)).Exitoso);
        var ultimo = await Actualizar(segundo.Id, "Ana Perez", _rolCajera, activo: true, actor: otroActor);
        Assert.Equal(UsuarioResultCode.NoPermitido, ultimo.Code);
    }

    [Fact]
    public async Task Actualizar_UsuarioInexistente_NoSeEncuentra()
    {
        Assert.Equal(UsuarioResultCode.NoEncontrado, (await Actualizar(Guid.NewGuid(), "X", _rolCajera, true)).Code);
    }

    [Fact]
    public async Task RestablecerPassword_LaNuevaFunciona_LaVieja_No_YSeQuitaElBloqueo()
    {
        var ana = (await Crear("ana")).Datos!;
        for (var i = 0; i < 5; i++) await Login("ana", "mala-contrasena");
        Assert.Equal(AuthResultCode.CuentaBloqueada, (await Login("ana", Password)).Code);

        using (var ctx = _db.NuevoContexto())
        {
            var r = await Servicio(ctx).RestablecerPasswordAsync(ana.Id, "NuevaClave#456");
            Assert.True(r.Exitoso, r.Mensaje);
        }

        Assert.True((await Login("ana", "NuevaClave#456")).Exitoso);
        Assert.False((await Login("ana", Password)).Exitoso);
    }

    [Fact]
    public async Task RestablecerPassword_ValidaLongitud_YExistencia()
    {
        var ana = (await Crear("ana")).Datos!;
        using var ctx = _db.NuevoContexto();
        var servicio = Servicio(ctx);

        Assert.Equal(UsuarioResultCode.Invalido, (await servicio.RestablecerPasswordAsync(ana.Id, "corta")).Code);
        Assert.Equal(UsuarioResultCode.NoEncontrado, (await servicio.RestablecerPasswordAsync(Guid.NewGuid(), "NuevaClave#456")).Code);
    }

    [Fact]
    public async Task Desbloquear_PermiteEntrarDeInmediato()
    {
        var ana = (await Crear("ana")).Datos!;
        for (var i = 0; i < 5; i++) await Login("ana", "mala-contrasena");
        Assert.Equal(AuthResultCode.CuentaBloqueada, (await Login("ana", Password)).Code);

        using (var ctx = _db.NuevoContexto())
        {
            var r = await Servicio(ctx).DesbloquearAsync(ana.Id);
            Assert.True(r.Exitoso);
            Assert.False(r.Datos!.Bloqueado);
        }

        Assert.True((await Login("ana", Password)).Exitoso);
    }

    [Fact]
    public async Task Listar_MarcaComoBloqueadoALaCuentaConIntentosFallidos()
    {
        await Crear("ana");
        for (var i = 0; i < 5; i++) await Login("ana", "mala-contrasena");

        using var ctx = _db.NuevoContexto();
        var lista = await Servicio(ctx).ListarAsync();

        Assert.True(lista.Single(u => u.NombreUsuario == "ana").Bloqueado);
    }

    [Fact]
    public async Task CambiarPasswordPropia_PideLaActual_ExigeUnaNuevaDistinta_YCierraSesiones()
    {
        var ana = (await Crear("ana")).Datos!;
        var sesion = (await Login("ana", Password)).Datos!;

        using (var ctx = _db.NuevoContexto())
        {
            var servicio = Servicio(ctx);
            Assert.Equal(UsuarioResultCode.PasswordIncorrecta,
                (await servicio.CambiarPasswordPropiaAsync(ana.Id, new CambiarPasswordRequest("equivocada", "NuevaClave#456"))).Code);
            Assert.Equal(UsuarioResultCode.Invalido,
                (await servicio.CambiarPasswordPropiaAsync(ana.Id, new CambiarPasswordRequest(Password, Password))).Code);
            Assert.Equal(UsuarioResultCode.Invalido,
                (await servicio.CambiarPasswordPropiaAsync(ana.Id, new CambiarPasswordRequest(Password, "corta"))).Code);
        }

        using (var ctx = _db.NuevoContexto())
        {
            var ok = await Servicio(ctx).CambiarPasswordPropiaAsync(ana.Id, new CambiarPasswordRequest(Password, "NuevaClave#456"));
            Assert.True(ok.Exitoso, ok.Mensaje);
        }

        Assert.True((await Login("ana", "NuevaClave#456")).Exitoso);
        Assert.False((await Login("ana", Password)).Exitoso);
        using var ctx2 = _db.NuevoContexto();
        Assert.False((await Auth(ctx2).RefreshAsync(sesion.RefreshToken)).Exitoso, "la sesion anterior debio cerrarse");
    }
}
