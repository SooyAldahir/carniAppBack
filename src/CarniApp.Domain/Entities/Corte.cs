using CarniApp.Domain.Common;

namespace CarniApp.Domain.Entities;

/// <summary>
/// Catalogo de cortes vendibles (RF-INV-002, RF-CONFIG-002): el
/// "producto" de la carniceria. Cada corte pertenece a un TipoAnimal
/// y define su unidad de venta, precio base y stock minimo de alerta
/// (RF-INV-003).
/// </summary>
public class Corte : BaseEntity
{
    public Guid TipoAnimalId { get; set; }
    public TipoAnimal TipoAnimal { get; set; } = null!;

    public string Nombre { get; set; } = string.Empty;

    public UnidadVenta UnidadVenta { get; set; } = UnidadVenta.Peso;

    public decimal PrecioBase { get; set; }

    public decimal StockMinimo { get; set; }

    /// <summary>
    /// Codigo de barras / SKU que lee el escaner en el POS (RF-POS-006). Opcional;
    /// si existe, es unico entre los cortes activos.
    /// </summary>
    public string? CodigoBarras { get; set; }
}
