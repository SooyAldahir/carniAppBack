namespace CarniApp.Domain.Entities;

/// <summary>
/// RF-INV-008: kardex -- historial inmutable de movimientos de
/// inventario. No hereda de BaseEntity: es un registro de auditoria
/// que nunca se edita ni se desactiva, usa "Fecha" (no FechaCreacion)
/// y no tiene FechaActualizacion ni Activo en la tabla SQL.
/// </summary>
public class MovimientoInventario
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid CorteId { get; set; }
    public Corte Corte { get; set; } = null!;

    public Guid SucursalId { get; set; }
    public Sucursal Sucursal { get; set; } = null!;

    public Guid? LoteAnimalId { get; set; }

    public TipoMovimientoInventario TipoMovimiento { get; set; }

    /// <summary>Positivo = entrada, negativo = salida.</summary>
    public decimal Cantidad { get; set; }

    public string? Motivo { get; set; }

    public Guid? UsuarioId { get; set; }
    public Usuario? Usuario { get; set; }

    public DateTime Fecha { get; set; } = DateTime.UtcNow;
}
