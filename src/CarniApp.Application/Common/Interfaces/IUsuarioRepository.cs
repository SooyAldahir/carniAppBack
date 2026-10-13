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
}
