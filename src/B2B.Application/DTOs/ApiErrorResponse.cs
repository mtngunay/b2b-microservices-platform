namespace B2B.Application.DTOs;

/// <summary>
/// Standardized error response for API errors.
/// </summary>
public class ApiErrorResponse
{
    /// <summary>
    /// Gets or sets the correlation ID for distributed tracing.
    /// </summary>
    public string CorrelationId { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the error code.
    /// </summary>
    public string ErrorCode { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the error message.
    /// </summary>
    public string Message { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets additional error details.
    /// </summary>
    public string? Details { get; set; }

    /// <summary>
    /// Gets or sets validation errors grouped by property name.
    /// </summary>
    public Dictionary<string, string[]>? ValidationErrors { get; set; }

    /// <summary>
    /// Gets or sets the timestamp when the error occurred.
    /// </summary>
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Creates a validation error response.
    /// </summary>
    /// <param name="correlationId">The correlation ID.</param>
    /// <param name="validationErrors">The validation errors.</param>
    /// <returns>An ApiErrorResponse for validation errors.</returns>
    public static ApiErrorResponse ValidationError(
        string correlationId,
        Dictionary<string, string[]> validationErrors)
    {
        return new ApiErrorResponse
        {
            CorrelationId = correlationId,
            ErrorCode = "VALIDATION_ERROR",
            Message = "One or more validation errors occurred.",
            ValidationErrors = validationErrors,
            Timestamp = DateTime.UtcNow
        };
    }

    /// <summary>
    /// Creates an unauthorized error response.
    /// </summary>
    /// <param name="correlationId">The correlation ID.</param>
    /// <param name="message">The error message.</param>
    /// <returns>An ApiErrorResponse for unauthorized errors.</returns>
    public static ApiErrorResponse Unauthorized(string correlationId, string message = "Authentication required.")
    {
        return new ApiErrorResponse
        {
            CorrelationId = correlationId,
            ErrorCode = "UNAUTHORIZED",
            Message = message,
            Timestamp = DateTime.UtcNow
        };
    }

    /// <summary>
    /// Creates a forbidden error response.
    /// </summary>
    /// <param name="correlationId">The correlation ID.</param>
    /// <param name="message">The error message.</param>
    /// <returns>An ApiErrorResponse for forbidden errors.</returns>
    public static ApiErrorResponse Forbidden(string correlationId, string message = "Insufficient permissions.")
    {
        return new ApiErrorResponse
        {
            CorrelationId = correlationId,
            ErrorCode = "FORBIDDEN",
            Message = message,
            Timestamp = DateTime.UtcNow
        };
    }

    /// <summary>
    /// Creates a not found error response.
    /// </summary>
    /// <param name="correlationId">The correlation ID.</param>
    /// <param name="message">The error message.</param>
    /// <returns>An ApiErrorResponse for not found errors.</returns>
    public static ApiErrorResponse NotFound(string correlationId, string message = "Resource not found.")
    {
        return new ApiErrorResponse
        {
            CorrelationId = correlationId,
            ErrorCode = "NOT_FOUND",
            Message = message,
            Timestamp = DateTime.UtcNow
        };
    }

    /// <summary>
    /// Creates a conflict error response.
    /// </summary>
    /// <param name="correlationId">The correlation ID.</param>
    /// <param name="message">The error message.</param>
    /// <returns>An ApiErrorResponse for conflict errors.</returns>
    public static ApiErrorResponse Conflict(string correlationId, string message = "Resource conflict.")
    {
        return new ApiErrorResponse
        {
            CorrelationId = correlationId,
            ErrorCode = "CONFLICT",
            Message = message,
            Timestamp = DateTime.UtcNow
        };
    }

    /// <summary>
    /// Creates a rate limited error response.
    /// </summary>
    /// <param name="correlationId">The correlation ID.</param>
    /// <param name="retryAfterSeconds">Seconds until the client can retry.</param>
    /// <returns>An ApiErrorResponse for rate limited errors.</returns>
    public static ApiErrorResponse RateLimited(string correlationId, int retryAfterSeconds)
    {
        return new ApiErrorResponse
        {
            CorrelationId = correlationId,
            ErrorCode = "RATE_LIMITED",
            Message = $"Rate limit exceeded. Retry after {retryAfterSeconds} seconds.",
            Details = $"Retry-After: {retryAfterSeconds}",
            Timestamp = DateTime.UtcNow
        };
    }

    /// <summary>
    /// Creates an internal error response.
    /// </summary>
    /// <param name="correlationId">The correlation ID.</param>
    /// <param name="message">The error message.</param>
    /// <returns>An ApiErrorResponse for internal errors.</returns>
    public static ApiErrorResponse InternalError(string correlationId, string message = "An unexpected error occurred.")
    {
        return new ApiErrorResponse
        {
            CorrelationId = correlationId,
            ErrorCode = "INTERNAL_ERROR",
            Message = message,
            Timestamp = DateTime.UtcNow
        };
    }
}
