using Microsoft.Extensions.DependencyInjection;

namespace B2B.Infrastructure.Messaging.Resilience;

/// <summary>
/// Extension methods for registering resilience services.
/// </summary>
public static class ResilienceExtensions
{
    /// <summary>
    /// Adds exception categorization services to the service collection.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddExceptionCategorization(this IServiceCollection services)
    {
        services.AddSingleton<IExceptionCategorizer, ExceptionCategorizer>();
        return services;
    }

    /// <summary>
    /// Adds exception categorization services with custom configuration.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configure">Action to configure custom exception mappings.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddExceptionCategorization(
        this IServiceCollection services,
        Action<IExceptionCategorizer> configure)
    {
        services.AddSingleton<IExceptionCategorizer>(sp =>
        {
            var logger = sp.GetRequiredService<Microsoft.Extensions.Logging.ILogger<ExceptionCategorizer>>();
            var categorizer = new ExceptionCategorizer(logger);
            configure(categorizer);
            return categorizer;
        });
        return services;
    }
}
