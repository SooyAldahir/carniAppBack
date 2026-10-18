using CarniApp.Application.Features.Clientes;
using CarniApp.Domain.Entities;
using CarniApp.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CarniApp.Tests;

public class ClienteServiceTests : IDisposable
{
    private readonly TestDb _db = new();
    private readonly FakeClock _clock = new();
    private Guid? _usuarioId;

    public void Dispose() => _db.Dispose();

    private Guid Usuario() => _usuarioId ??= _db.SembrarUsuarioParaMovimientos().Id;

    private async Task<T> Con<T>(Func<ClienteService, Task<T>> accion)
    {
        using var ctx = _db.NuevoContexto();
        var svc = new ClienteService(new ClienteRepository(ctx), new AbonoCreditoRepository(ctx), new UnitOfWork(ctx), _clock);
        return await accion(svc);
    }

    private static GuardarClienteRequest Req(string nombre = "Don Pepe", string? tel = "5551112222", decimal limite = 1000m) =>
        new(nombre, tel, "Calle 1", null, limite);

    private async Task<ClienteDto> Crear(string nombre = "Don Pepe", string? tel = "5551112222", decimal limite = 1000m)
    {
        var r = await Con(s => s.CrearAsync(Req(nombre, tel, limite)));
        Assert.True(r.Exitoso, r.Mensaje);
        return r.Datos!;
    }

    private void PonerSaldo(Guid id, decimal saldo)
    {
        using var ctx = _db.NuevoContexto();
        ctx.Clientes.Single(c => c.Id == id).SaldoCredito = saldo;
        ctx.SaveChanges();
    }

    [Fact]
    public async Task Crear_GuardaDatosLimpios_YCreditoDisponibleIgualAlLimite()
    {
        var r = await Con(s => s.CrearAsync(new GuardarClienteRequest("  Dona Lupe ", " 555 ", "  ", null, 750.456m)));

        Assert.True(r.Exitoso, r.Mensaje);
        Assert.Equal("Dona Lupe", r.Datos!.Nombre);
        Assert.Equal("555", r.Datos.Telefono);
        Assert.Null(r.Datos.Direccion);
        Assert.Equal(750.46m, r.Datos.LimiteCredito);
        Assert.Equal(750.46m, r.Datos.CreditoDisponible);
        Assert.Equal(0m, r.Datos.SaldoCredito);
    }

    [Fact]
    public async Task Crear_ConDatosInvalidos_SeRechaza()
    {
        Assert.Equal(ClienteResultCode.NombreRequerido, (await Con(s => s.CrearAsync(Req(nombre: "  ")))).Code);
        Assert.Equal(ClienteResultCode.LimiteInvalido, (await Con(s => s.CrearAsync(Req(limite: -1m)))).Code);
    }

    [Fact]
    public async Task Telefono_Duplicado_SeRechaza_PeroSePuedeConservarElPropio_YVariosSinTelefono()
    {
        var a = await Crear("A", "555");
        Assert.Equal(ClienteResultCode.TelefonoDuplicado, (await Con(s => s.CrearAsync(Req("B", "555")))).Code);
        Assert.True((await Con(s => s.ActualizarAsync(a.Id, Req("A2", "555")))).Exitoso);
        Assert.True((await Con(s => s.CrearAsync(Req("C", null)))).Exitoso);
        Assert.True((await Con(s => s.CrearAsync(Req("D", " ")))).Exitoso);
    }

    [Fact]
    public async Task Buscar_PorNombreOTelefono_SinImportarMayusculas_SoloActivos()
    {
        await Crear("Carniceria Lopez", "5551");
        await Crear("Maria Perez", "5552");
        var baja = await Crear("Pedro Baja", "5553");
        Assert.True((await Con(s => s.DesactivarAsync(baja.Id))).Exitoso);

        Assert.Equal(2, (await Con(s => s.ListarAsync(null))).Count);
        Assert.Single(await Con(s => s.ListarAsync("LOPEZ")));
        Assert.Single(await Con(s => s.ListarAsync("5552")));
        Assert.Empty(await Con(s => s.ListarAsync("Pedro")));
    }

    [Fact]
    public async Task Actualizar_CambiaElLimite_SinTocarElSaldo()
    {
        var c = await Crear(limite: 1000m);
        PonerSaldo(c.Id, 400m);

        var r = await Con(s => s.ActualizarAsync(c.Id, Req(limite: 300m)));

        Assert.True(r.Exitoso, r.Mensaje);
        Assert.Equal(400m, r.Datos!.SaldoCredito);
        Assert.Equal(0m, r.Datos.CreditoDisponible);   // nunca negativo
    }

    [Fact]
    public async Task Desactivar_ConSaldoPendiente_SeRechaza()
    {
        var c = await Crear();
        PonerSaldo(c.Id, 50m);

        Assert.Equal(ClienteResultCode.TieneSaldoPendiente, (await Con(s => s.DesactivarAsync(c.Id))).Code);
        Assert.True((await Con(s => s.ObtenerAsync(c.Id))).Exitoso);
    }

    [Fact]
    public async Task Obtener_Inexistente_O_DadoDeBaja_NoSeEncuentra()
    {
        var c = await Crear();
        Assert.True((await Con(s => s.DesactivarAsync(c.Id))).Exitoso);

        Assert.Equal(ClienteResultCode.NoEncontrado, (await Con(s => s.ObtenerAsync(c.Id))).Code);
        Assert.Equal(ClienteResultCode.NoEncontrado, (await Con(s => s.ObtenerAsync(Guid.NewGuid()))).Code);
    }

    [Fact]
    public async Task Abono_ReduceElSaldo_YQuedaRegistradoConSuUsuario()
    {
        var c = await Crear();
        PonerSaldo(c.Id, 500m);

        var r = await Con(s => s.RegistrarAbonoAsync(c.Id, new RegistrarAbonoRequest(200m), Usuario()));

        Assert.True(r.Exitoso, r.Mensaje);
        Assert.Equal(300m, r.Datos!.SaldoCredito);
        Assert.Equal(700m, r.Datos.CreditoDisponible);

        var abonos = await Con(s => s.ListarAbonosAsync(c.Id));
        var abono = Assert.Single(abonos);
        Assert.Equal(200m, abono.Importe);
        Assert.Equal("Test", abono.UsuarioNombre);
    }

    [Fact]
    public async Task Abono_Invalido_OMayorAlSaldo_SeRechazaSinCambios()
    {
        var c = await Crear();
        PonerSaldo(c.Id, 100m);

        Assert.Equal(ClienteResultCode.ImporteInvalido, (await Con(s => s.RegistrarAbonoAsync(c.Id, new RegistrarAbonoRequest(0m), Usuario()))).Code);
        Assert.Equal(ClienteResultCode.ImporteInvalido, (await Con(s => s.RegistrarAbonoAsync(c.Id, new RegistrarAbonoRequest(-5m), Usuario()))).Code);
        Assert.Equal(ClienteResultCode.AbonoExcedeSaldo, (await Con(s => s.RegistrarAbonoAsync(c.Id, new RegistrarAbonoRequest(100.01m), Usuario()))).Code);
        Assert.Equal(ClienteResultCode.NoEncontrado, (await Con(s => s.RegistrarAbonoAsync(Guid.NewGuid(), new RegistrarAbonoRequest(10m), Usuario()))).Code);

        Assert.Empty(await Con(s => s.ListarAbonosAsync(c.Id)));
        Assert.Equal(100m, (await Con(s => s.ObtenerAsync(c.Id))).Datos!.SaldoCredito);
    }

    [Fact]
    public async Task Abono_ExactoAlSaldo_LoLiquida()
    {
        var c = await Crear();
        PonerSaldo(c.Id, 100m);

        var r = await Con(s => s.RegistrarAbonoAsync(c.Id, new RegistrarAbonoRequest(100m), Usuario()));

        Assert.Equal(0m, r.Datos!.SaldoCredito);
        Assert.True((await Con(s => s.DesactivarAsync(c.Id))).Exitoso);
    }
}
