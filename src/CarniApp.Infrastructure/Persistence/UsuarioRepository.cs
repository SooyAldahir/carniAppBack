using CarniApp.Application.Common.Interfaces;
using CarniApp.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace CarniApp.Infrastructure.Persistence;

public class UsuarioRepository : Repository<Usuario>, IUsuarioRepository
{
    public UsuarioRepository(CarniAppDbContext context) : base(context)
    {
    }

    public async Task<Usuario?> GetByNombreUsuarioConRolYPermisosAsync(string nombreUsuario, CancellationToken cancellationToken = default) =>
        await _dbSet
            .Include(u => u.Rol)
                .ThenInclude(r => r.RolPermisos)
                    .ThenInclude(rp => rp.Permiso)
            .FirstOrDefaultAsync(u => u.NombreUsuario == nombreUsuario, cancellationToken);

    public async Task<bool> ExisteAlgunUsuarioAsync(CancellationToken cancellationToken = default) =>
        await _context.Usuarios.AnyAsync(cancellationToken);

    public async Task<IReadOnlyList<Usuario>> ListarConRolAsync(CancellationToken cancellationToken = default) =>
        await _dbSet.Include(u => u.Rol).OrderBy(u => u.NombreCompleto).ToListAsync(cancellationToken);

    public async Task<Usuario?> GetByIdConRolAsync(Guid id, CancellationToken cancellationToken = default) =>
        await _dbSet.Include(u => u.Rol).FirstOrDefaultAsync(u => u.Id == id, cancellationToken);

    public async Task<bool> ExisteNombreUsuarioAsync(string nombreUsuario, Guid? excluirId = null, CancellationToken cancellationToken = default)
    {
        var buscado = nombreUsuario.Trim().ToLower();
        return await _dbSet.AnyAsync(
            u => u.NombreUsuario.ToLower() == buscado && (excluirId == null || u.Id != excluirId),
            cancellationToken);
    }

    public async Task<int> ContarActivosPorRolAsync(Guid rolId, Guid? excluirUsuarioId = null, CancellationToken cancellationToken = default) =>
        await _dbSet.CountAsync(
            u => u.Activo && u.RolId == rolId && (excluirUsuarioId == null || u.Id != excluirUsuarioId),
            cancellationToken);

    public async Task<IReadOnlyList<Rol>> ListarRolesConPermisosAsync(CancellationToken cancellationToken = default) =>
        await _context.Roles
            .Where(r => r.Activo)
            .Include(r => r.RolPermisos).ThenInclude(rp => rp.Permiso)
            .OrderBy(r => r.Nombre)
            .ToListAsync(cancellationToken);

    public async Task<Rol?> GetRolAsync(Guid rolId, CancellationToken cancellationToken = default) =>
        await _context.Roles.FirstOrDefaultAsync(r => r.Id == rolId && r.Activo, cancellationToken);
}
