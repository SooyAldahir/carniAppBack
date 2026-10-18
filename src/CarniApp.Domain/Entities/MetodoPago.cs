namespace CarniApp.Domain.Entities;

/// <summary>
/// RF-POS-008: catalogo de metodos de pago. La tabla solo tiene Id, Nombre y
/// Activo, por eso no hereda de BaseEntity.
/// </summary>
public class MetodoPago
{
    public const string NombreCredito = "Credito";

    public Guid Id { get; set; } = Guid.NewGuid();
    public string Nombre { get; set; } = string.Empty;
    public bool Activo { get; set; } = true;
}
