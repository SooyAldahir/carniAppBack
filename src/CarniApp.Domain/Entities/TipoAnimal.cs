using CarniApp.Domain.Common;

namespace CarniApp.Domain.Entities;

/// <summary>
/// Catalogo configurable de tipos de animal (RF-INV-001): Res, Cerdo,
/// Pollo... No esta fijo en codigo, el Super Administrador lo administra.
/// </summary>
public class TipoAnimal : BaseEntity
{
    public string Nombre { get; set; } = string.Empty;

    public ICollection<Corte> Cortes { get; set; } = new List<Corte>();
}
