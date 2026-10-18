namespace CarniApp.Domain.Entities;

/// <summary>Linea del ticket: cantidad en kg o piezas segun la UnidadVenta del corte.</summary>
public class VentaDetalle
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid VentaId { get; set; }
    public Venta Venta { get; set; } = null!;

    public Guid CorteId { get; set; }
    public Corte Corte { get; set; } = null!;

    public decimal Cantidad { get; set; }
    public decimal PrecioUnitario { get; set; }
    public decimal Importe { get; set; }
}
