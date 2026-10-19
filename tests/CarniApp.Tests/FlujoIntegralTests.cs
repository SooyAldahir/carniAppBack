using CarniApp.Application.Features.Caja;
using CarniApp.Application.Features.Clientes;
using CarniApp.Application.Features.Inventario;
using CarniApp.Application.Features.VentaFacil;
using CarniApp.Application.Features.Ventas;
using CarniApp.Domain.Entities;
using CarniApp.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CarniApp.Tests;

/// <summary>
/// Un dia completo en la carniceria con TODOS los servicios reales sobre la misma base de datos:
/// caja, lotes de animal (pesados y estimados), ventas en efectivo / mixtas / a credito, abonos,
/// gastos, anulaciones, corte, reapertura. Cada llamada usa un contexto nuevo (= un request HTTP).
/// Al final se comprueban los invariantes que NO deben romperse nunca:
///   stock = suma del kardex, saldo del cliente = cargos - abonos, corte = ventas + fondo + abonos - gastos.
/// </summary>
public class FlujoIntegralTests : IDisposable
{
    private readonly TestDb _db = new();
    private readonly FakeClock _clock = new() { Now = new DateTime(2026, 10, 19, 14, 0, 0, DateTimeKind.Utc) };
    private readonly FakePosSettings _pos = new();   // recargo de credito 5%

    private Guid _usuarioId;
    private Guid _efectivo, _tarjeta, _credito;
    private Corte _bistec = null!, _molida = null!;
    private TipoAnimal _res = null!;

    public void Dispose() => _db.Dispose();

    // ------------------------------------------------------------------ fabrica de servicios

    private async Task<T> Con<T>(Func<CarniAppDbContext, Task<T>> accion)
    {
        using var ctx = _db.NuevoContexto();
        return await accion(ctx);
    }

    private Task<T> Caja<T>(Func<CajaService, Task<T>> f) => Con(ctx => f(new CajaService(
        new CorteCajaRepository(ctx), new VentaRepository(ctx), new AbonoCreditoRepository(ctx),
        new Repository<Sucursal>(ctx), new Repository<CarniApp.Domain.Entities.Negocio>(ctx), new UnitOfWork(ctx), _clock)));

    private Task<T> Pos<T>(Func<VentaService, Task<T>> f) => Con(ctx => f(new VentaService(
        new VentaRepository(ctx), new MetodoPagoRepository(ctx), new CorteRepository(ctx), new InventarioRepository(ctx),
        new MovimientoInventarioRepository(ctx), new Repository<Sucursal>(ctx), new Repository<CarniApp.Domain.Entities.Negocio>(ctx),
        new Repository<Cliente>(ctx), new CorteCajaRepository(ctx), _pos, new UnitOfWork(ctx), _clock)));

    private Task<T> VentaFacil<T>(Func<VentaFacilService, Task<T>> f) => Con(ctx => f(new VentaFacilService(
        new TipoAnimalRepository(ctx), new CorteRepository(ctx), new LoteAnimalRepository(ctx), new InventarioRepository(ctx),
        new MovimientoInventarioRepository(ctx), new Repository<Sucursal>(ctx), new UnitOfWork(ctx), _clock)));

    private Task<T> Clientes<T>(Func<ClienteService, Task<T>> f) => Con(ctx => f(new ClienteService(
        new ClienteRepository(ctx), new AbonoCreditoRepository(ctx), new UnitOfWork(ctx), _clock)));

    private void Pasar(int minutos) => _clock.Avanzar(TimeSpan.FromMinutes(minutos));

    // ------------------------------------------------------------------ datos base

    private void Sembrar()
    {
        _db.SembrarSucursal();
        _usuarioId = _db.SembrarUsuarioParaMovimientos().Id;

        using var ctx = _db.NuevoContexto();
        var e = new MetodoPago { Nombre = "Efectivo" };
        var t = new MetodoPago { Nombre = "Tarjeta" };
        var c = new MetodoPago { Nombre = "Credito" };
        ctx.MetodosPago.AddRange(e, t, c);

        _res = new TipoAnimal { Nombre = "Res" };
        ctx.TiposAnimal.Add(_res);
        _bistec = new Corte { TipoAnimalId = _res.Id, Nombre = "Bistec", UnidadVenta = UnidadVenta.Peso, PrecioBase = 200m };
        _molida = new Corte { TipoAnimalId = _res.Id, Nombre = "Molida", UnidadVenta = UnidadVenta.Peso, PrecioBase = 120m };
        ctx.Cortes.AddRange(_bistec, _molida);
        ctx.SaveChanges();

        _efectivo = e.Id;
        _tarjeta = t.Id;
        _credito = c.Id;
    }

    private async Task<Guid> CrearClienteAsync(decimal limite)
    {
        var r = await Clientes(s => s.CrearAsync(new GuardarClienteRequest("Don Pepe", "5551234567", "Calle 1 #23", null, limite)));
        Assert.True(r.Exitoso, r.Mensaje);
        return r.Datos!.Id;
    }

    private async Task<LoteStock> PesarLoteAsync(decimal peso, decimal kgBistec, decimal kgMolida)
    {
        var r = await VentaFacil(s => s.RegistrarLoteAsync(
            new LoteRequest(_res.Id, peso, peso * 50m, null, new[] { new KgCorteDto(_bistec.Id, kgBistec), new KgCorteDto(_molida.Id, kgMolida) }),
            _usuarioId));
        Assert.True(r.Exitoso, r.Mensaje);
        return new LoteStock(r.Datos!);
    }

    private sealed record LoteStock(RepartoDto Reparto);

    private async Task<VentaDto> VenderAsync(IReadOnlyList<VentaLineaRequest> lineas, IReadOnlyList<VentaPagoRequest> pagos, Guid? clienteId = null)
    {
        Pasar(5);
        var r = await Pos(s => s.CrearAsync(new CrearVentaRequest(lineas, pagos, 0m, clienteId), _usuarioId));
        Assert.True(r.Exitoso, r.Mensaje);
        return r.Datos!;
    }

    // ------------------------------------------------------------------ invariantes

    private async Task StockCuadraConElKardexAsync()
    {
        using var ctx = _db.NuevoContexto();
        var inventarios = await ctx.Inventarios.ToListAsync();
        var movimientos = await ctx.MovimientosInventario.ToListAsync();
        Assert.NotEmpty(inventarios);

        foreach (var inv in inventarios)
        {
            Assert.True(inv.StockActual >= 0, $"Stock negativo en {inv.CorteId}.");
            var suma = movimientos.Where(m => m.CorteId == inv.CorteId).Sum(m => m.Cantidad);
            Assert.Equal(suma, inv.StockActual);
        }
    }

    private async Task<decimal> StockDeAsync(Guid corteId)
    {
        using var ctx = _db.NuevoContexto();
        return (await ctx.Inventarios.SingleAsync(i => i.CorteId == corteId)).StockActual;
    }

    // ------------------------------------------------------------------ el dia completo

    [Fact]
    public async Task UnDiaCompleto_LosNumerosCuadranEnTodosLosModulos()
    {
        Sembrar();
        var clienteId = await CrearClienteAsync(limite: 5000m);

        // 1. Se abre la caja con $500 de fondo.
        var apertura = await Caja(s => s.AbrirAsync(new AbrirCajaRequest(500m), _usuarioId));
        Assert.True(apertura.Exitoso, apertura.Mensaje);

        // 2. Venta Facil: tres animales pesados (el sistema aprende) y un cuarto SIN pesar (estimado).
        Pasar(10);
        for (var i = 0; i < 3; i++)
        {
            var lote = await PesarLoteAsync(100m, kgBistec: 30m, kgMolida: 50m);
            Assert.False(lote.Reparto.EsEstimado);
            Pasar(1);
        }

        Assert.Equal(90m, await StockDeAsync(_bistec.Id));
        Assert.Equal(150m, await StockDeAsync(_molida.Id));

        var cuarto = await VentaFacil(s => s.RegistrarLoteAsync(new LoteRequest(_res.Id, 200m, 10000m), _usuarioId));
        Assert.True(cuarto.Exitoso, cuarto.Mensaje);
        Assert.True(cuarto.Datos!.EsEstimado);
        Assert.Equal("Aprendido", cuarto.Datos.Origen);
        Assert.Equal(60m, await StockDeAsync(_bistec.Id) - 90m);    // 30% de 200 kg
        Assert.Equal(100m, await StockDeAsync(_molida.Id) - 150m);   // 50% de 200 kg
        await StockCuadraConElKardexAsync();

        // Stock: bistec 150, molida 250.

        // 3. Ventas del dia.
        var v1 = await VenderAsync(                      // 5 kg de bistec = $1,000 en efectivo
            new[] { new VentaLineaRequest(_bistec.Id, 5m) },
            new[] { new VentaPagoRequest(_efectivo, 1000m) });
        Assert.Equal(1000m, v1.Total);

        var v2 = await VenderAsync(                      // 2 kg molida + 1 kg bistec = $440 (efectivo $140 + tarjeta $300)
            new[] { new VentaLineaRequest(_molida.Id, 2m), new VentaLineaRequest(_bistec.Id, 1m) },
            new[] { new VentaPagoRequest(_efectivo, 140m), new VentaPagoRequest(_tarjeta, 300m) });
        Assert.Equal(440m, v2.Total);

        var v3 = await VenderAsync(                      // 10 kg molida = $1,200 a credito + 5% visible = $1,260
            new[] { new VentaLineaRequest(_molida.Id, 10m) },
            new[] { new VentaPagoRequest(_credito, 1200m) },
            clienteId);
        Assert.Equal(60m, v3.RecargoCredito);
        Assert.Equal(1260m, v3.Total);

        var v4 = await VenderAsync(                      // se vende 1 kg y se anula (error de captura)
            new[] { new VentaLineaRequest(_bistec.Id, 1m) },
            new[] { new VentaPagoRequest(_efectivo, 200m) });
        Pasar(2);
        var anulacion = await Pos(s => s.AnularAsync(v4.Id, "Se equivoco el corte", _usuarioId));
        Assert.True(anulacion.Exitoso, anulacion.Mensaje);

        // Stock: bistec 150 - 5 - 1 - 1 + 1 = 144 ; molida 250 - 2 - 10 = 238.
        Assert.Equal(144m, await StockDeAsync(_bistec.Id));
        Assert.Equal(238m, await StockDeAsync(_molida.Id));
        await StockCuadraConElKardexAsync();

        // 4. El cliente abona $500 de los $1,260.
        Pasar(5);
        var cliente = await Clientes(s => s.ObtenerAsync(clienteId));
        Assert.Equal(1260m, cliente.Datos!.SaldoCredito);
        var abono = await Clientes(s => s.RegistrarAbonoAsync(clienteId, new RegistrarAbonoRequest(500m), _usuarioId));
        Assert.True(abono.Exitoso, abono.Mensaje);
        Assert.Equal(760m, abono.Datos!.SaldoCredito);

        // 5. Un gasto de caja.
        Pasar(5);
        var gasto = await Caja(s => s.RegistrarGastoAsync(new GastoCajaRequest("Hielo", 100m), _usuarioId));
        Assert.True(gasto.Exitoso, gasto.Mensaje);

        // 6. Cierre: esperado = 500 fondo + 1,140 efectivo (1,000 + 140) + 500 abono - 100 gasto = 2,040.
        Pasar(30);
        var cierre = await Caja(s => s.CerrarAsync(new CerrarCajaRequest(2000m, "Faltaron 40"), _usuarioId));
        Assert.True(cierre.Exitoso, cierre.Mensaje);
        var corte = cierre.Datos!;
        Assert.Equal(3, corte.NumeroVentas);
        Assert.Equal(1, corte.VentasAnuladas);
        Assert.Equal(2700m, corte.TotalVentas);                       // 1,000 + 440 + 1,260
        Assert.Equal(1140m, corte.VentasEfectivo);
        Assert.Equal(500m, corte.AbonosEfectivo);
        Assert.Equal(100m, corte.TotalGastos);
        Assert.Equal(2040m, corte.EfectivoEsperado);
        Assert.Equal(-40m, corte.Diferencia);
        Assert.Equal(new[] { "Efectivo", "Tarjeta", "Credito" }, corte.PorMetodo.Select(m => m.Metodo));
        Assert.Equal(new[] { 1140m, 300m, 1260m }, corte.PorMetodo.Select(m => m.Importe));

        // 7. Con el corte cerrado ya no se puede anular una de sus ventas...
        var bloqueada = await Pos(s => s.AnularAsync(v1.Id, "Cliente devolvio", _usuarioId));
        Assert.Equal(VentaResultCode.CorteCerrado, bloqueada.Code);
        Assert.Equal(144m, await StockDeAsync(_bistec.Id));

        // ...hasta que el administrador reabre el corte.
        var reabierto = await Caja(s => s.ReabrirAsync(corte.Id));
        Assert.True(reabierto.Exitoso, reabierto.Mensaje);
        var permitida = await Pos(s => s.AnularAsync(v1.Id, "Cliente devolvio", _usuarioId));
        Assert.True(permitida.Exitoso, permitida.Mensaje);
        Assert.Equal(149m, await StockDeAsync(_bistec.Id));          // se repusieron los 5 kg

        // 8. Se vuelve a cerrar: sin la venta de $1,000 el esperado baja a 1,040.
        Pasar(5);
        var recierre = await Caja(s => s.CerrarAsync(new CerrarCajaRequest(1040m), _usuarioId));
        Assert.True(recierre.Exitoso, recierre.Mensaje);
        Assert.Equal(1040m, recierre.Datos!.EfectivoEsperado);
        Assert.Equal(0m, recierre.Datos.Diferencia);
        Assert.Equal(1700m, recierre.Datos.TotalVentas);             // 440 + 1,260
        Assert.Equal(2, recierre.Datos.VentasAnuladas);

        // 9. Invariantes finales.
        await StockCuadraConElKardexAsync();

        using var ctx = _db.NuevoContexto();
        var c = await ctx.Clientes.SingleAsync();
        Assert.Equal(760m, c.SaldoCredito);

        var ventas = await ctx.Ventas.Include(v => v.Pagos).ToListAsync();
        Assert.Equal(4, ventas.Count);
        foreach (var v in ventas)
        {
            Assert.Equal(v.Total, v.Pagos.Sum(p => p.Importe));      // los pagos siempre suman el total
        }

        Assert.Equal(1, await ctx.CortesCaja.CountAsync());
        Assert.Equal(EstadoCorteCaja.Cerrado, (await ctx.CortesCaja.SingleAsync()).Estado);
    }

    [Fact]
    public async Task NoSePuedeVenderMasDeLoQueHay_YElStockNoCambia()
    {
        Sembrar();
        await PesarLoteAsync(100m, 30m, 50m);

        Pasar(5);
        var r = await Pos(s => s.CrearAsync(
            new CrearVentaRequest(
                new[] { new VentaLineaRequest(_bistec.Id, 31m) },
                new[] { new VentaPagoRequest(_efectivo, 6200m) }),
            _usuarioId));

        Assert.Equal(VentaResultCode.StockInsuficiente, r.Code);
        Assert.Equal(30m, await StockDeAsync(_bistec.Id));
        await StockCuadraConElKardexAsync();
        using var ctx = _db.NuevoContexto();
        Assert.Equal(0, await ctx.Ventas.CountAsync());
    }

    [Fact]
    public async Task VentaACredito_QueExcedeElLimite_NoDejaNadaAMedias()
    {
        Sembrar();
        var clienteId = await CrearClienteAsync(limite: 1000m);
        await PesarLoteAsync(100m, 30m, 50m);

        Pasar(5);
        var r = await Pos(s => s.CrearAsync(
            new CrearVentaRequest(
                new[] { new VentaLineaRequest(_molida.Id, 10m) },     // $1,200 + 5% = $1,260 > $1,000
                new[] { new VentaPagoRequest(_credito, 1200m) },
                0m, clienteId),
            _usuarioId));

        Assert.Equal(VentaResultCode.CreditoExcedido, r.Code);
        Assert.Equal(50m, await StockDeAsync(_molida.Id));
        using var ctx = _db.NuevoContexto();
        Assert.Equal(0m, (await ctx.Clientes.SingleAsync()).SaldoCredito);
        Assert.Equal(0, await ctx.Ventas.CountAsync());
        await StockCuadraConElKardexAsync();
    }

    [Fact]
    public async Task UnAjusteManualDeInventario_TambienCuadraConElKardex()
    {
        Sembrar();
        await PesarLoteAsync(100m, 30m, 50m);

        using (var ctx = _db.NuevoContexto())
        {
            var inv = new InventarioService(
                new InventarioRepository(ctx), new MovimientoInventarioRepository(ctx), new CorteRepository(ctx),
                new Repository<Sucursal>(ctx), new UnitOfWork(ctx), _clock);
            var r = await inv.RegistrarAjusteAsync(new RegistrarAjusteRequest(_bistec.Id, "Merma", -2.5m, "Se seco en vitrina"), _usuarioId);
            Assert.True(r.Exitoso, r.Mensaje);
        }

        Assert.Equal(27.5m, await StockDeAsync(_bistec.Id));
        await StockCuadraConElKardexAsync();
    }
}
