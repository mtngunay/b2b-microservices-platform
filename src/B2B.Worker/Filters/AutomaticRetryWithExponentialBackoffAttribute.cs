using Hangfire;
using Hangfire.Common;
using Hangfire.States;
using Hangfire.Storage;

namespace B2B.Worker.Filters;

/// <summary>
/// Hangfire filter attribute that implements automatic retry with exponential backoff.
/// </summary>
public class AutomaticRetryWithExponentialBackoffAttribute : JobFilterAttribute, IElectStateFilter, IApplyStateFilter
{
    private readonly int _maxRetryAttempts;
    private readonly int[] _delaysInSeconds;

    /// <summary>
    /// Default delays in seconds for exponential backoff.
    /// </summary>
    private static readonly int[] DefaultDelays = { 10, 30, 60, 300, 900 };

    /// <summary>
    /// Initializes a new instance with default settings.
    /// </summary>
    public AutomaticRetryWithExponentialBackoffAttribute()
        : this(5, DefaultDelays)
    {
    }

    /// <summary>
    /// Initializes a new instance with custom settings.
    /// </summary>
    /// <param name="maxRetryAttempts">Maximum number of retry attempts.</param>
    /// <param name="delaysInSeconds">Delays in seconds between retries.</param>
    public AutomaticRetryWithExponentialBackoffAttribute(int maxRetryAttempts, int[] delaysInSeconds)
    {
        _maxRetryAttempts = maxRetryAttempts;
        _delaysInSeconds = delaysInSeconds ?? DefaultDelays;
    }

    /// <summary>
    /// Called when a job state is being elected.
    /// </summary>
    public void OnStateElection(ElectStateContext context)
    {
        if (context.CandidateState is not FailedState failedState)
        {
            return;
        }

        var retryAttempt = context.GetJobParameter<int>("RetryCount") + 1;

        if (retryAttempt <= _maxRetryAttempts)
        {
            // Calculate delay with exponential backoff
            var delayIndex = Math.Min(retryAttempt - 1, _delaysInSeconds.Length - 1);
            var delay = TimeSpan.FromSeconds(_delaysInSeconds[delayIndex]);

            // Add jitter to prevent thundering herd
            var jitter = TimeSpan.FromMilliseconds(new Random().Next(0, 1000));
            delay += jitter;

            context.SetJobParameter("RetryCount", retryAttempt);

            var scheduledState = new ScheduledState(delay)
            {
                Reason = $"Retry attempt {retryAttempt} of {_maxRetryAttempts}. " +
                        $"Exception: {failedState.Exception?.Message ?? "Unknown error"}"
            };

            context.CandidateState = scheduledState;

            // Log retry attempt (using Console since we don't have logger in attribute)
            Console.WriteLine(
                $"Job {context.BackgroundJob.Id} failed. Scheduling retry {retryAttempt}/{_maxRetryAttempts} in {delay}. Error: {failedState.Exception?.Message}");
        }
        else
        {
            // Log permanent failure
            Console.WriteLine(
                $"Job {context.BackgroundJob.Id} exceeded maximum retry attempts ({_maxRetryAttempts}). Moving to failed state. Error: {failedState.Exception?.Message}");
        }
    }

    /// <summary>
    /// Called when a job state is being applied.
    /// </summary>
    public void OnStateApplied(ApplyStateContext context, IWriteOnlyTransaction transaction)
    {
        // No action needed on state applied
    }

    /// <summary>
    /// Called when a job state is being unapplied.
    /// </summary>
    public void OnStateUnapplied(ApplyStateContext context, IWriteOnlyTransaction transaction)
    {
        // No action needed on state unapplied
    }
}
