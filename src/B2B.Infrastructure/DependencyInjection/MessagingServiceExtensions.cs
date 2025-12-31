using B2B.Infrastructure.Messaging;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace B2B.Infrastructure.DependencyInjection;

/// <summary>
/// Extension methods for registering messaging services.
/// </summary>
public static class MessagingServiceExtensions
{
    /// <summary>
    /// Adds messaging services to the service collection.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configuration">The configuration.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddMessagingServices(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // Add MassTransit with RabbitMQ
        services.AddMassTransitWithRabbitMq(configuration);

        // Register message publisher
        services.AddScoped<IMessagePublisher, MassTransitMessagePublisher>();

        return services;
    }
}
