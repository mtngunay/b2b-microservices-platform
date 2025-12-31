using B2B.Infrastructure.Messaging.Resilience;
using MassTransit;
using Microsoft.Extensions.Logging;
using SerilogContext = Serilog.Context.LogContext;

namespace B2B.Infrastructure.Messaging.Consumers;

/// <summary>
/// Generic fault consumer that logs complete failure history when messages are moved to dead-letter queue.
/// </summary>
/// <typeparam name="TMessage">The type of message that faulted.</typeparam>
public class FaultConsumer<TMessage> : IConsumer<Fault<TMessage>>
    where TMessage : class
{
    private readonly ILogger<FaultConsumer<TMessage>> _logger;
    private readonly IExceptionCategorizer _exceptionCategorizer;

    /// <summary>
    /// Initializes a new instance of the <see cref="FaultConsumer{TMessage}"/> class.
    /// </summary>
    /// <param name="logger">The logger instance.</param>
    /// <param name="exceptionCategorizer">The exception categorizer service.</param>
    public FaultConsumer(
        ILogger<FaultConsumer<TMessage>> logger,
        IExceptionCategorizer exceptionCategorizer)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _exceptionCategorizer = exceptionCategorizer ?? throw new ArgumentNullException(nameof(exceptionCategorizer));
    }

    /// <summary>
    /// Consumes a fault message and logs complete failure history.
    /// </summary>
    /// <param name="context">The consume context containing the fault.</param>
    public Task Consume(ConsumeContext<Fault<TMessage>> context)
    {
        var fault = context.Message;
        var messageType = typeof(TMessage).Name;
        var failureHistory = BuildFailureHistory(fault);
        var dlqContext = BuildDlqContext(context, fault, failureHistory);

        // Push structured properties to Serilog LogContext
        using (SerilogContext.PushProperty("DlqContext", dlqContext, destructureObjects: true))
        using (SerilogContext.PushProperty("FailureHistory", failureHistory, destructureObjects: true))
        using (SerilogContext.PushProperty("FaultedMessageId", fault.FaultedMessageId))
        using (SerilogContext.PushProperty("FaultTimestamp", fault.Timestamp))
        using (SerilogContext.PushProperty("MessageType", messageType))
        using (SerilogContext.PushProperty("TotalFailures", fault.Exceptions.Length))
        {
            LogFailureHistory(fault, messageType, failureHistory, dlqContext);
        }

        return Task.CompletedTask;
    }


    /// <summary>
    /// Builds the failure history from fault exceptions.
    /// </summary>
    private List<StackTraceInfo> BuildFailureHistory(Fault<TMessage> fault)
    {
        var failureHistory = new List<StackTraceInfo>();

        foreach (var exceptionInfo in fault.Exceptions)
        {
            var stackTraceInfo = new StackTraceInfo
            {
                ExceptionType = exceptionInfo.ExceptionType,
                Message = exceptionInfo.Message ?? string.Empty,
                StackTrace = exceptionInfo.StackTrace ?? string.Empty,
                Source = exceptionInfo.Source ?? string.Empty,
                Category = CategorizeFromExceptionType(exceptionInfo.ExceptionType),
                CapturedAt = DateTime.UtcNow
            };

            // Try to extract inner exceptions if available
            if (exceptionInfo.InnerException != null)
            {
                stackTraceInfo.InnerExceptions.Add(new StackTraceInfo
                {
                    ExceptionType = exceptionInfo.InnerException.ExceptionType,
                    Message = exceptionInfo.InnerException.Message ?? string.Empty,
                    StackTrace = exceptionInfo.InnerException.StackTrace ?? string.Empty,
                    Source = exceptionInfo.InnerException.Source ?? string.Empty
                });
            }

            failureHistory.Add(stackTraceInfo);
        }

        return failureHistory;
    }

    /// <summary>
    /// Categorizes exception based on type name string.
    /// </summary>
    private ExceptionCategory CategorizeFromExceptionType(string exceptionTypeName)
    {
        if (string.IsNullOrEmpty(exceptionTypeName))
            return ExceptionCategory.Unknown;

        var typeName = exceptionTypeName.ToLowerInvariant();

        if (typeName.Contains("timeout") || 
            typeName.Contains("socket") ||
            typeName.Contains("httprequest") ||
            typeName.Contains("operationcanceled"))
        {
            return ExceptionCategory.Transient;
        }

        if (typeName.Contains("validation") ||
            typeName.Contains("argument"))
        {
            return ExceptionCategory.Validation;
        }

        if (typeName.Contains("unauthorized") ||
            typeName.Contains("forbidden") ||
            typeName.Contains("security"))
        {
            return ExceptionCategory.Security;
        }

        if (typeName.Contains("sql") ||
            typeName.Contains("mongo") ||
            typeName.Contains("redis") ||
            typeName.Contains("rabbitmq"))
        {
            return ExceptionCategory.Infrastructure;
        }

        if (typeName.Contains("domain") ||
            typeName.Contains("business") ||
            typeName.Contains("notfound") ||
            typeName.Contains("conflict"))
        {
            return ExceptionCategory.Business;
        }

        return ExceptionCategory.Unknown;
    }


    /// <summary>
    /// Builds the dead-letter queue context.
    /// </summary>
    private DlqContext BuildDlqContext(
        ConsumeContext<Fault<TMessage>> context,
        Fault<TMessage> fault,
        List<StackTraceInfo> failureHistory)
    {
        return new DlqContext
        {
            MessageType = typeof(TMessage).Name,
            FaultedMessageId = fault.FaultedMessageId ?? Guid.Empty,
            FaultTimestamp = fault.Timestamp,
            TotalFailures = fault.Exceptions.Length,
            FirstFailureMessage = failureHistory.FirstOrDefault()?.Message ?? string.Empty,
            LastFailureMessage = failureHistory.LastOrDefault()?.Message ?? string.Empty,
            FirstFailureCategory = failureHistory.FirstOrDefault()?.Category ?? ExceptionCategory.Unknown,
            LastFailureCategory = failureHistory.LastOrDefault()?.Category ?? ExceptionCategory.Unknown,
            QueueName = context.ReceiveContext?.InputAddress?.AbsolutePath ?? "unknown",
            CorrelationId = context.CorrelationId?.ToString() ?? string.Empty,
            MachineName = Environment.MachineName,
            ProcessId = Environment.ProcessId,
            ThreadId = Environment.CurrentManagedThreadId
        };
    }

    /// <summary>
    /// Logs the complete failure history.
    /// </summary>
    private void LogFailureHistory(
        Fault<TMessage> fault,
        string messageType,
        List<StackTraceInfo> failureHistory,
        DlqContext dlqContext)
    {
        _logger.LogError(
            "Message {MessageType} with ID {MessageId} moved to dead-letter queue. " +
            "Total failures: {FailureCount}. " +
            "First failure: [{FirstCategory}] {FirstFailure}. " +
            "Last failure: [{LastCategory}] {LastFailure}. " +
            "Queue: {QueueName}. CorrelationId: {CorrelationId}",
            messageType,
            fault.FaultedMessageId,
            fault.Exceptions.Length,
            dlqContext.FirstFailureCategory,
            dlqContext.FirstFailureMessage,
            dlqContext.LastFailureCategory,
            dlqContext.LastFailureMessage,
            dlqContext.QueueName,
            dlqContext.CorrelationId);

        // Log each failure in the history for detailed analysis
        for (int i = 0; i < failureHistory.Count; i++)
        {
            var failure = failureHistory[i];
            _logger.LogDebug(
                "Failure {FailureNumber}/{TotalFailures} for {MessageType}: " +
                "[{Category}] {ExceptionType}: {Message}",
                i + 1,
                failureHistory.Count,
                messageType,
                failure.Category,
                failure.ExceptionType,
                failure.Message);
        }
    }
}

/// <summary>
/// Context information for dead-letter queue logging.
/// </summary>
public class DlqContext
{
    /// <summary>
    /// Gets or sets the message type name.
    /// </summary>
    public string MessageType { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the faulted message ID.
    /// </summary>
    public Guid FaultedMessageId { get; set; }

    /// <summary>
    /// Gets or sets the fault timestamp.
    /// </summary>
    public DateTime FaultTimestamp { get; set; }

    /// <summary>
    /// Gets or sets the total number of failures.
    /// </summary>
    public int TotalFailures { get; set; }

    /// <summary>
    /// Gets or sets the first failure message.
    /// </summary>
    public string FirstFailureMessage { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the last failure message.
    /// </summary>
    public string LastFailureMessage { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the first failure category.
    /// </summary>
    public ExceptionCategory FirstFailureCategory { get; set; }

    /// <summary>
    /// Gets or sets the last failure category.
    /// </summary>
    public ExceptionCategory LastFailureCategory { get; set; }

    /// <summary>
    /// Gets or sets the queue name.
    /// </summary>
    public string QueueName { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the correlation ID.
    /// </summary>
    public string CorrelationId { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the machine name.
    /// </summary>
    public string MachineName { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the process ID.
    /// </summary>
    public int ProcessId { get; set; }

    /// <summary>
    /// Gets or sets the thread ID.
    /// </summary>
    public int ThreadId { get; set; }
}
