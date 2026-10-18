using System.Collections.Concurrent;
using CarniApp.Application.Common.Exceptions;
using CarniApp.Application.Common.Interfaces;
using CarniApp.Domain.Common;
using Microsoft.EntityFrameworkCore;

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

    public async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            return await _context.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException ex)
        {
            throw new ConcurrencyConflictException(
                "Otro proceso modifico los mismos datos al mismo tiempo.", ex);
        }
    }
}
