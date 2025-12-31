using System.Text.Json.Serialization;

namespace B2B.Infrastructure.Messaging.Resilience;

/// <summary>
/// Context information captured during message retry attempts.
/// Used for structured logging and debugging.
/// </summary>
public class RetryContext
{
    /// <summary>
    /// Gets or sets the current retry attempt number (1-based).
    /// </summary>
    [JsonPropertyName("retryAttempt")]
    public int RetryAttempt { get; set; }

    /// <summary>
    /// Gets or sets the maximum number of retries configured.
    /// </summary>
    [JsonPropertyName("maxRetries")]
    public int MaxRetries { get; set; }

    /// <summary>
    /// Gets or sets the delay until the next retry attempt.
    /// </summary>
    [JsonPropertyName("delayUntilNextRetry")]
    public TimeSpan DelayUntilNextRetry { get; set; }

    /// <summary>
    /// Gets or sets the name of the queue being processed.
    /// </summary>
    [JsonPropertyName("queueName")]
    public string QueueName { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the type name of the message being processed.
    /// </summary>
    [JsonPropertyName("messageType")]
    public string MessageType { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the type name of the consumer processing the message.
    /// </summary>
    [JsonPropertyName("consumerType")]
    public string ConsumerType { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the unique identifier of the message.
    /// </summary>
    [JsonPropertyName("messageId")]
    public Guid MessageId { get; set; }

    /// <summary>
    /// Gets or sets the correlation ID for distributed tracing.
    /// </summary>
    [JsonPropertyName("correlationId")]
    public string CorrelationId { get; set; } = string.Empty;


    /// <summary>
    /// Gets or sets the tenant ID associated with the message.
    /// </summary>
    [JsonPropertyName("tenantId")]
    public string TenantId { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the structured exception information.
    /// </summary>
    [JsonPropertyName("exceptionInfo")]
    public StackTraceInfo ExceptionInfo { get; set; } = new();

    /// <summary>
    /// Gets or sets the timestamp when the retry occurred.
    /// </summary>
    [JsonPropertyName("occurredAt")]
    public DateTime OccurredAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Gets or sets the machine name where the retry occurred.
    /// </summary>
    [JsonPropertyName("machineName")]
    public string MachineName { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the process ID where the retry occurred.
    /// </summary>
    [JsonPropertyName("processId")]
    public int ProcessId { get; set; }

    /// <summary>
    /// Gets or sets the thread ID where the retry occurred.
    /// </summary>
    [JsonPropertyName("threadId")]
    public int ThreadId { get; set; }

    /// <summary>
    /// Gets or sets the retry policy name being used.
    /// </summary>
    [JsonPropertyName("retryPolicyName")]
    public string RetryPolicyName { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets whether this is the final retry attempt.
    /// </summary>
    [JsonPropertyName("isFinalAttempt")]
    public bool IsFinalAttempt => RetryAttempt >= MaxRetries;

    /// <summary>
    /// Gets or sets the total elapsed time since first attempt.
    /// </summary>
    [JsonPropertyName("totalElapsedTime")]
    public TimeSpan TotalElapsedTime { get; set; }

    /// <summary>
    /// Gets or sets the history of all retry attempts for this message.
    /// </summary>
    [JsonPropertyName("retryHistory")]
    public List<RetryAttemptInfo> RetryHistory { get; set; } = new();

    /// <summary>
    /// Creates a RetryContext with environment information populated.
    /// </summary>
    /// <returns>A new RetryContext with environment info.</returns>
    public static RetryContext CreateWithEnvironmentInfo()
    {
        return new RetryContext
        {
            MachineName = Environment.MachineName,
            ProcessId = Environment.ProcessId,
            ThreadId = Environment.CurrentManagedThreadId,
            OccurredAt = DateTime.UtcNow
        };
    }
}

/// <summary>
/// Information about a single retry attempt.
/// </summary>
public class RetryAttemptInfo
{
    /// <summary>
    /// Gets or sets the attempt number.
    /// </summary>
    [JsonPropertyName("attemptNumber")]
    public int AttemptNumber { get; set; }

    /// <summary>
    /// Gets or sets when the attempt occurred.
    /// </summary>
    [JsonPropertyName("attemptedAt")]
    public DateTime AttemptedAt { get; set; }

    /// <summary>
    /// Gets or sets the delay before this attempt.
    /// </summary>
    [JsonPropertyName("delayBeforeAttempt")]
    public TimeSpan DelayBeforeAttempt { get; set; }

    /// <summary>
    /// Gets or sets the exception that caused the retry.
    /// </summary>
    [JsonPropertyName("exception")]
    public StackTraceInfo? Exception { get; set; }

    /// <summary>
    /// Gets or sets whether this attempt succeeded.
    /// </summary>
    [JsonPropertyName("succeeded")]
    public bool Succeeded { get; set; }
}
