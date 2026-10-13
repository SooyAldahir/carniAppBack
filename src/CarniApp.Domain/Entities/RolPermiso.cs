namespace CarniApp.Domain.Entities;

/// <summary>
/// Tabla de union Rol-Permiso. No hereda de BaseEntity porque es una
/// relacion pura N:N sin necesidad de auditoria propia.
/// </summary>
public class RolPermiso
{
    public Guid RolId { get; set; }
    public Rol Rol { get; set; } = null!;

    public Guid PermisoId { get; set; }
    public Permiso Permiso { get; set; } = null!;
}
