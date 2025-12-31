using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using B2B.Application.Interfaces.Services;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using StackExchange.Redis;

namespace B2B.Infrastructure.FeatureFlags;

/// <summary>
/// Redis-based implementation of IFeatureFlagService with support for
/// percentage rollouts, user segment targeting, and A/B testing variants.
/// </summary>
public class FeatureFlagService : IFeatureFlagService
{
    private readonly IConnectionMultiplexer _redis;
    private readonly ICurrentUserService _currentUserService;
    private readonly ILogger<FeatureFlagService> _logger;
    private readonly FeatureFlagOptions _options;
    private readonly IMemoryCache _localCache;
    private readonly JsonSerializerOptions _jsonOptions;

    /// <summary>
    /// Initializes a new instance of FeatureFlagService.
    /// </summary>
    public FeatureFlagService(
        IConnectionMultiplexer redis,
        ICurrentUserService currentUserService,
        ILogger<FeatureFlagService> logger,
        IOptions<FeatureFlagOptions> options,
        IMemoryCache localCache)
    {
        _redis = redis ?? throw new ArgumentNullException(nameof(redis));
        _currentUserService = currentUserService ?? throw new ArgumentNullException(nameof(currentUserService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
        _localCache = localCache ?? throw new ArgumentNullException(nameof(localCache));

        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = false
        };
    }

    /// <inheritdoc />
    public async Task<bool> IsEnabledAsync(
        string featureName,
        CancellationToken cancellationToken = default)
    {
        var flag = await GetFeatureFlagAsync(featureName, cancellationToken);
        if (flag == null)
        {
            _logger.LogDebug("Feature flag {FeatureName} not found, returning false", featureName);
            return false;
        }

        return flag.Enabled;
    }

    /// <inheritdoc />
    public async Task<bool> IsEnabledForUserAsync(
        string featureName,
        string userId,
        CancellationToken cancellationToken = default)
    {
        var flag = await GetFeatureFlagAsync(featureName, cancellationToken);
        if (flag == null)
        {
            _logger.LogDebug("Feature flag {FeatureName} not found for user {UserId}", featureName, userId);
            return false;
        }

        // If globally disabled, check if user is in enabled users list
        if (!flag.Enabled)
        {
            var isExplicitlyEnabled = flag.EnabledUsers.Contains(userId, StringComparer.OrdinalIgnoreCase);
            _logger.LogDebug(
                "Feature {FeatureName} globally disabled, user {UserId} explicitly enabled: {IsEnabled}",
                featureName, userId, isExplicitlyEnabled);
            return isExplicitlyEnabled;
        }

        // Check if user is explicitly enabled (bypass percentage)
        if (flag.EnabledUsers.Contains(userId, StringComparer.OrdinalIgnoreCase))
        {
            _logger.LogDebug("User {UserId} explicitly enabled for feature {FeatureName}", userId, featureName);
            return true;
        }

        // Check role-based targeting
        var userRoles = _currentUserService.Roles;
        if (flag.EnabledRoles.Any() && userRoles.Any(r => flag.EnabledRoles.Contains(r, StringComparer.OrdinalIgnoreCase)))
        {
            _logger.LogDebug("User {UserId} enabled for feature {FeatureName} via role", userId, featureName);
            return true;
        }

        // Apply percentage-based rollout
        if (flag.Percentage < 100)
        {
            var isInPercentage = IsUserInPercentage(userId, featureName, flag.Percentage);
            _logger.LogDebug(
                "Feature {FeatureName} percentage check for user {UserId}: {Percentage}% -> {Result}",
                featureName, userId, flag.Percentage, isInPercentage);
            return isInPercentage;
        }

        return true;
    }

    /// <inheritdoc />
    public async Task<bool> IsEnabledForTenantAsync(
        string featureName,
        string tenantId,
        CancellationToken cancellationToken = default)
    {
        var flag = await GetFeatureFlagAsync(featureName, cancellationToken);
        if (flag == null)
        {
            _logger.LogDebug("Feature flag {FeatureName} not found for tenant {TenantId}", featureName, tenantId);
            return false;
        }

        // If globally disabled, check if tenant is in enabled tenants list
        if (!flag.Enabled)
        {
            var isExplicitlyEnabled = flag.EnabledTenants.Contains(tenantId, StringComparer.OrdinalIgnoreCase);
            _logger.LogDebug(
                "Feature {FeatureName} globally disabled, tenant {TenantId} explicitly enabled: {IsEnabled}",
                featureName, tenantId, isExplicitlyEnabled);
            return isExplicitlyEnabled;
        }

        // If enabled tenants list is empty, feature is enabled for all tenants
        if (!flag.EnabledTenants.Any())
        {
            return true;
        }

        // Check if tenant is in the enabled list
        var isEnabled = flag.EnabledTenants.Contains(tenantId, StringComparer.OrdinalIgnoreCase);
        _logger.LogDebug(
            "Feature {FeatureName} tenant check for {TenantId}: {Result}",
            featureName, tenantId, isEnabled);
        return isEnabled;
    }

    /// <inheritdoc />
    public async Task<T?> GetVariantAsync<T>(
        string featureName,
        string userId,
        CancellationToken cancellationToken = default)
    {
        var flag = await GetFeatureFlagAsync(featureName, cancellationToken);
        if (flag == null || !flag.Variants.Any())
        {
            _logger.LogDebug("No variants found for feature {FeatureName}", featureName);
            return default;
        }

        // Determine which variant the user gets based on consistent hashing
        var variantIndex = GetConsistentHashIndex(userId, featureName, flag.Variants.Count);
        var variantKey = flag.Variants.Keys.ElementAt(variantIndex);
        var variantValue = flag.Variants[variantKey];

        _logger.LogDebug(
            "User {UserId} assigned variant {VariantKey} for feature {FeatureName}",
            userId, variantKey, featureName);

        if (variantValue == null)
        {
            return default;
        }

        try
        {
            // Handle JsonElement conversion
            if (variantValue is JsonElement jsonElement)
            {
                return JsonSerializer.Deserialize<T>(jsonElement.GetRawText(), _jsonOptions);
            }

            // Direct cast if already the correct type
            if (variantValue is T typedValue)
            {
                return typedValue;
            }

            // Serialize and deserialize for type conversion
            var json = JsonSerializer.Serialize(variantValue, _jsonOptions);
            return JsonSerializer.Deserialize<T>(json, _jsonOptions);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to deserialize variant for feature {FeatureName}", featureName);
            return default;
        }
    }

    /// <inheritdoc />
    public async Task SetFeatureAsync(
        string featureName,
        bool enabled,
        CancellationToken cancellationToken = default)
    {
        var flag = await GetFeatureFlagAsync(featureName, cancellationToken) ?? new FeatureFlag
        {
            Name = featureName,
            CreatedAt = DateTime.UtcNow
        };

        flag.Enabled = enabled;
        flag.UpdatedAt = DateTime.UtcNow;

        await SaveFeatureFlagAsync(flag, cancellationToken);
        InvalidateLocalCache(featureName);

        _logger.LogInformation("Feature flag {FeatureName} set to {Enabled}", featureName, enabled);
    }

    /// <inheritdoc />
    public async Task SetFeaturePercentageAsync(
        string featureName,
        int percentage,
        CancellationToken cancellationToken = default)
    {
        if (percentage < 0 || percentage > 100)
        {
            throw new ArgumentOutOfRangeException(nameof(percentage), "Percentage must be between 0 and 100");
        }

        var flag = await GetFeatureFlagAsync(featureName, cancellationToken) ?? new FeatureFlag
        {
            Name = featureName,
            Enabled = true,
            CreatedAt = DateTime.UtcNow
        };

        flag.Percentage = percentage;
        flag.UpdatedAt = DateTime.UtcNow;

        await SaveFeatureFlagAsync(flag, cancellationToken);
        InvalidateLocalCache(featureName);

        _logger.LogInformation(
            "Feature flag {FeatureName} percentage set to {Percentage}%",
            featureName, percentage);
    }

    /// <summary>
    /// Sets the enabled tenants for a feature flag.
    /// </summary>
    public async Task SetFeatureTenantsAsync(
        string featureName,
        IEnumerable<string> tenantIds,
        CancellationToken cancellationToken = default)
    {
        var flag = await GetFeatureFlagAsync(featureName, cancellationToken) ?? new FeatureFlag
        {
            Name = featureName,
            Enabled = true,
            CreatedAt = DateTime.UtcNow
        };

        flag.EnabledTenants = tenantIds.ToList();
        flag.UpdatedAt = DateTime.UtcNow;

        await SaveFeatureFlagAsync(flag, cancellationToken);
        InvalidateLocalCache(featureName);

        _logger.LogInformation(
            "Feature flag {FeatureName} tenants set to {TenantCount} tenants",
            featureName, flag.EnabledTenants.Count);
    }

    /// <summary>
    /// Sets the enabled users for a feature flag.
    /// </summary>
    public async Task SetFeatureUsersAsync(
        string featureName,
        IEnumerable<string> userIds,
        CancellationToken cancellationToken = default)
    {
        var flag = await GetFeatureFlagAsync(featureName, cancellationToken) ?? new FeatureFlag
        {
            Name = featureName,
            Enabled = true,
            CreatedAt = DateTime.UtcNow
        };

        flag.EnabledUsers = userIds.ToList();
        flag.UpdatedAt = DateTime.UtcNow;

        await SaveFeatureFlagAsync(flag, cancellationToken);
        InvalidateLocalCache(featureName);

        _logger.LogInformation(
            "Feature flag {FeatureName} users set to {UserCount} users",
            featureName, flag.EnabledUsers.Count);
    }

    /// <summary>
    /// Sets the enabled roles for a feature flag.
    /// </summary>
    public async Task SetFeatureRolesAsync(
        string featureName,
        IEnumerable<string> roles,
        CancellationToken cancellationToken = default)
    {
        var flag = await GetFeatureFlagAsync(featureName, cancellationToken) ?? new FeatureFlag
        {
            Name = featureName,
            Enabled = true,
            CreatedAt = DateTime.UtcNow
        };

        flag.EnabledRoles = roles.ToList();
        flag.UpdatedAt = DateTime.UtcNow;

        await SaveFeatureFlagAsync(flag, cancellationToken);
        InvalidateLocalCache(featureName);

        _logger.LogInformation(
            "Feature flag {FeatureName} roles set to {RoleCount} roles",
            featureName, flag.EnabledRoles.Count);
    }

    /// <summary>
    /// Sets the variants for a feature flag (A/B testing).
    /// </summary>
    public async Task SetFeatureVariantsAsync(
        string featureName,
        Dictionary<string, object?> variants,
        CancellationToken cancellationToken = default)
    {
        var flag = await GetFeatureFlagAsync(featureName, cancellationToken) ?? new FeatureFlag
        {
            Name = featureName,
            Enabled = true,
            CreatedAt = DateTime.UtcNow
        };

        flag.Variants = variants;
        flag.UpdatedAt = DateTime.UtcNow;

        await SaveFeatureFlagAsync(flag, cancellationToken);
        InvalidateLocalCache(featureName);

        _logger.LogInformation(
            "Feature flag {FeatureName} variants set to {VariantCount} variants",
            featureName, flag.Variants.Count);
    }

    /// <summary>
    /// Deletes a feature flag.
    /// </summary>
    public async Task DeleteFeatureAsync(
        string featureName,
        CancellationToken cancellationToken = default)
    {
        var key = BuildFeatureKey(featureName);

        try
        {
            var db = _redis.GetDatabase();
            await db.KeyDeleteAsync(key);
            InvalidateLocalCache(featureName);

            _logger.LogInformation("Feature flag {FeatureName} deleted", featureName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to delete feature flag {FeatureName}", featureName);
            throw;
        }
    }

    /// <summary>
    /// Gets all feature flags.
    /// </summary>
    public async Task<IEnumerable<FeatureFlag>> GetAllFeaturesAsync(
        CancellationToken cancellationToken = default)
    {
        var flags = new List<FeatureFlag>();
        var pattern = $"{_options.KeyPrefix}:*";

        try
        {
            var endpoints = _redis.GetEndPoints();
            foreach (var endpoint in endpoints)
            {
                var server = _redis.GetServer(endpoint);
                var db = _redis.GetDatabase();

                await foreach (var key in server.KeysAsync(pattern: pattern))
                {
                    var value = await db.StringGetAsync(key);
                    if (!value.IsNullOrEmpty)
                    {
                        var flag = JsonSerializer.Deserialize<FeatureFlag>(value!, _jsonOptions);
                        if (flag != null)
                        {
                            flags.Add(flag);
                        }
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get all feature flags");
        }

        return flags;
    }

    /// <summary>
    /// Gets a feature flag from Redis with local caching.
    /// </summary>
    private async Task<FeatureFlag?> GetFeatureFlagAsync(
        string featureName,
        CancellationToken cancellationToken)
    {
        var cacheKey = $"ff:{featureName}";

        // Try local cache first
        if (_options.EnableLocalCache && _localCache.TryGetValue(cacheKey, out FeatureFlag? cachedFlag))
        {
            return cachedFlag;
        }

        // Get from Redis
        var key = BuildFeatureKey(featureName);

        try
        {
            var db = _redis.GetDatabase();
            var value = await db.StringGetAsync(key);

            if (value.IsNullOrEmpty)
            {
                return null;
            }

            var flag = JsonSerializer.Deserialize<FeatureFlag>(value!, _jsonOptions);

            // Cache locally
            if (_options.EnableLocalCache && flag != null)
            {
                var cacheOptions = new MemoryCacheEntryOptions
                {
                    AbsoluteExpirationRelativeToNow = TimeSpan.FromSeconds(_options.LocalCacheExpirationSeconds)
                };
                _localCache.Set(cacheKey, flag, cacheOptions);
            }

            return flag;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get feature flag {FeatureName} from Redis", featureName);
            return null;
        }
    }

    /// <summary>
    /// Saves a feature flag to Redis.
    /// </summary>
    private async Task SaveFeatureFlagAsync(
        FeatureFlag flag,
        CancellationToken cancellationToken)
    {
        var key = BuildFeatureKey(flag.Name);
        var value = JsonSerializer.Serialize(flag, _jsonOptions);

        try
        {
            var db = _redis.GetDatabase();
            await db.StringSetAsync(key, value);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save feature flag {FeatureName} to Redis", flag.Name);
            throw;
        }
    }

    /// <summary>
    /// Builds the Redis key for a feature flag.
    /// </summary>
    private string BuildFeatureKey(string featureName)
    {
        return $"{_options.KeyPrefix}:{featureName}";
    }

    /// <summary>
    /// Invalidates the local cache for a feature flag.
    /// </summary>
    private void InvalidateLocalCache(string featureName)
    {
        var cacheKey = $"ff:{featureName}";
        _localCache.Remove(cacheKey);
    }

    /// <summary>
    /// Determines if a user is within the percentage rollout using consistent hashing.
    /// </summary>
    private static bool IsUserInPercentage(string userId, string featureName, int percentage)
    {
        var hash = GetConsistentHash(userId, featureName);
        var bucket = hash % 100;
        return bucket < percentage;
    }

    /// <summary>
    /// Gets a consistent hash value for a user and feature combination.
    /// </summary>
    private static int GetConsistentHash(string userId, string featureName)
    {
        var input = $"{userId}:{featureName}";
        var bytes = Encoding.UTF8.GetBytes(input);
        var hash = SHA256.HashData(bytes);
        return Math.Abs(BitConverter.ToInt32(hash, 0));
    }

    /// <summary>
    /// Gets a consistent hash index for variant selection.
    /// </summary>
    private static int GetConsistentHashIndex(string userId, string featureName, int count)
    {
        if (count <= 0) return 0;
        var hash = GetConsistentHash(userId, featureName);
        return hash % count;
    }
}
