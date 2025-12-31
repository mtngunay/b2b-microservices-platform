using B2B.Application.Interfaces.Services;
using Hangfire;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;

namespace B2B.Worker.Jobs;

/// <summary>
/// Background job that cleans up expired tokens from Redis.
/// While Redis handles TTL-based expiration automatically, this job
/// performs additional cleanup and maintenance tasks.
/// </summary>
public class CleanupExpiredTokensJob
{
    private readonly IConnectionMultiplexer _redis;
    private readonly ILogger<CleanupExpiredTokensJob> _logger;

    private const string TokenKeyPrefix = "token:*";
    private const string RefreshTokenKeyPrefix = "refresh_token:*";
    private const string BlacklistKeyPrefix = "blacklist:*";

    /// <summary>
    /// Initializes a new instance of CleanupExpiredTokensJob.
    /// </summary>
    public CleanupExpiredTokensJob(
        IConnectionMultiplexer redis,
        ILogger<CleanupExpiredTokensJob> logger)
    {
        _redis = redis ?? throw new ArgumentNullException(nameof(redis));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Cleans up expired tokens and performs maintenance tasks.
    /// This method is called by Hangfire on a recurring schedule.
    /// </summary>
    [AutomaticRetry(Attempts = 3, DelaysInSeconds = new[] { 30, 60, 120 })]
    [Queue("low")]
    public async Task CleanupAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Starting expired tokens cleanup job");

        try
        {
            var db = _redis.GetDatabase();
            var server = _redis.GetServer(_redis.GetEndPoints().First());

            var tokenStats = await CleanupKeysByPatternAsync(server, db, TokenKeyPrefix, cancellationToken);
            var refreshTokenStats = await CleanupKeysByPatternAsync(server, db, RefreshTokenKeyPrefix, cancellationToken);
            var blacklistStats = await CleanupKeysByPatternAsync(server, db, BlacklistKeyPrefix, cancellationToken);

            _logger.LogInformation(
                "Token cleanup completed. " +
                "Access tokens - Total: {TokenTotal}, Expired: {TokenExpired}. " +
                "Refresh tokens - Total: {RefreshTotal}, Expired: {RefreshExpired}. " +
                "Blacklist entries - Total: {BlacklistTotal}, Expired: {BlacklistExpired}.",
                tokenStats.Total, tokenStats.Expired,
                refreshTokenStats.Total, refreshTokenStats.Expired,
                blacklistStats.Total, blacklistStats.Expired);

            // Log memory usage statistics
            await LogMemoryStatsAsync(server);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in expired tokens cleanup job");
            throw;
        }
    }

    /// <summary>
    /// Cleans up keys matching a pattern and returns statistics.
    /// </summary>
    private async Task<(int Total, int Expired)> CleanupKeysByPatternAsync(
        IServer server,
        IDatabase db,
        string pattern,
        CancellationToken cancellationToken)
    {
        var total = 0;
        var expired = 0;

        await foreach (var key in server.KeysAsync(pattern: pattern))
        {
            if (cancellationToken.IsCancellationRequested)
            {
                break;
            }

            total++;

            // Check if key has no TTL (shouldn't happen, but clean up if it does)
            var ttl = await db.KeyTimeToLiveAsync(key);
            if (ttl == null)
            {
                // Key exists but has no TTL - this is unexpected, log and set a TTL
                _logger.LogWarning(
                    "Found token key without TTL: {Key}. Setting default expiry.",
                    key.ToString());

                // Set a default TTL of 1 hour for orphaned keys
                await db.KeyExpireAsync(key, TimeSpan.FromHours(1));
            }
            else if (ttl.Value.TotalSeconds <= 0)
            {
                // Key is expired but not yet removed (edge case)
                await db.KeyDeleteAsync(key);
                expired++;
            }
        }

        return (total, expired);
    }

    /// <summary>
    /// Logs Redis memory statistics for monitoring.
    /// </summary>
    private async Task LogMemoryStatsAsync(IServer server)
    {
        try
        {
            var info = await server.InfoAsync("memory");
            
            // Find the memory section
            IGrouping<string, KeyValuePair<string, string>>? memorySection = null;
            foreach (var section in info)
            {
                if (section.Key == "memory")
                {
                    memorySection = section;
                    break;
                }
            }

            if (memorySection == null)
            {
                _logger.LogDebug("Memory section not found in Redis INFO");
                return;
            }

            string usedMemory = "unknown";
            string peakMemory = "unknown";

            foreach (var kv in memorySection)
            {
                if (kv.Key == "used_memory_human")
                {
                    usedMemory = kv.Value;
                }
                else if (kv.Key == "used_memory_peak_human")
                {
                    peakMemory = kv.Value;
                }
            }

            _logger.LogInformation(
                "Redis memory stats - Used: {UsedMemory}, Peak: {PeakMemory}",
                usedMemory,
                peakMemory);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to retrieve Redis memory statistics");
        }
    }

    /// <summary>
    /// Registers the recurring job with Hangfire.
    /// Runs every hour.
    /// </summary>
    public static void RegisterRecurringJob()
    {
        RecurringJob.AddOrUpdate<CleanupExpiredTokensJob>(
            "cleanup-expired-tokens",
            job => job.CleanupAsync(CancellationToken.None),
            "0 * * * *"); // Every hour at minute 0
    }
}
