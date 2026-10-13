namespace CarniApp.Application.Features.Inventario;

public record InventarioItemDto(
    Guid CorteId,
    string CorteNombre,
    string UnidadVenta,
    decimal StockActual,
    decimal StockMinimo,
    bool EnAlerta);

public record RegistrarAjusteRequest(
    Guid CorteId,
    string TipoMovimiento,
    decimal Cantidad,
    string Motivo);

public record MovimientoDto(
    Guid Id,
    string TipoMovimiento,
    decimal Cantidad,
    string? Motivo,
    string? UsuarioNombre,
    DateTime Fecha);

public enum InventarioResultCode
{
    Ok,
    CorteNoEncontrado,
    TipoMovimientoInvalido,
    MotivoRequerido,
    CantidadInvalida,
    SinSucursalActiva
}

public record InventarioResultDto<T>(InventarioResultCode Code, string? Mensaje, T? Datos = default)
{
    public bool Exitoso => Code == InventarioResultCode.Ok;

    public static InventarioResultDto<T> Ok(T datos) => new(InventarioResultCode.Ok, null, datos);

    public static InventarioResultDto<T> Fail(InventarioResultCode code, string mensaje) => new(code, mensaje);
}
