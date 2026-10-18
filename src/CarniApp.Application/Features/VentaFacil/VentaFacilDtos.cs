namespace CarniApp.Application.Features.VentaFacil;

/// <summary>
/// Un corte por peso del tipo de animal. Porcentaje = configurado a mano (respaldo,
/// null = sin configurar); PorcentajeAprendido = promedio de los lotes pesados (null = sin datos).
/// </summary>
public record RendimientoItemDto(Guid CorteId, string CorteNombre, decimal PrecioBase, decimal? Porcentaje, decimal? PorcentajeAprendido);

/// <summary>UsaAprendido = ya hay suficientes lotes pesados (LotesParaAprender) para estimar sin pesar.</summary>
public record RendimientosDto(
    Guid TipoAnimalId,
    IReadOnlyList<RendimientoItemDto> Cortes,
    decimal TotalPorcentaje,
    int LotesMedidos,
    int LotesParaAprender,
    bool UsaAprendido);

public record RendimientoEntradaDto(Guid CorteId, decimal? Porcentaje);

public record GuardarRendimientosRequest(Guid TipoAnimalId, IReadOnlyList<RendimientoEntradaDto> Cortes);

/// <summary>Kilos reales (pesados) de un corte del animal.</summary>
public record KgCorteDto(Guid CorteId, decimal Kg);

/// <summary>
/// Cortes = null o vacio: el reparto se ESTIMA con los porcentajes (aprendidos o configurados).
/// Cortes con kilos: el lote queda como MEDIDO y alimenta el aprendizaje.
/// </summary>
public record LoteRequest(
    Guid TipoAnimalId,
    decimal PesoKg,
    decimal PrecioCompra,
    DateTime? FechaCompra = null,
    IReadOnlyList<KgCorteDto>? Cortes = null);

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
    decimal UtilidadEstimada,
    bool EsEstimado,
    string Origen,
    int LotesMedidos,
    int LotesParaAprender);

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
    KgInvalido,
    MedidoExcedePeso,
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
