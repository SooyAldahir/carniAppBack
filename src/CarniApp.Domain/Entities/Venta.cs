namespace CarniApp.Domain.Entities;

public static class EstadoVenta
{
    public const string Completada = "Completada";
    public const string Anulada = "Anulada";
}

/// <summary>
/// RF-POS-001..011: encabezado del ticket. No hereda de BaseEntity: la tabla
/// usa 'Fecha' y no tiene FechaCreacion/FechaActualizacion/Activo.
/// </summary>
public class Venta
{
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>Consecutivo legible para el ticket; lo genera SQL Server (IDENTITY).</summary>
    public int Folio { get; set; }

    public Guid SucursalId { get; set; }
    public Sucursal Sucursal { get; set; } = null!;

    /// <summary>Cliente asociado (Sprint 7). Por ahora siempre null.</summary>
    public Guid? ClienteId { get; set; }

    public Guid UsuarioId { get; set; }
    public Usuario Usuario { get; set; } = null!;

    public decimal Subtotal { get; set; }
    public decimal Descuento { get; set; }
    public decimal Total { get; set; }

    public string Estado { get; set; } = EstadoVenta.Completada;
    public string? MotivoAnulacion { get; set; }
    public bool PendienteSincronizar { get; set; }
    public DateTime Fecha { get; set; } = DateTime.UtcNow;

    public ICollection<VentaDetalle> Detalles { get; set; } = new List<VentaDetalle>();
    public ICollection<VentaPago> Pagos { get; set; } = new List<VentaPago>();
}
