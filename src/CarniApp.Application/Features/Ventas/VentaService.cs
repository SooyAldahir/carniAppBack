using CarniApp.Application.Common.Exceptions;
using CarniApp.Application.Common.Interfaces;
using CarniApp.Domain.Entities;

namespace CarniApp.Application.Features.Ventas;

/// <summary>
/// RF-POS-001/002/008/011 y RN-003. Una venta es una sola transaccion:
/// encabezado + lineas + pagos + descuento de inventario + movimientos de
/// kardex se guardan juntos o no se guarda nada. El stock lleva un token de
/// concurrencia (ver CarniAppDbContext), asi que dos ventas simultaneas del
/// mismo corte no pueden sobrevender: la segunda recibe Conflicto y reintenta.
///
/// MVP de una sola sucursal (D-01): opera sobre la primera Sucursal activa.
/// Credito queda deshabilitado hasta el Sprint 7 (requiere cliente).
/// </summary>
public class VentaService : IVentaService
{
    private readonly IVentaRepository _ventaRepository;
    private readonly IMetodoPagoRepository _metodoPagoRepository;
    private readonly ICorteRepository _corteRepository;
    private readonly IInventarioRepository _inventarioRepository;
    private readonly IMovimientoInventarioRepository _movimientoRepository;
    private readonly IRepository<Sucursal> _sucursalRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IDateTimeProvider _dateTimeProvider;

    public VentaService(
        IVentaRepository ventaRepository,
        IMetodoPagoRepository metodoPagoRepository,
        ICorteRepository corteRepository,
        IInventarioRepository inventarioRepository,
        IMovimientoInventarioRepository movimientoRepository,
        IRepository<Sucursal> sucursalRepository,
        IUnitOfWork unitOfWork,
        IDateTimeProvider dateTimeProvider)
    {
        _ventaRepository = ventaRepository;
        _metodoPagoRepository = metodoPagoRepository;
        _corteRepository = corteRepository;
        _inventarioRepository = inventarioRepository;
        _movimientoRepository = movimientoRepository;
        _sucursalRepository = sucursalRepository;
        _unitOfWork = unitOfWork;
        _dateTimeProvider = dateTimeProvider;
    }

    public async Task<IReadOnlyList<CortePosDto>> ObtenerCatalogoAsync(CancellationToken cancellationToken = default)
    {
        var cortes = await _corteRepository.GetAllConTipoAnimalAsync(cancellationToken);
        var sucursal = await ObtenerSucursalPrincipalAsync(cancellationToken);

        var stockPorCorte = sucursal is null
            ? new Dictionary<Guid, decimal>()
            : (await _inventarioRepository.ObtenerConCorteAsync(sucursal.Id, cancellationToken))
                .ToDictionary(i => i.CorteId, i => i.StockActual);

        return cortes
            .Select(c => new CortePosDto(
                c.Id,
                c.Nombre,
                c.TipoAnimal.Nombre,
                c.UnidadVenta.ToString(),
                c.PrecioBase,
                stockPorCorte.GetValueOrDefault(c.Id, 0m)))
            .ToList();
    }

    public async Task<IReadOnlyList<MetodoPagoDto>> ListarMetodosPagoAsync(CancellationToken cancellationToken = default)
    {
        var metodos = await _metodoPagoRepository.ListarActivosAsync(cancellationToken);
        return metodos
            .Select(m => new MetodoPagoDto(m.Id, m.Nombre, !EsCredito(m)))
            .ToList();
    }

    public async Task<IReadOnlyList<VentaResumenDto>> ListarAsync(DateTime? desdeUtc, DateTime? hastaUtc, CancellationToken cancellationToken = default)
    {
        var hoy = _dateTimeProvider.UtcNow.Date;
        var desde = desdeUtc ?? hoy;
        var hasta = hastaUtc ?? desde.AddDays(1);

        var ventas = await _ventaRepository.ListarPorRangoAsync(desde, hasta, cancellationToken);
        return ventas
            .Select(v => new VentaResumenDto(v.Id, v.Folio, v.Fecha, v.Estado, v.Usuario?.NombreCompleto, v.Total))
            .ToList();
    }

    public async Task<VentaResultDto<VentaDto>> ObtenerAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var venta = await _ventaRepository.GetByIdCompletoAsync(id, cancellationToken);
        return venta is null
            ? VentaResultDto<VentaDto>.Fail(VentaResultCode.VentaNoEncontrada, "La venta no existe.")
            : VentaResultDto<VentaDto>.Ok(Mapear(venta));
    }

    public async Task<VentaResultDto<VentaDto>> CrearAsync(CrearVentaRequest request, Guid usuarioId, CancellationToken cancellationToken = default)
    {
        if (request.Lineas is null || request.Lineas.Count == 0)
        {
            return Fail(VentaResultCode.SinArticulos, "La venta no tiene articulos.");
        }

        var sucursal = await ObtenerSucursalPrincipalAsync(cancellationToken);
        if (sucursal is null)
        {
            return Fail(VentaResultCode.SinSucursalActiva, "No hay ninguna sucursal activa configurada.");
        }

        // 1) Lineas: corte vigente, cantidad valida, importe con el precio del servidor.
        var lineas = new List<(Corte Corte, decimal Cantidad, decimal Importe)>();
        foreach (var linea in request.Lineas)
        {
            var corte = await _corteRepository.GetByIdConTipoAnimalAsync(linea.CorteId, cancellationToken);
            if (corte is null || !corte.Activo)
            {
                return Fail(VentaResultCode.CorteNoEncontrado, "Uno de los cortes de la venta no existe o esta dado de baja.");
            }

            if (linea.Cantidad <= 0)
            {
                return Fail(VentaResultCode.CantidadInvalida, $"La cantidad de '{corte.Nombre}' debe ser mayor a cero.");
            }

            if (corte.UnidadVenta == UnidadVenta.Pieza && linea.Cantidad != decimal.Truncate(linea.Cantidad))
            {
                return Fail(VentaResultCode.CantidadInvalida, $"'{corte.Nombre}' se vende por pieza: la cantidad debe ser un numero entero.");
            }

            var importe = Math.Round(linea.Cantidad * corte.PrecioBase, 2, MidpointRounding.AwayFromZero);
            lineas.Add((corte, linea.Cantidad, importe));
        }

        var total = lineas.Sum(l => l.Importe);

        // 2) Stock: nunca se vende mas de lo que hay (RN-006). Se suma por corte
        //    por si el mismo corte viene en varias lineas.
        var inventarios = new Dictionary<Guid, Inventario>();
        foreach (var grupo in lineas.GroupBy(l => l.Corte.Id))
        {
            var pedido = grupo.Sum(l => l.Cantidad);
            var inventario = await _inventarioRepository.ObtenerOCrearAsync(grupo.Key, sucursal.Id, cancellationToken);

            if (inventario.StockActual < pedido)
            {
                return Fail(
                    VentaResultCode.StockInsuficiente,
                    $"Stock insuficiente de '{grupo.First().Corte.Nombre}': hay {inventario.StockActual:0.###} y se piden {pedido:0.###}.");
            }

            inventarios[grupo.Key] = inventario;
        }

        // 3) Pagos: deben cuadrar exactamente con el total.
        var pagosValidados = await ValidarPagosAsync(request.Pagos, total, cancellationToken);
        if (pagosValidados.Error is not null) return pagosValidados.Error;

        // 4) Todo valido: ahora si se modifica el estado.
        var ahora = _dateTimeProvider.UtcNow;
        var venta = new Venta
        {
            SucursalId = sucursal.Id,
            UsuarioId = usuarioId,
            Subtotal = total,
            Descuento = 0m,
            Total = total,
            Estado = EstadoVenta.Completada,
            Fecha = ahora
        };

        foreach (var (corte, cantidad, importe) in lineas)
        {
            venta.Detalles.Add(new VentaDetalle
            {
                CorteId = corte.Id,
                Cantidad = cantidad,
                PrecioUnitario = corte.PrecioBase,
                Importe = importe
            });
        }

        foreach (var (metodo, importe) in pagosValidados.Pagos)
        {
            venta.Pagos.Add(new VentaPago { MetodoPagoId = metodo.Id, Importe = importe });
        }

        await _ventaRepository.AddAsync(venta, cancellationToken);

        foreach (var (corteId, inventario) in inventarios)
        {
            inventario.StockActual -= lineas.Where(l => l.Corte.Id == corteId).Sum(l => l.Cantidad);
            inventario.FechaActualizacion = ahora;
        }

        foreach (var (corte, cantidad, _) in lineas)
        {
            await _movimientoRepository.AddAsync(new MovimientoInventario
            {
                CorteId = corte.Id,
                SucursalId = sucursal.Id,
                TipoMovimiento = TipoMovimientoInventario.Venta,
                Cantidad = -cantidad,
                Motivo = "Venta en punto de venta",
                UsuarioId = usuarioId,
                Fecha = ahora
            }, cancellationToken);
        }

        try
        {
            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }
        catch (ConcurrencyConflictException)
        {
            return Fail(VentaResultCode.Conflicto, "El inventario cambio mientras se registraba la venta. Revisa el stock e intenta de nuevo.");
        }

        var completa = await _ventaRepository.GetByIdCompletoAsync(venta.Id, cancellationToken);
        return VentaResultDto<VentaDto>.Ok(Mapear(completa!));
    }

    public async Task<VentaResultDto<VentaDto>> AnularAsync(Guid id, string motivo, Guid usuarioId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(motivo))
        {
            return Fail(VentaResultCode.MotivoRequerido, "El motivo de anulacion es obligatorio.");
        }

        var venta = await _ventaRepository.GetByIdCompletoAsync(id, cancellationToken);
        if (venta is null)
        {
            return Fail(VentaResultCode.VentaNoEncontrada, "La venta no existe.");
        }

        if (venta.Estado == EstadoVenta.Anulada)
        {
            return Fail(VentaResultCode.VentaYaAnulada, "Esta venta ya estaba anulada.");
        }

        var ahora = _dateTimeProvider.UtcNow;
        var motivoLimpio = motivo.Trim();

        foreach (var detalle in venta.Detalles)
        {
            var inventario = await _inventarioRepository.ObtenerOCrearAsync(detalle.CorteId, venta.SucursalId, cancellationToken);
            inventario.StockActual += detalle.Cantidad;
            inventario.FechaActualizacion = ahora;

            await _movimientoRepository.AddAsync(new MovimientoInventario
            {
                CorteId = detalle.CorteId,
                SucursalId = venta.SucursalId,
                TipoMovimiento = TipoMovimientoInventario.Venta,
                Cantidad = detalle.Cantidad,
                Motivo = $"Anulacion de venta #{venta.Folio}: {motivoLimpio}",
                UsuarioId = usuarioId,
                Fecha = ahora
            }, cancellationToken);
        }

        venta.Estado = EstadoVenta.Anulada;
        venta.MotivoAnulacion = motivoLimpio;

        try
        {
            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }
        catch (ConcurrencyConflictException)
        {
            return Fail(VentaResultCode.Conflicto, "El inventario cambio mientras se anulaba la venta. Intenta de nuevo.");
        }

        return VentaResultDto<VentaDto>.Ok(Mapear(venta));
    }

    private async Task<(VentaResultDto<VentaDto>? Error, List<(MetodoPago Metodo, decimal Importe)> Pagos)> ValidarPagosAsync(
        IReadOnlyList<VentaPagoRequest>? pagos,
        decimal total,
        CancellationToken cancellationToken)
    {
        var resultado = new List<(MetodoPago, decimal)>();

        if (pagos is null || pagos.Count == 0)
        {
            return (Fail(VentaResultCode.SinPagos, "Indica con que se paga la venta."), resultado);
        }

        foreach (var pago in pagos)
        {
            if (pago.Importe <= 0)
            {
                return (Fail(VentaResultCode.PagoInvalido, "Cada pago debe ser mayor a cero."), resultado);
            }

            var metodo = await _metodoPagoRepository.GetByIdAsync(pago.MetodoPagoId, cancellationToken);
            if (metodo is null || !metodo.Activo)
            {
                return (Fail(VentaResultCode.MetodoPagoNoEncontrado, "Uno de los metodos de pago no existe o esta desactivado."), resultado);
            }

            if (EsCredito(metodo))
            {
                return (Fail(VentaResultCode.MetodoPagoNoDisponible, "La venta a credito requiere un cliente y se habilita en un sprint posterior."), resultado);
            }

            resultado.Add((metodo, Math.Round(pago.Importe, 2, MidpointRounding.AwayFromZero)));
        }

        var suma = resultado.Sum(p => p.Item2);
        if (suma != total)
        {
            return (Fail(VentaResultCode.PagosNoCuadran, $"Los pagos suman {suma:0.00} y el total es {total:0.00}."), resultado);
        }

        return (null, resultado);
    }

    private static bool EsCredito(MetodoPago metodo) =>
        metodo.Nombre.Equals(MetodoPago.NombreCredito, StringComparison.OrdinalIgnoreCase);

    private static VentaResultDto<VentaDto> Fail(VentaResultCode code, string mensaje) =>
        VentaResultDto<VentaDto>.Fail(code, mensaje);

    private static VentaDto Mapear(Venta venta) => new(
        venta.Id,
        venta.Folio,
        venta.Fecha,
        venta.Estado,
        venta.MotivoAnulacion,
        venta.Usuario?.NombreCompleto,
        venta.Subtotal,
        venta.Descuento,
        venta.Total,
        venta.Detalles
            .Select(d => new VentaLineaDto(d.CorteId, d.Corte.Nombre, d.Corte.UnidadVenta.ToString(), d.Cantidad, d.PrecioUnitario, d.Importe))
            .ToList(),
        venta.Pagos
            .Select(p => new VentaPagoDto(p.MetodoPagoId, p.MetodoPago.Nombre, p.Importe))
            .ToList());

    private async Task<Sucursal?> ObtenerSucursalPrincipalAsync(CancellationToken cancellationToken)
    {
        var sucursales = await _sucursalRepository.GetAllAsync(cancellationToken);
        return sucursales.FirstOrDefault(s => s.EsPrincipal) ?? sucursales.FirstOrDefault();
    }
}
