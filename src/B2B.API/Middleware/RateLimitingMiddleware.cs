using System.Net;
using System.Security.Claims;
using System.Text.Json;
using B2B.Application.DTOs;
using StackExchange.Redis;

namespace B2B.API.Middleware;

/// <summary>
/// Rate limiting middleware using sliding window algorithm with Redis.
/// Supports per-IP and per-user rate limiting with configurable limits.
/// </summary>
public class RateLimitingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<RateLimitingMiddleware> _logger;
    private readonly RateLimitingOptions _options;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false
    };

    public RateLimitingMiddleware(
        RequestDelegate next,
        ILogger<RateLimitingMiddleware> logger,
        RateLimitingOptions options)
    {
        _next = next ?? throw new ArgumentNullException(nameof(next));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _options = options ?? throw new ArgumentNullException(nameof(options));
    }

    public async Task InvokeAsync(HttpContext context, IConnectionMultiplexer redis)
    {
        // Skip rate limiting for health check endpoints
        if (IsExcludedPath(context.Request.Path))
        {
            await _next(context);
            return;
        }

        var clientIdentifier = GetClientIdentifier(context);
        var endpoint = GetEndpointKey(context);
        var key = $"rate_limit:{clientIdentifier}:{endpoint}";

        var db = redis.GetDatabase();
        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var windowStart = now - _options.WindowSeconds;

        try
        {
            // Use Redis sorted set for sliding window
            var transaction = db.CreateTransaction();

            // Remove old entries outside the window
            _ = transaction.SortedSetRemoveRangeByScoreAsync(key, 0, windowStart);

            // Count requests in current window
            var countTask = transaction.SortedSetLengthAsync(key);

            // Add current request
            _ = transaction.SortedSetAddAsync(key, now.ToString(), now);

            // Set expiry on the key
            _ = transaction.KeyExpireAsync(key, TimeSpan.FromSeconds(_options.WindowSeconds * 2));

            await transaction.ExecuteAsync();

            var requestCount = await countTask;

            // Get the limit for this client
            var limit = GetLimitForClient(context);

            // Add rate limit headers
            context.Response.Headers["X-RateLimit-Limit"] = limit.ToString();
            context.Response.Headers["X-RateLimit-Remaining"] = Math.Max(0, limit - requestCount - 1).ToString();
            context.Response.Headers["X-RateLimit-Reset"] = (now + _options.WindowSeconds).ToString();

            if (requestCount >= limit)
            {
                _logger.LogWarning(
                    "Rate limit exceeded for client {ClientIdentifier} on endpoint {Endpoint}. Count: {Count}, Limit: {Limit}",
                    clientIdentifier,
                    endpoint,
                    requestCount,
                    limit);

                await WriteRateLimitResponse(context, _options.WindowSeconds);
                return;
            }
        }
        catch (RedisException ex)
        {
            // Log but don't block requests if Redis is unavailable
            _logger.LogError(ex, "Redis error during rate limiting. Allowing request to proceed.");
        }

        await _next(context);
    }

    private string GetClientIdentifier(HttpContext context)
    {
        // Prefer user ID for authenticated requests
        var userId = context.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value
                     ?? context.User?.FindFirst("sub")?.Value;

        if (!string.IsNullOrEmpty(userId))
        {
            return $"user:{userId}";
        }

        // Fall back to IP address
        var ipAddress = context.Connection.RemoteIpAddress?.ToString() ?? "unknown";

        // Check for forwarded IP (behind proxy/load balancer)
        if (context.Request.Headers.TryGetValue("X-Forwarded-For", out var forwardedFor))
        {
            var firstIp = forwardedFor.ToString().Split(',').FirstOrDefault()?.Trim();
            if (!string.IsNullOrEmpty(firstIp))
            {
                ipAddress = firstIp;
            }
        }

        return $"ip:{ipAddress}";
    }

    private static string GetEndpointKey(HttpContext context)
    {
        var path = context.Request.Path.Value?.ToLowerInvariant() ?? "/";
        var method = context.Request.Method.ToUpperInvariant();

        // Normalize path by removing IDs (e.g., /api/users/123 -> /api/users/{id})
        var segments = path.Split('/', StringSplitOptions.RemoveEmptyEntries);
        var normalizedSegments = segments.Select(s =>
            Guid.TryParse(s, out _) || int.TryParse(s, out _) ? "{id}" : s);

        return $"{method}:/{string.Join("/", normalizedSegments)}";
    }

    private int GetLimitForClient(HttpContext context)
    {
        // Check for premium/elevated tier
        var tier = context.User?.FindFirst("tier")?.Value;

        return tier?.ToLowerInvariant() switch
        {
            "premium" => _options.PremiumLimit,
            "enterprise" => _options.EnterpriseLimit,
            _ => _options.PermitLimit
        };
    }

    private bool IsExcludedPath(PathString path)
    {
        var pathValue = path.Value?.ToLowerInvariant() ?? "";

        return _options.ExcludedPaths.Any(excluded =>
            pathValue.StartsWith(excluded, StringComparison.OrdinalIgnoreCase));
    }

    private static async Task WriteRateLimitResponse(HttpContext context, int retryAfterSeconds)
    {
        var correlationId = context.Items["CorrelationId"]?.ToString() ?? Guid.NewGuid().ToString();

        context.Response.StatusCode = (int)HttpStatusCode.TooManyRequests;
        context.Response.ContentType = "application/json";
        context.Response.Headers["Retry-After"] = retryAfterSeconds.ToString();

        var errorResponse = ApiErrorResponse.RateLimited(correlationId, retryAfterSeconds);
        var json = JsonSerializer.Serialize(errorResponse, JsonOptions);

        await context.Response.WriteAsync(json);
    }
}

/// <summary>
/// Configuration options for rate limiting.
/// </summary>
public class RateLimitingOptions
{
    /// <summary>
    /// Default rate limit for standard users.
    /// </summary>
    public int PermitLimit { get; set; } = 100;

    /// <summary>
    /// Rate limit for premium tier users.
    /// </summary>
    public int PremiumLimit { get; set; } = 500;

    /// <summary>
    /// Rate limit for enterprise tier users.
    /// </summary>
    public int EnterpriseLimit { get; set; } = 2000;

    /// <summary>
    /// Time window in seconds for rate limiting.
    /// </summary>
    public int WindowSeconds { get; set; } = 60;

    /// <summary>
    /// Maximum number of requests to queue when limit is reached.
    /// </summary>
    public int QueueLimit { get; set; } = 10;

    /// <summary>
    /// Paths excluded from rate limiting.
    /// </summary>
    public List<string> ExcludedPaths { get; set; } = new()
    {
        "/health",
        "/metrics"
    };
}

/// <summary>
/// Extension methods for RateLimitingMiddleware.
/// </summary>
public static class RateLimitingMiddlewareExtensions
{
    /// <summary>
    /// Adds the rate limiting middleware to the application pipeline.
    /// </summary>
    public static IApplicationBuilder UseCustomRateLimiting(
        this IApplicationBuilder builder,
        RateLimitingOptions options)
    {
        return builder.UseMiddleware<RateLimitingMiddleware>(options);
    }

    /// <summary>
    /// Adds rate limiting services to the DI container.
    /// </summary>
    public static IServiceCollection AddCustomRateLimiting(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var options = new RateLimitingOptions();
        configuration.GetSection("RateLimiting").Bind(options);
        services.AddSingleton(options);

        return services;
    }
}
