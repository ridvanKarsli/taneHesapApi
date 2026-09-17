using TaneHesap.Application.Common.Interfaces;
using TaneHesap.Domain.Common;

namespace TaneHesap.Infrastructure.Persistence;

public class UnitOfWork : IUnitOfWork
{
    private readonly ApplicationDbContext _context;
    private readonly Dictionary<Type, object> _repositories = new();

    public UnitOfWork(ApplicationDbContext context)
    {
        _context = context;
    }

    public IRepository<T> Repository<T>() where T : BaseEntity
    {
        if (_repositories.TryGetValue(typeof(T), out var existing))
        {
            return (IRepository<T>)existing;
        }

        var repo = new Repository<T>(_context);
        _repositories[typeof(T)] = repo;
        return repo;
    }

    public Task<int> SaveChangesAsync(CancellationToken ct = default) => _context.SaveChangesAsync(ct);
}
