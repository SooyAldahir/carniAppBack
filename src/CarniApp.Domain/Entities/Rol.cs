using CarniApp.Domain.Common;

namespace CarniApp.Domain.Entities;

/// <summary>
/// Catalogo de roles configurable (SUP-03). No se limita a los 3 roles
/// iniciales (Super Administrador, Cajera, Repartidor): cada instalacion
/// puede ajustar sus roles y permisos sin requerir un cambio de codigo.
/// </summary>
public class Rol : BaseEntity
{
    public string Nombre { get; set; } = string.Empty;

    public string? Descripcion { get; set; }

    public ICollection<RolPermiso> RolPermisos { get; set; } = new List<RolPermiso>();

    public ICollection<Usuario> Usuarios { get; set; } = new List<Usuario>();
}
