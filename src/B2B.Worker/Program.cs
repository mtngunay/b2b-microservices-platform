using B2B.Application.Interfaces.Services;
using B2B.Infrastructure.Caching;
using B2B.Infrastructure.DependencyInjection;
using B2B.Infrastructure.Identity;
using B2B.Infrastructure.Messaging;
using B2B.Infrastructure.Outbox;
using B2B.Infrastructure.Persistence.ReadDb;
using B2B.Infrastructure.Persistence.WriteDb;
using B2B.Worker.Configuration;
using B2B.Worker.Extensions;
using B2B.Worker.Jobs;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Serilog;
using Serilog.Events;
using StackExchange.Redis;

// Configure Serilog early for startup logging
Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Debug()
    .MinimumLevel.Override("Microsoft", LogEventLevel.Information)
    .MinimumLevel.Override("Microsoft.EntityFrameworkCore", LogEventLevel.Warning)
    .Enrich.FromLogContext()
    .Enrich.WithMachineName()
    .Enrich.WithEnvironmentName()
    .WriteTo.Console(outputTemplate: 
        "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj} {Properties:j}{NewLine}{Exception}")
    .CreateBootstrapLogger();

try
{
    Log.Information("Starting B2B Worker Service");

    var builder = WebApplication.CreateBuilder(args);

    // Configure Serilog from configuration
    builder.Host.UseSerilog((context, services, configuration) => configuration
        .ReadFrom.Configuration(context.Configuration)
        .ReadFrom.Services(services)
        .Enrich.FromLogContext()
        .Enrich.WithMachineName()
        .Enrich.WithEnvironmentName()
        .Enrich.WithProperty("Application", "B2B.Worker"));

    // Add configuration
    builder.Services.Configure<HangfireOptions>(
        builder.Configuration.GetSection(HangfireOptions.SectionName));
    builder.Services.Configure<JobRetryOptions>(
        builder.Configuration.GetSection(JobRetryOptions.SectionName));
    builder.Services.Configure<JwtOptions>(
        builder.Configuration.GetSection("Jwt"));

    // Add EF Core DbContext
    var writeDbConnectionString = builder.Configuration.GetConnectionString("WriteDb")
        ?? throw new InvalidOperationException("WriteDb connection string is required");

    builder.Services.AddDbContext<WriteDbContext>(options =>
        options.UseSqlServer(writeDbConnectionString, sqlOptions =>
        {
            sqlOptions.EnableRetryOnFailure(
                maxRetryCount: 5,
                maxRetryDelay: TimeSpan.FromSeconds(30),
                errorNumbersToAdd: null);
        }));

    // Add Redis
    var redisConnectionString = builder.Configuration.GetConnectionString("Redis")
        ?? "localhost:6379";

    builder.Services.AddSingleton<IConnectionMultiplexer>(sp =>
    {
        var configuration = ConfigurationOptions.Parse(redisConnectionString);
        configuration.AbortOnConnectFail = false;
        configuration.ConnectRetry = 5;
        configuration.ConnectTimeout = 5000;
        return ConnectionMultiplexer.Connect(configuration);
    });

    // Add application services
    builder.Services.AddScoped<ICurrentUserService, WorkerCurrentUserService>();
    builder.Services.AddScoped<ICorrelationIdAccessor, WorkerCorrelationIdAccessor>();
    builder.Services.AddScoped<ICacheService, RedisCacheService>();
    builder.Services.AddScoped<IOutboxService, OutboxService>();
    builder.Services.AddScoped<IPermissionService, PermissionService>();

    // Add MongoDB for read models
    builder.Services.Configure<MongoDbSettings>(builder.Configuration.GetSection("MongoDB"));
    builder.Services.AddSingleton<MongoDbContext>(sp =>
    {
        var settings = sp.GetRequiredService<IOptions<MongoDbSettings>>();
        return new MongoDbContext(settings);
    });
    builder.Services.AddScoped<UserReadRepository>();

    // Add MassTransit for message publishing
    builder.Services.AddMessagingServices(builder.Configuration);

    // Add Hangfire services
    builder.Services.AddHangfireServices(builder.Configuration);

    // Add health checks
    builder.Services.AddHealthChecks()
        .AddSqlServer(writeDbConnectionString, name: "sqlserver")
        .AddRedis(redisConnectionString, name: "redis");

    var app = builder.Build();

    // Configure middleware pipeline
    app.UseSerilogRequestLogging();

    // Health check endpoints
    app.MapHealthChecks("/health/live", new Microsoft.AspNetCore.Diagnostics.HealthChecks.HealthCheckOptions
    {
        Predicate = _ => false // Just check if app is running
    });

    app.MapHealthChecks("/health/ready", new Microsoft.AspNetCore.Diagnostics.HealthChecks.HealthCheckOptions
    {
        Predicate = check => check.Tags.Contains("ready") || true
    });

    // Configure Hangfire dashboard with authentication
    app.UseHangfireDashboardWithAuth(builder.Configuration);

    // Register recurring jobs
    JobRegistration.RegisterAllRecurringJobs(
        app.Services.GetRequiredService<ILogger<Program>>());

    Log.Information("B2B Worker Service started successfully");

    await app.RunAsync();
}
catch (Exception ex)
{
    Log.Fatal(ex, "B2B Worker Service terminated unexpectedly");
    throw;
}
finally
{
    Log.CloseAndFlush();
}

/// <summary>
/// Worker-specific implementation of ICurrentUserService.
/// In the worker context, there's no HTTP request, so we use system defaults.
/// </summary>
internal class WorkerCurrentUserService : ICurrentUserService
{
    public string? UserId => "system";
    public string? TenantId => null; // Worker operates across all tenants
    public string? Email => "worker@system.local";
    public IEnumerable<string> Roles => new[] { "System" };
    public bool IsAuthenticated => true;
}

/// <summary>
/// Worker-specific implementation of ICorrelationIdAccessor.
/// Generates a new correlation ID for each job execution.
/// </summary>
internal class WorkerCorrelationIdAccessor : ICorrelationIdAccessor
{
    private readonly AsyncLocal<string> _correlationId = new();

    public string CorrelationId => _correlationId.Value ?? Guid.NewGuid().ToString();

    public void SetCorrelationId(string correlationId)
    {
        _correlationId.Value = correlationId;
    }
}
