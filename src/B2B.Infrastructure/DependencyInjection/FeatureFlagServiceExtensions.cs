using B2B.Application.Interfaces.Services;
using B2B.Infrastructure.FeatureFlags;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace B2B.Infrastructure.DependencyInjection;

/// <summary>
/// Extension methods for registering feature flag services.
/// </summary>
public static class FeatureFlagServiceExtensions
{
    /// <summary>
    /// Adds feature flag services to the service collection.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configuration">The configuration.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddFeatureFlagServices(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // Configure options
        services.Configure<FeatureFlagOptions>(
            configuration.GetSection(FeatureFlagOptions.SectionName));

        // Add memory cache for local caching
        services.AddMemoryCache();

        // Register the feature flag service
        services.AddScoped<IFeatureFlagService, FeatureFlagService>();

        return services;
    }
}
