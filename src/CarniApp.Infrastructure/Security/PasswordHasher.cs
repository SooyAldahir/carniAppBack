using CarniApp.Application.Common.Interfaces;

namespace CarniApp.Infrastructure.Security;

/// <summary>
/// Implementacion con BCrypt (RNF-SEC-002): salt automatico por
/// contrasena, factor de trabajo 12 (balance seguridad/latencia para
/// un login interactivo).
/// </summary>
public class PasswordHasher : IPasswordHasher
{
    private const int WorkFactor = 12;

    public string Hash(string password) => BCrypt.Net.BCrypt.HashPassword(password, WorkFactor);

    public bool Verify(string password, string passwordHash)
    {
        try
        {
            return BCrypt.Net.BCrypt.Verify(password, passwordHash);
        }
        catch (BCrypt.Net.SaltParseException)
        {
            // Hash corrupto o con formato invalido: tratar como credencial invalida,
            // nunca lanzar hacia el controller.
            return false;
        }
    }
}
