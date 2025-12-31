using System.Diagnostics;
using System.Text;

namespace B2B.API.Logging;

/// <summary>
/// Represents detailed stack trace information for logging.
/// </summary>
public class StackTraceInfo
{
    /// <summary>
    /// Unique trace identifier for correlating logs.
    /// </summary>
    public string TraceId { get; set; } = string.Empty;

    /// <summary>
    /// Span identifier for distributed tracing.
    /// </summary>
    public string SpanId { get; set; } = string.Empty;

    /// <summary>
    /// Parent span identifier.
    /// </summary>
    public string? ParentSpanId { get; set; }

    /// <summary>
    /// Operation name (e.g., "CreateUser", "GetUsers").
    /// </summary>
    public string OperationName { get; set; } = string.Empty;

    /// <summary>
    /// Log category (READ, CREATE, UPDATE, DELETE, etc.).
    /// </summary>
    public string Category { get; set; } = string.Empty;

    /// <summary>
    /// HTTP method.
    /// </summary>
    public string HttpMethod { get; set; } = string.Empty;

    /// <summary>
    /// Request path.
    /// </summary>
    public string RequestPath { get; set; } = string.Empty;

    /// <summary>
    /// Controller name.
    /// </summary>
    public string? Controller { get; set; }

    /// <summary>
    /// Action name.
    /// </summary>
    public string? Action { get; set; }

    /// <summary>
    /// Execution steps with timestamps.
    /// </summary>
    public List<ExecutionStep> Steps { get; set; } = new();

    /// <summary>
    /// Start timestamp.
    /// </summary>
    public DateTime StartTime { get; set; }

    /// <summary>
    /// End timestamp.
    /// </summary>
    public DateTime? EndTime { get; set; }

    /// <summary>
    /// Total duration in milliseconds.
    /// </summary>
    public double? DurationMs { get; set; }

    /// <summary>
    /// User ID if authenticated.
    /// </summary>
    public string? UserId { get; set; }

    /// <summary>
    /// Tenant ID if available.
    /// </summary>
    public string? TenantId { get; set; }

    /// <summary>
    /// Client IP address.
    /// </summary>
    public string? ClientIp { get; set; }

    /// <summary>
    /// HTTP status code.
    /// </summary>
    public int? StatusCode { get; set; }

    /// <summary>
    /// Error message if any.
    /// </summary>
    public string? ErrorMessage { get; set; }

    /// <summary>
    /// Exception stack trace if error occurred.
    /// </summary>
    public string? ExceptionStackTrace { get; set; }

    /// <summary>
    /// Additional metadata.
    /// </summary>
    public Dictionary<string, object> Metadata { get; set; } = new();

    /// <summary>
    /// Adds an execution step to the trace.
    /// </summary>
    public void AddStep(string stepName, string description, string? component = null)
    {
        Steps.Add(new ExecutionStep
        {
            StepNumber = Steps.Count + 1,
            StepName = stepName,
            Description = description,
            Component = component,
            Timestamp = DateTime.UtcNow,
            ElapsedFromStart = (DateTime.UtcNow - StartTime).TotalMilliseconds
        });
    }

    /// <summary>
    /// Completes the trace with final status.
    /// </summary>
    public void Complete(int statusCode, string? errorMessage = null, Exception? exception = null)
    {
        EndTime = DateTime.UtcNow;
        DurationMs = (EndTime.Value - StartTime).TotalMilliseconds;
        StatusCode = statusCode;
        ErrorMessage = errorMessage;
        
        if (exception != null)
        {
            ExceptionStackTrace = exception.ToString();
        }
    }

    /// <summary>
    /// Converts to a dictionary for structured logging.
    /// </summary>
    public Dictionary<string, object> ToDictionary()
    {
        var dict = new Dictionary<string, object>
        {
            ["TraceId"] = TraceId,
            ["SpanId"] = SpanId,
            ["Category"] = Category,
            ["OperationName"] = OperationName,
            ["HttpMethod"] = HttpMethod,
            ["RequestPath"] = RequestPath,
            ["StartTime"] = StartTime.ToString("O"),
            ["StepCount"] = Steps.Count
        };

        if (!string.IsNullOrEmpty(ParentSpanId))
            dict["ParentSpanId"] = ParentSpanId;

        if (!string.IsNullOrEmpty(Controller))
            dict["Controller"] = Controller;

        if (!string.IsNullOrEmpty(Action))
            dict["Action"] = Action;

        if (EndTime.HasValue)
            dict["EndTime"] = EndTime.Value.ToString("O");

        if (DurationMs.HasValue)
            dict["DurationMs"] = DurationMs.Value;

        if (!string.IsNullOrEmpty(UserId))
            dict["UserId"] = UserId;

        if (!string.IsNullOrEmpty(TenantId))
            dict["TenantId"] = TenantId;

        if (!string.IsNullOrEmpty(ClientIp))
            dict["ClientIp"] = ClientIp;

        if (StatusCode.HasValue)
            dict["StatusCode"] = StatusCode.Value;

        if (!string.IsNullOrEmpty(ErrorMessage))
            dict["ErrorMessage"] = ErrorMessage;

        if (!string.IsNullOrEmpty(ExceptionStackTrace))
            dict["ExceptionStackTrace"] = ExceptionStackTrace;

        if (Steps.Count > 0)
            dict["Steps"] = Steps.Select(s => s.ToDictionary()).ToList();

        foreach (var kvp in Metadata)
        {
            dict[$"Meta_{kvp.Key}"] = kvp.Value;
        }

        return dict;
    }
}

/// <summary>
/// Represents a single execution step in the trace.
/// </summary>
public class ExecutionStep
{
    public int StepNumber { get; set; }
    public string StepName { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string? Component { get; set; }
    public DateTime Timestamp { get; set; }
    public double ElapsedFromStart { get; set; }

    public Dictionary<string, object> ToDictionary()
    {
        var dict = new Dictionary<string, object>
        {
            ["StepNumber"] = StepNumber,
            ["StepName"] = StepName,
            ["Description"] = Description,
            ["Timestamp"] = Timestamp.ToString("O"),
            ["ElapsedFromStartMs"] = ElapsedFromStart
        };

        if (!string.IsNullOrEmpty(Component))
            dict["Component"] = Component;

        return dict;
    }
}
