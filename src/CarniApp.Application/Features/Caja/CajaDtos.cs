namespace CarniApp.Application.Features.Caja;

public record AbrirCajaRequest(decimal FondoInicial, decimal? MetaEfectivo = null);

/// <summary>Retiro de efectivo de la caja (p. ej. al alcanzar la meta). Concepto opcional.</summary>
public record RetiroCajaRequest(decimal Importe, string? Concepto = null);

/// <summary>Meta de efectivo en caja. Null o 0 = sin meta.</summary>
public record ActualizarMetaRequest(decimal? Meta);

public record GastoCajaRequest(string Concepto, decimal Importe);

public record CerrarCajaRequest(decimal EfectivoContado, string? Notas = null);

public record MetodoTotalDto(string Metodo, decimal Importe);

public record GastoCajaDto(Guid Id, string Concepto, decimal Importe, string? UsuarioNombre, DateTime Fecha, string Tipo = "Gasto");

/// <summary>
/// Detalle de un turno. EfectivoEsperado = fondo + ventas en efectivo + abonos - gastos - retiros.
/// Gastos contiene gastos y retiros (campo Tipo); TotalGastos y TotalRetiros los separan.
/// EfectivoContado y Diferencia solo existen cuando el corte esta cerrado.
/// </summary>
public record CorteCajaDto(
    Guid Id,
    string Estado,
    string? UsuarioNombre,
    DateTime FechaInicio,
    DateTime? FechaCierre,
    decimal FondoInicial,
    int NumeroVentas,
    int VentasAnuladas,
    decimal TotalVentas,
    IReadOnlyList<MetodoTotalDto> PorMetodo,
    decimal VentasEfectivo,
    decimal AbonosEfectivo,
    decimal TotalGastos,
    IReadOnlyList<GastoCajaDto> Gastos,
    decimal EfectivoEsperado,
    decimal? EfectivoContado,
    decimal? Diferencia,
    string? Notas,
    decimal TotalRetiros = 0m,
    decimal? MetaEfectivo = null);

/// <summary>MetaSugerida: la meta del ultimo turno, para precargarla al abrir uno nuevo.</summary>
public record EstadoCajaDto(bool Abierta, CorteCajaDto? Corte, decimal? MetaSugerida = null);

public record CorteCajaResumenDto(
    Guid Id,
    string Estado,
    string? UsuarioNombre,
    DateTime FechaInicio,
    DateTime? FechaCierre,
    decimal? TotalVentas,
    decimal? EfectivoEsperado,
    decimal? EfectivoContado,
    decimal? Diferencia);

public enum CajaResultCode
{
    Ok,
    YaHayCajaAbierta,
    SinCajaAbierta,
    ImporteInvalido,
    EfectivoInsuficiente,
    ConceptoInvalido,
    NotasInvalidas,
    CorteNoEncontrado,
    CorteNoCerrado,
    NoEsElUltimoCorte,
    SinSucursalActiva
}

public record CajaResultDto<T>(CajaResultCode Code, string? Mensaje, T? Datos = default)
{
    public bool Exitoso => Code == CajaResultCode.Ok;

    public static CajaResultDto<T> Ok(T datos) => new(CajaResultCode.Ok, null, datos);

    public static CajaResultDto<T> Fail(CajaResultCode code, string mensaje) => new(code, mensaje);
}
