using B2B.Worker.Configuration;
using B2B.Worker.Filters;
using Hangfire;
using Hangfire.SqlServer;
using Microsoft.Extensions.Options;

namespace B2B.Worker.Extensions;

/// <summary>
/// Extension methods for configuring Hangfire.
/// </summary>
public static class HangfireExtensions
{
    /// <summary>
    /// Adds Hangfire services to the service collection.
    /// </summary>
    public static IServiceCollection AddHangfireServices(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // Bind configuration options
        services.Configure<HangfireOptions>(configuration.GetSection(HangfireOptions.SectionName));
        services.Configure<JobRetryOptions>(configuration.GetSection(JobRetryOptions.SectionName));

        var connectionString = configuration.GetConnectionString("WriteDb")
            ?? throw new InvalidOperationException("WriteDb connection string is required");

        var hangfireOptions = configuration
            .GetSection(HangfireOptions.SectionName)
            .Get<HangfireOptions>() ?? new HangfireOptions();

        var jobRetryOptions = configuration
            .GetSection(JobRetryOptions.SectionName)
            .Get<JobRetryOptions>() ?? new JobRetryOptions();

        // Configure Hangfire with SQL Server storage
        services.AddHangfire((serviceProvider, config) =>
        {
            config
                .SetDataCompatibilityLevel(CompatibilityLevel.Version_180)
                .UseSimpleAssemblyNameTypeSerializer()
                .UseRecommendedSerializerSettings()
                .UseSqlServerStorage(connectionString, new SqlServerStorageOptions
                {
                    SchemaName = hangfireOptions.SchemaName,
                    CommandBatchMaxTimeout = TimeSpan.FromMinutes(5),
                    SlidingInvisibilityTimeout = TimeSpan.FromMinutes(5),
                    QueuePollInterval = TimeSpan.Zero,
                    UseRecommendedIsolationLevel = true,
                    DisableGlobalLocks = true,
                    PrepareSchemaIfNecessary = true,
                    EnableHeavyMigrations = true
                });

            // Configure global job filters
            config.UseFilter(new AutomaticRetryWithExponentialBackoffAttribute(
                jobRetryOptions.MaxRetryAttempts,
                jobRetryOptions.DelaysInSeconds));

            // Add failure notification filter
            var logger = serviceProvider.GetRequiredService<ILogger<JobFailureNotificationFilter>>();
            var options = serviceProvider.GetRequiredService<IOptions<JobRetryOptions>>();
            config.UseFilter(new JobFailureNotificationFilter(logger, options));
        });

        // Configure Hangfire server
        services.AddHangfireServer(options =>
        {
            options.WorkerCount = hangfireOptions.WorkerCount;
            options.Queues = hangfireOptions.Queues;
            options.ServerTimeout = TimeSpan.FromMinutes(hangfireOptions.ServerTimeoutMinutes);
            options.ServerCheckInterval = TimeSpan.FromSeconds(hangfireOptions.ServerCheckIntervalSeconds);
            options.SchedulePollingInterval = TimeSpan.FromSeconds(15);
            options.HeartbeatInterval = TimeSpan.FromSeconds(30);
        });

        return services;
    }

    /// <summary>
    /// Configures Hangfire dashboard middleware.
    /// </summary>
    public static IApplicationBuilder UseHangfireDashboardWithAuth(
        this IApplicationBuilder app,
        IConfiguration configuration)
    {
        var hangfireOptions = configuration
            .GetSection(HangfireOptions.SectionName)
            .Get<HangfireOptions>() ?? new HangfireOptions();

        if (!hangfireOptions.EnableDashboard)
        {
            return app;
        }

        var dashboardOptions = new DashboardOptions
        {
            Authorization = new[]
            {
                new HangfireDashboardAuthorizationFilter(
                    hangfireOptions.DashboardUsername,
                    hangfireOptions.DashboardPassword)
            },
            DashboardTitle = "B2B Worker Dashboard",
            DisplayStorageConnectionString = false,
            IsReadOnlyFunc = _ => false
        };

        app.UseHangfireDashboard(hangfireOptions.DashboardPath, dashboardOptions);

        return app;
    }
}
