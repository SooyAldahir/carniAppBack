using CarniApp.Application.Common.Interfaces;
using CarniApp.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace CarniApp.Infrastructure.Persistence;

public class ClienteRepository : Repository<Cliente>, IClienteRepository
{
    public ClienteRepository(CarniAppDbContext context) : base(context)
    {
    }

    public async Task<IReadOnlyList<Cliente>> BuscarActivosAsync(string? texto, CancellationToken cancellationToken = default)
    {
        var consulta = _dbSet.AsNoTracking().Where(c => c.Activo);

        if (!string.IsNullOrWhiteSpace(texto))
        {
            var t = texto.Trim().ToLower();
            consulta = consulta.Where(c => c.Nombre.ToLower().Contains(t) || (c.Telefono != null && c.Telefono.Contains(t)));
        }

        return await consulta.OrderBy(c => c.Nombre).ToListAsync(cancellationToken);
    }

    public async Task<bool> ExisteTelefonoAsync(string telefono, Guid? excluirId = null, CancellationToken cancellationToken = default) =>
        await _dbSet.AnyAsync(c =>
            c.Activo &&
            c.Telefono == telefono &&
            (excluirId == null || c.Id != excluirId), cancellationToken);
}
