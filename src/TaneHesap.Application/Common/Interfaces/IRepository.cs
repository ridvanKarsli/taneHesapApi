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

    /// <summary>Koşula uyan en az bir kayıt var mı (silme öncesi kullanım kontrolleri için — satırları yüklemez).</summary>
    Task<bool> AnyAsync(Expression<Func<T, bool>> predicate, CancellationToken ct = default);

    /// <summary>Koşula uyan kayıtların toplamı veritabanında hesaplanır (satırlar belleğe çekilmez; örn. kasa bakiyesi).</summary>
    Task<decimal> SumAsync(Expression<Func<T, bool>> predicate, Expression<Func<T, decimal>> selector, CancellationToken ct = default);

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
