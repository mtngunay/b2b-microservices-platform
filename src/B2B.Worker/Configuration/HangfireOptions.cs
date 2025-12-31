namespace B2B.Worker.Configuration;

/// <summary>
/// Configuration options for Hangfire.
/// </summary>
public class HangfireOptions
{
    /// <summary>
    /// Configuration section name.
    /// </summary>
    public const string SectionName = "Hangfire";

    /// <summary>
    /// Number of worker threads to process jobs.
    /// </summary>
    public int WorkerCount { get; set; } = Environment.ProcessorCount * 2;

    /// <summary>
    /// Queues to process, in priority order.
    /// </summary>
    public string[] Queues { get; set; } = { "critical", "default", "low" };

    /// <summary>
    /// Whether to enable the Hangfire dashboard.
    /// </summary>
    public bool EnableDashboard { get; set; } = true;

    /// <summary>
    /// Dashboard path (relative to base URL).
    /// </summary>
    public string DashboardPath { get; set; } = "/hangfire";

    /// <summary>
    /// Dashboard username for basic authentication.
    /// </summary>
    public string DashboardUsername { get; set; } = "admin";

    /// <summary>
    /// Dashboard password for basic authentication.
    /// </summary>
    public string DashboardPassword { get; set; } = "admin";

    /// <summary>
    /// SQL Server schema name for Hangfire tables.
    /// </summary>
    public string SchemaName { get; set; } = "HangFire";

    /// <summary>
    /// Job expiration timeout in days.
    /// </summary>
    public int JobExpirationTimeoutDays { get; set; } = 7;

    /// <summary>
    /// Server timeout in minutes.
    /// </summary>
    public int ServerTimeoutMinutes { get; set; } = 5;

    /// <summary>
    /// Server check interval in seconds.
    /// </summary>
    public int ServerCheckIntervalSeconds { get; set; } = 30;
}
