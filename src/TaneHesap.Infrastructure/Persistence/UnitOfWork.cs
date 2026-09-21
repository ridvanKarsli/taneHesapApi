using Microsoft.EntityFrameworkCore;
using Npgsql;
using TaneHesap.Application.Common.Exceptions;
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

    /// <summary>
    /// Silinmek istenen kayıt başka kayıtlarda kullanılıyorsa (PostgreSQL 23503 — yabancı anahtar ihlali)
    /// kullanıcıya anlaşılır bir 409 döner. Servislerdeki açık kullanım kontrollerinin arkasındaki son
    /// güvenlik ağıdır; "Beklenmeyen bir hata oluştu" yerine ne yapılacağı söylenir.
    /// </summary>
    public async Task<int> SaveChangesAsync(CancellationToken ct = default)
    {
        try
        {
            return await _context.SaveChangesAsync(ct);
        }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException { SqlState: PostgresErrorCodes.ForeignKeyViolation })
        {
            throw new ConflictAppException(
                "Bu kayıt başka kayıtlarda kullanıldığı için silinemez. Silmek yerine pasif yapabilirsiniz.");
        }
    }
}
