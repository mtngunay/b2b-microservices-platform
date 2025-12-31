using B2B.Application.Interfaces.Services;
using Serilog.Context;

namespace B2B.API.Middleware;

/// <summary>
/// Middleware that extracts or generates a correlation ID for distributed tracing.
/// The correlation ID is stored in AsyncLocal, added to Serilog LogContext,
/// and included in response headers.
/// </summary>
public class CorrelationIdMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<CorrelationIdMiddleware> _logger;

    /// <summary>
    /// The header name used for correlation ID.
    /// </summary>
    public const string CorrelationIdHeader = "X-Correlation-Id";

    public CorrelationIdMiddleware(RequestDelegate next, ILogger<CorrelationIdMiddleware> logger)
    {
        _next = next ?? throw new ArgumentNullException(nameof(next));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task InvokeAsync(HttpContext context, ICorrelationIdAccessor correlationIdAccessor)
    {
        // Extract correlation ID from request header or generate a new one
        var correlationId = GetOrGenerateCorrelationId(context);

        // Store in the accessor (AsyncLocal)
        correlationIdAccessor.SetCorrelationId(correlationId);

        // Store in HttpContext.Items for easy access
        context.Items["CorrelationId"] = correlationId;

        // Add to response headers
        context.Response.OnStarting(() =>
        {
            if (!context.Response.Headers.ContainsKey(CorrelationIdHeader))
            {
                context.Response.Headers.Append(CorrelationIdHeader, correlationId);
            }
            return Task.CompletedTask;
        });

        // Push to Serilog LogContext so all logs include the correlation ID
        using (LogContext.PushProperty("CorrelationId", correlationId))
        {
            _logger.LogDebug("Processing request with CorrelationId: {CorrelationId}", correlationId);

            try
            {
                await _next(context);
            }
            finally
            {
                _logger.LogDebug("Completed request with CorrelationId: {CorrelationId}, StatusCode: {StatusCode}",
                    correlationId, context.Response.StatusCode);
            }
        }
    }

    private string GetOrGenerateCorrelationId(HttpContext context)
    {
        // Try to extract from request header
        if (context.Request.Headers.TryGetValue(CorrelationIdHeader, out var correlationIdValues))
        {
            var correlationId = correlationIdValues.FirstOrDefault();
            if (!string.IsNullOrWhiteSpace(correlationId))
            {
                _logger.LogDebug("Using existing CorrelationId from header: {CorrelationId}", correlationId);
                return correlationId;
            }
        }

        // Generate a new correlation ID
        var newCorrelationId = Guid.NewGuid().ToString();
        _logger.LogDebug("Generated new CorrelationId: {CorrelationId}", newCorrelationId);
        return newCorrelationId;
    }
}

/// <summary>
/// Extension methods for adding CorrelationId middleware to the application pipeline.
/// </summary>
public static class CorrelationIdMiddlewareExtensions
{
    /// <summary>
    /// Adds the CorrelationId middleware to the application pipeline.
    /// This should be added early in the pipeline to ensure all subsequent
    /// middleware and handlers have access to the correlation ID.
    /// </summary>
    public static IApplicationBuilder UseCorrelationId(this IApplicationBuilder builder)
    {
        return builder.UseMiddleware<CorrelationIdMiddleware>();
    }

    /// <summary>
    /// Adds the CorrelationIdAccessor service to the DI container.
    /// </summary>
    public static IServiceCollection AddCorrelationId(this IServiceCollection services)
    {
        services.AddScoped<ICorrelationIdAccessor, B2B.Infrastructure.Identity.CorrelationIdAccessor>();
        return services;
    }
}
