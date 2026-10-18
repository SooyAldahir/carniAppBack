namespace CarniApp.Domain.Entities;

/// <summary>
/// RF-VF: animal (o lote) comprado. En Venta Facil el reparto en cortes es
/// ESTIMADO (EsEstimado = true) con los porcentajes de rendimiento; cuando
/// exista Produccion, los pesos medidos reemplazaran la estimacion.
/// No hereda de BaseEntity: la tabla no tiene FechaActualizacion.
/// </summary>
public class LoteAnimal
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid TipoAnimalId { get; set; }
    public TipoAnimal TipoAnimal { get; set; } = null!;

    /// <summary>Opcional; el CRUD de proveedores llega en un sprint posterior.</summary>
    public Guid? ProveedorId { get; set; }

    public decimal PesoKg { get; set; }

    public decimal PrecioCompra { get; set; }

    public DateTime FechaCompra { get; set; } = DateTime.UtcNow;

    public bool EsEstimado { get; set; } = true;

    public DateTime FechaCreacion { get; set; } = DateTime.UtcNow;

    public bool Activo { get; set; } = true;
}
