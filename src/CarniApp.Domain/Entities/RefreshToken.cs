using CarniApp.Domain.Common;

namespace CarniApp.Domain.Entities;

/// <summary>
/// Refresh token de larga duracion (RF-AUTH-002). Se guarda el HASH
/// (SHA-256), nunca el token en texto plano -- igual que la contrasena
/// (RNF-SEC-002). Permite revocacion remota (RF-AUTH-007) y rotacion:
/// cada vez que se usa, se revoca y se emite uno nuevo.
/// </summary>
public class RefreshToken : BaseEntity
{
    public Guid UsuarioId { get; set; }
    public Usuario Usuario { get; set; } = null!;

    public string TokenHash { get; set; } = string.Empty;

    public DateTime ExpiraUtc { get; set; }

    public DateTime? RevocadoUtc { get; set; }

    public bool EstaVigente(DateTime ahoraUtc) => RevocadoUtc is null && ExpiraUtc > ahoraUtc;
}
