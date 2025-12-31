using B2B.Application.Interfaces.Services;
using B2B.Infrastructure.Outbox;
using Microsoft.Extensions.DependencyInjection;

namespace B2B.Infrastructure.DependencyInjection;

/// <summary>
/// Extension methods for registering outbox services.
/// </summary>
public static class OutboxServiceExtensions
{
    /// <summary>
    /// Adds outbox pattern services to the service collection.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddOutboxServices(this IServiceCollection services)
    {
        services.AddScoped<IOutboxService, OutboxService>();

        return services;
    }
}
