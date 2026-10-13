using CarniApp.Domain.Common;

namespace CarniApp.Domain.Entities;

/// <summary>
/// Usuario del sistema (RF-AUTH-001, RF-AUTH-005). La contrasena se
/// almacena como hash seguro (RNF-SEC-002), nunca en texto plano.
/// </summary>
public class Usuario : BaseEntity
{
    public string NombreCompleto { get; set; } = string.Empty;

    public string NombreUsuario { get; set; } = string.Empty;

    public string? Correo { get; set; }

    public string PasswordHash { get; set; } = string.Empty;

    public Guid RolId { get; set; }
    public Rol Rol { get; set; } = null!;

    public int IntentosFallidos { get; set; } = 0;

    public DateTime? BloqueadoHasta { get; set; }
}
