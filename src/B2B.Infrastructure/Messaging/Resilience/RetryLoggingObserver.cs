using MassTransit;
using Microsoft.Extensions.Logging;
using SerilogContext = Serilog.Context.LogContext;

namespace B2B.Infrastructure.Messaging.Resilience;

/// <summary>
/// MassTransit consume observer that logs detailed retry information with categorized stack traces.
/// Implements IConsumeObserver to intercept message consumption failures.
/// </summary>
public class RetryLoggingObserver : IConsumeObserver
{
    private readonly ILogger<RetryLoggingObserver> _logger;
    private readonly IExceptionCategorizer _exceptionCategorizer;

    /// <summary>
    /// Initializes a new instance of the <see cref="RetryLoggingObserver"/> class.
    /// </summary>
    /// <param name="logger">The logger instance.</param>
    /// <param name="exceptionCategorizer">The exception categorizer service.</param>
    public RetryLoggingObserver(
        ILogger<RetryLoggingObserver> logger,
        IExceptionCategorizer exceptionCategorizer)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _exceptionCategorizer = exceptionCategorizer ?? throw new ArgumentNullException(nameof(exceptionCategorizer));
    }

    /// <summary>
    /// Called before a message is consumed.
    /// </summary>
    public Task PreConsume<T>(ConsumeContext<T> context) where T : class
    {
        _logger.LogDebug(
            "Starting consumption of {MessageType} with MessageId {MessageId}",
            typeof(T).Name,
            context.MessageId);
        return Task.CompletedTask;
    }

    /// <summary>
    /// Called after a message is successfully consumed.
    /// </summary>
    public Task PostConsume<T>(ConsumeContext<T> context) where T : class
    {
        _logger.LogDebug(
            "Successfully consumed {MessageType} with MessageId {MessageId}",
            typeof(T).Name,
            context.MessageId);
        return Task.CompletedTask;
    }


    /// <summary>
    /// Called when message consumption fails.
    /// Logs detailed exception information with categorized stack trace.
    /// </summary>
    public Task ConsumeFault<T>(ConsumeContext<T> context, Exception exception) where T : class
    {
        var stackTraceInfo = _exceptionCategorizer.ExtractStackTraceInfo(exception);
        var retryContext = BuildRetryContext(context, stackTraceInfo);

        // Push structured properties to Serilog LogContext
        using (SerilogContext.PushProperty("RetryContext", retryContext, destructureObjects: true))
        using (SerilogContext.PushProperty("ExceptionCategory", stackTraceInfo.Category.ToString()))
        using (SerilogContext.PushProperty("ExceptionFingerprint", stackTraceInfo.ExceptionFingerprint))
        using (SerilogContext.PushProperty("MessageType", retryContext.MessageType))
        using (SerilogContext.PushProperty("QueueName", retryContext.QueueName))
        using (SerilogContext.PushProperty("ConsumerType", retryContext.ConsumerType))
        {
            _logger.LogError(
                exception,
                "Message consumption failed for {MessageType} on queue {QueueName}. " +
                "Category: {Category}. MessageId: {MessageId}. Fingerprint: {Fingerprint}",
                retryContext.MessageType,
                retryContext.QueueName,
                stackTraceInfo.Category,
                retryContext.MessageId,
                stackTraceInfo.ExceptionFingerprint);
        }

        return Task.CompletedTask;
    }

    /// <summary>
    /// Builds a RetryContext from MassTransit consume context.
    /// </summary>
    private Resilience.RetryContext BuildRetryContext<T>(
        ConsumeContext<T> context, 
        StackTraceInfo stackTraceInfo) where T : class
    {
        var retryContext = Resilience.RetryContext.CreateWithEnvironmentInfo();
        retryContext.MessageType = typeof(T).Name;
        retryContext.ExceptionInfo = stackTraceInfo;
        retryContext.QueueName = context.ReceiveContext?.InputAddress?.AbsolutePath ?? "unknown";
        retryContext.MessageId = context.MessageId ?? Guid.Empty;
        retryContext.CorrelationId = context.CorrelationId?.ToString() ?? string.Empty;

        // Try to extract tenant ID from headers
        if (context.Headers.TryGetHeader("X-Tenant-Id", out var tenantId))
        {
            retryContext.TenantId = tenantId?.ToString() ?? string.Empty;
        }

        // Try to get consumer type from endpoint name
        var endpointName = context.ReceiveContext?.InputAddress?.Segments.LastOrDefault();
        retryContext.ConsumerType = !string.IsNullOrEmpty(endpointName) ? endpointName : "unknown";

        return retryContext;
    }
}

/// <summary>
/// MassTransit filter that logs detailed exception information with categorized stack traces.
/// </summary>
/// <typeparam name="TContext">The pipe context type.</typeparam>
public class ExceptionLoggingFilter<TContext> : IFilter<TContext>
    where TContext : class, PipeContext
{
    private readonly ILogger<ExceptionLoggingFilter<TContext>> _logger;
    private readonly IExceptionCategorizer _exceptionCategorizer;

    /// <summary>
    /// Initializes a new instance of the <see cref="ExceptionLoggingFilter{TContext}"/> class.
    /// </summary>
    public ExceptionLoggingFilter(
        ILogger<ExceptionLoggingFilter<TContext>> logger,
        IExceptionCategorizer exceptionCategorizer)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _exceptionCategorizer = exceptionCategorizer ?? throw new ArgumentNullException(nameof(exceptionCategorizer));
    }

    /// <summary>
    /// Probes the filter for diagnostic information.
    /// </summary>
    public void Probe(ProbeContext context)
    {
        context.CreateFilterScope("exceptionLogging");
    }

    /// <summary>
    /// Sends the context through the filter.
    /// </summary>
    public async Task Send(TContext context, IPipe<TContext> next)
    {
        try
        {
            await next.Send(context);
        }
        catch (Exception ex)
        {
            var stackTraceInfo = _exceptionCategorizer.ExtractStackTraceInfo(ex);
            LogException(stackTraceInfo, ex);
            throw;
        }
    }

    private void LogException(StackTraceInfo stackTraceInfo, Exception ex)
    {
        using (SerilogContext.PushProperty("ExceptionCategory", stackTraceInfo.Category.ToString()))
        using (SerilogContext.PushProperty("ExceptionFingerprint", stackTraceInfo.ExceptionFingerprint))
        using (SerilogContext.PushProperty("StackTraceInfo", stackTraceInfo, destructureObjects: true))
        {
            _logger.LogError(
                ex,
                "Exception occurred. Type: {ExceptionType}. Category: {Category}. Fingerprint: {Fingerprint}",
                stackTraceInfo.ExceptionType,
                stackTraceInfo.Category,
                stackTraceInfo.ExceptionFingerprint);
        }
    }
}
