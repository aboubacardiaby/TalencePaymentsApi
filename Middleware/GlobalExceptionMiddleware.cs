using System.Net;
using System.Text.Json;
using Stripe;
using TalenceInformatixs.Payments.Api.Models;

namespace TalenceInformatixs.Payments.Api.Middleware;

/// <summary>
/// Catches all unhandled exceptions and returns a consistent JSON error envelope,
/// preventing stack traces leaking to the client in production.
/// </summary>
public sealed class GlobalExceptionMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<GlobalExceptionMiddleware> _logger;

    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public GlobalExceptionMiddleware(
        RequestDelegate next,
        ILogger<GlobalExceptionMiddleware> logger)
    {
        _next   = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (StripeException ex)
        {
            _logger.LogError(ex, "Stripe API error on {Method} {Path}",
                context.Request.Method, context.Request.Path);

            await WriteErrorAsync(context,
                HttpStatusCode.BadGateway,
                ex.StripeError?.Message ?? ex.Message,
                ex.StripeError?.Code    ?? "stripe_error");
        }
        catch (OperationCanceledException)
        {
            // Client disconnected – not a server fault
            _logger.LogWarning("Request cancelled on {Method} {Path}",
                context.Request.Method, context.Request.Path);

            context.Response.StatusCode = StatusCodes.Status499ClientClosedRequest;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unhandled exception on {Method} {Path}",
                context.Request.Method, context.Request.Path);

            await WriteErrorAsync(context,
                HttpStatusCode.InternalServerError,
                "An unexpected error occurred. Please try again later.",
                "internal_server_error");
        }
    }

    private static async Task WriteErrorAsync(
        HttpContext context,
        HttpStatusCode statusCode,
        string error,
        string code)
    {
        context.Response.StatusCode  = (int)statusCode;
        context.Response.ContentType = "application/json";

        var body = new ApiErrorResponse
        {
            Error     = error,
            Code      = code,
            Status    = (int)statusCode,
            Timestamp = DateTime.UtcNow
        };

        await context.Response.WriteAsync(
            JsonSerializer.Serialize(body, _jsonOptions));
    }
}
