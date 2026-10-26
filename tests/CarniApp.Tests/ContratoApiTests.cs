using System.Reflection;
using CarniApp.Api.Controllers;
using CarniApp.Api.Security;
using CarniApp.Application.Features.Ventas;
using CarniApp.Infrastructure;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace CarniApp.Tests;

/// <summary>
/// Pruebas de contrato del API (sin levantar el servidor): seguridad de cada endpoint,
/// permisos que existen en los scripts SQL y servicios registrados en el contenedor de DI.
/// Atrapan el olvido de un [PermisoAuthorize], una clave de permiso mal escrita o un
/// servicio nuevo que nadie registro en DependencyInjection.
/// </summary>
public class ContratoApiTests
{
    /// <summary>Unicos endpoints que pueden llamarse sin sesion.</summary>
    private static readonly HashSet<string> Anonimos = new()
    {
        "AuthController.Login",
        "AuthController.Refresh",
        "AuthController.BootstrapAdmin",
        "HealthController.Get"
    };

    /// <summary>Escrituras que solo piden estar autenticado (cerrar la propia sesion).</summary>
    private static readonly HashSet<string> EscriturasSoloAutenticadas = new() { "AuthController.Logout", "AuthController.CambiarPassword" };

    private static IEnumerable<Type> Controllers() =>
        typeof(CajaController).Assembly.GetTypes()
            .Where(t => typeof(ControllerBase).IsAssignableFrom(t) && !t.IsAbstract);

    private static IEnumerable<(Type Controller, MethodInfo Accion)> Acciones() =>
        Controllers().SelectMany(c => c
            .GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
            .Where(m => m.GetCustomAttributes<HttpMethodAttribute>(true).Any())
            .Select(m => (c, m)));

    private static string Nombre((Type Controller, MethodInfo Accion) a) => $"{a.Controller.Name}.{a.Accion.Name}";

    [Fact]
    public void ElProyectoTieneControllersYAcciones()
    {
        Assert.True(Controllers().Count() >= 8);
        Assert.True(Acciones().Count() >= 40);
    }

    [Fact]
    public void TodoEndpoint_ExigeSesion_SalvoLosAnonimosPermitidos()
    {
        var sinProteger = new List<string>();
        foreach (var a in Acciones())
        {
            var nombre = Nombre(a);
            var anonimo = a.Accion.GetCustomAttributes<AllowAnonymousAttribute>(true).Any() ||
                          a.Controller.GetCustomAttributes<AllowAnonymousAttribute>(true).Any();

            if (Anonimos.Contains(nombre)) continue;

            var protegido = !anonimo &&
                (a.Accion.GetCustomAttributes(true).OfType<IAuthorizeData>().Any() ||
                 a.Controller.GetCustomAttributes(true).OfType<IAuthorizeData>().Any());

            if (!protegido) sinProteger.Add(nombre);
        }

        Assert.True(sinProteger.Count == 0, "Endpoints sin proteger: " + string.Join(", ", sinProteger));
    }

    [Fact]
    public void TodaEscritura_PidePermisoEspecifico()
    {
        var sinPermiso = new List<string>();
        foreach (var a in Acciones())
        {
            var nombre = Nombre(a);
            var esLectura = a.Accion.GetCustomAttributes<HttpGetAttribute>(true).Any();
            if (esLectura || Anonimos.Contains(nombre) || EscriturasSoloAutenticadas.Contains(nombre)) continue;

            if (!a.Accion.GetCustomAttributes<PermisoAuthorizeAttribute>(true).Any()) sinPermiso.Add(nombre);
        }

        Assert.True(sinPermiso.Count == 0, "Escrituras sin [PermisoAuthorize]: " + string.Join(", ", sinPermiso));
    }

    [Fact]
    public void LosPermisosQueUsaElCodigo_ExistenEnLosScriptsYLosTieneElSuperAdministrador()
    {
        var sql = SqlScriptsReader.LeerPermisos();

        var enControllers = Acciones()
            .SelectMany(a => a.Accion.GetCustomAttributes<PermisoAuthorizeAttribute>(true))
            .Select(p => p.Policy!.Substring(PermisoAuthorizeAttribute.Prefijo.Length));

        var enServicios = typeof(PermisosPos).GetFields(BindingFlags.Public | BindingFlags.Static)
            .Where(f => f.IsLiteral)
            .Select(f => (string)f.GetRawConstantValue()!);

        var usados = enControllers.Concat(enServicios).ToHashSet();
        var faltantes = usados.Where(u => !sql.Declarados.Contains(u)).ToList();
        Assert.True(faltantes.Count == 0, "Permisos usados en codigo pero sin INSERT en los scripts: " + string.Join(", ", faltantes));

        var superAdmin = sql.PorRol["Super Administrador"];
        var sinAsignar = sql.Declarados.Where(d => !superAdmin.Contains(d)).ToList();
        Assert.True(sinAsignar.Count == 0, "Permisos que el Super Administrador no recibe: " + string.Join(", ", sinAsignar));
    }

    [Fact]
    public void LaCajera_TieneLoNecesarioParaSuDia_YNadaDeAdministracion()
    {
        var cajera = SqlScriptsReader.LeerPermisos().PorRol["Cajera"];

        var debeTener = new[]
        {
            "pos.vender", "pos.descuento", "pos.recargo.reducir", "caja.cortar",
            "clientes.administrar", "creditos.administrar", "inventario.consultar"
        };
        foreach (var p in debeTener) Assert.True(cajera.Contains(p), $"A la Cajera le falta '{p}'.");

        var noDebeTener = new[]
        {
            "pos.anular", "pos.descuento.alto", "pos.recargo.ajustar", "caja.reabrir", "inventario.ajustar",
            "ventafacil.registrar", "catalogo.administrar", "config.critica.editar", "usuarios.administrar"
        };
        foreach (var p in noDebeTener) Assert.False(cajera.Contains(p), $"La Cajera no deberia tener '{p}'.");
    }

    [Fact]
    public void ElContenedorDeDI_PuedeConstruirTodosLosControllers()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:DefaultConnection"] = "Server=localhost;Database=Prueba;User Id=sa;Password=x;TrustServerCertificate=True",
                ["Jwt:Key"] = new string('k', 64),
                ["Jwt:Issuer"] = "carniapp-test",
                ["Jwt:Audience"] = "carniapp-test"
            })
            .Build();

        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(config);   // en la app real lo registra el host
        services.AddLogging();
        services.AddInfrastructure(config);

        using var provider = services.BuildServiceProvider(new ServiceProviderOptions
        {
            ValidateScopes = true,
            ValidateOnBuild = true   // falla si algun servicio registrado depende de algo no registrado
        });

        using var scope = provider.CreateScope();
        var problemas = new List<string>();
        foreach (var controller in Controllers())
        {
            foreach (var ctor in controller.GetConstructors())
            {
                foreach (var parametro in ctor.GetParameters())
                {
                    if (scope.ServiceProvider.GetService(parametro.ParameterType) is null)
                    {
                        problemas.Add($"{controller.Name} necesita {parametro.ParameterType.Name}");
                    }
                }
            }
        }

        Assert.True(problemas.Count == 0, "Servicios sin registrar: " + string.Join("; ", problemas));
    }
}
