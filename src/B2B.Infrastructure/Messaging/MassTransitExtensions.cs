using B2B.Infrastructure.Messaging.Resilience;
using MassTransit;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace B2B.Infrastructure.Messaging;

/// <summary>
/// Extension methods for configuring MassTransit with RabbitMQ.
/// </summary>
public static class MassTransitExtensions
{
    /// <summary>
    /// Adds MassTransit with RabbitMQ transport to the service collection.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configuration">The configuration.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddMassTransitWithRabbitMq(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var options = new RabbitMqOptions();
        configuration.GetSection(RabbitMqOptions.SectionName).Bind(options);
        services.Configure<RabbitMqOptions>(configuration.GetSection(RabbitMqOptions.SectionName));

        // Register resilience services
        services.AddExceptionCategorization();
        services.AddSingleton<RetryLoggingObserver>();

        services.AddMassTransit(busConfig =>
        {
            // Add consumers from the Infrastructure assembly
            busConfig.AddConsumers(typeof(MassTransitExtensions).Assembly);

            // Configure RabbitMQ transport
            busConfig.UsingRabbitMq((context, cfg) =>
            {
                cfg.Host(options.Host, (ushort)options.Port, options.VirtualHost, h =>
                {
                    h.Username(options.Username);
                    h.Password(options.Password);
                });

                // Connect the retry logging observer for detailed exception logging
                var retryLoggingObserver = context.GetRequiredService<RetryLoggingObserver>();
                cfg.ConnectConsumeObserver(retryLoggingObserver);

                // Configure global retry policy with exponential backoff
                cfg.UseMessageRetry(retryConfig =>
                {
                    ConfigureRetryPolicy(retryConfig, options, context);
                });


                // Configure delayed redelivery for persistent failures
                cfg.UseDelayedRedelivery(r => r.Intervals(
                    TimeSpan.FromMinutes(5),
                    TimeSpan.FromMinutes(15),
                    TimeSpan.FromMinutes(30),
                    TimeSpan.FromHours(1)));

                // Configure prefetch count
                cfg.PrefetchCount = options.PrefetchCount;

                // Configure concurrent message limit
                cfg.ConcurrentMessageLimit = options.ConcurrentMessageLimit;

                // Configure dead-letter queue behavior
                cfg.ConfigureEndpoints(context);
            });
        });

        return services;
    }

    /// <summary>
    /// Configures the retry policy with exception categorization.
    /// </summary>
    private static void ConfigureRetryPolicy(
        IRetryConfigurator retryConfig,
        RabbitMqOptions options,
        IBusRegistrationContext context)
    {
        var exceptionCategorizer = context.GetRequiredService<IExceptionCategorizer>();
        var logger = context.GetRequiredService<ILogger<RetryLoggingObserver>>();

        retryConfig.Exponential(
            retryLimit: options.RetryCount,
            minInterval: TimeSpan.FromSeconds(options.RetryIntervalSeconds),
            maxInterval: TimeSpan.FromSeconds(options.MaxRetryIntervalSeconds),
            intervalDelta: TimeSpan.FromSeconds(2));

        // Handle transient exceptions (should retry)
        retryConfig.Handle<TimeoutException>();
        retryConfig.Handle<OperationCanceledException>();
        retryConfig.Handle<TaskCanceledException>();
        retryConfig.Handle<System.Net.Sockets.SocketException>();
        retryConfig.Handle<System.Net.Http.HttpRequestException>();
        retryConfig.Handle<IOException>();

        // Ignore non-retryable exceptions
        retryConfig.Ignore<B2B.Domain.Exceptions.ValidationException>();
        retryConfig.Ignore<B2B.Domain.Exceptions.UnauthorizedException>();
        retryConfig.Ignore<B2B.Domain.Exceptions.ForbiddenException>();
        retryConfig.Ignore<ArgumentException>();
        retryConfig.Ignore<ArgumentNullException>();
        retryConfig.Ignore<FormatException>();
    }

    /// <summary>
    /// Adds MassTransit with RabbitMQ transport and custom retry configuration.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configuration">The configuration.</param>
    /// <param name="configureRetry">Action to configure custom retry policies.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddMassTransitWithRabbitMq(
        this IServiceCollection services,
        IConfiguration configuration,
        Action<IRetryConfigurator> configureRetry)
    {
        var options = new RabbitMqOptions();
        configuration.GetSection(RabbitMqOptions.SectionName).Bind(options);
        services.Configure<RabbitMqOptions>(configuration.GetSection(RabbitMqOptions.SectionName));

        // Register resilience services
        services.AddExceptionCategorization();
        services.AddSingleton<RetryLoggingObserver>();

        services.AddMassTransit(busConfig =>
        {
            // Add consumers from the Infrastructure assembly
            busConfig.AddConsumers(typeof(MassTransitExtensions).Assembly);

            // Configure RabbitMQ transport
            busConfig.UsingRabbitMq((context, cfg) =>
            {
                cfg.Host(options.Host, (ushort)options.Port, options.VirtualHost, h =>
                {
                    h.Username(options.Username);
                    h.Password(options.Password);
                });

                // Connect the retry logging observer
                var retryLoggingObserver = context.GetRequiredService<RetryLoggingObserver>();
                cfg.ConnectConsumeObserver(retryLoggingObserver);

                // Configure custom retry policy
                cfg.UseMessageRetry(configureRetry);

                // Configure delayed redelivery for persistent failures
                cfg.UseDelayedRedelivery(r => r.Intervals(
                    TimeSpan.FromMinutes(5),
                    TimeSpan.FromMinutes(15),
                    TimeSpan.FromMinutes(30),
                    TimeSpan.FromHours(1)));

                // Configure prefetch count
                cfg.PrefetchCount = options.PrefetchCount;

                // Configure concurrent message limit
                cfg.ConcurrentMessageLimit = options.ConcurrentMessageLimit;

                // Configure dead-letter queue behavior
                cfg.ConfigureEndpoints(context);
            });
        });

        return services;
    }
}
