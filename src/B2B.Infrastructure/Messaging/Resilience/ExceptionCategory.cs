namespace B2B.Infrastructure.Messaging.Resilience;

/// <summary>
/// Categories for classifying exceptions during message processing.
/// Used for retry decisions and structured logging.
/// </summary>
public enum ExceptionCategory
{
    /// <summary>
    /// Transient failures that may succeed on retry.
    /// Examples: Network timeouts, temporary unavailability, connection drops.
    /// </summary>
    Transient,

    /// <summary>
    /// Business rule violations that should not be retried.
    /// Examples: Invalid business state, domain rule violations.
    /// </summary>
    Business,

    /// <summary>
    /// Infrastructure failures related to external dependencies.
    /// Examples: Database errors, cache failures, message broker issues.
    /// </summary>
    Infrastructure,

    /// <summary>
    /// Input validation errors that should not be retried.
    /// Examples: Invalid input format, missing required fields.
    /// </summary>
    Validation,

    /// <summary>
    /// Security-related failures that should not be retried.
    /// Examples: Authentication failures, authorization denials.
    /// </summary>
    Security,

    /// <summary>
    /// Unclassified exceptions that don't fit other categories.
    /// Default category for unknown exception types.
    /// </summary>
    Unknown
}
