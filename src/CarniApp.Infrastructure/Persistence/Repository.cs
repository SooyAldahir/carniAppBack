using CarniApp.Application.Common.Interfaces;
using CarniApp.Domain.Common;
using Microsoft.EntityFrameworkCore;

namespace CarniApp.Infrastructure.Persistence;

/// <summary>
/// Implementacion generica del Repository Pattern (D-04) sobre EF Core.
/// _context y _dbSet son protected para que los repositorios
/// especializados (UsuarioRepository, RefreshTokenRepository, ...)
/// puedan agregar consultas propias sin reescribir el CRUD base.
/// </summary>
public class Repository<T> : IRepository<T> where T : BaseEntity
{
    protected readonly CarniAppDbContext _context;
    protected readonly DbSet<T> _dbSet;

    public Repository(CarniAppDbContext context)
    {
        _context = context;
        _dbSet = context.Set<T>();
    }

    public async Task<T?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        await _dbSet.FirstOrDefaultAsync(e => e.Id == id, cancellationToken);

    public async Task<IReadOnlyList<T>> GetAllAsync(CancellationToken cancellationToken = default) =>
        await _dbSet.Where(e => e.Activo).ToListAsync(cancellationToken);

    public async Task AddAsync(T entity, CancellationToken cancellationToken = default) =>
        await _dbSet.AddAsync(entity, cancellationToken);

    public void Update(T entity)
    {
        entity.FechaActualizacion = DateTime.UtcNow;
        _dbSet.Update(entity);
    }

    public void Remove(T entity) => _dbSet.Remove(entity);
}
