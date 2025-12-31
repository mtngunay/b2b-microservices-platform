namespace B2B.Infrastructure.FeatureFlags;

/// <summary>
/// Represents a feature flag configuration stored in Redis.
/// </summary>
public class FeatureFlag
{
    /// <summary>
    /// Gets or sets the feature name.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets whether the feature is globally enabled.
    /// </summary>
    public bool Enabled { get; set; }

    /// <summary>
    /// Gets or sets the percentage of users to enable the feature for (0-100).
    /// Used for percentage-based rollouts.
    /// </summary>
    public int Percentage { get; set; } = 100;

    /// <summary>
    /// Gets or sets the list of tenant IDs that have this feature enabled.
    /// Empty list means all tenants (if Enabled is true).
    /// </summary>
    public List<string> EnabledTenants { get; set; } = new();

    /// <summary>
    /// Gets or sets the list of user IDs that have this feature enabled.
    /// These users always get the feature regardless of percentage.
    /// </summary>
    public List<string> EnabledUsers { get; set; } = new();

    /// <summary>
    /// Gets or sets the list of role names that have this feature enabled.
    /// Users with these roles always get the feature.
    /// </summary>
    public List<string> EnabledRoles { get; set; } = new();

    /// <summary>
    /// Gets or sets the variants for A/B testing.
    /// Key is the variant name, value is the variant configuration.
    /// </summary>
    public Dictionary<string, object?> Variants { get; set; } = new();

    /// <summary>
    /// Gets or sets the description of the feature flag.
    /// </summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the timestamp when the feature flag was created.
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Gets or sets the timestamp when the feature flag was last updated.
    /// </summary>
    public DateTime? UpdatedAt { get; set; }
}
