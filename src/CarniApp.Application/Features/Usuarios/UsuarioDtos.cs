namespace CarniApp.Application.Features.Usuarios;

/// <summary>Usuario tal como lo ve quien administra al personal (nunca incluye la contrasena).</summary>
public record UsuarioAdminDto(
    Guid Id,
    string NombreCompleto,
    string NombreUsuario,
    string? Correo,
    Guid RolId,
    string Rol,
    bool Activo,
    bool Bloqueado,
    DateTime FechaCreacion);

public record RolAdminDto(Guid Id, string Nombre, string? Descripcion, IReadOnlyList<string> Permisos);

public record CrearUsuarioRequest(string NombreCompleto, string NombreUsuario, string? Correo, string Password, Guid RolId);

public record ActualizarUsuarioRequest(string NombreCompleto, string? Correo, Guid RolId, bool Activo);

public record RestablecerPasswordRequest(string NuevaPassword);

public record CambiarPasswordRequest(string PasswordActual, string PasswordNueva);

public enum UsuarioResultCode
{
    Ok,
    NoEncontrado,
    Invalido,
    NombreUsuarioDuplicado,
    NoPermitido,
    PasswordIncorrecta
}

public record UsuarioResultDto<T>(UsuarioResultCode Code, string? Mensaje, T? Datos = default)
{
    public bool Exitoso => Code == UsuarioResultCode.Ok;

    public static UsuarioResultDto<T> Ok(T datos) => new(UsuarioResultCode.Ok, null, datos);

    public static UsuarioResultDto<T> Fail(UsuarioResultCode code, string mensaje) => new(code, mensaje);
}
