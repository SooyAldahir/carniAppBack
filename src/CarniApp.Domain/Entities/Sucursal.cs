using CarniApp.Domain.Common;

namespace CarniApp.Domain.Entities;

/// <summary>
/// Entidad Sucursal (SUP-01): se modela desde el dia uno aunque el negocio
/// opere hoy en una sola ubicacion, para no reestructurar Inventario/Caja/POS
/// si se abre una segunda ubicacion en el futuro.
/// </summary>
public class Sucursal : BaseEntity
{
    public string Nombre { get; set; } = string.Empty;

    public string? Direccion { get; set; }

    public bool EsPrincipal { get; set; } = true;
}
