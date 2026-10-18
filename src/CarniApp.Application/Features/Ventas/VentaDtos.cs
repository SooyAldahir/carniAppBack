namespace CarniApp.Application.Features.Ventas;

public record CortePosDto(
    Guid CorteId,
    string Nombre,
    string TipoAnimal,
    string UnidadVenta,
    decimal PrecioBase,
    decimal StockActual,
    string? CodigoBarras = null);

public record MetodoPagoDto(Guid Id, string Nombre, bool Disponible);

/// <summary>Descuento de la linea en pesos (RF-POS-004). 0 = sin descuento.</summary>
public record VentaLineaRequest(Guid CorteId, decimal Cantidad, decimal Descuento = 0m);

public record VentaPagoRequest(Guid MetodoPagoId, decimal Importe);

/// <summary>
/// El precio NO viaja en la peticion: el servidor usa siempre el precio vigente
/// del catalogo, para que un cliente alterado no pueda vender mas barato.
/// </summary>
/// <param name="DescuentoTotal">Descuento global en pesos, aplicado despues de los de linea (RF-POS-004).</param>
public record CrearVentaRequest(
    IReadOnlyList<VentaLineaRequest> Lineas,
    IReadOnlyList<VentaPagoRequest> Pagos,
    decimal DescuentoTotal = 0m,
    Guid? ClienteId = null,
    string? Nota = null);

public record TicketDto(string Texto);

public record AnularVentaRequest(string Motivo);

public record VentaLineaDto(
    Guid CorteId,
    string CorteNombre,
    string UnidadVenta,
    decimal Cantidad,
    decimal PrecioUnitario,
    decimal Importe,
    decimal Descuento);

public record VentaPagoDto(Guid MetodoPagoId, string MetodoPago, decimal Importe);

public record VentaDto(
    Guid Id,
    int Folio,
    DateTime Fecha,
    string Estado,
    string? MotivoAnulacion,
    string? UsuarioNombre,
    decimal Subtotal,
    decimal Descuento,
    decimal Total,
    IReadOnlyList<VentaLineaDto> Lineas,
    IReadOnlyList<VentaPagoDto> Pagos,
    Guid? ClienteId = null,
    string? ClienteNombre = null,
    string? ClienteDireccion = null,
    string? Nota = null);

public record VentaResumenDto(
    Guid Id,
    int Folio,
    DateTime Fecha,
    string Estado,
    string? UsuarioNombre,
    decimal Total);

public enum VentaResultCode
{
    Ok,
    SinArticulos,
    CantidadInvalida,
    DescuentoInvalido,
    DescuentoNoAutorizado,
    CorteNoEncontrado,
    StockInsuficiente,
    SinSucursalActiva,
    SinPagos,
    PagoInvalido,
    MetodoPagoNoEncontrado,
    MetodoPagoNoDisponible,
    ClienteNoEncontrado,
    CreditoExcedido,
    NotaInvalida,
    PagosNoCuadran,
    VentaNoEncontrada,
    VentaYaAnulada,
    MotivoRequerido,
    Conflicto
}

public record VentaResultDto<T>(VentaResultCode Code, string? Mensaje, T? Datos = default)
{
    public bool Exitoso => Code == VentaResultCode.Ok;

    public static VentaResultDto<T> Ok(T datos) => new(VentaResultCode.Ok, null, datos);

    public static VentaResultDto<T> Fail(VentaResultCode code, string mensaje) => new(code, mensaje);
}
