namespace CarniApp.Application.Features.Clientes;

public record ClienteDto(
    Guid Id,
    string Nombre,
    string? Telefono,
    string? Direccion,
    string? Referencias,
    decimal LimiteCredito,
    decimal SaldoCredito,
    decimal CreditoDisponible);

public record GuardarClienteRequest(
    string Nombre,
    string? Telefono,
    string? Direccion,
    string? Referencias,
    decimal LimiteCredito = 0m);

public record RegistrarAbonoRequest(decimal Importe);

public record AbonoDto(Guid Id, decimal Importe, DateTime Fecha, string? UsuarioNombre);

public enum ClienteResultCode
{
    Ok,
    NoEncontrado,
    NombreRequerido,
    TelefonoDuplicado,
    LimiteInvalido,
    ImporteInvalido,
    AbonoExcedeSaldo,
    TieneSaldoPendiente,
    Conflicto
}

public record ClienteResultDto<T>(ClienteResultCode Code, string? Mensaje, T? Datos = default)
{
    public bool Exitoso => Code == ClienteResultCode.Ok;

    public static ClienteResultDto<T> Ok(T datos) => new(ClienteResultCode.Ok, null, datos);

    public static ClienteResultDto<T> Fail(ClienteResultCode code, string mensaje) => new(code, mensaje);
}
