namespace CarniApp.Application.Features.Caja;

public record AbrirCajaRequest(decimal FondoInicial);

public record GastoCajaRequest(string Concepto, decimal Importe);

public record CerrarCajaRequest(decimal EfectivoContado, string? Notas = null);

public record MetodoTotalDto(string Metodo, decimal Importe);

public record GastoCajaDto(Guid Id, string Concepto, decimal Importe, string? UsuarioNombre, DateTime Fecha);

/// <summary>
/// Detalle de un turno. EfectivoEsperado = fondo + ventas en efectivo + abonos - gastos.
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
    string? Notas);

public record EstadoCajaDto(bool Abierta, CorteCajaDto? Corte);

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
