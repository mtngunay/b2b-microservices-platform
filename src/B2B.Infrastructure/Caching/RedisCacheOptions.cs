namespace B2B.Infrastructure.Caching;

/// <summary>
/// Configuration options for Redis cache.
/// </summary>
public class RedisCacheOptions
{
    /// <summary>
    /// Configuration section name.
    /// </summary>
    public const string SectionName = "Redis";

    /// <summary>
    /// Gets or sets the Redis connection string.
    /// </summary>
    public string ConnectionString { get; set; } = "localhost:6379";

    /// <summary>
    /// Gets or sets the instance name prefix for cache keys.
    /// </summary>
    public string InstanceName { get; set; } = "B2B:";

    /// <summary>
    /// Gets or sets the default cache expiration in minutes.
    /// </summary>
    public int DefaultExpirationMinutes { get; set; } = 30;

    /// <summary>
    /// Gets or sets the connection timeout in milliseconds.
    /// </summary>
    public int ConnectTimeoutMs { get; set; } = 5000;

    /// <summary>
    /// Gets or sets the sync timeout in milliseconds.
    /// </summary>
    public int SyncTimeoutMs { get; set; } = 5000;

    /// <summary>
    /// Gets or sets whether to abort on connect fail.
    /// </summary>
    public bool AbortOnConnectFail { get; set; } = false;

    /// <summary>
    /// Gets or sets the number of retry attempts for failed operations.
    /// </summary>
    public int RetryCount { get; set; } = 3;

    /// <summary>
    /// Gets or sets whether SSL is enabled.
    /// </summary>
    public bool Ssl { get; set; } = false;

    /// <summary>
    /// Gets or sets the password for Redis authentication.
    /// </summary>
    public string? Password { get; set; }
}
