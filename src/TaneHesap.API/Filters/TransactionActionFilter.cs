using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.EntityFrameworkCore;
using TaneHesap.Infrastructure.Persistence;

namespace TaneHesap.API.Filters;

/// <summary>
/// Yazan isteklerin (POST/PUT/PATCH/DELETE) tamamını tek veritabanı işlemi (transaction) içinde çalıştırır.
/// Birçok akış birden fazla SaveChanges yapar (satış girişi → stok düşümü → kasa → otomatik gider →
/// kapanış yeniden hesabı; çalışan oluşturma → Identity kullanıcısı + profil). Ortada bir adım patlarsa
/// önceki adımlar geri alınır; "satış kaydedildi ama kasa/stok eksik" gibi yarım veri oluşmaz.
/// Kesişen bir kaygı (cross-cutting concern) olduğu için servislerin içine değil, tek yere konur.
/// Identity'nin UserManager'ı da aynı kapsamdaki DbContext'i kullandığından aynı işleme katılır.
/// </summary>
public sealed class TransactionActionFilter : IAsyncActionFilter
{
    private static readonly HashSet<string> WritingMethods = new(StringComparer.OrdinalIgnoreCase)
    {
        HttpMethods.Post, HttpMethods.Put, HttpMethods.Patch, HttpMethods.Delete
    };

    private readonly ApplicationDbContext _dbContext;

    public TransactionActionFilter(ApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        if (!WritingMethods.Contains(context.HttpContext.Request.Method))
        {
            await next();
            return;
        }

        await using var transaction = await _dbContext.Database.BeginTransactionAsync(context.HttpContext.RequestAborted);

        var executed = await next();

        if (executed.Exception is not null && !executed.ExceptionHandled)
        {
            await transaction.RollbackAsync(CancellationToken.None);
            return; // İstisna ExceptionHandlingMiddleware'e ulaşır ve uygun HTTP koduna çevrilir.
        }

        await transaction.CommitAsync(CancellationToken.None);
    }
}
