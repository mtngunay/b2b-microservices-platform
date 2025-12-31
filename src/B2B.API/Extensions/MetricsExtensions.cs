using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace B2B.API.Extensions;

/// <summary>
/// Custom application metrics for Prometheus monitoring.
/// </summary>
public static class AppMetrics
{
    private static readonly Meter Meter = new("B2B.API", "1.0.0");

    /// <summary>
    /// Counter for total HTTP requests by method, endpoint, and status code.
    /// </summary>
    public static readonly Counter<long> HttpRequestsTotal = Meter.CreateCounter<long>(
        "b2b_http_requests_total",
        unit: "{requests}",
        description: "Total number of HTTP requests");

    /// <summary>
    /// Histogram for HTTP request duration in seconds.
    /// </summary>
    public static readonly Histogram<double> HttpRequestDuration = Meter.CreateHistogram<double>(
        "b2b_http_request_duration_seconds",
        unit: "s",
        description: "HTTP request duration in seconds");

    /// <summary>
    /// Gauge for active HTTP connections.
    /// </summary>
    public static readonly UpDownCounter<long> ActiveConnections = Meter.CreateUpDownCounter<long>(
        "b2b_active_connections",
        unit: "{connections}",
        description: "Number of active HTTP connections");

    /// <summary>
    /// Counter for total messages published to the message bus.
    /// </summary>
    public static readonly Counter<long> MessagesPublished = Meter.CreateCounter<long>(
        "b2b_messages_published_total",
        unit: "{messages}",
        description: "Total number of messages published");

    /// <summary>
    /// Counter for total messages consumed from the message bus.
    /// </summary>
    public static readonly Counter<long> MessagesConsumed = Meter.CreateCounter<long>(
        "b2b_messages_consumed_total",
        unit: "{messages}",
        description: "Total number of messages consumed");

    /// <summary>
    /// Counter for total errors by type.
    /// </summary>
    public static readonly Counter<long> ErrorsTotal = Meter.CreateCounter<long>(
        "b2b_errors_total",
        unit: "{errors}",
        description: "Total number of errors");

    /// <summary>
    /// Counter for cache hits and misses.
    /// </summary>
    public static readonly Counter<long> CacheOperations = Meter.CreateCounter<long>(
        "b2b_cache_operations_total",
        unit: "{operations}",
        description: "Total number of cache operations");

    /// <summary>
    /// Histogram for database query duration.
    /// </summary>
    public static readonly Histogram<double> DatabaseQueryDuration = Meter.CreateHistogram<double>(
        "b2b_database_query_duration_seconds",
        unit: "s",
        description: "Database query duration in seconds");

    /// <summary>
    /// Counter for authentication attempts.
    /// </summary>
    public static readonly Counter<long> AuthenticationAttempts = Meter.CreateCounter<long>(
        "b2b_authentication_attempts_total",
        unit: "{attempts}",
        description: "Total number of authentication attempts");

    /// <summary>
    /// Counter for authorization decisions.
    /// </summary>
    public static readonly Counter<long> AuthorizationDecisions = Meter.CreateCounter<long>(
        "b2b_authorization_decisions_total",
        unit: "{decisions}",
        description: "Total number of authorization decisions");

    /// <summary>
    /// Counter for rate limit hits.
    /// </summary>
    public static readonly Counter<long> RateLimitHits = Meter.CreateCounter<long>(
        "b2b_rate_limit_hits_total",
        unit: "{hits}",
        description: "Total number of rate limit hits");

    /// <summary>
    /// Counter for outbox messages by status.
    /// </summary>
    public static readonly Counter<long> OutboxMessages = Meter.CreateCounter<long>(
        "b2b_outbox_messages_total",
        unit: "{messages}",
        description: "Total number of outbox messages");
}

/// <summary>
/// Middleware for collecting HTTP request metrics.
/// </summary>
public class MetricsMiddleware
{
    private readonly RequestDelegate _next;

    public MetricsMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        // Skip metrics for health and metrics endpoints
        var path = context.Request.Path.Value;
        if (path != null && (path.StartsWith("/health") || path.StartsWith("/metrics")))
        {
            await _next(context);
            return;
        }

        AppMetrics.ActiveConnections.Add(1);
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();

        try
        {
            await _next(context);
        }
        finally
        {
            stopwatch.Stop();
            AppMetrics.ActiveConnections.Add(-1);

            var tags = new TagList
            {
                { "method", context.Request.Method },
                { "endpoint", GetNormalizedEndpoint(context) },
                { "status_code", context.Response.StatusCode.ToString() }
            };

            AppMetrics.HttpRequestsTotal.Add(1, tags);
            AppMetrics.HttpRequestDuration.Record(stopwatch.Elapsed.TotalSeconds, tags);

            // Track errors
            if (context.Response.StatusCode >= 400)
            {
                var errorTags = new TagList
                {
                    { "type", context.Response.StatusCode >= 500 ? "server_error" : "client_error" },
                    { "status_code", context.Response.StatusCode.ToString() }
                };
                AppMetrics.ErrorsTotal.Add(1, errorTags);
            }
        }
    }

    private static string GetNormalizedEndpoint(HttpContext context)
    {
        // Normalize endpoint to avoid high cardinality
        var endpoint = context.GetEndpoint();
        if (endpoint != null)
        {
            var routePattern = (endpoint as RouteEndpoint)?.RoutePattern?.RawText;
            if (!string.IsNullOrEmpty(routePattern))
            {
                return routePattern;
            }
        }

        // Fallback to path with ID normalization
        var path = context.Request.Path.Value ?? "/";
        
        // Replace GUIDs with {id}
        path = System.Text.RegularExpressions.Regex.Replace(
            path, 
            @"[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}", 
            "{id}");
        
        // Replace numeric IDs with {id}
        path = System.Text.RegularExpressions.Regex.Replace(
            path, 
            @"/\d+", 
            "/{id}");

        return path;
    }
}

/// <summary>
/// Extension methods for metrics configuration.
/// </summary>
public static class MetricsExtensions
{
    /// <summary>
    /// Adds the metrics middleware to the application pipeline.
    /// </summary>
    public static IApplicationBuilder UseMetricsMiddleware(this IApplicationBuilder builder)
    {
        return builder.UseMiddleware<MetricsMiddleware>();
    }
}
