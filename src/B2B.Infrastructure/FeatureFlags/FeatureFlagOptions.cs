namespace B2B.Infrastructure.FeatureFlags;

/// <summary>
/// Configuration options for the feature flag service.
/// </summary>
public class FeatureFlagOptions
{
    /// <summary>
    /// Configuration section name.
    /// </summary>
    public const string SectionName = "FeatureFlags";

    /// <summary>
    /// Gets or sets the Redis key prefix for feature flags.
    /// </summary>
    public string KeyPrefix { get; set; } = "feature";

    /// <summary>
    /// Gets or sets the default cache expiration in minutes.
    /// </summary>
    public int CacheExpirationMinutes { get; set; } = 5;

    /// <summary>
    /// Gets or sets whether to enable local caching of feature flags.
    /// </summary>
    public bool EnableLocalCache { get; set; } = true;

    /// <summary>
    /// Gets or sets the local cache expiration in seconds.
    /// </summary>
    public int LocalCacheExpirationSeconds { get; set; } = 30;
}
