using MediatR;
using Microsoft.Extensions.Logging;
using Polly;
using Polly.Retry;

namespace B2B.Application.Behaviors;

/// <summary>
/// MediatR pipeline behavior that implements retry logic with exponential backoff.
/// Uses Polly for resilience patterns to handle transient failures.
/// </summary>
/// <typeparam name="TRequest">The type of request being handled.</typeparam>
/// <typeparam name="TResponse">The type of response returned.</typeparam>
public class RetryBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    private readonly ILogger<RetryBehavior<TRequest, TResponse>> _logger;
    private readonly AsyncRetryPolicy _retryPolicy;

    private const int MaxRetryAttempts = 3;
    private static readonly TimeSpan BaseDelay = TimeSpan.FromMilliseconds(200);

    public RetryBehavior(ILogger<RetryBehavior<TRequest, TResponse>> logger)
    {
        _logger = logger;
        _retryPolicy = CreateRetryPolicy();
    }

    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        var requestName = typeof(TRequest).Name;

        return await _retryPolicy.ExecuteAsync(async () =>
        {
            return await next();
        });
    }

    private AsyncRetryPolicy CreateRetryPolicy()
    {
        var requestName = typeof(TRequest).Name;

        return Policy
            .Handle<Exception>(ex => IsTransientException(ex))
            .WaitAndRetryAsync(
                retryCount: MaxRetryAttempts,
                sleepDurationProvider: retryAttempt =>
                {
                    // Exponential backoff with jitter
                    var exponentialDelay = TimeSpan.FromMilliseconds(
                        BaseDelay.TotalMilliseconds * Math.Pow(2, retryAttempt - 1));
                    var jitter = TimeSpan.FromMilliseconds(Random.Shared.Next(0, 100));
                    return exponentialDelay + jitter;
                },
                onRetry: (exception, timeSpan, retryCount, context) =>
                {
                    _logger.LogWarning(
                        exception,
                        "Retry {RetryCount}/{MaxRetries} for {RequestName} after {Delay}ms. Error: {ErrorMessage}",
                        retryCount,
                        MaxRetryAttempts,
                        requestName,
                        timeSpan.TotalMilliseconds,
                        exception.Message);
                });
    }

    /// <summary>
    /// Determines if an exception is transient and should be retried.
    /// </summary>
    /// <param name="exception">The exception to check.</param>
    /// <returns>True if the exception is transient; otherwise, false.</returns>
    private static bool IsTransientException(Exception exception)
    {
        // Common transient exceptions that should be retried
        return exception switch
        {
            TimeoutException => true,
            OperationCanceledException => false, // Don't retry cancellations
            _ when exception.Message.Contains("deadlock", StringComparison.OrdinalIgnoreCase) => true,
            _ when exception.Message.Contains("timeout", StringComparison.OrdinalIgnoreCase) => true,
            _ when exception.Message.Contains("connection", StringComparison.OrdinalIgnoreCase) => true,
            _ => false
        };
    }
}
