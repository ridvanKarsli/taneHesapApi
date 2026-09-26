using System.Diagnostics;
using Microsoft.EntityFrameworkCore;
using TaneHesap.Infrastructure.Persistence;

namespace TaneHesap.API.Extensions;

/// <summary>Sağlık uçları: <c>/health</c> (Railway kontrolü) ve <c>/health/db</c> (API ↔ veritabanı gecikmesi).</summary>
public static class HealthEndpoints
{
    public static void MapHealthEndpoints(this WebApplication app)
    {
        app.MapGet("/health", () => Results.Ok(new { status = "ok", utc = DateTime.UtcNow })).AllowAnonymous();

        // Tek bir "SELECT 1" sorgusunun süresi. Aynı bölgede, özel ağ üzerinden bağlı bir veritabanında 1-3 ms olmalı;
        // 50 ms üstü veritabanının başka bölgede ya da genel (public) adres üzerinden bağlı olduğunu gösterir.
        app.MapGet("/health/db", async (ApplicationDbContext db, CancellationToken ct) =>
        {
            await db.Database.ExecuteSqlRawAsync("SELECT 1", ct); // bağlantı havuzunu ısıt
            var samples = new List<double>();
            for (var i = 0; i < 3; i++)
            {
                var started = Stopwatch.GetTimestamp();
                await db.Database.ExecuteSqlRawAsync("SELECT 1", ct);
                samples.Add(Math.Round(Stopwatch.GetElapsedTime(started).TotalMilliseconds, 1));
            }

            return Results.Ok(new { status = "ok", queryMs = samples.Min(), samplesMs = samples });
        }).AllowAnonymous();
    }
}
