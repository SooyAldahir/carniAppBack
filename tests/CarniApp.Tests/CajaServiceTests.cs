using CarniApp.Application.Features.Caja;
using CarniApp.Domain.Entities;
using CarniApp.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CarniApp.Tests;

public class CajaServiceTests : IDisposable
{
    private readonly TestDb _db = new();
    private readonly FakeClock _clock = new() { Now = new DateTime(2026, 10, 19, 14, 0, 0, DateTimeKind.Utc) };
    private readonly Sucursal _sucursal;
    private readonly Guid _usuarioId;
    private readonly Guid _efectivo, _tarjeta, _credito;

    public CajaServiceTests()
    {
        _sucursal = _db.SembrarSucursal();
        _usuarioId = _db.SembrarUsuarioParaMovimientos().Id;
        using var ctx = _db.NuevoContexto();
        var e = new MetodoPago { Nombre = "Efectivo" };
        var t = new MetodoPago { Nombre = "Tarjeta" };
        var c = new MetodoPago { Nombre = "Credito" };
        ctx.MetodosPago.AddRange(e, t, c);
        ctx.SaveChanges();
        _efectivo = e.Id;
        _tarjeta = t.Id;
        _credito = c.Id;
    }

    public void Dispose() => _db.Dispose();

    private async Task<T> Con<T>(Func<CajaService, Task<T>> accion)
    {
        using var ctx = _db.NuevoContexto();
        return await accion(new CajaService(
            new CorteCajaRepository(ctx),
            new VentaRepository(ctx),
            new AbonoCreditoRepository(ctx),
            new Repository<Sucursal>(ctx),
            new Repository<CarniApp.Domain.Entities.Negocio>(ctx),
            new UnitOfWork(ctx),
            _clock));
    }

    private Task<CajaResultDto<CorteCajaDto>> Abrir(decimal fondo = 500m) =>
        Con(s => s.AbrirAsync(new AbrirCajaRequest(fondo), _usuarioId));

    private Task<CajaResultDto<CorteCajaDto>> Cerrar(decimal contado, string? notas = null) =>
        Con(s => s.CerrarAsync(new CerrarCajaRequest(contado, notas), _usuarioId));

    private void AgregarVenta(decimal total, DateTime fecha, string estado, params (Guid Metodo, decimal Importe)[] pagos)
    {
        using var ctx = _db.NuevoContexto();
        var venta = new Venta
        {
            SucursalId = _sucursal.Id,
            UsuarioId = _usuarioId,
            Subtotal = total,
            Total = total,
            Estado = estado,
            Fecha = fecha
        };
        foreach (var p in pagos) venta.Pagos.Add(new VentaPago { MetodoPagoId = p.Metodo, Importe = p.Importe });
        ctx.Ventas.Add(venta);
        ctx.SaveChanges();
    }

    private void AgregarAbono(decimal importe, DateTime fecha)
    {
        using var ctx = _db.NuevoContexto();
        var cliente = new Cliente { Nombre = "Don Pepe" };
        ctx.Clientes.Add(cliente);
        ctx.AbonosCredito.Add(new AbonoCredito { ClienteId = cliente.Id, Importe = importe, UsuarioId = _usuarioId, Fecha = fecha });
        ctx.SaveChanges();
    }

    // ------------------------------------------------------------------ apertura

    [Fact]
    public async Task Abrir_CreaTurnoAbierto_ConFondo()
    {
        var r = await Abrir(500m);

        Assert.True(r.Exitoso, r.Mensaje);
        Assert.Equal(EstadoCorteCaja.Abierto, r.Datos!.Estado);
        Assert.Equal(500m, r.Datos.FondoInicial);
        Assert.Equal(500m, r.Datos.EfectivoEsperado);
        Assert.Equal(_clock.Now, r.Datos.FechaInicio);
        Assert.Equal("Test", r.Datos.UsuarioNombre);
    }

    [Fact]
    public async Task Abrir_FondoNegativo_SeRechaza()
    {
        var r = await Abrir(-1m);
        Assert.Equal(CajaResultCode.ImporteInvalido, r.Code);
    }

    [Fact]
    public async Task Abrir_ConTurnoAbierto_SeRechaza()
    {
        await Abrir();
        var r = await Abrir();
        Assert.Equal(CajaResultCode.YaHayCajaAbierta, r.Code);
        using var ctx = _db.NuevoContexto();
        Assert.Equal(1, await ctx.CortesCaja.CountAsync());
    }

    [Fact]
    public async Task Estado_ReflejaSiHayTurnoAbierto()
    {
        var antes = await Con(s => s.ObtenerEstadoAsync());
        Assert.False(antes.Datos!.Abierta);
        Assert.Null(antes.Datos.Corte);

        await Abrir(100m);

        var despues = await Con(s => s.ObtenerEstadoAsync());
        Assert.True(despues.Datos!.Abierta);
        Assert.Equal(100m, despues.Datos.Corte!.FondoInicial);
    }

    // ------------------------------------------------------------------ meta y retiros

    [Fact]
    public async Task Abrir_ConMeta_LaGuarda_YSinMetaOCeroQuedaNula()
    {
        var conMeta = await Con(s => s.AbrirAsync(new AbrirCajaRequest(500m, 3000m), _usuarioId));
        Assert.True(conMeta.Exitoso, conMeta.Mensaje);
        Assert.Equal(3000m, conMeta.Datos!.MetaEfectivo);

        await Cerrar(500m);
        var cero = await Con(s => s.AbrirAsync(new AbrirCajaRequest(500m, 0m), _usuarioId));
        Assert.Null(cero.Datos!.MetaEfectivo);
    }

    [Fact]
    public async Task Abrir_ConMetaNegativa_SeRechaza()
    {
        var r = await Con(s => s.AbrirAsync(new AbrirCajaRequest(500m, -5m), _usuarioId));
        Assert.Equal(CajaResultCode.ImporteInvalido, r.Code);
    }

    [Fact]
    public async Task Estado_SinTurno_OfreceLaMetaDelUltimoTurnoComoSugerida()
    {
        await Con(s => s.AbrirAsync(new AbrirCajaRequest(500m, 2500m), _usuarioId));
        await Cerrar(500m);

        var estado = (await Con(s => s.ObtenerEstadoAsync())).Datos!;

        Assert.False(estado.Abierta);
        Assert.Equal(2500m, estado.MetaSugerida);
    }

    [Fact]
    public async Task ActualizarMeta_CambiaYQuita_PeroRequiereTurnoAbierto()
    {
        var sinTurno = await Con(s => s.ActualizarMetaAsync(new ActualizarMetaRequest(1000m)));
        Assert.Equal(CajaResultCode.SinCajaAbierta, sinTurno.Code);

        await Abrir(500m);
        var puesta = await Con(s => s.ActualizarMetaAsync(new ActualizarMetaRequest(4000m)));
        Assert.Equal(4000m, puesta.Datos!.MetaEfectivo);

        var quitada = await Con(s => s.ActualizarMetaAsync(new ActualizarMetaRequest(null)));
        Assert.Null(quitada.Datos!.MetaEfectivo);

        var mala = await Con(s => s.ActualizarMetaAsync(new ActualizarMetaRequest(-1m)));
        Assert.Equal(CajaResultCode.ImporteInvalido, mala.Code);
    }

    [Fact]
    public async Task Retiro_BajaElEfectivoEsperado_PeroNoCuentaComoGasto()
    {
        await Abrir(500m);
        AgregarVenta(1500m, _clock.Now.AddMinutes(10), EstadoVenta.Completada, (_efectivo, 1500m));
        _clock.Avanzar(TimeSpan.FromHours(1));

        var r = await Con(s => s.RegistrarRetiroAsync(new RetiroCajaRequest(1200m), _usuarioId));

        Assert.True(r.Exitoso, r.Mensaje);
        Assert.Equal(1200m, r.Datos!.TotalRetiros);
        Assert.Equal(0m, r.Datos.TotalGastos);
        Assert.Equal(800m, r.Datos.EfectivoEsperado);   // 500 + 1500 - 1200
        var mov = Assert.Single(r.Datos.Gastos);
        Assert.Equal("Retiro", mov.Tipo);
        Assert.Equal("Retiro de efectivo", mov.Concepto);
    }

    [Fact]
    public async Task Retiro_MayorAlEfectivoEnCaja_SeRechaza_YSinTurnoTambien()
    {
        var sinTurno = await Con(s => s.RegistrarRetiroAsync(new RetiroCajaRequest(10m), _usuarioId));
        Assert.Equal(CajaResultCode.SinCajaAbierta, sinTurno.Code);

        await Abrir(500m);
        var demasiado = await Con(s => s.RegistrarRetiroAsync(new RetiroCajaRequest(500.01m), _usuarioId));
        Assert.Equal(CajaResultCode.EfectivoInsuficiente, demasiado.Code);

        var cero = await Con(s => s.RegistrarRetiroAsync(new RetiroCajaRequest(0m), _usuarioId));
        Assert.Equal(CajaResultCode.ImporteInvalido, cero.Code);
    }

    [Fact]
    public async Task Cierre_ConRetiro_Cuadra_YElTicketLoListaAparte()
    {
        await Abrir(500m);
        AgregarVenta(1000m, _clock.Now.AddMinutes(5), EstadoVenta.Completada, (_efectivo, 1000m));
        await Con(s => s.RegistrarGastoAsync(new GastoCajaRequest("Hielo", 100m), _usuarioId));
        _clock.Avanzar(TimeSpan.FromHours(1));
        await Con(s => s.RegistrarRetiroAsync(new RetiroCajaRequest(900m, "Al sobre"), _usuarioId));

        var cierre = await Cerrar(500m);   // 500 + 1000 - 100 - 900

        Assert.True(cierre.Exitoso, cierre.Mensaje);
        Assert.Equal(500m, cierre.Datos!.EfectivoEsperado);
        Assert.Equal(0m, cierre.Datos.Diferencia);
        Assert.Equal(100m, cierre.Datos.TotalGastos);
        Assert.Equal(900m, cierre.Datos.TotalRetiros);

        var ticket = (await Con(s => s.ObtenerTicketAsync(cierre.Datos.Id))).Datos!.Texto;
        Assert.Contains("- Retiros", ticket);
        Assert.Contains("Retiros:", ticket);
        Assert.Contains("Al sobre", ticket);
    }

    // ------------------------------------------------------------------ resumen

    [Fact]
    public async Task Resumen_CuentaVentasPorMetodo_ExcluyeAnuladasYFueraDelTurno()
    {
        AgregarVenta(70m, _clock.Now.AddHours(-1), EstadoVenta.Completada, (_efectivo, 70m));   // antes del turno
        await Abrir(500m);
        var t = _clock.Now;
        AgregarVenta(100m, t.AddMinutes(10), EstadoVenta.Completada, (_efectivo, 100m));
        AgregarVenta(50m, t.AddMinutes(20), EstadoVenta.Completada, (_tarjeta, 50m));
        AgregarVenta(200m, t.AddMinutes(30), EstadoVenta.Completada, (_credito, 200m));
        AgregarVenta(999m, t.AddMinutes(40), EstadoVenta.Anulada, (_efectivo, 999m));
        _clock.Avanzar(TimeSpan.FromHours(1));

        var r = (await Con(s => s.ObtenerEstadoAsync())).Datos!.Corte!;

        Assert.Equal(3, r.NumeroVentas);
        Assert.Equal(1, r.VentasAnuladas);
        Assert.Equal(350m, r.TotalVentas);
        Assert.Equal(new[] { "Efectivo", "Tarjeta", "Credito" }, r.PorMetodo.Select(m => m.Metodo));
        Assert.Equal(new[] { 100m, 50m, 200m }, r.PorMetodo.Select(m => m.Importe));
        Assert.Equal(100m, r.VentasEfectivo);
        Assert.Equal(600m, r.EfectivoEsperado);   // 500 de fondo + 100 en efectivo
    }

    [Fact]
    public async Task Resumen_PagoMixto_SumaCadaMetodo()
    {
        await Abrir(0m);
        AgregarVenta(300m, _clock.Now.AddMinutes(5), EstadoVenta.Completada, (_efectivo, 120m), (_tarjeta, 180m));

        _clock.Avanzar(TimeSpan.FromMinutes(10));
        var r = (await Con(s => s.ObtenerEstadoAsync())).Datos!.Corte!;

        Assert.Equal(120m, r.VentasEfectivo);
        Assert.Equal(300m, r.TotalVentas);
        Assert.Equal(120m, r.EfectivoEsperado);
    }

    [Fact]
    public async Task Resumen_LosAbonosDeCredito_EntranComoEfectivo()
    {
        await Abrir(100m);
        AgregarAbono(80m, _clock.Now.AddMinutes(5));
        _clock.Avanzar(TimeSpan.FromMinutes(10));

        var r = (await Con(s => s.ObtenerEstadoAsync())).Datos!.Corte!;

        Assert.Equal(80m, r.AbonosEfectivo);
        Assert.Equal(180m, r.EfectivoEsperado);
    }

    // ------------------------------------------------------------------ gastos

    [Fact]
    public async Task Gasto_ReduceElEfectivoEsperado_YQuedaListado()
    {
        await Abrir(500m);
        _clock.Avanzar(TimeSpan.FromMinutes(5));

        var r = await Con(s => s.RegistrarGastoAsync(new GastoCajaRequest("  Hielo  ", 120.50m), _usuarioId));

        Assert.True(r.Exitoso, r.Mensaje);
        Assert.Equal(120.50m, r.Datos!.TotalGastos);
        Assert.Equal(379.50m, r.Datos.EfectivoEsperado);
        var g = Assert.Single(r.Datos.Gastos);
        Assert.Equal("Hielo", g.Concepto);
        Assert.Equal("Test", g.UsuarioNombre);
    }

    [Fact]
    public async Task Gasto_SinTurnoAbierto_SeRechaza()
    {
        var r = await Con(s => s.RegistrarGastoAsync(new GastoCajaRequest("Hielo", 10m), _usuarioId));
        Assert.Equal(CajaResultCode.SinCajaAbierta, r.Code);
    }

    [Theory]
    [InlineData("", 10)]
    [InlineData("   ", 10)]
    [InlineData("Hielo", 0)]
    [InlineData("Hielo", -5)]
    public async Task Gasto_DatosInvalidos_SeRechazan(string concepto, double importe)
    {
        await Abrir();
        var r = await Con(s => s.RegistrarGastoAsync(new GastoCajaRequest(concepto, (decimal)importe), _usuarioId));
        Assert.False(r.Exitoso);
        using var ctx = _db.NuevoContexto();
        Assert.Equal(0, await ctx.GastosCaja.CountAsync());
    }

    // ------------------------------------------------------------------ cierre

    [Fact]
    public async Task Cerrar_Faltante_GuardaTotalesYDiferencia()
    {
        await Abrir(500m);
        var t = _clock.Now;
        AgregarVenta(100m, t.AddMinutes(10), EstadoVenta.Completada, (_efectivo, 100m));
        AgregarVenta(50m, t.AddMinutes(20), EstadoVenta.Completada, (_tarjeta, 50m));
        _clock.Avanzar(TimeSpan.FromHours(2));
        await Con(s => s.RegistrarGastoAsync(new GastoCajaRequest("Bolsas", 40m), _usuarioId));   // esperado 560

        var r = await Cerrar(550m, " Faltaron 10 ");

        Assert.True(r.Exitoso, r.Mensaje);
        var c = r.Datos!;
        Assert.Equal(EstadoCorteCaja.Cerrado, c.Estado);
        Assert.Equal(560m, c.EfectivoEsperado);
        Assert.Equal(550m, c.EfectivoContado);
        Assert.Equal(-10m, c.Diferencia);
        Assert.Equal("Faltaron 10", c.Notas);
        Assert.Equal(_clock.Now, c.FechaCierre);

        using var ctx = _db.NuevoContexto();
        var fila = await ctx.CortesCaja.SingleAsync();
        Assert.Equal(150m, fila.TotalVentas);
        Assert.Equal(40m, fila.TotalGastos);
        Assert.Equal(560m, fila.EfectivoEsperado);
        Assert.Equal(-10m, fila.Diferencia);
    }

    [Fact]
    public async Task Cerrar_Sobrante_DiferenciaPositiva_YCuadraEsCero()
    {
        await Abrir(100m);
        _clock.Avanzar(TimeSpan.FromHours(1));
        Assert.Equal(25m, (await Cerrar(125m)).Datos!.Diferencia);

        await Abrir(100m);
        _clock.Avanzar(TimeSpan.FromHours(1));
        Assert.Equal(0m, (await Cerrar(100m)).Datos!.Diferencia);
    }

    [Fact]
    public async Task Cerrar_SinTurnoAbierto_SeRechaza()
    {
        var r = await Cerrar(100m);
        Assert.Equal(CajaResultCode.SinCajaAbierta, r.Code);
    }

    [Fact]
    public async Task Cerrar_DatosInvalidos_NoCierran()
    {
        await Abrir();
        Assert.Equal(CajaResultCode.ImporteInvalido, (await Cerrar(-1m)).Code);
        Assert.Equal(CajaResultCode.NotasInvalidas, (await Cerrar(10m, new string('x', 301))).Code);

        var estado = await Con(s => s.ObtenerEstadoAsync());
        Assert.True(estado.Datos!.Abierta);
    }

    [Fact]
    public async Task TrasCerrar_LasVentasNuevasNoCambianElCorte()
    {
        var abierto = await Abrir(100m);
        AgregarVenta(100m, _clock.Now.AddMinutes(5), EstadoVenta.Completada, (_efectivo, 100m));
        _clock.Avanzar(TimeSpan.FromHours(1));
        await Cerrar(200m);

        _clock.Avanzar(TimeSpan.FromHours(1));
        AgregarVenta(999m, _clock.Now, EstadoVenta.Completada, (_efectivo, 999m));

        var c = (await Con(s => s.ObtenerAsync(abierto.Datos!.Id))).Datos!;
        Assert.Equal(1, c.NumeroVentas);
        Assert.Equal(100m, c.TotalVentas);
        Assert.Equal(200m, c.EfectivoEsperado);
    }

    [Fact]
    public async Task DespuesDeCerrar_SePuedeAbrirOtroTurno()
    {
        await Abrir();
        _clock.Avanzar(TimeSpan.FromHours(1));
        await Cerrar(500m);

        var r = await Abrir(300m);

        Assert.True(r.Exitoso, r.Mensaje);
        using var ctx = _db.NuevoContexto();
        Assert.Equal(2, await ctx.CortesCaja.CountAsync());
    }

    // ------------------------------------------------------------------ reabrir

    [Fact]
    public async Task Reabrir_UltimoCorteCerrado_LoDejaAbiertoYSinConteo()
    {
        var c = await Abrir(100m);
        _clock.Avanzar(TimeSpan.FromHours(1));
        await Cerrar(100m);

        var r = await Con(s => s.ReabrirAsync(c.Datos!.Id));

        Assert.True(r.Exitoso, r.Mensaje);
        Assert.Equal(EstadoCorteCaja.Abierto, r.Datos!.Estado);
        Assert.Null(r.Datos.FechaCierre);
        Assert.Null(r.Datos.EfectivoContado);
        Assert.Null(r.Datos.Diferencia);
        Assert.True((await Con(s => s.ObtenerEstadoAsync())).Datos!.Abierta);
    }

    [Fact]
    public async Task Reabrir_CasosNoPermitidos()
    {
        Assert.Equal(CajaResultCode.CorteNoEncontrado, (await Con(s => s.ReabrirAsync(Guid.NewGuid()))).Code);

        var primero = await Abrir();
        Assert.Equal(CajaResultCode.CorteNoCerrado, (await Con(s => s.ReabrirAsync(primero.Datos!.Id))).Code);

        _clock.Avanzar(TimeSpan.FromHours(1));
        await Cerrar(500m);
        _clock.Avanzar(TimeSpan.FromHours(1));
        var segundo = await Abrir();

        // Hay un turno abierto: no se puede reabrir otro.
        Assert.Equal(CajaResultCode.YaHayCajaAbierta, (await Con(s => s.ReabrirAsync(primero.Datos!.Id))).Code);

        _clock.Avanzar(TimeSpan.FromHours(1));
        await Cerrar(500m);

        // El primero ya no es el ultimo.
        Assert.Equal(CajaResultCode.NoEsElUltimoCorte, (await Con(s => s.ReabrirAsync(primero.Datos!.Id))).Code);
        Assert.True((await Con(s => s.ReabrirAsync(segundo.Datos!.Id))).Exitoso);
    }

    // ------------------------------------------------------------------ listado y ticket

    [Fact]
    public async Task Listar_MasRecientesPrimero_YElAbiertoSinTotales()
    {
        await Abrir();
        _clock.Avanzar(TimeSpan.FromHours(1));
        await Cerrar(500m);
        _clock.Avanzar(TimeSpan.FromHours(1));
        await Abrir(50m);

        var lista = await Con(s => s.ListarAsync(30));

        Assert.Equal(2, lista.Count);
        Assert.Equal(EstadoCorteCaja.Abierto, lista[0].Estado);
        Assert.Null(lista[0].TotalVentas);
        Assert.Equal(EstadoCorteCaja.Cerrado, lista[1].Estado);
        Assert.Equal(0m, lista[1].Diferencia);
    }

    [Fact]
    public async Task Ticket_Cabe_En32Columnas_YMuestraElFaltante()
    {
        var c = await Abrir(500m);
        AgregarVenta(100m, _clock.Now.AddMinutes(5), EstadoVenta.Completada, (_efectivo, 100m));
        _clock.Avanzar(TimeSpan.FromHours(1));
        await Con(s => s.RegistrarGastoAsync(new GastoCajaRequest("Hielo para la vitrina de la carniceria", 40m), _usuarioId));
        await Cerrar(500m, "Todo bien");

        var ticket = (await Con(s => s.ObtenerTicketAsync(c.Datos!.Id))).Datos!.Texto;

        Assert.Contains("CORTE DE CAJA", ticket);
        Assert.Contains("Faltante", ticket);
        Assert.Contains("Hielo", ticket);
        Assert.All(ticket.Split('\n'), l => Assert.True(l.TrimEnd('\r').Length <= 32, $"Linea de {l.Length} columnas: {l}"));
    }

    [Fact]
    public async Task Ticket_CorteInexistente()
    {
        var r = await Con(s => s.ObtenerTicketAsync(Guid.NewGuid()));
        Assert.Equal(CajaResultCode.CorteNoEncontrado, r.Code);
    }
}
