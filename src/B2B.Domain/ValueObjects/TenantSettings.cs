namespace B2B.Domain.ValueObjects;

/// <summary>
/// Value object representing tenant-specific configuration settings.
/// </summary>
public class TenantSettings
{
    /// <summary>
    /// Gets the maximum number of users allowed for this tenant.
    /// </summary>
    public int MaxUsers { get; private set; }

    /// <summary>
    /// Gets the maximum storage quota in megabytes.
    /// </summary>
    public long MaxStorageMb { get; private set; }

    /// <summary>
    /// Gets a value indicating whether the tenant has API access enabled.
    /// </summary>
    public bool ApiAccessEnabled { get; private set; }

    /// <summary>
    /// Gets the rate limit (requests per minute) for this tenant.
    /// </summary>
    public int RateLimitPerMinute { get; private set; }

    /// <summary>
    /// Gets the subscription tier for this tenant.
    /// </summary>
    public string SubscriptionTier { get; private set; } = string.Empty;

    /// <summary>
    /// Gets the custom branding settings as JSON.
    /// </summary>
    public string? CustomBrandingJson { get; private set; }

    /// <summary>
    /// Gets the allowed features for this tenant.
    /// </summary>
    public List<string> AllowedFeatures { get; private set; } = new();

    /// <summary>
    /// Private constructor for EF Core.
    /// </summary>
    private TenantSettings() { }

    /// <summary>
    /// Creates a new TenantSettings instance with default values.
    /// </summary>
    /// <returns>A new TenantSettings instance with default values.</returns>
    public static TenantSettings CreateDefault()
    {
        return new TenantSettings
        {
            MaxUsers = 10,
            MaxStorageMb = 1024,
            ApiAccessEnabled = true,
            RateLimitPerMinute = 100,
            SubscriptionTier = "Free",
            AllowedFeatures = new List<string> { "basic" }
        };
    }

    /// <summary>
    /// Creates a new TenantSettings instance with custom values.
    /// </summary>
    /// <param name="maxUsers">Maximum number of users.</param>
    /// <param name="maxStorageMb">Maximum storage in megabytes.</param>
    /// <param name="apiAccessEnabled">Whether API access is enabled.</param>
    /// <param name="rateLimitPerMinute">Rate limit per minute.</param>
    /// <param name="subscriptionTier">Subscription tier name.</param>
    /// <param name="allowedFeatures">List of allowed features.</param>
    /// <returns>A new TenantSettings instance.</returns>
    public static TenantSettings Create(
        int maxUsers,
        long maxStorageMb,
        bool apiAccessEnabled,
        int rateLimitPerMinute,
        string subscriptionTier,
        List<string>? allowedFeatures = null)
    {
        return new TenantSettings
        {
            MaxUsers = maxUsers,
            MaxStorageMb = maxStorageMb,
            ApiAccessEnabled = apiAccessEnabled,
            RateLimitPerMinute = rateLimitPerMinute,
            SubscriptionTier = subscriptionTier,
            AllowedFeatures = allowedFeatures ?? new List<string>()
        };
    }

    /// <summary>
    /// Updates the subscription tier and related settings.
    /// </summary>
    /// <param name="tier">The new subscription tier.</param>
    /// <param name="maxUsers">The new maximum users limit.</param>
    /// <param name="maxStorageMb">The new storage limit.</param>
    /// <param name="rateLimitPerMinute">The new rate limit.</param>
    public void UpdateSubscription(string tier, int maxUsers, long maxStorageMb, int rateLimitPerMinute)
    {
        SubscriptionTier = tier;
        MaxUsers = maxUsers;
        MaxStorageMb = maxStorageMb;
        RateLimitPerMinute = rateLimitPerMinute;
    }

    /// <summary>
    /// Enables or disables API access.
    /// </summary>
    /// <param name="enabled">Whether API access should be enabled.</param>
    public void SetApiAccess(bool enabled)
    {
        ApiAccessEnabled = enabled;
    }

    /// <summary>
    /// Sets the custom branding configuration.
    /// </summary>
    /// <param name="brandingJson">The branding configuration as JSON.</param>
    public void SetCustomBranding(string? brandingJson)
    {
        CustomBrandingJson = brandingJson;
    }

    /// <summary>
    /// Adds a feature to the allowed features list.
    /// </summary>
    /// <param name="feature">The feature to add.</param>
    public void AddFeature(string feature)
    {
        if (!AllowedFeatures.Contains(feature))
        {
            AllowedFeatures.Add(feature);
        }
    }

    /// <summary>
    /// Removes a feature from the allowed features list.
    /// </summary>
    /// <param name="feature">The feature to remove.</param>
    public void RemoveFeature(string feature)
    {
        AllowedFeatures.Remove(feature);
    }

    /// <summary>
    /// Checks if a feature is allowed for this tenant.
    /// </summary>
    /// <param name="feature">The feature to check.</param>
    /// <returns>True if the feature is allowed; otherwise, false.</returns>
    public bool HasFeature(string feature)
    {
        return AllowedFeatures.Contains(feature);
    }
}
