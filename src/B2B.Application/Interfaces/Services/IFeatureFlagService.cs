namespace B2B.Application.Interfaces.Services;

/// <summary>
/// Service for managing feature flags and dynamic configuration.
/// </summary>
public interface IFeatureFlagService
{
    /// <summary>
    /// Checks if a feature is enabled globally.
    /// </summary>
    /// <param name="featureName">The name of the feature.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>True if the feature is enabled; otherwise, false.</returns>
    Task<bool> IsEnabledAsync(
        string featureName,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks if a feature is enabled for a specific user.
    /// Supports percentage-based rollouts and user segment targeting.
    /// </summary>
    /// <param name="featureName">The name of the feature.</param>
    /// <param name="userId">The user identifier.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>True if the feature is enabled for the user; otherwise, false.</returns>
    Task<bool> IsEnabledForUserAsync(
        string featureName,
        string userId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks if a feature is enabled for a specific tenant.
    /// </summary>
    /// <param name="featureName">The name of the feature.</param>
    /// <param name="tenantId">The tenant identifier.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>True if the feature is enabled for the tenant; otherwise, false.</returns>
    Task<bool> IsEnabledForTenantAsync(
        string featureName,
        string tenantId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets a feature flag variant for A/B testing.
    /// </summary>
    /// <typeparam name="T">The type of the variant value.</typeparam>
    /// <param name="featureName">The name of the feature.</param>
    /// <param name="userId">The user identifier.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The variant value, or default if not found.</returns>
    Task<T?> GetVariantAsync<T>(
        string featureName,
        string userId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Sets a feature flag state.
    /// </summary>
    /// <param name="featureName">The name of the feature.</param>
    /// <param name="enabled">Whether the feature should be enabled.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task SetFeatureAsync(
        string featureName,
        bool enabled,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Sets a feature flag with percentage-based rollout.
    /// </summary>
    /// <param name="featureName">The name of the feature.</param>
    /// <param name="percentage">The percentage of users to enable the feature for (0-100).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task SetFeaturePercentageAsync(
        string featureName,
        int percentage,
        CancellationToken cancellationToken = default);
}
