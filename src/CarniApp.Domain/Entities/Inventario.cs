namespace CarniApp.Domain.Entities;

/// <summary>
/// RF-INV-004: saldo vivo de stock por corte y sucursal (separado del
/// kardex de movimientos, para consultar el stock actual sin tener que
/// sumar todo el historial cada vez). No hereda de BaseEntity: la
/// tabla Inventario no tiene FechaCreacion ni Activo -- es un saldo
/// que siempre existe mientras el corte este activo, no algo que se
/// "desactive" por separado.
/// </summary>
public class Inventario
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid CorteId { get; set; }
    public Corte Corte { get; set; } = null!;

    public Guid SucursalId { get; set; }
    public Sucursal Sucursal { get; set; } = null!;

    public decimal StockActual { get; set; }

    public DateTime FechaActualizacion { get; set; } = DateTime.UtcNow;
}
