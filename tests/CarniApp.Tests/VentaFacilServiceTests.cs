using CarniApp.Application.Features.VentaFacil;
using CarniApp.Domain.Entities;
using CarniApp.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CarniApp.Tests;

public class VentaFacilServiceTests : IDisposable
{
    private readonly TestDb _db = new();
    private readonly FakeClock _clock = new();
    private Usuario? _usuario;

    private Usuario UsuarioDePrueba() => _usuario ??= _db.SembrarUsuarioParaMovimientos();

    public void Dispose() => _db.Dispose();

    private VentaFacilService Crear(CarniAppDbContext ctx) => new(
        new TipoAnimalRepository(ctx),
        new CorteRepository(ctx),
        new LoteAnimalRepository(ctx),
        new InventarioRepository(ctx),
        new MovimientoInventarioRepository(ctx),
        new Repository<Sucursal>(ctx),
        new UnitOfWork(ctx),
        _clock);

    private (TipoAnimal Tipo, Corte Bistec, Corte Molida) Sembrar(decimal? bistec = 30m, decimal? molida = 50m)
    {
        _db.SembrarSucursal();
        using var ctx = _db.NuevoContexto();
        var tipo = new TipoAnimal { Nombre = "Res" };
        ctx.TiposAnimal.Add(tipo);
        var b = new Corte { TipoAnimalId = tipo.Id, Nombre = "Bistec", UnidadVenta = UnidadVenta.Peso, PrecioBase = 200m, RendimientoPorcentaje = bistec };
        var m = new Corte { TipoAnimalId = tipo.Id, Nombre = "Molida", UnidadVenta = UnidadVenta.Peso, PrecioBase = 120m, RendimientoPorcentaje = molida };
        ctx.Cortes.AddRange(b, m);
        ctx.SaveChanges();
        return (tipo, b, m);
    }

    private async Task<VentaFacilResultDto<T>> Run<T>(Func<VentaFacilService, Task<VentaFacilResultDto<T>>> accion)
    {
        using var ctx = _db.NuevoContexto();
        return await accion(Crear(ctx));
    }

    [Fact]
    public async Task Previsualizar_RepartePorPorcentaje_YCalculaMermaYUtilidad()
    {
        var (tipo, _, _) = Sembrar();

        var r = await Run(s => s.PrevisualizarAsync(new LoteRequest(tipo.Id, 200m, 10000m)));

        Assert.True(r.Exitoso, r.Mensaje);
        var d = r.Datos!;
        Assert.Equal(2, d.Lineas.Count);
        Assert.Equal(100m, d.Lineas.Single(l => l.CorteNombre == "Molida").KgEstimados);   // 50% de 200
        Assert.Equal(60m, d.Lineas.Single(l => l.CorteNombre == "Bistec").KgEstimados);    // 30% de 200
        Assert.Equal(160m, d.KgAsignados);
        Assert.Equal(40m, d.KgMerma);
        Assert.Equal(50m, d.CostoPorKg);
        Assert.Equal(12000m + 12000m, d.VentaEstimadaTotal);
        Assert.Equal(14000m, d.UtilidadEstimada);
    }

    [Fact]
    public async Task Previsualizar_NoGuardaNada()
    {
        var (tipo, _, _) = Sembrar();
        await Run(s => s.PrevisualizarAsync(new LoteRequest(tipo.Id, 200m, 10000m)));

        using var ctx = _db.NuevoContexto();
        Assert.Equal(0, await ctx.LotesAnimal.CountAsync());
        Assert.Equal(0, await ctx.MovimientosInventario.CountAsync());
        Assert.Equal(0, await ctx.Inventarios.CountAsync());
    }

    [Fact]
    public async Task RegistrarLote_CreaLoteEstimado_Movimientos_YStock()
    {
        var (tipo, bistec, molida) = Sembrar();
        var usuario = _db.SembrarUsuarioParaMovimientos();

        var r = await Run(s => s.RegistrarLoteAsync(new LoteRequest(tipo.Id, 200m, 10000m), usuario.Id));

        Assert.True(r.Exitoso, r.Mensaje);
        using var ctx = _db.NuevoContexto();
        var lote = await ctx.LotesAnimal.SingleAsync();
        Assert.True(lote.EsEstimado);
        Assert.Equal(200m, lote.PesoKg);

        var movs = await ctx.MovimientosInventario.ToListAsync();
        Assert.Equal(2, movs.Count);
        Assert.All(movs, m =>
        {
            Assert.Equal(lote.Id, m.LoteAnimalId);
            Assert.Equal(TipoMovimientoInventario.Entrada, m.TipoMovimiento);
            Assert.Equal(usuario.Id, m.UsuarioId);
        });

        var stock = await ctx.Inventarios.ToDictionaryAsync(i => i.CorteId, i => i.StockActual);
        Assert.Equal(60m, stock[bistec.Id]);
        Assert.Equal(100m, stock[molida.Id]);
    }

    [Fact]
    public async Task RegistrarLote_SumaAlStockExistente()
    {
        var (tipo, bistec, _) = Sembrar();
        var usuario = _db.SembrarUsuarioParaMovimientos();

        await Run(s => s.RegistrarLoteAsync(new LoteRequest(tipo.Id, 100m, 5000m), usuario.Id));
        await Run(s => s.RegistrarLoteAsync(new LoteRequest(tipo.Id, 100m, 5000m), usuario.Id));

        using var ctx = _db.NuevoContexto();
        Assert.Equal(60m, (await ctx.Inventarios.SingleAsync(i => i.CorteId == bistec.Id)).StockActual);
        Assert.Equal(2, await ctx.LotesAnimal.CountAsync());
    }

    [Fact]
    public async Task RegistrarLote_SinRendimientos_NoHaceNada()
    {
        var (tipo, _, _) = Sembrar(bistec: null, molida: null);

        var r = await Run(s => s.RegistrarLoteAsync(new LoteRequest(tipo.Id, 100m, 5000m), Guid.NewGuid()));

        Assert.Equal(VentaFacilResultCode.SinRendimientos, r.Code);
        using var ctx = _db.NuevoContexto();
        Assert.Equal(0, await ctx.LotesAnimal.CountAsync());
    }

    [Theory]
    [InlineData(0, 100, VentaFacilResultCode.PesoInvalido)]
    [InlineData(-5, 100, VentaFacilResultCode.PesoInvalido)]
    [InlineData(20000, 100, VentaFacilResultCode.PesoInvalido)]
    [InlineData(100, -1, VentaFacilResultCode.PrecioInvalido)]
    public async Task Previsualizar_ValidaPesoYPrecio(double peso, double precio, VentaFacilResultCode esperado)
    {
        var (tipo, _, _) = Sembrar();
        var r = await Run(s => s.PrevisualizarAsync(new LoteRequest(tipo.Id, (decimal)peso, (decimal)precio)));
        Assert.Equal(esperado, r.Code);
    }

    [Fact]
    public async Task Previsualizar_TipoInexistente()
    {
        Sembrar();
        var r = await Run(s => s.PrevisualizarAsync(new LoteRequest(Guid.NewGuid(), 100m, 100m)));
        Assert.Equal(VentaFacilResultCode.TipoAnimalNoEncontrado, r.Code);
    }

    [Fact]
    public async Task Redondeo_AMilesimasDeKg()
    {
        var (tipo, _, _) = Sembrar(bistec: 33.33m, molida: null);
        var r = await Run(s => s.PrevisualizarAsync(new LoteRequest(tipo.Id, 10m, 100m)));
        Assert.Equal(3.333m, r.Datos!.Lineas.Single().KgEstimados);
    }

    [Fact]
    public async Task GuardarRendimientos_PersisteYLimpiaConCeroONulo()
    {
        var (tipo, bistec, molida) = Sembrar(bistec: null, molida: 10m);

        var r = await Run(s => s.GuardarRendimientosAsync(new GuardarRendimientosRequest(tipo.Id, new[]
        {
            new RendimientoEntradaDto(bistec.Id, 40m),
            new RendimientoEntradaDto(molida.Id, 0m)
        })));

        Assert.True(r.Exitoso, r.Mensaje);
        Assert.Equal(40m, r.Datos!.TotalPorcentaje);
        using var ctx = _db.NuevoContexto();
        Assert.Equal(40m, (await ctx.Cortes.FindAsync(bistec.Id))!.RendimientoPorcentaje);
        Assert.Null((await ctx.Cortes.FindAsync(molida.Id))!.RendimientoPorcentaje);
    }

    [Fact]
    public async Task GuardarRendimientos_RechazaSumaMayorA100_SinCambios()
    {
        var (tipo, bistec, molida) = Sembrar();

        var r = await Run(s => s.GuardarRendimientosAsync(new GuardarRendimientosRequest(tipo.Id, new[]
        {
            new RendimientoEntradaDto(bistec.Id, 60m),
            new RendimientoEntradaDto(molida.Id, 50m)
        })));

        Assert.Equal(VentaFacilResultCode.SumaExcede100, r.Code);
        using var ctx = _db.NuevoContexto();
        Assert.Equal(30m, (await ctx.Cortes.FindAsync(bistec.Id))!.RendimientoPorcentaje);
    }

    [Fact]
    public async Task GuardarRendimientos_ConsideraLosCortesNoEnviados()
    {
        var (tipo, bistec, _) = Sembrar();   // molida queda en 50
        var r = await Run(s => s.GuardarRendimientosAsync(new GuardarRendimientosRequest(tipo.Id, new[]
        {
            new RendimientoEntradaDto(bistec.Id, 60m)   // 60 + 50 = 110
        })));
        Assert.Equal(VentaFacilResultCode.SumaExcede100, r.Code);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(101)]
    public async Task GuardarRendimientos_PorcentajeFueraDeRango(double pct)
    {
        var (tipo, bistec, _) = Sembrar();
        var r = await Run(s => s.GuardarRendimientosAsync(new GuardarRendimientosRequest(tipo.Id, new[]
        {
            new RendimientoEntradaDto(bistec.Id, (decimal)pct)
        })));
        Assert.Equal(VentaFacilResultCode.PorcentajeInvalido, r.Code);
    }

    [Fact]
    public async Task GuardarRendimientos_RechazaCorteDeOtroTipo_YRepetidos()
    {
        var (tipo, bistec, _) = Sembrar();
        Guid otroCorte;
        using (var ctx = _db.NuevoContexto())
        {
            var otro = new TipoAnimal { Nombre = "Cerdo" };
            ctx.TiposAnimal.Add(otro);
            var c = new Corte { TipoAnimalId = otro.Id, Nombre = "Costilla", UnidadVenta = UnidadVenta.Peso, PrecioBase = 100m };
            ctx.Cortes.Add(c);
            ctx.SaveChanges();
            otroCorte = c.Id;
        }

        var r1 = await Run(s => s.GuardarRendimientosAsync(new GuardarRendimientosRequest(tipo.Id, new[] { new RendimientoEntradaDto(otroCorte, 10m) })));
        Assert.Equal(VentaFacilResultCode.CorteInvalido, r1.Code);

        var r2 = await Run(s => s.GuardarRendimientosAsync(new GuardarRendimientosRequest(tipo.Id, new[]
        {
            new RendimientoEntradaDto(bistec.Id, 10m),
            new RendimientoEntradaDto(bistec.Id, 20m)
        })));
        Assert.Equal(VentaFacilResultCode.CorteInvalido, r2.Code);
    }

    [Fact]
    public async Task GuardarRendimientos_IgnoraCortesPorPieza()
    {
        var (tipo, _, _) = Sembrar();
        Guid pieza;
        using (var ctx = _db.NuevoContexto())
        {
            var c = new Corte { TipoAnimalId = tipo.Id, Nombre = "Pollo entero", UnidadVenta = UnidadVenta.Pieza, PrecioBase = 90m };
            ctx.Cortes.Add(c);
            ctx.SaveChanges();
            pieza = c.Id;
        }

        var r = await Run(s => s.GuardarRendimientosAsync(new GuardarRendimientosRequest(tipo.Id, new[] { new RendimientoEntradaDto(pieza, 10m) })));
        Assert.Equal(VentaFacilResultCode.CorteInvalido, r.Code);

        var lista = await Run(s => s.ObtenerRendimientosAsync(tipo.Id));
        Assert.DoesNotContain(lista.Datos!.Cortes, c => c.CorteId == pieza);
    }

    [Fact]
    public async Task ListarLotes_DevuelveCostoPorKg()
    {
        var (tipo, _, _) = Sembrar();
        var usuario = _db.SembrarUsuarioParaMovimientos();
        await Run(s => s.RegistrarLoteAsync(new LoteRequest(tipo.Id, 200m, 10000m), usuario.Id));

        using var ctx = _db.NuevoContexto();
        var lotes = await Crear(ctx).ListarLotesAsync();

        var l = Assert.Single(lotes);
        Assert.Equal("Res", l.TipoAnimalNombre);
        Assert.Equal(50m, l.CostoPorKg);
        Assert.True(l.EsEstimado);
    }

    // ------------------------------------------------ lotes pesados y aprendizaje

    private Task<VentaFacilResultDto<RepartoDto>> Pesar(Guid tipoId, decimal peso, params (Guid Corte, decimal Kg)[] kgs)
    {
        var usuario = UsuarioDePrueba();
        return Run(s => s.RegistrarLoteAsync(
            new LoteRequest(tipoId, peso, peso * 50m, null, kgs.Select(k => new KgCorteDto(k.Corte, k.Kg)).ToList()),
            usuario.Id));
    }

    [Fact]
    public async Task LotePesado_QuedaMedido_ConLosKgCapturados()
    {
        var (tipo, bistec, molida) = Sembrar();

        var r = await Pesar(tipo.Id, 200m, (bistec.Id, 70m), (molida.Id, 90m));

        Assert.True(r.Exitoso, r.Mensaje);
        Assert.False(r.Datos!.EsEstimado);
        Assert.Equal("Pesado", r.Datos.Origen);
        using var ctx = _db.NuevoContexto();
        Assert.False((await ctx.LotesAnimal.SingleAsync()).EsEstimado);
        var stock = await ctx.Inventarios.ToDictionaryAsync(i => i.CorteId, i => i.StockActual);
        Assert.Equal(70m, stock[bistec.Id]);
        Assert.Equal(90m, stock[molida.Id]);
    }

    [Fact]
    public async Task LotePesado_RechazaKgMayoresAlPeso_YDatosInvalidos()
    {
        var (tipo, bistec, molida) = Sembrar();

        var exceso = await Pesar(tipo.Id, 100m, (bistec.Id, 60m), (molida.Id, 50m));
        Assert.Equal(VentaFacilResultCode.MedidoExcedePeso, exceso.Code);

        var negativo = await Pesar(tipo.Id, 100m, (bistec.Id, -1m));
        Assert.Equal(VentaFacilResultCode.KgInvalido, negativo.Code);

        var repetido = await Pesar(tipo.Id, 100m, (bistec.Id, 10m), (bistec.Id, 10m));
        Assert.Equal(VentaFacilResultCode.KgInvalido, repetido.Code);

        var ajeno = await Pesar(tipo.Id, 100m, (Guid.NewGuid(), 10m));
        Assert.Equal(VentaFacilResultCode.KgInvalido, ajeno.Code);

        var todoCero = await Pesar(tipo.Id, 100m, (bistec.Id, 0m));
        Assert.Equal(VentaFacilResultCode.KgInvalido, todoCero.Code);

        using var ctx = _db.NuevoContexto();
        Assert.Equal(0, await ctx.LotesAnimal.CountAsync());
    }

    [Fact]
    public async Task LotePesado_PermiteTipoSinPorcentajesConfigurados()
    {
        var (tipo, bistec, _) = Sembrar(bistec: null, molida: null);
        var r = await Pesar(tipo.Id, 100m, (bistec.Id, 40m));
        Assert.True(r.Exitoso, r.Mensaje);
    }

    [Fact]
    public async Task SinHistorialNiConfiguracion_PideCapturarKilos()
    {
        var (tipo, _, _) = Sembrar(bistec: null, molida: null);
        var r = await Run(s => s.PrevisualizarAsync(new LoteRequest(tipo.Id, 100m, 5000m)));
        Assert.Equal(VentaFacilResultCode.SinRendimientos, r.Code);
    }

    [Fact]
    public async Task ConMenosDeTresLotesPesados_UsaLoConfigurado()
    {
        var (tipo, bistec, molida) = Sembrar();   // configurado: 30% / 50%
        await Pesar(tipo.Id, 100m, (bistec.Id, 20m), (molida.Id, 20m));
        await Pesar(tipo.Id, 100m, (bistec.Id, 20m), (molida.Id, 20m));

        var r = await Run(s => s.PrevisualizarAsync(new LoteRequest(tipo.Id, 100m, 5000m)));

        Assert.Equal("Configurado", r.Datos!.Origen);
        Assert.True(r.Datos.EsEstimado);
        Assert.Equal(2, r.Datos.LotesMedidos);
        Assert.Equal(30m, r.Datos.Lineas.Single(l => l.CorteNombre == "Bistec").KgEstimados);
    }

    [Fact]
    public async Task ConTresLotesPesados_AprendeYEstimaSinPesar()
    {
        var (tipo, bistec, molida) = Sembrar();   // la config manual (30/50) debe ignorarse
        await Pesar(tipo.Id, 100m, (bistec.Id, 20m), (molida.Id, 40m));
        await Pesar(tipo.Id, 100m, (bistec.Id, 20m), (molida.Id, 40m));
        await Pesar(tipo.Id, 100m, (bistec.Id, 20m), (molida.Id, 40m));

        var r = await Run(s => s.PrevisualizarAsync(new LoteRequest(tipo.Id, 250m, 12500m)));

        Assert.True(r.Exitoso, r.Mensaje);
        Assert.Equal("Aprendido", r.Datos!.Origen);
        Assert.True(r.Datos.EsEstimado);
        Assert.Equal(50m, r.Datos.Lineas.Single(l => l.CorteNombre == "Bistec").KgEstimados);   // 20% de 250
        Assert.Equal(100m, r.Datos.Lineas.Single(l => l.CorteNombre == "Molida").KgEstimados);  // 40% de 250
        Assert.Equal(100m, r.Datos.KgMerma);
    }

    [Fact]
    public async Task Aprendizaje_EsPromedioPonderadoPorPeso()
    {
        var (tipo, bistec, _) = Sembrar();
        await Pesar(tipo.Id, 100m, (bistec.Id, 10m));    // 10%
        await Pesar(tipo.Id, 300m, (bistec.Id, 90m));    // 30%
        await Pesar(tipo.Id, 100m, (bistec.Id, 20m));    // 20%  -> 120 kg / 500 kg = 24%

        var r = await Run(s => s.PrevisualizarAsync(new LoteRequest(tipo.Id, 100m, 5000m)));

        Assert.Equal("Aprendido", r.Datos!.Origen);
        Assert.Equal(24m, r.Datos.Lineas.Single().KgEstimados);
    }

    [Fact]
    public async Task LotesEstimados_NoAlimentanElAprendizaje()
    {
        var (tipo, bistec, molida) = Sembrar();
        var usuario = UsuarioDePrueba();
        for (var i = 0; i < 3; i++)
        {
            await Run(s => s.RegistrarLoteAsync(new LoteRequest(tipo.Id, 100m, 5000m), usuario.Id));   // estimados
        }

        var r = await Run(s => s.PrevisualizarAsync(new LoteRequest(tipo.Id, 100m, 5000m)));

        Assert.Equal("Configurado", r.Datos!.Origen);
        Assert.Equal(0, r.Datos.LotesMedidos);
    }

    [Fact]
    public async Task Rendimientos_ExponeLoAprendidoYSiYaSeUsa()
    {
        var (tipo, bistec, _) = Sembrar();
        var antes = await Run(s => s.ObtenerRendimientosAsync(tipo.Id));
        Assert.False(antes.Datos!.UsaAprendido);
        Assert.Null(antes.Datos.Cortes.Single(c => c.CorteId == bistec.Id).PorcentajeAprendido);

        for (var i = 0; i < 3; i++) await Pesar(tipo.Id, 100m, (bistec.Id, 25m));

        var despues = await Run(s => s.ObtenerRendimientosAsync(tipo.Id));
        Assert.True(despues.Datos!.UsaAprendido);
        Assert.Equal(3, despues.Datos.LotesMedidos);
        Assert.Equal(25m, despues.Datos.Cortes.Single(c => c.CorteId == bistec.Id).PorcentajeAprendido);
    }
}
