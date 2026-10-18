namespace CarniApp.Application.Features.VentaFacil;

/// <summary>Un corte por peso del tipo de animal con su % de rendimiento (null = sin configurar).</summary>
public record RendimientoItemDto(Guid CorteId, string CorteNombre, decimal PrecioBase, decimal? Porcentaje);

public record RendimientosDto(Guid TipoAnimalId, IReadOnlyList<RendimientoItemDto> Cortes, decimal TotalPorcentaje);

public record RendimientoEntradaDto(Guid CorteId, decimal? Porcentaje);

public record GuardarRendimientosRequest(Guid TipoAnimalId, IReadOnlyList<RendimientoEntradaDto> Cortes);

public record LoteRequest(Guid TipoAnimalId, decimal PesoKg, decimal PrecioCompra, DateTime? FechaCompra = null);

/// <summary>Linea del reparto estimado: kg y valor de venta a precio base.</summary>
public record RepartoLineaDto(
    Guid CorteId,
    string CorteNombre,
    decimal Porcentaje,
    decimal KgEstimados,
    decimal PrecioBase,
    decimal VentaEstimada);

public record RepartoDto(
    Guid TipoAnimalId,
    string TipoAnimalNombre,
    decimal PesoKg,
    decimal PrecioCompra,
    IReadOnlyList<RepartoLineaDto> Lineas,
    decimal KgAsignados,
    decimal KgMerma,
    decimal CostoPorKg,
    decimal VentaEstimadaTotal,
    decimal UtilidadEstimada);

public record LoteDto(
    Guid Id,
    Guid TipoAnimalId,
    string TipoAnimalNombre,
    decimal PesoKg,
    decimal PrecioCompra,
    decimal CostoPorKg,
    DateTime FechaCompra,
    bool EsEstimado);

public enum VentaFacilResultCode
{
    Ok,
    TipoAnimalNoEncontrado,
    PesoInvalido,
    PrecioInvalido,
    PorcentajeInvalido,
    SumaExcede100,
    CorteInvalido,
    SinRendimientos,
    SinSucursalActiva,
    Conflicto
}

public record VentaFacilResultDto<T>(VentaFacilResultCode Code, string? Mensaje, T? Datos = default)
{
    public bool Exitoso => Code == VentaFacilResultCode.Ok;

    public static VentaFacilResultDto<T> Ok(T datos) => new(VentaFacilResultCode.Ok, null, datos);

    public static VentaFacilResultDto<T> Fail(VentaFacilResultCode code, string mensaje) => new(code, mensaje);
}
