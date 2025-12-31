using B2B.Worker.Configuration;
using Hangfire.Common;
using Hangfire.States;
using Hangfire.Storage;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace B2B.Worker.Filters;

/// <summary>
/// Hangfire filter that sends notifications when jobs fail permanently.
/// </summary>
public class JobFailureNotificationFilter : JobFilterAttribute, IApplyStateFilter
{
    private readonly ILogger<JobFailureNotificationFilter> _logger;
    private readonly JobRetryOptions _options;

    /// <summary>
    /// Initializes a new instance of JobFailureNotificationFilter.
    /// </summary>
    public JobFailureNotificationFilter(
        ILogger<JobFailureNotificationFilter> logger,
        IOptions<JobRetryOptions> options)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
    }

    /// <summary>
    /// Called when a job state is being applied.
    /// </summary>
    public void OnStateApplied(ApplyStateContext context, IWriteOnlyTransaction transaction)
    {
        if (context.NewState is not FailedState failedState)
        {
            return;
        }

        var retryCount = context.GetJobParameter<int>("RetryCount");
        
        // Only notify if this is a permanent failure (exceeded max retries)
        if (retryCount < _options.MaxRetryAttempts)
        {
            return;
        }

        var jobId = context.BackgroundJob.Id;
        var jobType = context.BackgroundJob.Job?.Type?.Name ?? "Unknown";
        var jobMethod = context.BackgroundJob.Job?.Method?.Name ?? "Unknown";
        var exception = failedState.Exception;

        _logger.LogError(
            exception,
            "Job {JobId} ({JobType}.{JobMethod}) failed permanently after {RetryCount} attempts",
            jobId,
            jobType,
            jobMethod,
            retryCount);

        if (_options.EnableFailureNotifications)
        {
            SendFailureNotification(jobId, jobType, jobMethod, exception, retryCount);
        }
    }

    /// <summary>
    /// Called when a job state is being unapplied.
    /// </summary>
    public void OnStateUnapplied(ApplyStateContext context, IWriteOnlyTransaction transaction)
    {
        // No action needed on state unapplied
    }

    /// <summary>
    /// Sends a failure notification for a permanently failed job.
    /// </summary>
    private void SendFailureNotification(
        string jobId,
        string jobType,
        string jobMethod,
        Exception? exception,
        int retryCount)
    {
        // Log the notification (in production, this would send an email, Slack message, etc.)
        _logger.LogCritical(
            "FAILURE NOTIFICATION: Job {JobId} ({JobType}.{JobMethod}) failed permanently. " +
            "Retry attempts: {RetryCount}. Error: {Error}. Stack trace: {StackTrace}",
            jobId,
            jobType,
            jobMethod,
            retryCount,
            exception?.Message ?? "Unknown error",
            exception?.StackTrace ?? "No stack trace available");

        // TODO: Implement actual notification mechanism (email, Slack, PagerDuty, etc.)
        // Example:
        // await _notificationService.SendAsync(new JobFailureNotification
        // {
        //     JobId = jobId,
        //     JobType = jobType,
        //     JobMethod = jobMethod,
        //     Exception = exception,
        //     RetryCount = retryCount,
        //     Timestamp = DateTime.UtcNow
        // });
    }
}
