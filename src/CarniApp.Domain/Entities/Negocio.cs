using CarniApp.Domain.Common;

namespace CarniApp.Domain.Entities;

/// <summary>
/// Configuracion general de la instalacion (RF-CONFIG-003).
/// Cada instalacion independiente (D-01) tiene exactamente un registro de Negocio,
/// con su marca (nombre, logo, telefono, redes sociales) para personalizar
/// tickets, reportes y pantallas de cliente/repartidor.
/// </summary>
public class Negocio : BaseEntity
{
    public string NombreComercial { get; set; } = string.Empty;

    public string? LogoUrl { get; set; }

    public string? Telefono { get; set; }

    public string? RedSocialPrincipal { get; set; }

    public string? Direccion { get; set; }

    public string Moneda { get; set; } = "MXN";

    public string ZonaHoraria { get; set; } = "America/Mexico_City";

    /// <summary>Color de acento de la app en hex (#RRGGBB). Null = el de la marca por defecto.</summary>
    public string? ColorPrimario { get; set; }

    /// <summary>Mensaje al pie del ticket. Null = "Gracias por su compra".</summary>
    public string? PieTicket { get; set; }
}
