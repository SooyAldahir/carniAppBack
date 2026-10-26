using CarniApp.Domain.Entities;

namespace CarniApp.Application.Common.Interfaces;

/// <summary>
/// Repositorio especializado de Usuario: ademas del CRUD generico de
/// IRepository, necesita busqueda por nombre de usuario con su Rol y
/// Permisos ya cargados (para armar el JWT en un solo viaje a la BD).
/// </summary>
public interface IUsuarioRepository : IRepository<Usuario>
{
    Task<Usuario?> GetByNombreUsuarioConRolYPermisosAsync(string nombreUsuario, CancellationToken cancellationToken = default);

    Task<bool> ExisteAlgunUsuarioAsync(CancellationToken cancellationToken = default);

    /// <summary>Todos los usuarios (activos o no) con su rol, por nombre.</summary>
    Task<IReadOnlyList<Usuario>> ListarConRolAsync(CancellationToken cancellationToken = default);

    Task<Usuario?> GetByIdConRolAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>Compara sin importar mayusculas/minusculas.</summary>
    Task<bool> ExisteNombreUsuarioAsync(string nombreUsuario, Guid? excluirId = null, CancellationToken cancellationToken = default);

    /// <summary>Usuarios activos con ese rol (para no dejar el sistema sin Super Administrador).</summary>
    Task<int> ContarActivosPorRolAsync(Guid rolId, Guid? excluirUsuarioId = null, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Rol>> ListarRolesConPermisosAsync(CancellationToken cancellationToken = default);

    Task<Rol?> GetRolAsync(Guid rolId, CancellationToken cancellationToken = default);
}
