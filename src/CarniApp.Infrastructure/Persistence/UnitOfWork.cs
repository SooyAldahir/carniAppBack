using System.Collections.Concurrent;
using CarniApp.Application.Common.Interfaces;
using CarniApp.Domain.Common;

namespace CarniApp.Infrastructure.Persistence;

public class UnitOfWork : IUnitOfWork
{
    private readonly CarniAppDbContext _context;
    private readonly ConcurrentDictionary<Type, object> _repositories = new();

    public UnitOfWork(CarniAppDbContext context)
    {
        _context = context;
    }

    public IRepository<T> Repository<T>() where T : BaseEntity
    {
        return (IRepository<T>)_repositories.GetOrAdd(
            typeof(T),
            _ => new Repository<T>(_context));
    }

    public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default) =>
        _context.SaveChangesAsync(cancellationToken);
}
