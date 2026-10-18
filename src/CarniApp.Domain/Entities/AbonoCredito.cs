namespace CarniApp.Domain.Entities;

/// <summary>RF-CRED-002/003: pago que el cliente hace para reducir su saldo. Sin BaseEntity (la tabla solo tiene Fecha).</summary>
public class AbonoCredito
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ClienteId { get; set; }
    public Cliente Cliente { get; set; } = null!;
    public decimal Importe { get; set; }
    public Guid UsuarioId { get; set; }
    public Usuario Usuario { get; set; } = null!;
    public DateTime Fecha { get; set; } = DateTime.UtcNow;
}
