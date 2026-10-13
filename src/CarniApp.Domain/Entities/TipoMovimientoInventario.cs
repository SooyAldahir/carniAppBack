namespace CarniApp.Domain.Entities;

/// <summary>
/// RF-INV-008: tipos de movimiento del kardex. Entrada/Merma/Ajuste se
/// registran manualmente (RF-INV-005, Sprint 3); Venta lo inserta el
/// POS automaticamente en un sprint posterior.
/// </summary>
public enum TipoMovimientoInventario
{
    Entrada,
    Merma,
    Ajuste,
    Venta
}
