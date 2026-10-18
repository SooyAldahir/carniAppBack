namespace CarniApp.Application.Features.Catalogos;

public record TipoAnimalDto(Guid Id, string Nombre, int CantidadCortes);

public record GuardarTipoAnimalRequest(string Nombre);

public record CorteDto(
    Guid Id,
    Guid TipoAnimalId,
    string TipoAnimalNombre,
    string Nombre,
    string UnidadVenta,
    decimal PrecioBase,
    decimal StockMinimo,
    string? CodigoBarras = null);

public record GuardarCorteRequest(
    Guid TipoAnimalId,
    string Nombre,
    string UnidadVenta,
    decimal PrecioBase,
    decimal StockMinimo,
    string? CodigoBarras = null);

public enum CatalogoResultCode
{
    Ok,
    NoEncontrado,
    TipoAnimalNoEncontrado,
    NombreDuplicado,
    CodigoBarrasDuplicado,
    UnidadVentaInvalida,
    PrecioInvalido,
    TipoAnimalConCortesActivos
}

public record CatalogoResultDto<T>(CatalogoResultCode Code, string? Mensaje, T? Datos = default)
{
    public bool Exitoso => Code == CatalogoResultCode.Ok;

    public static CatalogoResultDto<T> Ok(T datos) => new(CatalogoResultCode.Ok, null, datos);

    public static CatalogoResultDto<T> Fail(CatalogoResultCode code, string mensaje) => new(code, mensaje);
}
