using System.Linq.Expressions;
using TaneHesap.Domain.Common;

namespace TaneHesap.Application.Common.Interfaces;

/// <summary>
/// Genel amaçlı repository arayüzü. Infrastructure katmanında EF Core (ApplicationDbContext)
/// üzerinden implemente edilir. Application katmanı EF Core'a doğrudan bağımlı olmaz.
/// </summary>
public interface IRepository<T> where T : BaseEntity
{
    Task<T?> GetByIdAsync(Guid id, CancellationToken ct = default);

    Task<List<T>> ListAsync(Expression<Func<T, bool>>? predicate = null, CancellationToken ct = default);

    Task AddAsync(T entity, CancellationToken ct = default);

    void Update(T entity);

    void Remove(T entity);
}

/// <summary>
/// Bir HTTP isteği içindeki tüm repository işlemlerini tek bir transaction/SaveChanges altında toplar.
/// </summary>
public interface IUnitOfWork
{
    IRepository<T> Repository<T>() where T : BaseEntity;

    Task<int> SaveChangesAsync(CancellationToken ct = default);
}
