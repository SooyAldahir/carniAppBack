using CarniApp.Domain.Common;

namespace CarniApp.Application.Common.Interfaces;

/// <summary>
/// Repository Pattern generico (D-04). Cada entidad de dominio se accede
/// a traves de esta abstraccion, para que Application nunca dependa
/// directamente de Entity Framework ni de SQL Server.
/// </summary>
public interface IRepository<T> where T : BaseEntity
{
    Task<T?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<T>> GetAllAsync(CancellationToken cancellationToken = default);

    Task AddAsync(T entity, CancellationToken cancellationToken = default);

    void Update(T entity);

    void Remove(T entity);
}
