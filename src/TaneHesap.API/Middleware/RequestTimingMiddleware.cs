using System.Diagnostics;

namespace TaneHesap.API.Middleware;

/// <summary>
/// Her yanıta sunucuda geçen süreyi <c>Server-Timing</c> başlığıyla ekler (tarayıcı geliştirici araçlarında görünür)
/// ve eşiği aşan istekleri uyarı olarak loglar — "site yavaş" şikayetinde sorunun ağda mı, sunucuda mı olduğu buradan anlaşılır.
/// </summary>
public sealed class RequestTimingMiddleware
{
    private static readonly TimeSpan SlowThreshold = TimeSpan.FromSeconds(1);

    private readonly RequestDelegate _next;
    private readonly ILogger<RequestTimingMiddleware> _logger;

    public RequestTimingMiddleware(RequestDelegate next, ILogger<RequestTimingMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var started = Stopwatch.GetTimestamp();
        context.Response.OnStarting(() =>
        {
            var elapsed = Stopwatch.GetElapsedTime(started);
            context.Response.Headers["Server-Timing"] = $"app;dur={elapsed.TotalMilliseconds:0.0}";
            return Task.CompletedTask;
        });

        await _next(context);

        var total = Stopwatch.GetElapsedTime(started);
        if (total > SlowThreshold)
        {
            _logger.LogWarning("Yavaş istek: {Method} {Path} {StatusCode} {ElapsedMs:0} ms",
                context.Request.Method, context.Request.Path, context.Response.StatusCode, total.TotalMilliseconds);
        }
    }
}
