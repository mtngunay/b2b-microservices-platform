using B2B.Application.Interfaces.Services;
using B2B.Infrastructure.Persistence.WriteDb;
using Hangfire;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace B2B.Worker.Jobs;

/// <summary>
/// Background job that refreshes permission caches for active users.
/// This ensures permission caches stay warm and reduces cache misses.
/// </summary>
public class PermissionCacheRefreshJob
{
    private readonly WriteDbContext _dbContext;
    private readonly IPermissionService _permissionService;
    private readonly ILogger<PermissionCacheRefreshJob> _logger;

    private const int BatchSize = 100;

    /// <summary>
    /// Initializes a new instance of PermissionCacheRefreshJob.
    /// </summary>
    public PermissionCacheRefreshJob(
        WriteDbContext dbContext,
        IPermissionService permissionService,
        ILogger<PermissionCacheRefreshJob> logger)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        _permissionService = permissionService ?? throw new ArgumentNullException(nameof(permissionService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Refreshes permission caches for active users.
    /// This method is called by Hangfire on a recurring schedule.
    /// </summary>
    [AutomaticRetry(Attempts = 3, DelaysInSeconds = new[] { 30, 60, 120 })]
    [Queue("low")]
    public async Task RefreshAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Starting permission cache refresh job");

        try
        {
            var processedCount = 0;
            var errorCount = 0;

            // Get all active users with their tenant IDs
            var activeUsers = await _dbContext.Users
                .AsNoTracking()
                .Where(u => u.IsActive && !u.IsDeleted)
                .Select(u => new { u.Id, u.TenantId })
                .ToListAsync(cancellationToken);

            _logger.LogInformation(
                "Found {Count} active users for permission cache refresh",
                activeUsers.Count);

            // Process users in batches
            foreach (var batch in activeUsers.Chunk(BatchSize))
            {
                foreach (var user in batch)
                {
                    try
                    {
                        // This will refresh the cache if expired or populate if missing
                        await _permissionService.GetUserPermissionsAsync(
                            user.Id.ToString(),
                            user.TenantId,
                            cancellationToken);

                        processedCount++;
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(
                            ex,
                            "Failed to refresh permission cache for user {UserId} in tenant {TenantId}",
                            user.Id,
                            user.TenantId);
                        errorCount++;
                    }
                }

                // Small delay between batches to avoid overwhelming Redis
                await Task.Delay(100, cancellationToken);
            }

            _logger.LogInformation(
                "Permission cache refresh completed. Processed: {ProcessedCount}, Errors: {ErrorCount}",
                processedCount,
                errorCount);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in permission cache refresh job");
            throw;
        }
    }

    /// <summary>
    /// Registers the recurring job with Hangfire.
    /// Runs every 10 minutes.
    /// </summary>
    public static void RegisterRecurringJob()
    {
        RecurringJob.AddOrUpdate<PermissionCacheRefreshJob>(
            "permission-cache-refresh",
            job => job.RefreshAsync(CancellationToken.None),
            "*/10 * * * *"); // Every 10 minutes
    }
}
