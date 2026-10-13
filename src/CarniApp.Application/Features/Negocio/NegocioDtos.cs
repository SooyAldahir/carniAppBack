namespace CarniApp.Application.Features.Negocio;

public record NegocioDto(
    Guid Id,
    string NombreComercial,
    string? LogoUrl,
    string? Telefono,
    string? RedSocialPrincipal,
    string? Direccion,
    string Moneda,
    string ZonaHoraria);

public record ActualizarNegocioRequest(
    string NombreComercial,
    string? LogoUrl,
    string? Telefono,
    string? RedSocialPrincipal,
    string? Direccion,
    string Moneda,
    string ZonaHoraria);
