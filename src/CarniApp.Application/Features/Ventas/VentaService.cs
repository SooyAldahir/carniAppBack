using CarniApp.Application.Common.Exceptions;
using CarniApp.Application.Common.Interfaces;
using CarniApp.Domain.Entities;

// 'Inventario' tambien es un namespace hermano (Features.Inventario): alias para la entidad.
using InventarioEntidad = CarniApp.Domain.Entities.Inventario;
using NegocioEntidad = CarniApp.Domain.Entities.Negocio;

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
    private readonly IRepository<NegocioEntidad> _negocioRepository;
    private readonly IRepository<Cliente> _clienteRepository;
    private readonly IPosSettings _posSettings;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IDateTimeProvider _dateTimeProvider;

    public VentaService(
        IVentaRepository ventaRepository,
        IMetodoPagoRepository metodoPagoRepository,
        ICorteRepository corteRepository,
        IInventarioRepository inventarioRepository,
        IMovimientoInventarioRepository movimientoRepository,
        IRepository<Sucursal> sucursalRepository,
        IRepository<NegocioEntidad> negocioRepository,
        IRepository<Cliente> clienteRepository,
        IPosSettings posSettings,
        IUnitOfWork unitOfWork,
        IDateTimeProvider dateTimeProvider)
    {
        _ventaRepository = ventaRepository;
        _metodoPagoRepository = metodoPagoRepository;
        _corteRepository = corteRepository;
        _inventarioRepository = inventarioRepository;
        _movimientoRepository = movimientoRepository;
        _sucursalRepository = sucursalRepository;
        _negocioRepository = negocioRepository;
        _clienteRepository = clienteRepository;
        _posSettings = posSettings;
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
                stockPorCorte.GetValueOrDefault(c.Id, 0m),
                c.CodigoBarras))
            .ToList();
    }

    public async Task<IReadOnlyList<MetodoPagoDto>> ListarMetodosPagoAsync(CancellationToken cancellationToken = default)
    {
        var metodos = await _metodoPagoRepository.ListarActivosAsync(cancellationToken);
        return metodos
            .Select(m => new MetodoPagoDto(m.Id, m.Nombre, true))
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

    public async Task<VentaResultDto<VentaDto>> CrearAsync(CrearVentaRequest request, Guid usuarioId, IReadOnlyCollection<string>? permisos = null, CancellationToken cancellationToken = default)
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

        var nota = string.IsNullOrWhiteSpace(request.Nota) ? null : request.Nota.Trim();
        if (nota is { Length: > 200 })
        {
            return Fail(VentaResultCode.NotaInvalida, "La nota no puede pasar de 200 caracteres.");
        }

        // 1) Lineas: corte vigente, cantidad valida, importe con el precio del servidor
        //    y descuento de linea (nunca mayor al importe bruto de esa linea).
        var lineas = new List<(Corte Corte, decimal Cantidad, decimal Bruto, decimal Descuento)>();
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

            var bruto = Math.Round(linea.Cantidad * corte.PrecioBase, 2, MidpointRounding.AwayFromZero);
            var descuento = Math.Round(linea.Descuento, 2, MidpointRounding.AwayFromZero);
            if (descuento < 0 || descuento > bruto)
            {
                return Fail(VentaResultCode.DescuentoInvalido, $"El descuento de '{corte.Nombre}' debe estar entre 0 y {bruto:0.00}.");
            }

            lineas.Add((corte, linea.Cantidad, bruto, descuento));
        }

        var subtotal = lineas.Sum(l => l.Bruto);
        var descuentoLineas = lineas.Sum(l => l.Descuento);

        var descuentoGlobal = Math.Round(request.DescuentoTotal, 2, MidpointRounding.AwayFromZero);
        if (descuentoGlobal < 0 || descuentoGlobal > subtotal - descuentoLineas)
        {
            return Fail(VentaResultCode.DescuentoInvalido, "El descuento al total no puede ser negativo ni mayor al total de la venta.");
        }

        var descuentoTotal = descuentoLineas + descuentoGlobal;
        var total = subtotal - descuentoTotal;
        if (total <= 0)
        {
            return Fail(VentaResultCode.DescuentoInvalido, "El total de la venta no puede quedar en cero.");
        }

        var errorDescuento = ValidarAutorizacionDescuento(descuentoTotal, subtotal, permisos);
        if (errorDescuento is not null) return errorDescuento;

        // 2) Stock: nunca se vende mas de lo que hay (RN-006). Se suma por corte
        //    por si el mismo corte viene en varias lineas.
        var inventarios = new Dictionary<Guid, InventarioEntidad>();
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

        // 3) Cliente (opcional, obligatorio si hay pago a credito) y pagos: deben
        //    cuadrar exactamente con el total.
        Cliente? cliente = null;
        if (request.ClienteId is not null)
        {
            cliente = await _clienteRepository.GetByIdAsync(request.ClienteId.Value, cancellationToken);
            if (cliente is null || !cliente.Activo)
            {
                return Fail(VentaResultCode.ClienteNoEncontrado, "El cliente de la venta no existe o esta dado de baja.");
            }
        }

        var pagosValidados = await ValidarPagosAsync(request.Pagos, total, cliente, cancellationToken);
        if (pagosValidados.Error is not null) return pagosValidados.Error;

        // RF-CRED-001: lo que se carga a credito no puede pasar del credito disponible.
        // Lo fiado lleva un cargo (% configurable) que se suma a la deuda y al total, y
        // se imprime siempre en el ticket como renglon aparte.
        var creditoBase = pagosValidados.Pagos.Where(p => EsCredito(p.Metodo)).Sum(p => p.Importe);
        var porcentajeRecargo = _posSettings.RecargoCreditoPorcentaje;
        if (creditoBase > 0 && request.RecargoCreditoPorcentaje is { } pedido)
        {
            // Cambiar el % al cobrar: dentro del tope y solo con permiso. El ticket siempre
            // muestra el % aplicado, asi que el cliente lo ve.
            pedido = Math.Round(pedido, 2, MidpointRounding.AwayFromZero);
            if (pedido < 0 || pedido > _posSettings.RecargoCreditoMaximoPorcentaje)
            {
                return Fail(VentaResultCode.RecargoInvalido, $"El cargo por credito debe estar entre 0% y {_posSettings.RecargoCreditoMaximoPorcentaje:0.##}%.");
            }

            if (pedido != porcentajeRecargo)
            {
                // Subir requiere 'ajustar'; bajar basta con 'reducir' (o 'ajustar').
                var puedeAjustar = permisos?.Contains(PermisosPos.AjustarRecargo) == true;
                var puedeReducir = puedeAjustar || permisos?.Contains(PermisosPos.ReducirRecargo) == true;
                if (pedido > porcentajeRecargo ? !puedeAjustar : !puedeReducir)
                {
                    return Fail(
                        VentaResultCode.RecargoNoAutorizado,
                        pedido > porcentajeRecargo
                            ? $"No tienes permiso para subir el cargo por credito por encima del {porcentajeRecargo:0.##}%."
                            : "No tienes permiso para cambiar el porcentaje de cargo por credito.");
                }

                porcentajeRecargo = pedido;
            }
        }

        var recargo = creditoBase > 0
            ? Math.Round(creditoBase * porcentajeRecargo / 100m, 2, MidpointRounding.AwayFromZero)
            : 0m;
        var credito = creditoBase + recargo;
        if (credito > 0 && credito > cliente!.CreditoDisponible)
        {
            return Fail(
                VentaResultCode.CreditoExcedido,
                $"El credito solicitado ({credito:0.00}, incluye cargo de {recargo:0.00}) excede el disponible de {cliente.Nombre}: {cliente.CreditoDisponible:0.00} (limite {cliente.LimiteCredito:0.00}, debe {cliente.SaldoCredito:0.00}).");
        }

        // 4) Todo valido: ahora si se modifica el estado.
        var ahora = _dateTimeProvider.UtcNow;
        var venta = new Venta
        {
            SucursalId = sucursal.Id,
            UsuarioId = usuarioId,
            ClienteId = cliente?.Id,
            Nota = nota,
            Subtotal = subtotal,
            Descuento = descuentoTotal,
            RecargoCredito = recargo,
            Total = total + recargo,
            Estado = EstadoVenta.Completada,
            Fecha = ahora
        };

        foreach (var (corte, cantidad, bruto, descuento) in lineas)
        {
            venta.Detalles.Add(new VentaDetalle
            {
                CorteId = corte.Id,
                Cantidad = cantidad,
                PrecioUnitario = corte.PrecioBase,
                // Importe de la linea ya con su descuento; el de linea se deduce como
                // (cantidad x precio) - importe, sin columna extra en el esquema.
                Importe = bruto - descuento
            });
        }

        var recargoPendiente = recargo;   // el cargo se registra dentro del pago a credito
        foreach (var (metodo, importe) in pagosValidados.Pagos)
        {
            var importePago = importe;
            if (EsCredito(metodo) && recargoPendiente > 0)
            {
                importePago += recargoPendiente;
                recargoPendiente = 0m;
            }

            venta.Pagos.Add(new VentaPago { MetodoPagoId = metodo.Id, Importe = importePago });
        }

        await _ventaRepository.AddAsync(venta, cancellationToken);

        if (credito > 0)
        {
            cliente!.SaldoCredito += credito;
            cliente.FechaActualizacion = ahora;
        }

        foreach (var (corteId, inventario) in inventarios)
        {
            inventario.StockActual -= lineas.Where(l => l.Corte.Id == corteId).Sum(l => l.Cantidad);
            inventario.FechaActualizacion = ahora;
        }

        foreach (var (corte, cantidad, _, _) in lineas)
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
            return Fail(VentaResultCode.Conflicto, "El inventario o el saldo del cliente cambio mientras se registraba la venta. Revisa e intenta de nuevo.");
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

        // RF-CRED: si parte de la venta fue a credito, esa deuda deja de existir. Si el
        // cliente ya la habia abonado, el saldo queda en cero (no se maneja saldo a favor).
        var creditoVenta = venta.Pagos.Where(p => EsCredito(p.MetodoPago)).Sum(p => p.Importe);
        if (creditoVenta > 0 && venta.Cliente is not null)
        {
            venta.Cliente.SaldoCredito = Math.Max(0m, venta.Cliente.SaldoCredito - creditoVenta);
            venta.Cliente.FechaActualizacion = ahora;
        }

        venta.Estado = EstadoVenta.Anulada;
        venta.MotivoAnulacion = motivoLimpio;

        try
        {
            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }
        catch (ConcurrencyConflictException)
        {
            return Fail(VentaResultCode.Conflicto, "El inventario o el saldo del cliente cambio mientras se anulaba la venta. Intenta de nuevo.");
        }

        return VentaResultDto<VentaDto>.Ok(Mapear(venta));
    }

    private async Task<(VentaResultDto<VentaDto>? Error, List<(MetodoPago Metodo, decimal Importe)> Pagos)> ValidarPagosAsync(
        IReadOnlyList<VentaPagoRequest>? pagos,
        decimal total,
        Cliente? cliente,
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

            if (EsCredito(metodo) && cliente is null)
            {
                return (Fail(VentaResultCode.MetodoPagoNoDisponible, "Para vender a credito primero elige un cliente."), resultado);
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

    /// <summary>
    /// RF-POS-004: permisos diferenciados segun la magnitud del descuento. Un descuento
    /// requiere 'pos.descuento'; si pasa del porcentaje configurado requiere
    /// 'pos.descuento.alto'. Devuelve null si esta autorizado.
    /// </summary>
    private VentaResultDto<VentaDto>? ValidarAutorizacionDescuento(decimal descuento, decimal subtotal, IReadOnlyCollection<string>? permisos)
    {
        if (descuento <= 0) return null;

        var tieneBasico = permisos?.Contains(PermisosPos.Descuento) == true;
        var tieneAlto = permisos?.Contains(PermisosPos.DescuentoAlto) == true;

        if (!tieneBasico && !tieneAlto)
        {
            return Fail(VentaResultCode.DescuentoNoAutorizado, "No tienes permiso para aplicar descuentos.");
        }

        var porcentaje = descuento / subtotal * 100m;
        var maximo = _posSettings.DescuentoMaximoSinAutorizacionPorcentaje;
        if (porcentaje > maximo && !tieneAlto)
        {
            return Fail(
                VentaResultCode.DescuentoNoAutorizado,
                $"Un descuento de {porcentaje:0.##}% excede el {maximo:0.##}% permitido. Requiere autorizacion de un Super Administrador.");
        }

        return null;
    }

    public async Task<VentaResultDto<TicketDto>> ObtenerTicketAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var venta = await _ventaRepository.GetByIdCompletoAsync(id, cancellationToken);
        if (venta is null)
        {
            return VentaResultDto<TicketDto>.Fail(VentaResultCode.VentaNoEncontrada, "La venta no existe.");
        }

        var negocio = (await _negocioRepository.GetAllAsync(cancellationToken)).FirstOrDefault();
        var zona = ResolverZonaHoraria(negocio?.ZonaHoraria);

        return VentaResultDto<TicketDto>.Ok(new TicketDto(TicketBuilder.Construir(Mapear(venta), negocio, zona)));
    }

    private static TimeZoneInfo ResolverZonaHoraria(string? id)
    {
        if (string.IsNullOrWhiteSpace(id)) return TimeZoneInfo.Utc;
        try
        {
            return TimeZoneInfo.FindSystemTimeZoneById(id);
        }
        catch (Exception ex) when (ex is TimeZoneNotFoundException or InvalidTimeZoneException)
        {
            return TimeZoneInfo.Utc;
        }
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
            .Select(d => new VentaLineaDto(
                d.CorteId,
                d.Corte.Nombre,
                d.Corte.UnidadVenta.ToString(),
                d.Cantidad,
                d.PrecioUnitario,
                d.Importe,
                Math.Max(0m, Math.Round(d.Cantidad * d.PrecioUnitario, 2, MidpointRounding.AwayFromZero) - d.Importe)))
            .ToList(),
        venta.Pagos
            .Select(p => new VentaPagoDto(p.MetodoPagoId, p.MetodoPago.Nombre, p.Importe))
            .ToList(),
        venta.ClienteId,
        venta.Cliente?.Nombre,
        venta.Cliente?.Direccion,
        venta.Nota,
        venta.RecargoCredito);

    private async Task<Sucursal?> ObtenerSucursalPrincipalAsync(CancellationToken cancellationToken)
    {
        var sucursales = await _sucursalRepository.GetAllAsync(cancellationToken);
        return sucursales.FirstOrDefault(s => s.EsPrincipal) ?? sucursales.FirstOrDefault();
    }
}
