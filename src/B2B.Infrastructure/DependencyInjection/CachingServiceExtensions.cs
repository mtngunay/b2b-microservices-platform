using B2B.Application.Interfaces.Services;
using B2B.Infrastructure.Caching;
using B2B.Infrastructure.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using StackExchange.Redis;

namespace B2B.Infrastructure.DependencyInjection;

/// <summary>
/// Extension methods for registering caching and identity services.
/// </summary>
public static class CachingServiceExtensions
{
    /// <summary>
    /// Adds Redis caching services to the service collection.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configuration">The configuration.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddRedisCaching(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // Configure Redis options
        services.Configure<RedisCacheOptions>(
            configuration.GetSection(RedisCacheOptions.SectionName));

        var redisOptions = configuration
            .GetSection(RedisCacheOptions.SectionName)
            .Get<RedisCacheOptions>() ?? new RedisCacheOptions();

        // Configure Redis connection
        var configurationOptions = ConfigurationOptions.Parse(redisOptions.ConnectionString);
        configurationOptions.AbortOnConnectFail = redisOptions.AbortOnConnectFail;
        configurationOptions.ConnectTimeout = redisOptions.ConnectTimeoutMs;
        configurationOptions.SyncTimeout = redisOptions.SyncTimeoutMs;
        configurationOptions.Ssl = redisOptions.Ssl;

        if (!string.IsNullOrEmpty(redisOptions.Password))
        {
            configurationOptions.Password = redisOptions.Password;
        }

        // Register Redis connection multiplexer as singleton
        services.AddSingleton<IConnectionMultiplexer>(sp =>
            ConnectionMultiplexer.Connect(configurationOptions));

        // Register cache service
        services.AddScoped<ICacheService, RedisCacheService>();

        return services;
    }

    /// <summary>
    /// Adds JWT authentication services to the service collection.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configuration">The configuration.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddJwtAuthentication(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // Configure JWT options
        services.Configure<JwtOptions>(
            configuration.GetSection(JwtOptions.SectionName));

        // Register token service
        services.AddScoped<ITokenService, JwtTokenService>();

        return services;
    }

    /// <summary>
    /// Adds permission services to the service collection.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddPermissionServices(this IServiceCollection services)
    {
        services.AddScoped<IPermissionService, PermissionService>();
        return services;
    }

    /// <summary>
    /// Adds current user service to the service collection.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddCurrentUserService(this IServiceCollection services)
    {
        services.AddHttpContextAccessor();
        services.AddScoped<ICurrentUserService, CurrentUserService>();
        return services;
    }

    /// <summary>
    /// Adds all caching and identity services to the service collection.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configuration">The configuration.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddCachingAndIdentityServices(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddCurrentUserService();
        services.AddRedisCaching(configuration);
        services.AddJwtAuthentication(configuration);
        services.AddPermissionServices();

        return services;
    }
}
