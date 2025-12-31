namespace B2B.Infrastructure.Messaging.Resilience;

/// <summary>
/// Interface for categorizing exceptions and extracting stack trace information.
/// </summary>
public interface IExceptionCategorizer
{
    /// <summary>
    /// Categorizes an exception into a predefined category.
    /// </summary>
    /// <param name="exception">The exception to categorize.</param>
    /// <returns>The exception category.</returns>
    ExceptionCategory Categorize(Exception exception);

    /// <summary>
    /// Extracts detailed stack trace information from an exception.
    /// </summary>
    /// <param name="exception">The exception to extract information from.</param>
    /// <returns>Structured stack trace information.</returns>
    StackTraceInfo ExtractStackTraceInfo(Exception exception);

    /// <summary>
    /// Determines if an exception is retryable based on its category.
    /// </summary>
    /// <param name="exception">The exception to check.</param>
    /// <returns>True if the exception is retryable, false otherwise.</returns>
    bool IsRetryable(Exception exception);

    /// <summary>
    /// Registers a custom exception type to category mapping.
    /// </summary>
    /// <typeparam name="TException">The exception type to register.</typeparam>
    /// <param name="category">The category to assign to this exception type.</param>
    void RegisterExceptionCategory<TException>(ExceptionCategory category) where TException : Exception;
}
