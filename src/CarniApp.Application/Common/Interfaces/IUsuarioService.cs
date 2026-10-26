using CarniApp.Application.Features.Usuarios;

namespace CarniApp.Application.Common.Interfaces;

/// <summary>Administracion del personal: cajeros y demas usuarios, sus roles y contrasenas.</summary>
public interface IUsuarioService
{
    Task<IReadOnlyList<UsuarioAdminDto>> ListarAsync(CancellationToken cancellationToken = default);

    Task<IReadOnlyList<RolAdminDto>> ListarRolesAsync(CancellationToken cancellationToken = default);

    Task<UsuarioResultDto<UsuarioAdminDto>> CrearAsync(CrearUsuarioRequest request, CancellationToken cancellationToken = default);

    Task<UsuarioResultDto<UsuarioAdminDto>> ActualizarAsync(Guid id, ActualizarUsuarioRequest request, Guid actorId, CancellationToken cancellationToken = default);

    /// <summary>El administrador fija una contrasena nueva; cierra las sesiones del usuario y lo desbloquea.</summary>
    Task<UsuarioResultDto<bool>> RestablecerPasswordAsync(Guid id, string nuevaPassword, CancellationToken cancellationToken = default);

    Task<UsuarioResultDto<UsuarioAdminDto>> DesbloquearAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>Cada usuario cambia su propia contrasena (pide la actual). Cierra sus sesiones.</summary>
    Task<UsuarioResultDto<bool>> CambiarPasswordPropiaAsync(Guid usuarioId, CambiarPasswordRequest request, CancellationToken cancellationToken = default);
}
