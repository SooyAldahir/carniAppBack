namespace CarniApp.Application.Features.Health;

/// <summary>
/// Respuesta del endpoint de salud (Sprint 0): confirma que el API
/// esta vivo y, opcionalmente, que la base de datos responde.
/// </summary>
public record HealthStatusDto(
    string Estado,
    bool BaseDeDatosConectada,
    string? NombreNegocio,
    DateTime HoraServidorUtc
);
