namespace CarniApp.Application.Features.Auth;

public record LoginRequest(string NombreUsuario, string Password);

public record RefreshRequest(string RefreshToken);

public record BootstrapAdminRequest(string NombreCompleto, string NombreUsuario, string? Correo, string Password);

public record UsuarioResumenDto(
    Guid Id,
    string NombreCompleto,
    string NombreUsuario,
    string? Correo,
    string Rol,
    IReadOnlyList<string> Permisos);

public record AuthTokensDto(
    string AccessToken,
    DateTime AccessTokenExpiraUtc,
    string RefreshToken,
    UsuarioResumenDto Usuario);

/// <summary>
/// Codigos de resultado para que el controller decida el status HTTP
/// correcto sin que Application dependa de ASP.NET Core.
/// </summary>
public enum AuthResultCode
{
    Ok,
    CredencialesInvalidas,
    CuentaBloqueada,
    CuentaInactiva,
    TokenInvalido,
    YaExisteAlMenosUnUsuario,
    RolSuperAdministradorNoConfigurado,
    NombreUsuarioYaExiste
}

public record AuthResultDto(AuthResultCode Code, string Mensaje, AuthTokensDto? Datos = null)
{
    public bool Exitoso => Code == AuthResultCode.Ok;

    public static AuthResultDto Ok(AuthTokensDto datos) => new(AuthResultCode.Ok, "OK", datos);

    public static AuthResultDto Fail(AuthResultCode code, string mensaje) => new(code, mensaje);
}
