using System.Text;
using B2B.API.Middleware;
using B2B.Application.Behaviors;
using B2B.Application.Interfaces.Persistence;
using B2B.Application.Interfaces.Services;
using B2B.Infrastructure.Caching;
using B2B.Infrastructure.Identity;
using B2B.Infrastructure.Messaging;
using B2B.Infrastructure.Outbox;
using B2B.Infrastructure.Persistence.ReadDb;
using B2B.Infrastructure.Persistence.WriteDb;
using FluentValidation;
using MediatR;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.ResponseCompression;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using MongoDB.Driver;
using StackExchange.Redis;

namespace B2B.API.Extensions;

/// <summary>
/// Extension methods for configuring services in the DI container.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Adds all application services to the DI container.
    /// </summary>
    public static IServiceCollection AddApplicationServices(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // Add MediatR with pipeline behaviors
        services.AddMediatR(cfg =>
        {
            cfg.RegisterServicesFromAssembly(typeof(LoggingBehavior<,>).Assembly);
        });

        // Add pipeline behaviors in order
        services.AddTransient(typeof(IPipelineBehavior<,>), typeof(LoggingBehavior<,>));
        services.AddTransient(typeof(IPipelineBehavior<,>), typeof(ValidationBehavior<,>));
        services.AddTransient(typeof(IPipelineBehavior<,>), typeof(AuthorizationBehavior<,>));
        services.AddTransient(typeof(IPipelineBehavior<,>), typeof(TransactionBehavior<,>));
        services.AddTransient(typeof(IPipelineBehavior<,>), typeof(RetryBehavior<,>));

        // Add FluentValidation validators
        services.AddValidatorsFromAssembly(typeof(ValidationBehavior<,>).Assembly);

        // Add AutoMapper
        services.AddAutoMapper(typeof(B2B.Application.Mappings.UserMappingProfile).Assembly);

        return services;
    }

    /// <summary>
    /// Adds infrastructure services to the DI container.
    /// </summary>
    public static IServiceCollection AddInfrastructureServices(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // Add Write Database (MSSQL with EF Core)
        services.AddDbContext<WriteDbContext>(options =>
            options.UseSqlServer(
                configuration.GetConnectionString("WriteDb"),
                sqlOptions =>
                {
                    sqlOptions.EnableRetryOnFailure(
                        maxRetryCount: 3,
                        maxRetryDelay: TimeSpan.FromSeconds(30),
                        errorNumbersToAdd: null);
                }));

        // Add Read Database (MongoDB)
        services.Configure<MongoDbSettings>(options =>
        {
            var connectionString = configuration.GetConnectionString("ReadDb") ?? "mongodb://localhost:27017/B2B";
            var mongoUrl = new MongoUrl(connectionString);
            options.ConnectionString = connectionString;
            options.DatabaseName = mongoUrl.DatabaseName ?? "B2B";
        });
        
        services.AddSingleton<IMongoClient>(sp =>
        {
            var connectionString = configuration.GetConnectionString("ReadDb") ?? "mongodb://localhost:27017/B2B";
            return new MongoClient(connectionString);
        });
        services.AddScoped<MongoDbContext>();

        // Add Redis
        services.AddSingleton<IConnectionMultiplexer>(sp =>
        {
            var connectionString = configuration.GetConnectionString("Redis") ?? "localhost:6379";
            var configOptions = ConfigurationOptions.Parse(connectionString);
            configOptions.AbortOnConnectFail = false;
            configOptions.ConnectRetry = 3;
            configOptions.ConnectTimeout = 5000;
            return ConnectionMultiplexer.Connect(configOptions);
        });

        // Add repositories
        services.AddScoped(typeof(IWriteRepository<,>), typeof(WriteRepository<,>));
        services.AddScoped(typeof(IReadRepository<,>), typeof(ReadRepository<,>));
        services.AddScoped<IUnitOfWork, UnitOfWork>();
        
        // Register IApplicationDbContext (same instance as WriteDbContext)
        services.AddScoped<IApplicationDbContext>(sp => sp.GetRequiredService<WriteDbContext>());

        // Add HttpContextAccessor (required for CurrentUserService)
        services.AddHttpContextAccessor();

        // Add application services
        services.AddScoped<ICacheService, RedisCacheService>();
        services.AddScoped<ICurrentUserService, CurrentUserService>();
        services.AddScoped<IPermissionService, PermissionService>();
        services.AddScoped<IOutboxService, OutboxService>();
        
        // Add Feature Flag Service
        services.AddMemoryCache();
        services.Configure<B2B.Infrastructure.FeatureFlags.FeatureFlagOptions>(
            configuration.GetSection(B2B.Infrastructure.FeatureFlags.FeatureFlagOptions.SectionName));
        services.AddScoped<IFeatureFlagService, B2B.Infrastructure.FeatureFlags.FeatureFlagService>();

        // Add JWT Token Service
        services.Configure<JwtOptions>(configuration.GetSection("Jwt"));
        services.AddScoped<ITokenService, JwtTokenService>();

        return services;
    }

    /// <summary>
    /// Adds JWT authentication to the DI container.
    /// </summary>
    public static IServiceCollection AddJwtAuthentication(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var jwtSection = configuration.GetSection("Jwt");
        // Try SecretKey first (used by JwtTokenService), then fall back to Secret
        var secretKey = jwtSection["SecretKey"] ?? jwtSection["Secret"] ?? throw new InvalidOperationException("JWT Secret is not configured");
        var issuer = jwtSection["Issuer"] ?? "B2B-Platform";
        var audience = jwtSection["Audience"] ?? "B2B-API";

        services.AddAuthentication(options =>
        {
            options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
            options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
        })
        .AddJwtBearer(options =>
        {
            options.TokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidateAudience = true,
                ValidateLifetime = true,
                ValidateIssuerSigningKey = true,
                ValidIssuer = issuer,
                ValidAudience = audience,
                IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secretKey)),
                ClockSkew = TimeSpan.FromMinutes(5) // Allow 5 minutes clock skew
            };

            options.Events = new JwtBearerEvents
            {
                // Handle token from header with or without "Bearer " prefix
                OnMessageReceived = context =>
                {
                    var authHeader = context.Request.Headers.Authorization.ToString();
                    if (!string.IsNullOrEmpty(authHeader))
                    {
                        // Remove "Bearer " prefix if present, otherwise use as-is
                        context.Token = authHeader.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase)
                            ? authHeader["Bearer ".Length..].Trim()
                            : authHeader.Trim();
                    }
                    return Task.CompletedTask;
                },
                OnAuthenticationFailed = context =>
                {
                    var logger = context.HttpContext.RequestServices
                        .GetRequiredService<ILogger<JwtBearerEvents>>();
                    logger.LogWarning(context.Exception, "JWT authentication failed");
                    return Task.CompletedTask;
                },
                OnTokenValidated = context =>
                {
                    // Skip Redis validation for now - JWT signature validation is sufficient
                    // In production, you may want to enable Redis validation for token revocation
                    var logger = context.HttpContext.RequestServices
                        .GetRequiredService<ILogger<JwtBearerEvents>>();
                    logger.LogDebug("JWT token validated successfully for user {Email}", 
                        context.Principal?.FindFirst("email")?.Value);
                    return Task.CompletedTask;
                }
            };
        });

        services.AddAuthorization();

        return services;
    }

    /// <summary>
    /// Adds response compression to the DI container.
    /// </summary>
    public static IServiceCollection AddCustomResponseCompression(this IServiceCollection services)
    {
        services.AddResponseCompression(options =>
        {
            options.EnableForHttps = true;
            options.Providers.Add<BrotliCompressionProvider>();
            options.Providers.Add<GzipCompressionProvider>();
            options.MimeTypes = ResponseCompressionDefaults.MimeTypes.Concat(new[]
            {
                "application/json",
                "text/json",
                "application/xml",
                "text/xml"
            });
        });

        services.Configure<BrotliCompressionProviderOptions>(options =>
        {
            options.Level = System.IO.Compression.CompressionLevel.Fastest;
        });

        services.Configure<GzipCompressionProviderOptions>(options =>
        {
            options.Level = System.IO.Compression.CompressionLevel.Fastest;
        });

        return services;
    }

    /// <summary>
    /// Adds health checks to the DI container.
    /// </summary>
    public static IServiceCollection AddCustomHealthChecks(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var writeDbConnectionString = configuration.GetConnectionString("WriteDb") ?? "";
        var readDbConnectionString = configuration.GetConnectionString("ReadDb") ?? "";
        var redisConnectionString = configuration.GetConnectionString("Redis") ?? "localhost:6379";

        services.AddHealthChecks()
            .AddSqlServer(
                connectionString: writeDbConnectionString,
                name: "mssql",
                tags: new[] { "db", "sql", "ready" })
            .AddMongoDb(
                sp => new MongoClient(readDbConnectionString),
                name: "mongodb",
                tags: new[] { "db", "nosql", "ready" })
            .AddRedis(
                redisConnectionString: redisConnectionString,
                name: "redis",
                tags: new[] { "cache", "ready" });

        return services;
    }
}
