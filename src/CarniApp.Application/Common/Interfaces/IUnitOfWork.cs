namespace CarniApp.Application.Common.Interfaces;

/// <summary>
/// Unit of Work: agrupa los repositorios de una misma transaccion.
/// Es la pieza clave para RF-POS-011 (descuento de inventario en
/// transaccion atomica que evite sobreventa por concurrencia).
/// </summary>
public interface IUnitOfWork
{
    IRepository<T> Repository<T>() where T : Domain.Common.BaseEntity;

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
