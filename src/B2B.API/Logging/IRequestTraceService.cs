namespace B2B.API.Logging;

/// <summary>
/// Service for managing request traces throughout the request lifecycle.
/// </summary>
public interface IRequestTraceService
{
    /// <summary>
    /// Gets the current request's trace information.
    /// </summary>
    StackTraceInfo? CurrentTrace { get; }

    /// <summary>
    /// Starts a new trace for the current request.
    /// </summary>
    StackTraceInfo StartTrace(string httpMethod, string requestPath, string? correlationId = null);

    /// <summary>
    /// Adds an execution step to the current trace.
    /// </summary>
    void AddStep(string stepName, string description, string? component = null);

    /// <summary>
    /// Sets controller and action information.
    /// </summary>
    void SetControllerAction(string controller, string action);

    /// <summary>
    /// Sets user information.
    /// </summary>
    void SetUserInfo(string? userId, string? tenantId);

    /// <summary>
    /// Adds metadata to the current trace.
    /// </summary>
    void AddMetadata(string key, object value);

    /// <summary>
    /// Completes the current trace.
    /// </summary>
    void CompleteTrace(int statusCode, string? errorMessage = null, Exception? exception = null);
}
