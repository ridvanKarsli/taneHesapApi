using System.Net;
using System.Text.Json;
using TaneHesap.Application.Common.Exceptions;

namespace TaneHesap.API.Middleware;

/// <summary>
/// Application katmanından fırlatılan istisnaları tutarlı JSON hata yanıtlarına çevirir.
/// </summary>
public class ExceptionHandlingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ExceptionHandlingMiddleware> _logger;

    public ExceptionHandlingMiddleware(RequestDelegate next, ILogger<ExceptionHandlingMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (OperationCanceledException) when (context.RequestAborted.IsCancellationRequested)
        {
            // İstemci isteği iptal etti (sayfa değişti vb.) — hata değildir, 499 ile sessizce kapatılır.
            if (!context.Response.HasStarted)
            {
                context.Response.StatusCode = 499;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "İşlenmeyen hata: {Method} {Path} — {Message}", context.Request.Method, context.Request.Path, ex.Message);
            if (context.Response.HasStarted)
            {
                throw; // Yanıt başladıysa gövde artık değiştirilemez; sunucu bağlantıyı kapatır.
            }

            await HandleExceptionAsync(context, ex);
        }
    }

    private static Task HandleExceptionAsync(HttpContext context, Exception exception)
    {
        var (statusCode, message) = exception switch
        {
            NotFoundException => (HttpStatusCode.NotFound, exception.Message),
            ForbiddenAccessException => (HttpStatusCode.Forbidden, exception.Message),
            ValidationAppException => (HttpStatusCode.BadRequest, exception.Message),
            ConflictAppException => (HttpStatusCode.Conflict, exception.Message),
            _ => (HttpStatusCode.InternalServerError, "Beklenmeyen bir hata oluştu.")
        };

        context.Response.ContentType = "application/json";
        context.Response.StatusCode = (int)statusCode;

        var payload = JsonSerializer.Serialize(new { error = message });
        return context.Response.WriteAsync(payload);
    }
}
