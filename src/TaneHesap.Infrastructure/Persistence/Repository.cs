using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using TaneHesap.Application.Common.Interfaces;
using TaneHesap.Domain.Common;

namespace TaneHesap.Infrastructure.Persistence;

public class Repository<T> : IRepository<T> where T : BaseEntity
{
    private readonly ApplicationDbContext _context;
    private readonly DbSet<T> _set;

    public Repository(ApplicationDbContext context)
    {
        _context = context;
        _set = context.Set<T>();
    }

    public async Task<T?> GetByIdAsync(Guid id, CancellationToken ct = default)
        => await _set.FirstOrDefaultAsync(e => e.Id == id, ct);

    public async Task<List<T>> ListAsync(Expression<Func<T, bool>>? predicate = null, CancellationToken ct = default)
    {
        IQueryable<T> query = _set;
        if (predicate is not null)
        {
            query = query.Where(predicate);
        }

        return await query.ToListAsync(ct);
    }

    public async Task AddAsync(T entity, CancellationToken ct = default) => await _set.AddAsync(entity, ct);

    public void Update(T entity) => _set.Update(entity);

    public void Remove(T entity) => _set.Remove(entity);
}
