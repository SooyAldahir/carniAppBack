using CarniApp.Domain.Common;

namespace CarniApp.Domain.Entities;

/// <summary>
/// Catalogo de permisos atomicos (SUP-03), p. ej. "pos.vender",
/// "config.critica.editar", "caja.reabrir". Se combinan con Rol
/// mediante RolPermiso para RBAC configurable (RF-AUTH-004).
/// </summary>
public class Permiso : BaseEntity
{
    public string Clave { get; set; } = string.Empty;

    public string? Descripcion { get; set; }

    public ICollection<RolPermiso> RolPermisos { get; set; } = new List<RolPermiso>();
}
