using System.Text.Json;
using B2B.Application.Interfaces.Services;
using Microsoft.Extensions.Logging;
using Polly;
using Polly.Retry;
using StackExchange.Redis;

namespace B2B.Infrastructure.Caching;

/// <summary>
/// Redis-based implementation of ICacheService with tenant-scoped keys and Polly retry policies.
/// </summary>
public class RedisCacheService : ICacheService
{
    private readonly IConnectionMultiplexer _redis;
    private readonly ICurrentUserService _currentUserService;
    private readonly ILogger<RedisCacheService> _logger;
    private readonly AsyncRetryPolicy _retryPolicy;
    private readonly JsonSerializerOptions _jsonOptions;

    private const string TenantKeyPrefix = "tenant";
    private const int DefaultRetryCount = 3;
    private static readonly TimeSpan DefaultExpiry = TimeSpan.FromMinutes(30);

    /// <summary>
    /// Initializes a new instance of RedisCacheService.
    /// </summary>
    /// <param name="redis">The Redis connection multiplexer.</param>
    /// <param name="currentUserService">Service to get current user context.</param>
    /// <param name="logger">Logger instance.</param>
    public RedisCacheService(
        IConnectionMultiplexer redis,
        ICurrentUserService currentUserService,
        ILogger<RedisCacheService> logger)
    {
        _redis = redis ?? throw new ArgumentNullException(nameof(redis));
        _currentUserService = currentUserService ?? throw new ArgumentNullException(nameof(currentUserService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = false
        };

        // Configure Polly retry policy with exponential backoff and jitter
        _retryPolicy = Policy
            .Handle<RedisConnectionException>()
            .Or<RedisTimeoutException>()
            .Or<RedisServerException>()
            .WaitAndRetryAsync(
                DefaultRetryCount,
                retryAttempt => TimeSpan.FromMilliseconds(Math.Pow(2, retryAttempt) * 100)
                    + TimeSpan.FromMilliseconds(new Random().Next(0, 100)),
                onRetry: (exception, timeSpan, retryCount, context) =>
                {
                    _logger.LogWarning(
                        exception,
                        "Redis operation failed. Retry attempt {RetryCount} after {Delay}ms",
                        retryCount,
                        timeSpan.TotalMilliseconds);
                });
    }

    /// <inheritdoc />
    public async Task<T?> GetAsync<T>(string key, CancellationToken cancellationToken = default)
    {
        var tenantKey = BuildTenantKey(key);

        try
        {
            return await _retryPolicy.ExecuteAsync(async () =>
            {
                var db = _redis.GetDatabase();
                var value = await db.StringGetAsync(tenantKey);

                if (value.IsNullOrEmpty)
                {
                    _logger.LogDebug("Cache miss for key: {Key}", tenantKey);
                    return default;
                }

                _logger.LogDebug("Cache hit for key: {Key}", tenantKey);
                return JsonSerializer.Deserialize<T>(value!, _jsonOptions);
            });
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "Failed to get value from cache for key: {Key}", tenantKey);
            return default;
        }
    }

    /// <inheritdoc />
    public async Task SetAsync<T>(
        string key,
        T value,
        TimeSpan? expiry = null,
        CancellationToken cancellationToken = default)
    {
        var tenantKey = BuildTenantKey(key);
        var effectiveExpiry = expiry ?? DefaultExpiry;

        try
        {
            await _retryPolicy.ExecuteAsync(async () =>
            {
                var db = _redis.GetDatabase();
                var serializedValue = JsonSerializer.Serialize(value, _jsonOptions);
                await db.StringSetAsync(tenantKey, serializedValue, effectiveExpiry);
                _logger.LogDebug("Cache set for key: {Key} with expiry: {Expiry}", tenantKey, effectiveExpiry);
            });
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "Failed to set value in cache for key: {Key}", tenantKey);
            // Graceful degradation - don't throw, just log
        }
    }

    /// <inheritdoc />
    public async Task RemoveAsync(string key, CancellationToken cancellationToken = default)
    {
        var tenantKey = BuildTenantKey(key);

        try
        {
            await _retryPolicy.ExecuteAsync(async () =>
            {
                var db = _redis.GetDatabase();
                await db.KeyDeleteAsync(tenantKey);
                _logger.LogDebug("Cache removed for key: {Key}", tenantKey);
            });
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "Failed to remove value from cache for key: {Key}", tenantKey);
        }
    }

    /// <inheritdoc />
    public async Task<T> GetOrSetAsync<T>(
        string key,
        Func<Task<T>> factory,
        TimeSpan? expiry = null,
        CancellationToken cancellationToken = default)
    {
        var cachedValue = await GetAsync<T>(key, cancellationToken);
        if (cachedValue is not null)
        {
            return cachedValue;
        }

        var value = await factory();
        await SetAsync(key, value, expiry, cancellationToken);
        return value;
    }

    /// <inheritdoc />
    public async Task RemoveByPatternAsync(string pattern, CancellationToken cancellationToken = default)
    {
        var tenantPattern = BuildTenantKey(pattern);

        try
        {
            await _retryPolicy.ExecuteAsync(async () =>
            {
                var endpoints = _redis.GetEndPoints();
                foreach (var endpoint in endpoints)
                {
                    var server = _redis.GetServer(endpoint);
                    var db = _redis.GetDatabase();

                    await foreach (var key in server.KeysAsync(pattern: tenantPattern))
                    {
                        await db.KeyDeleteAsync(key);
                        _logger.LogDebug("Cache removed for pattern match: {Key}", key);
                    }
                }
            });
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "Failed to remove values from cache for pattern: {Pattern}", tenantPattern);
        }
    }

    /// <inheritdoc />
    public async Task<bool> ExistsAsync(string key, CancellationToken cancellationToken = default)
    {
        var tenantKey = BuildTenantKey(key);

        try
        {
            return await _retryPolicy.ExecuteAsync(async () =>
            {
                var db = _redis.GetDatabase();
                return await db.KeyExistsAsync(tenantKey);
            });
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "Failed to check existence in cache for key: {Key}", tenantKey);
            return false;
        }
    }

    /// <summary>
    /// Builds a tenant-scoped cache key.
    /// </summary>
    /// <param name="key">The original key.</param>
    /// <returns>A tenant-scoped key.</returns>
    private string BuildTenantKey(string key)
    {
        var tenantId = _currentUserService.TenantId;
        
        // If no tenant context, use global namespace
        if (string.IsNullOrEmpty(tenantId))
        {
            return $"global:{key}";
        }

        return $"{TenantKeyPrefix}:{tenantId}:{key}";
    }
}
