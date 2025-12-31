namespace B2B.Worker.Configuration;

/// <summary>
/// Configuration options for job retry policies.
/// </summary>
public class JobRetryOptions
{
    /// <summary>
    /// Configuration section name.
    /// </summary>
    public const string SectionName = "JobRetry";

    /// <summary>
    /// Maximum number of retry attempts for failed jobs.
    /// </summary>
    public int MaxRetryAttempts { get; set; } = 5;

    /// <summary>
    /// Delays in seconds between retry attempts (exponential backoff).
    /// </summary>
    public int[] DelaysInSeconds { get; set; } = { 10, 30, 60, 300, 900 };

    /// <summary>
    /// Whether to send notifications on job failure.
    /// </summary>
    public bool EnableFailureNotifications { get; set; } = true;

    /// <summary>
    /// Email address to send failure notifications to.
    /// </summary>
    public string? FailureNotificationEmail { get; set; }

    /// <summary>
    /// Whether to log detailed failure information.
    /// </summary>
    public bool LogDetailedFailures { get; set; } = true;
}
