namespace CarniApp.Domain.Entities;

/// <summary>Una venta puede tener varios pagos (pago mixto, RF-POS-008).</summary>
public class VentaPago
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid VentaId { get; set; }
    public Venta Venta { get; set; } = null!;

    public Guid MetodoPagoId { get; set; }
    public MetodoPago MetodoPago { get; set; } = null!;

    public decimal Importe { get; set; }
}
