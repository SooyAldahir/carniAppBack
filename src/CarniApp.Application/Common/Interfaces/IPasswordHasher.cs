namespace CarniApp.Application.Common.Interfaces;

/// <summary>
/// Abstrae el algoritmo de hash de contrasenas (RNF-SEC-002) para que
/// Application no dependa de la libreria concreta (BCrypt, en Infrastructure).
/// </summary>
public interface IPasswordHasher
{
    string Hash(string password);

    bool Verify(string password, string passwordHash);
}
