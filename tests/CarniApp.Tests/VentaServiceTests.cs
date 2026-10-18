using CarniApp.Application.Common.Exceptions;
using CarniApp.Application.Features.Inventario;
using CarniApp.Application.Features.Ventas;
using CarniApp.Domain.Entities;
using CarniApp.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CarniApp.Tests;

public class VentaServiceTests : IDisposable
{
    private readonly TestDb _db = new();
    private readonly FakeClock _clock = new();
    private Guid? _usuarioId;
    private Guid _efectivoId;
    private Guid _tarjetaId;
    private Guid _creditoId;

    public VentaServiceTests()
    {
        using var ctx = _db.NuevoContexto();
        var efectivo = new MetodoPago { Nombre = "Efectivo" };
        var tarjeta = new MetodoPago { Nombre = "Tarjeta" };
        var credito = new MetodoPago { Nombre = "Credito" };
        ctx.MetodosPago.AddRange(efectivo, tarjeta, credito);
        ctx.SaveChanges();
        _efectivoId = efectivo.Id;
        _tarjetaId = tarjeta.Id;
        _creditoId = credito.Id;
    }

    public void Dispose() => _db.Dispose();

    private Guid Usuario() => _usuarioId ??= _db.SembrarUsuarioParaMovimientos().Id;

    private VentaService CrearServicio(CarniAppDbContext ctx) => new(
        new VentaRepository(ctx),
        new MetodoPagoRepository(ctx),
        new CorteRepository(ctx),
        new InventarioRepository(ctx),
        new MovimientoInventarioRepository(ctx),
        new Repository<Sucursal>(ctx),
        new Repository<CarniApp.Domain.Entities.Negocio>(ctx),
        new Repository<Cliente>(ctx),
        _posSettings,
        new UnitOfWork(ctx),
        _clock);

    private readonly FakePosSettings _posSettings = new();

    private async Task<T> Con<T>(Func<VentaService, Task<T>> accion)
    {
        using var ctx = _db.NuevoContexto();
        return await accion(CrearServicio(ctx));
    }

    /// <summary>Sucursal + corte con stock inicial, listo para vender.</summary>
    private async Task<Corte> CorteConStock(decimal stock, decimal precio = 100m, string nombre = "Bistec", UnidadVenta unidad = UnidadVenta.Peso)
    {
        if (!await HaySucursal()) _db.SembrarSucursal();
        var corte = _db.SembrarCorte(nombre, precio: precio);

        if (unidad != UnidadVenta.Peso)
        {
            using var ctx = _db.NuevoContexto();
            var c = await ctx.Cortes.SingleAsync(x => x.Id == corte.Id);
            c.UnidadVenta = unidad;
            await ctx.SaveChangesAsync();
        }

        if (stock != 0)
        {
            using var ctx = _db.NuevoContexto();
            var inv = new InventarioService(
                new InventarioRepository(ctx), new MovimientoInventarioRepository(ctx), new CorteRepository(ctx),
                new Repository<Sucursal>(ctx), new UnitOfWork(ctx), _clock);
            var r = await inv.RegistrarAjusteAsync(new RegistrarAjusteRequest(corte.Id, "Entrada", stock, "Stock inicial"), Usuario());
            Assert.True(r.Exitoso, r.Mensaje);
        }

        return corte;
    }

    private async Task<bool> HaySucursal()
    {
        using var ctx = _db.NuevoContexto();
        return await ctx.Sucursales.AnyAsync();
    }

    private async Task<decimal> StockDe(Guid corteId)
    {
        using var ctx = _db.NuevoContexto();
        return (await ctx.Inventarios.SingleAsync(i => i.CorteId == corteId)).StockActual;
    }

    private Task<VentaResultDto<VentaDto>> Vender(IReadOnlyList<VentaLineaRequest> lineas, IReadOnlyList<VentaPagoRequest> pagos) =>
        Con(s => s.CrearAsync(new CrearVentaRequest(lineas, pagos), Usuario()));

    private Task<VentaResultDto<VentaDto>> VenderConDescuento(
        IReadOnlyList<VentaLineaRequest> lineas,
        IReadOnlyList<VentaPagoRequest> pagos,
        decimal descuentoTotal,
        params string[] permisos) =>
        Con(s => s.CrearAsync(new CrearVentaRequest(lineas, pagos, descuentoTotal), Usuario(), permisos));

    private Task<VentaResultDto<VentaDto>> VenderUno(Corte corte, decimal cantidad, decimal totalEsperado) =>
        Vender(new[] { new VentaLineaRequest(corte.Id, cantidad) }, new[] { new VentaPagoRequest(_efectivoId, totalEsperado) });

    [Fact]
    public async Task Venta_Correcta_GuardaTicket_DescuentaStock_YRegistraKardex()
    {
        var corte = await CorteConStock(10m, precio: 120m);

        var r = await VenderUno(corte, 2.5m, 300m);

        Assert.True(r.Exitoso, r.Mensaje);
        Assert.Equal(300m, r.Datos!.Total);
        Assert.Equal(EstadoVenta.Completada, r.Datos.Estado);
        Assert.Single(r.Datos.Lineas);
        Assert.Equal(120m, r.Datos.Lineas[0].PrecioUnitario);
        Assert.Equal(7.5m, await StockDe(corte.Id));

        using var ctx = _db.NuevoContexto();
        var mov = await ctx.MovimientosInventario
            .Where(m => m.TipoMovimiento == TipoMovimientoInventario.Venta)
            .SingleAsync();
        Assert.Equal(-2.5m, mov.Cantidad);
    }

    [Fact]
    public async Task Venta_UsaElPrecioDelCatalogo_NoUnoEnviadoPorElCliente()
    {
        var corte = await CorteConStock(10m, precio: 80m);

        // El request ni siquiera tiene campo de precio: el total sale del catalogo.
        var r = await VenderUno(corte, 1m, 80m);
        Assert.True(r.Exitoso, r.Mensaje);
        Assert.Equal(80m, r.Datos!.Lineas[0].PrecioUnitario);
    }

    [Fact]
    public async Task Venta_RedondeaElImporteACentavos()
    {
        var corte = await CorteConStock(10m, precio: 149.99m);

        // 0.333 * 149.99 = 49.94667 -> 49.95
        var r = await VenderUno(corte, 0.333m, 49.95m);

        Assert.True(r.Exitoso, r.Mensaje);
        Assert.Equal(49.95m, r.Datos!.Total);
    }

    [Fact]
    public async Task Venta_ConStockInsuficiente_SeRechaza_SinTocarNada()
    {
        var corte = await CorteConStock(2m, precio: 100m);

        var r = await VenderUno(corte, 3m, 300m);

        Assert.Equal(VentaResultCode.StockInsuficiente, r.Code);
        Assert.Equal(2m, await StockDe(corte.Id));
        using var ctx = _db.NuevoContexto();
        Assert.Equal(0, await ctx.Ventas.CountAsync());
    }

    [Fact]
    public async Task Venta_DeCorteSinInventarioPrevio_SeRechazaPorStockInsuficiente()
    {
        var corte = await CorteConStock(0m);
        var r = await VenderUno(corte, 1m, 100m);
        Assert.Equal(VentaResultCode.StockInsuficiente, r.Code);
    }

    [Fact]
    public async Task Venta_ConMismoCorteEnVariasLineas_SumaParaValidarYDescontar()
    {
        var corte = await CorteConStock(5m, precio: 10m);

        var sobrepasa = await Vender(
            new[] { new VentaLineaRequest(corte.Id, 3m), new VentaLineaRequest(corte.Id, 3m) },
            new[] { new VentaPagoRequest(_efectivoId, 60m) });
        Assert.Equal(VentaResultCode.StockInsuficiente, sobrepasa.Code);

        var ok = await Vender(
            new[] { new VentaLineaRequest(corte.Id, 2m), new VentaLineaRequest(corte.Id, 3m) },
            new[] { new VentaPagoRequest(_efectivoId, 50m) });
        Assert.True(ok.Exitoso, ok.Mensaje);
        Assert.Equal(0m, await StockDe(corte.Id));
    }

    [Fact]
    public async Task Venta_PagoMixto_Cuadra()
    {
        var corte = await CorteConStock(10m, precio: 100m);

        var r = await Vender(
            new[] { new VentaLineaRequest(corte.Id, 3m) },
            new[] { new VentaPagoRequest(_efectivoId, 100m), new VentaPagoRequest(_tarjetaId, 200m) });

        Assert.True(r.Exitoso, r.Mensaje);
        Assert.Equal(2, r.Datos!.Pagos.Count);
        Assert.Equal(300m, r.Datos.Pagos.Sum(p => p.Importe));
    }

    [Theory]
    [InlineData(299.99)]
    [InlineData(300.01)]
    public async Task Venta_ConPagosQueNoCuadran_SeRechaza(double pago)
    {
        var corte = await CorteConStock(10m, precio: 100m);

        var r = await VenderUno(corte, 3m, (decimal)pago);

        Assert.Equal(VentaResultCode.PagosNoCuadran, r.Code);
        Assert.Equal(10m, await StockDe(corte.Id));
    }

    [Fact]
    public async Task Venta_SinPagos_ConPagoEnCeroOMetodoInexistente_SeRechaza()
    {
        var corte = await CorteConStock(10m, precio: 100m);
        var linea = new[] { new VentaLineaRequest(corte.Id, 1m) };

        Assert.Equal(VentaResultCode.SinPagos, (await Vender(linea, Array.Empty<VentaPagoRequest>())).Code);
        Assert.Equal(VentaResultCode.PagoInvalido, (await Vender(linea, new[] { new VentaPagoRequest(_efectivoId, 0m) })).Code);
        Assert.Equal(VentaResultCode.MetodoPagoNoEncontrado, (await Vender(linea, new[] { new VentaPagoRequest(Guid.NewGuid(), 100m) })).Code);
    }

    private Guid SembrarCliente(decimal limite, decimal saldo = 0m, string nombre = "Don Pepe")
    {
        using var ctx = _db.NuevoContexto();
        var c = new Cliente { Nombre = nombre, LimiteCredito = limite, SaldoCredito = saldo };
        ctx.Clientes.Add(c);
        ctx.SaveChanges();
        return c.Id;
    }

    private async Task<decimal> SaldoDe(Guid clienteId)
    {
        using var ctx = _db.NuevoContexto();
        return (await ctx.Clientes.SingleAsync(c => c.Id == clienteId)).SaldoCredito;
    }

    private Task<VentaResultDto<VentaDto>> VenderACliente(Guid? clienteId, IReadOnlyList<VentaLineaRequest> lineas, IReadOnlyList<VentaPagoRequest> pagos) =>
        Con(s => s.CrearAsync(new CrearVentaRequest(lineas, pagos, 0m, clienteId), Usuario()));

    [Fact]
    public async Task Venta_ACredito_SinCliente_SeRechaza()
    {
        var corte = await CorteConStock(10m, precio: 100m);

        var r = await Vender(
            new[] { new VentaLineaRequest(corte.Id, 1m) },
            new[] { new VentaPagoRequest(_creditoId, 100m) });

        Assert.Equal(VentaResultCode.MetodoPagoNoDisponible, r.Code);
        Assert.Equal(10m, await StockDe(corte.Id));
    }

    [Fact]
    public async Task Venta_ACredito_SubeElSaldo_YGuardaElCliente()
    {
        var corte = await CorteConStock(10m, precio: 100m);
        var cliente = SembrarCliente(limite: 500m);

        var r = await VenderACliente(cliente,
            new[] { new VentaLineaRequest(corte.Id, 3m) },
            new[] { new VentaPagoRequest(_creditoId, 300m) });

        Assert.True(r.Exitoso, r.Mensaje);
        Assert.Equal(cliente, r.Datos!.ClienteId);
        Assert.Equal("Don Pepe", r.Datos.ClienteNombre);
        Assert.Equal(300m, await SaldoDe(cliente));
        Assert.Equal(7m, await StockDe(corte.Id));
    }

    [Fact]
    public async Task Venta_ACredito_Parcial_SoloLoFiadoSumaAlSaldo()
    {
        var corte = await CorteConStock(10m, precio: 100m);
        var cliente = SembrarCliente(limite: 500m);

        var r = await VenderACliente(cliente,
            new[] { new VentaLineaRequest(corte.Id, 3m) },
            new[] { new VentaPagoRequest(_efectivoId, 100m), new VentaPagoRequest(_creditoId, 200m) });

        Assert.True(r.Exitoso, r.Mensaje);
        Assert.Equal(200m, await SaldoDe(cliente));
    }

    [Fact]
    public async Task Venta_ACredito_QueExcedeElDisponible_SeRechazaSinTocarNada()
    {
        var corte = await CorteConStock(10m, precio: 100m);
        var cliente = SembrarCliente(limite: 500m, saldo: 300m);   // disponible: 200

        var r = await VenderACliente(cliente,
            new[] { new VentaLineaRequest(corte.Id, 3m) },
            new[] { new VentaPagoRequest(_creditoId, 300m) });

        Assert.Equal(VentaResultCode.CreditoExcedido, r.Code);
        Assert.Equal(300m, await SaldoDe(cliente));
        Assert.Equal(10m, await StockDe(corte.Id));
    }

    [Fact]
    public async Task Venta_ConClienteInexistente_SeRechaza()
    {
        var corte = await CorteConStock(10m, precio: 100m);

        var r = await VenderACliente(Guid.NewGuid(),
            new[] { new VentaLineaRequest(corte.Id, 1m) },
            new[] { new VentaPagoRequest(_efectivoId, 100m) });

        Assert.Equal(VentaResultCode.ClienteNoEncontrado, r.Code);
    }

    [Fact]
    public async Task Venta_ConCliente_PeroPagadaEnEfectivo_NoTocaElSaldo()
    {
        var corte = await CorteConStock(10m, precio: 100m);
        var cliente = SembrarCliente(limite: 500m, saldo: 50m);

        var r = await VenderACliente(cliente,
            new[] { new VentaLineaRequest(corte.Id, 1m) },
            new[] { new VentaPagoRequest(_efectivoId, 100m) });

        Assert.True(r.Exitoso, r.Mensaje);
        Assert.Equal(50m, await SaldoDe(cliente));
    }

    [Fact]
    public async Task Anular_VentaACredito_QuitaLaDeudaDelCliente()
    {
        var corte = await CorteConStock(10m, precio: 100m);
        var cliente = SembrarCliente(limite: 500m, saldo: 100m);
        var venta = (await VenderACliente(cliente,
            new[] { new VentaLineaRequest(corte.Id, 2m) },
            new[] { new VentaPagoRequest(_creditoId, 200m) })).Datos!;
        Assert.Equal(300m, await SaldoDe(cliente));

        var r = await Con(s => s.AnularAsync(venta.Id, "Error", Usuario()));

        Assert.True(r.Exitoso, r.Mensaje);
        Assert.Equal(100m, await SaldoDe(cliente));
    }

    [Fact]
    public async Task Anular_VentaACredito_YaAbonada_DejaElSaldoEnCeroSinNegativos()
    {
        var corte = await CorteConStock(10m, precio: 100m);
        var cliente = SembrarCliente(limite: 500m);
        var venta = (await VenderACliente(cliente,
            new[] { new VentaLineaRequest(corte.Id, 2m) },
            new[] { new VentaPagoRequest(_creditoId, 200m) })).Datos!;
        using (var ctx = _db.NuevoContexto())
        {
            ctx.Clientes.Single(c => c.Id == cliente).SaldoCredito = 50m;   // ya abono 150
            ctx.SaveChanges();
        }

        Assert.True((await Con(s => s.AnularAsync(venta.Id, "Error", Usuario()))).Exitoso);
        Assert.Equal(0m, await SaldoDe(cliente));
    }

    [Fact]
    public async Task DosCargosSimultaneos_AlSaldo_ElSegundoFallaPorConcurrencia()
    {
        var cliente = SembrarCliente(limite: 300m);

        // Dos cajas leen el mismo saldo (0) antes de que cualquiera guarde.
        using var ctxA = _db.NuevoContexto();
        using var ctxB = _db.NuevoContexto();
        var cA = await ctxA.Clientes.SingleAsync(c => c.Id == cliente);
        var cB = await ctxB.Clientes.SingleAsync(c => c.Id == cliente);

        cA.SaldoCredito += 200m;
        await new UnitOfWork(ctxA).SaveChangesAsync();

        cB.SaldoCredito += 200m;
        await Assert.ThrowsAsync<ConcurrencyConflictException>(() => new UnitOfWork(ctxB).SaveChangesAsync());

        Assert.Equal(200m, await SaldoDe(cliente));
    }

    [Fact]
    public async Task Ticket_ImprimeDireccionDelCliente_YLaNota_AjustadasA32Columnas()
    {
        var corte = await CorteConStock(10m, precio: 100m);
        var cliente = SembrarCliente(limite: 500m, nombre: "Dona Lupe");
        using (var ctx = _db.NuevoContexto())
        {
            ctx.Clientes.Single(c => c.Id == cliente).Direccion = "Av. Siempre Viva 742, Colonia Centro, entre Juarez y Morelos";
            ctx.SaveChanges();
        }

        var venta = (await Con(s => s.CrearAsync(
            new CrearVentaRequest(
                new[] { new VentaLineaRequest(corte.Id, 1m) },
                new[] { new VentaPagoRequest(_efectivoId, 100m) },
                0m, cliente, "  Entregar en la puerta de atras, tocar dos veces  "),
            Usuario()))).Datos!;

        var ticket = (await Con(s => s.ObtenerTicketAsync(venta.Id))).Datos!.Texto;

        Assert.Contains("Dir: Av. Siempre Viva 742, Colonia", ticket);
        Assert.Contains("Nota: Entregar en la puerta de atras,", ticket);
        Assert.Contains("tocar dos veces", ticket);
        Assert.All(ticket.Split('\n'), linea => Assert.True(linea.TrimEnd('\r').Length <= 32, $"Linea de mas de 32 columnas: '{linea}'"));
        Assert.Equal("Entregar en la puerta de atras, tocar dos veces", venta.Nota);
    }

    [Fact]
    public async Task Nota_Vacia_SeGuardaNula_YMuyLarga_SeRechaza()
    {
        var corte = await CorteConStock(10m, precio: 100m);
        var lineas = new[] { new VentaLineaRequest(corte.Id, 1m) };
        var pagos = new[] { new VentaPagoRequest(_efectivoId, 100m) };

        var sinNota = await Con(s => s.CrearAsync(new CrearVentaRequest(lineas, pagos, 0m, null, "   "), Usuario()));
        Assert.True(sinNota.Exitoso, sinNota.Mensaje);
        Assert.Null(sinNota.Datos!.Nota);

        var larga = await Con(s => s.CrearAsync(new CrearVentaRequest(lineas, pagos, 0m, null, new string('x', 201)), Usuario()));
        Assert.Equal(VentaResultCode.NotaInvalida, larga.Code);
        Assert.Equal(9m, await StockDe(corte.Id));   // solo se vendio la primera
    }

    [Fact]
    public async Task Ticket_MuestraElNombreDelCliente()
    {
        var corte = await CorteConStock(10m, precio: 100m);
        var cliente = SembrarCliente(limite: 500m, nombre: "Dona Lupe");
        var venta = (await VenderACliente(cliente,
            new[] { new VentaLineaRequest(corte.Id, 1m) },
            new[] { new VentaPagoRequest(_creditoId, 100m) })).Datos!;

        var ticket = await Con(s => s.ObtenerTicketAsync(venta.Id));

        Assert.Contains("Cliente: Dona Lupe", ticket.Datos!.Texto);
    }

    [Fact]
    public async Task Venta_ConCantidadInvalida_OSinArticulos_OCorteInexistente_SeRechaza()
    {
        var corte = await CorteConStock(10m, precio: 100m);
        var pago = new[] { new VentaPagoRequest(_efectivoId, 100m) };

        Assert.Equal(VentaResultCode.SinArticulos, (await Vender(Array.Empty<VentaLineaRequest>(), pago)).Code);
        Assert.Equal(VentaResultCode.CantidadInvalida, (await Vender(new[] { new VentaLineaRequest(corte.Id, 0m) }, pago)).Code);
        Assert.Equal(VentaResultCode.CantidadInvalida, (await Vender(new[] { new VentaLineaRequest(corte.Id, -1m) }, pago)).Code);
        Assert.Equal(VentaResultCode.CorteNoEncontrado, (await Vender(new[] { new VentaLineaRequest(Guid.NewGuid(), 1m) }, pago)).Code);
    }

    [Fact]
    public async Task Venta_PorPieza_NoAceptaFracciones()
    {
        var corte = await CorteConStock(10m, precio: 50m, nombre: "Chorizo", unidad: UnidadVenta.Pieza);

        var fraccion = await VenderUno(corte, 1.5m, 75m);
        Assert.Equal(VentaResultCode.CantidadInvalida, fraccion.Code);

        var entera = await VenderUno(corte, 2m, 100m);
        Assert.True(entera.Exitoso, entera.Mensaje);
    }

    [Fact]
    public async Task Venta_DeCorteDadoDeBaja_SeRechaza()
    {
        var corte = await CorteConStock(10m, precio: 100m);
        using (var ctx = _db.NuevoContexto())
        {
            (await ctx.Cortes.SingleAsync(c => c.Id == corte.Id)).Activo = false;
            await ctx.SaveChangesAsync();
        }

        Assert.Equal(VentaResultCode.CorteNoEncontrado, (await VenderUno(corte, 1m, 100m)).Code);
    }

    [Fact]
    public async Task Anular_RepoNeElStock_RegistraKardex_YMarcaLaVenta()
    {
        var corte = await CorteConStock(10m, precio: 100m);
        var venta = (await VenderUno(corte, 4m, 400m)).Datos!;
        Assert.Equal(6m, await StockDe(corte.Id));

        var r = await Con(s => s.AnularAsync(venta.Id, "Error de captura", Usuario()));

        Assert.True(r.Exitoso, r.Mensaje);
        Assert.Equal(EstadoVenta.Anulada, r.Datos!.Estado);
        Assert.Equal("Error de captura", r.Datos.MotivoAnulacion);
        Assert.Equal(10m, await StockDe(corte.Id));

        using var ctx = _db.NuevoContexto();
        Assert.Equal(2, await ctx.MovimientosInventario.CountAsync(m => m.TipoMovimiento == TipoMovimientoInventario.Venta));
    }

    [Fact]
    public async Task Anular_SinMotivo_YaAnulada_OInexistente_SeRechaza()
    {
        var corte = await CorteConStock(10m, precio: 100m);
        var venta = (await VenderUno(corte, 1m, 100m)).Datos!;

        Assert.Equal(VentaResultCode.MotivoRequerido, (await Con(s => s.AnularAsync(venta.Id, "  ", Usuario()))).Code);
        Assert.Equal(VentaResultCode.VentaNoEncontrada, (await Con(s => s.AnularAsync(Guid.NewGuid(), "x", Usuario()))).Code);

        Assert.True((await Con(s => s.AnularAsync(venta.Id, "Cliente se arrepintio", Usuario()))).Exitoso);
        Assert.Equal(VentaResultCode.VentaYaAnulada, (await Con(s => s.AnularAsync(venta.Id, "otra vez", Usuario()))).Code);
        Assert.Equal(10m, await StockDe(corte.Id));   // no se repuso dos veces
    }

    [Fact]
    public async Task Obtener_DevuelveElTicketCompleto()
    {
        var corte = await CorteConStock(10m, precio: 100m, nombre: "Costilla");
        var creada = (await VenderUno(corte, 2m, 200m)).Datos!;

        var r = await Con(s => s.ObtenerAsync(creada.Id));

        Assert.True(r.Exitoso);
        Assert.Equal("Costilla", r.Datos!.Lineas.Single().CorteNombre);
        Assert.Equal("Efectivo", r.Datos.Pagos.Single().MetodoPago);
        Assert.NotNull(r.Datos.UsuarioNombre);
        Assert.Equal(VentaResultCode.VentaNoEncontrada, (await Con(s => s.ObtenerAsync(Guid.NewGuid()))).Code);
    }

    [Fact]
    public async Task Listar_FiltraPorRangoDeFechas_YOrdenaMasRecientePrimero()
    {
        var corte = await CorteConStock(50m, precio: 10m);
        var ayer = _clock.Now;

        _clock.Now = ayer.AddDays(-1);
        var vieja = (await VenderUno(corte, 1m, 10m)).Datos!;
        _clock.Now = ayer;
        var a = (await VenderUno(corte, 1m, 10m)).Datos!;
        _clock.Now = ayer.AddMinutes(5);
        var b = (await VenderUno(corte, 1m, 10m)).Datos!;

        var hoy = await Con(s => s.ListarAsync(null, null));
        Assert.Equal(new[] { b.Id, a.Id }, hoy.Select(v => v.Id).ToArray());
        Assert.DoesNotContain(hoy, v => v.Id == vieja.Id);

        var todas = await Con(s => s.ListarAsync(ayer.AddDays(-2), ayer.AddDays(2)));
        Assert.Equal(3, todas.Count);
    }

    [Fact]
    public async Task Catalogo_MuestraPrecioYStock_YMetodosDePago()
    {
        var corte = await CorteConStock(7m, precio: 99m, nombre: "Arrachera");

        var catalogo = await Con(s => s.ObtenerCatalogoAsync());
        var item = catalogo.Single(c => c.CorteId == corte.Id);
        Assert.Equal(99m, item.PrecioBase);
        Assert.Equal(7m, item.StockActual);

        var metodos = await Con(s => s.ListarMetodosPagoAsync());
        Assert.True(metodos.Single(m => m.Nombre == "Credito").Disponible);
        Assert.True(metodos.Single(m => m.Nombre == "Efectivo").Disponible);
    }

    [Fact]
    public async Task Catalogo_IncluyeElCodigoDeBarras_ParaQueElPosLoBusqueAlEscanear()
    {
        var corte = await CorteConStock(3m, nombre: "Costilla");
        using (var ctx = _db.NuevoContexto())
        {
            ctx.Cortes.Single(c => c.Id == corte.Id).CodigoBarras = "7501234567890";
            ctx.SaveChanges();
        }

        var catalogo = await Con(s => s.ObtenerCatalogoAsync());
        Assert.Equal("7501234567890", catalogo.Single(c => c.CorteId == corte.Id).CodigoBarras);
    }

    [Fact]
    public async Task DosVentasSimultaneas_DelMismoStock_LaSegundaFallaPorConcurrencia()
    {
        var corte = await CorteConStock(5m, precio: 10m);

        // Dos "cajeras" leen el mismo stock (5) antes de que cualquiera guarde.
        using var ctxA = _db.NuevoContexto();
        using var ctxB = _db.NuevoContexto();
        var invA = await ctxA.Inventarios.SingleAsync(i => i.CorteId == corte.Id);
        var invB = await ctxB.Inventarios.SingleAsync(i => i.CorteId == corte.Id);

        invA.StockActual -= 5m;
        await new UnitOfWork(ctxA).SaveChangesAsync();

        invB.StockActual -= 5m;
        await Assert.ThrowsAsync<ConcurrencyConflictException>(() => new UnitOfWork(ctxB).SaveChangesAsync());

        Assert.Equal(0m, await StockDe(corte.Id));
    }

    // ---------------- Sprint 5: descuentos, venta mixta y ticket ----------------

    [Fact]
    public async Task Descuento_DeLinea_ReduceElTotal_YSeConservaEnElTicket()
    {
        var corte = await CorteConStock(10m, precio: 100m);

        // 2 kg x 100 = 200, con $10 de descuento en la linea (5%) -> total 190.
        var r = await VenderConDescuento(
            new[] { new VentaLineaRequest(corte.Id, 2m, 10m) },
            new[] { new VentaPagoRequest(_efectivoId, 190m) },
            0m,
            PermisosPos.Descuento);

        Assert.True(r.Exitoso, r.Mensaje);
        Assert.Equal(200m, r.Datos!.Subtotal);
        Assert.Equal(10m, r.Datos.Descuento);
        Assert.Equal(190m, r.Datos.Total);
        Assert.Equal(10m, r.Datos.Lineas.Single().Descuento);
        Assert.Equal(190m, r.Datos.Lineas.Single().Importe);

        // Al releer el ticket el descuento de linea se reconstruye igual.
        var releida = (await Con(s => s.ObtenerAsync(r.Datos.Id))).Datos!;
        Assert.Equal(10m, releida.Lineas.Single().Descuento);
    }

    [Fact]
    public async Task Descuento_Global_SeAplicaDespuesDelDeLinea()
    {
        var corte = await CorteConStock(10m, precio: 100m);

        // 300 bruto - 10 (linea) - 20 (global) = 270. 10% de 300 = 30 -> justo en el limite.
        var r = await VenderConDescuento(
            new[] { new VentaLineaRequest(corte.Id, 3m, 10m) },
            new[] { new VentaPagoRequest(_efectivoId, 270m) },
            20m,
            PermisosPos.Descuento);

        Assert.True(r.Exitoso, r.Mensaje);
        Assert.Equal(300m, r.Datos!.Subtotal);
        Assert.Equal(30m, r.Datos.Descuento);
        Assert.Equal(270m, r.Datos.Total);
    }

    [Fact]
    public async Task Descuento_SinPermiso_EsRechazado()
    {
        var corte = await CorteConStock(10m, precio: 100m);

        var r = await VenderConDescuento(
            new[] { new VentaLineaRequest(corte.Id, 1m) },
            new[] { new VentaPagoRequest(_efectivoId, 95m) },
            5m);   // sin permisos

        Assert.Equal(VentaResultCode.DescuentoNoAutorizado, r.Code);
        Assert.Equal(10m, await StockDe(corte.Id));
    }

    [Fact]
    public async Task Descuento_QuePasaDelPorcentajePermitido_RequierePermisoAlto()
    {
        var corte = await CorteConStock(10m, precio: 100m);
        var linea = new[] { new VentaLineaRequest(corte.Id, 1m) };
        var pago = new[] { new VentaPagoRequest(_efectivoId, 80m) };   // 20% de descuento

        var basico = await VenderConDescuento(linea, pago, 20m, PermisosPos.Descuento);
        Assert.Equal(VentaResultCode.DescuentoNoAutorizado, basico.Code);

        var alto = await VenderConDescuento(linea, pago, 20m, PermisosPos.DescuentoAlto);
        Assert.True(alto.Exitoso, alto.Mensaje);
    }

    [Fact]
    public async Task Descuento_ElTopeBasicoEsConfigurable()
    {
        _posSettings.Maximo = 25m;
        var corte = await CorteConStock(10m, precio: 100m);

        var r = await VenderConDescuento(
            new[] { new VentaLineaRequest(corte.Id, 1m) },
            new[] { new VentaPagoRequest(_efectivoId, 80m) },
            20m,
            PermisosPos.Descuento);

        Assert.True(r.Exitoso, r.Mensaje);
    }

    [Fact]
    public async Task Descuento_Invalido_SeRechaza()
    {
        var corte = await CorteConStock(10m, precio: 100m);
        var pago = new[] { new VentaPagoRequest(_efectivoId, 100m) };

        // Negativo, mayor a la linea, global mayor al total, o que deja el total en cero.
        Assert.Equal(VentaResultCode.DescuentoInvalido, (await VenderConDescuento(
            new[] { new VentaLineaRequest(corte.Id, 1m, -1m) }, pago, 0m, PermisosPos.DescuentoAlto)).Code);
        Assert.Equal(VentaResultCode.DescuentoInvalido, (await VenderConDescuento(
            new[] { new VentaLineaRequest(corte.Id, 1m, 150m) }, pago, 0m, PermisosPos.DescuentoAlto)).Code);
        Assert.Equal(VentaResultCode.DescuentoInvalido, (await VenderConDescuento(
            new[] { new VentaLineaRequest(corte.Id, 1m) }, pago, 101m, PermisosPos.DescuentoAlto)).Code);
        Assert.Equal(VentaResultCode.DescuentoInvalido, (await VenderConDescuento(
            new[] { new VentaLineaRequest(corte.Id, 1m) }, pago, 100m, PermisosPos.DescuentoAlto)).Code);
    }

    [Fact]
    public async Task Descuento_LosPagosDebenCuadrarConElTotalYaConDescuento()
    {
        var corte = await CorteConStock(10m, precio: 100m);

        var r = await VenderConDescuento(
            new[] { new VentaLineaRequest(corte.Id, 1m) },
            new[] { new VentaPagoRequest(_efectivoId, 100m) },   // ignora el descuento
            5m,
            PermisosPos.Descuento);

        Assert.Equal(VentaResultCode.PagosNoCuadran, r.Code);
    }

    [Fact]
    public async Task VentaMixta_PesoYPieza_EnUnMismoTicket_DescuentaCadaStock()
    {
        var res = await CorteConStock(10m, precio: 120m, nombre: "Bistec");
        var chorizo = await CorteConStock(20m, precio: 45m, nombre: "Chorizo", unidad: UnidadVenta.Pieza);

        var r = await Vender(
            new[] { new VentaLineaRequest(res.Id, 1.5m), new VentaLineaRequest(chorizo.Id, 4m) },
            new[] { new VentaPagoRequest(_efectivoId, 100m), new VentaPagoRequest(_tarjetaId, 260m) });

        // 1.5 x 120 = 180, 4 x 45 = 180 -> 360
        Assert.True(r.Exitoso, r.Mensaje);
        Assert.Equal(360m, r.Datos!.Total);
        Assert.Equal(2, r.Datos.Lineas.Count);
        Assert.Equal(8.5m, await StockDe(res.Id));
        Assert.Equal(16m, await StockDe(chorizo.Id));
    }

    [Fact]
    public async Task Ticket_IncluyeFolio_Lineas_Descuento_Total_YPagos_En32Columnas()
    {
        using (var ctx = _db.NuevoContexto())
        {
            ctx.Negocios.Add(new CarniApp.Domain.Entities.Negocio
            {
                NombreComercial = "Carniceria Don Pepe",
                Direccion = "Calle 5 #10",
                Telefono = "5551234567",
                ZonaHoraria = "UTC"
            });
            await ctx.SaveChangesAsync();
        }

        var corte = await CorteConStock(10m, precio: 100m, nombre: "Costilla");
        var venta = (await VenderConDescuento(
            new[] { new VentaLineaRequest(corte.Id, 2m, 10m) },
            new[] { new VentaPagoRequest(_efectivoId, 190m) },
            0m,
            PermisosPos.Descuento)).Datos!;

        var ticket = (await Con(s => s.ObtenerTicketAsync(venta.Id))).Datos!.Texto;

        Assert.Contains("Carniceria Don Pepe", ticket);
        Assert.Contains("Costilla", ticket);
        Assert.Contains("2 kg x $100.00", ticket);
        Assert.Contains("-$10.00", ticket);
        Assert.Contains("TOTAL", ticket);
        Assert.Contains("$190.00", ticket);
        Assert.Contains("Efectivo", ticket);
        Assert.All(ticket.Split('\n'), linea => Assert.True(linea.TrimEnd('\r').Length <= TicketBuilder.Ancho, $"Linea mas ancha que {TicketBuilder.Ancho}: '{linea}'"));
    }

    [Fact]
    public async Task Ticket_DeVentaAnulada_LoIndica_YDeVentaInexistenteFalla()
    {
        var corte = await CorteConStock(10m, precio: 100m);
        var venta = (await VenderUno(corte, 1m, 100m)).Datos!;
        await Con(s => s.AnularAsync(venta.Id, "Prueba", Usuario()));

        var ticket = (await Con(s => s.ObtenerTicketAsync(venta.Id))).Datos!.Texto;

        Assert.Contains("VENTA ANULADA", ticket);
        Assert.Equal(VentaResultCode.VentaNoEncontrada, (await Con(s => s.ObtenerTicketAsync(Guid.NewGuid()))).Code);
    }
}
