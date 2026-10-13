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
}
