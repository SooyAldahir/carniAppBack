using CarniApp.Application.Features.Inventario;
using CarniApp.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CarniApp.Tests;

public class InventarioServiceTests : IDisposable
{
    private readonly TestDb _db = new();
    private readonly FakeClock _clock = new();
    private Guid? _usuarioPorDefecto;

    public void Dispose() => _db.Dispose();

    /// <summary>Arma el servicio con un contexto nuevo (= un request nuevo).</summary>
    private InventarioService CrearServicio(CarniAppDbContext ctx) => new(
        new InventarioRepository(ctx),
        new MovimientoInventarioRepository(ctx),
        new CorteRepository(ctx),
        new Repository<CarniApp.Domain.Entities.Sucursal>(ctx),
        new UnitOfWork(ctx),
        _clock);

    private async Task<InventarioResultDto<InventarioItemDto>> Ajustar(Guid corteId, string tipo, decimal cantidad, string motivo, Guid? usuarioId = null)
    {
        using var ctx = _db.NuevoContexto();
        return await CrearServicio(ctx).RegistrarAjusteAsync(new RegistrarAjusteRequest(corteId, tipo, cantidad, motivo), usuarioId ?? (_usuarioPorDefecto ??= _db.SembrarUsuarioParaMovimientos().Id));
    }

    [Fact]
    public async Task PrimerAjuste_CreaFilaDeInventario_RegresionDbUpdateConcurrency()
    {
        _db.SembrarSucursal();
        var corte = _db.SembrarCorte();

        var r = await Ajustar(corte.Id, "Entrada", 20m, "Compra inicial");

        Assert.True(r.Exitoso, r.Mensaje);
        Assert.Equal(20m, r.Datos!.StockActual);

        using var ctx = _db.NuevoContexto();
        Assert.Equal(1, await ctx.Inventarios.CountAsync());
        Assert.Equal(1, await ctx.MovimientosInventario.CountAsync());
    }

    [Fact]
    public async Task SegundoAjuste_ActualizaLaMismaFila_YSumaStock()
    {
        _db.SembrarSucursal();
        var corte = _db.SembrarCorte();

        await Ajustar(corte.Id, "Entrada", 20m, "Compra");
        var r = await Ajustar(corte.Id, "Merma", -5.5m, "Merma por corte");

        Assert.True(r.Exitoso, r.Mensaje);
        Assert.Equal(14.5m, r.Datos!.StockActual);

        using var ctx = _db.NuevoContexto();
        Assert.Equal(1, await ctx.Inventarios.CountAsync());
        Assert.Equal(2, await ctx.MovimientosInventario.CountAsync());
    }

    [Fact]
    public async Task Ajuste_PuedeDejarStockNegativo_ConMotivo_RN006()
    {
        _db.SembrarSucursal();
        var corte = _db.SembrarCorte();

        var r = await Ajustar(corte.Id, "Ajuste", -3m, "Correccion de conteo");

        Assert.True(r.Exitoso, r.Mensaje);
        Assert.Equal(-3m, r.Datos!.StockActual);
    }

    [Theory]
    [InlineData("", 5, InventarioResultCode.MotivoRequerido)]
    [InlineData("   ", 5, InventarioResultCode.MotivoRequerido)]
    [InlineData("ok", 0, InventarioResultCode.CantidadInvalida)]
    public async Task Validaciones_BasicasRechazanSinTocarLaBd(string motivo, decimal cantidad, InventarioResultCode esperado)
    {
        _db.SembrarSucursal();
        var corte = _db.SembrarCorte();

        var r = await Ajustar(corte.Id, "Entrada", cantidad, motivo);

        Assert.Equal(esperado, r.Code);
        using var ctx = _db.NuevoContexto();
        Assert.Equal(0, await ctx.MovimientosInventario.CountAsync());
        Assert.Equal(0, await ctx.Inventarios.CountAsync());
    }

    [Fact]
    public async Task TipoMovimientoInvalido_EsRechazado()
    {
        _db.SembrarSucursal();
        var corte = _db.SembrarCorte();
        var r = await Ajustar(corte.Id, "Venta", 1m, "x");
        Assert.Equal(InventarioResultCode.TipoMovimientoInvalido, r.Code);
    }

    [Fact]
    public async Task EntradaNegativa_YMermaPositiva_SonRechazadas()
    {
        _db.SembrarSucursal();
        var corte = _db.SembrarCorte();

        Assert.Equal(InventarioResultCode.CantidadInvalida, (await Ajustar(corte.Id, "Entrada", -1m, "x")).Code);
        Assert.Equal(InventarioResultCode.CantidadInvalida, (await Ajustar(corte.Id, "Merma", 1m, "x")).Code);
    }

    [Fact]
    public async Task CorteInexistente_Rechaza()
    {
        _db.SembrarSucursal();
        var r = await Ajustar(Guid.NewGuid(), "Entrada", 1m, "x");
        Assert.Equal(InventarioResultCode.CorteNoEncontrado, r.Code);
    }

    [Fact]
    public async Task SinSucursalActiva_Rechaza()
    {
        var corte = _db.SembrarCorte();
        var r = await Ajustar(corte.Id, "Entrada", 1m, "x");
        Assert.Equal(InventarioResultCode.SinSucursalActiva, r.Code);
    }

    [Fact]
    public async Task Listar_MarcaAlerta_CuandoStockEstaBajoElMinimo_YMuestraCortesSinInventario()
    {
        _db.SembrarSucursal();
        var bajo = _db.SembrarCorte("Costilla", stockMinimo: 10m);
        var sinFila = _db.SembrarCorte("Molida", stockMinimo: 2m);
        var ok = _db.SembrarCorte("Arrachera", stockMinimo: 5m);

        await Ajustar(bajo.Id, "Entrada", 4m, "Compra");
        await Ajustar(ok.Id, "Entrada", 8m, "Compra");

        using var ctx = _db.NuevoContexto();
        var lista = await CrearServicio(ctx).ListarAsync();

        Assert.Equal(3, lista.Count);
        Assert.True(lista.Single(i => i.CorteNombre == "Costilla").EnAlerta);
        Assert.True(lista.Single(i => i.CorteNombre == "Molida").EnAlerta);   // 0 < 2
        Assert.False(lista.Single(i => i.CorteNombre == "Arrachera").EnAlerta);
    }

    [Fact]
    public async Task Kardex_DevuelveMovimientosDelCorte_ConElUsuarioQueLosHizo()
    {
        _db.SembrarSucursal();
        var corte = _db.SembrarCorte();
        var otro = _db.SembrarCorte("Otro");
        var usuario = _db.SembrarUsuarioParaMovimientos();

        await Ajustar(corte.Id, "Entrada", 10m, "Compra", usuario.Id);
        await Ajustar(corte.Id, "Merma", -1m, "Merma", usuario.Id);
        await Ajustar(otro.Id, "Entrada", 99m, "No debe aparecer", usuario.Id);

        using var ctx = _db.NuevoContexto();
        var r = await CrearServicio(ctx).ObtenerKardexAsync(corte.Id);

        Assert.True(r.Exitoso);
        Assert.Equal(2, r.Datos!.Count);
        Assert.All(r.Datos, m => Assert.Equal(usuario.NombreCompleto, m.UsuarioNombre));
    }

    [Fact]
    public async Task AjusteFallido_NoDejaMovimientoNiStockAMedias()
    {
        _db.SembrarSucursal();
        var corte = _db.SembrarCorte();
        await Ajustar(corte.Id, "Entrada", 10m, "Compra");

        // usuarioId inexistente => FK de MovimientoInventario falla al guardar.
        await Assert.ThrowsAnyAsync<Exception>(() => Ajustar(corte.Id, "Entrada", 5m, "Compra 2", Guid.NewGuid()));

        using var ctx = _db.NuevoContexto();
        Assert.Equal(10m, (await ctx.Inventarios.SingleAsync()).StockActual);
        Assert.Equal(1, await ctx.MovimientosInventario.CountAsync());
    }
}
