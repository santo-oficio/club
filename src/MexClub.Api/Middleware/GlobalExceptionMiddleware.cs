using System.Net;
using System.Text.Json;

namespace MexClub.Api.Middleware;

public class GlobalExceptionMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<GlobalExceptionMiddleware> _logger;

    public GlobalExceptionMiddleware(RequestDelegate next, ILogger<GlobalExceptionMiddleware> logger)
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
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unhandled exception: {Message}", ex.Message);
            await HandleExceptionAsync(context, ex);
        }
    }

    private static async Task HandleExceptionAsync(HttpContext context, Exception exception)
    {
        context.Response.ContentType = "application/json";

        var (statusCode, message) = exception switch
        {
            UnauthorizedAccessException => (HttpStatusCode.Unauthorized, "No autorizado."),
            KeyNotFoundException => (HttpStatusCode.NotFound, "Recurso no encontrado."),
            ArgumentException => (HttpStatusCode.BadRequest, exception.Message),
            InvalidOperationException => (HttpStatusCode.Conflict, exception.Message),
            _ => (HttpStatusCode.InternalServerError, "Ha ocurrido un error interno. Por favor, inténtelo de nuevo.")
        };

        context.Response.StatusCode = (int)statusCode;

        var result = JsonSerializer.Serialize(new
        {
            success = false,
            message,
            statusCode = (int)statusCode
        });

        await context.Response.WriteAsync(result);
    }
}
