namespace CarniApp.Domain.Common;

/// <summary>
/// Clase base para todas las entidades del dominio.
/// Provee identidad, auditoria basica y soporte de borrado logico (soft delete),
/// consistente con RNF-OBS-001 (logs/auditoria de operaciones criticas).
/// </summary>
public abstract class BaseEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public DateTime FechaCreacion { get; set; } = DateTime.UtcNow;

    public DateTime? FechaActualizacion { get; set; }

    public bool Activo { get; set; } = true;
}
