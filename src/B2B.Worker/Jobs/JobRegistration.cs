using B2B.Infrastructure.Outbox;
using Microsoft.Extensions.Logging;

namespace B2B.Worker.Jobs;

/// <summary>
/// Helper class for registering all recurring Hangfire jobs.
/// </summary>
public static class JobRegistration
{
    /// <summary>
    /// Registers all recurring jobs with Hangfire.
    /// </summary>
    public static void RegisterAllRecurringJobs(ILogger logger)
    {
        logger.LogInformation("Registering recurring Hangfire jobs...");

        try
        {
            // Register Outbox Processor Job (every 5 seconds)
            OutboxProcessorJob.RegisterRecurringJob();
            logger.LogInformation("Registered OutboxProcessorJob (every 5 seconds)");

            // Register Permission Cache Refresh Job (every 10 minutes)
            PermissionCacheRefreshJob.RegisterRecurringJob();
            logger.LogInformation("Registered PermissionCacheRefreshJob (every 10 minutes)");

            // Register Cleanup Expired Tokens Job (every hour)
            CleanupExpiredTokensJob.RegisterRecurringJob();
            logger.LogInformation("Registered CleanupExpiredTokensJob (every hour)");

            logger.LogInformation("All recurring jobs registered successfully");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to register recurring jobs");
            throw;
        }
    }
}
