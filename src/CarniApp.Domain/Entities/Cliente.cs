using CarniApp.Domain.Common;

namespace CarniApp.Domain.Entities;

/// <summary>
/// RF-CLI-001, RF-CRED-001: cliente de la carniceria, con limite y saldo de credito.
/// Latitud/Longitud/Foto/Puntos existen en la tabla para los modulos de mapa y
/// recompensas (fases posteriores); por ahora no se capturan.
/// </summary>
public class Cliente : BaseEntity
{
    public string Nombre { get; set; } = string.Empty;
    public string? Telefono { get; set; }
    public string? Direccion { get; set; }
    public decimal? Latitud { get; set; }
    public decimal? Longitud { get; set; }
    public string? FotoFachadaUrl { get; set; }
    public string? Referencias { get; set; }

    public decimal LimiteCredito { get; set; }

    /// <summary>Lo que el cliente debe. Sube con ventas a credito y baja con abonos.</summary>
    public decimal SaldoCredito { get; set; }

    public int PuntosAcumulados { get; set; }
    public bool ConsentimientoUbicacionFoto { get; set; }

    public decimal CreditoDisponible => Math.Max(0m, LimiteCredito - SaldoCredito);
}
