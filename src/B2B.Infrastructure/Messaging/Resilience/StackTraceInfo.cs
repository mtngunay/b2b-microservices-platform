using System.Text.Json.Serialization;

namespace B2B.Infrastructure.Messaging.Resilience;

/// <summary>
/// Structured model containing detailed exception and stack trace information.
/// Designed for structured logging and ELK Stack compatibility.
/// </summary>
public class StackTraceInfo
{
    /// <summary>
    /// Gets or sets the full type name of the exception.
    /// </summary>
    [JsonPropertyName("exceptionType")]
    public string ExceptionType { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the category of the exception.
    /// </summary>
    [JsonPropertyName("category")]
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public ExceptionCategory Category { get; set; }

    /// <summary>
    /// Gets or sets the exception message.
    /// </summary>
    [JsonPropertyName("message")]
    public string Message { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the source of the exception (assembly name).
    /// </summary>
    [JsonPropertyName("source")]
    public string Source { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the method name where the exception occurred.
    /// </summary>
    [JsonPropertyName("method")]
    public string Method { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the line number where the exception occurred.
    /// </summary>
    [JsonPropertyName("lineNumber")]
    public int LineNumber { get; set; }

    /// <summary>
    /// Gets or sets the file path where the exception occurred.
    /// </summary>
    [JsonPropertyName("filePath")]
    public string FilePath { get; set; } = string.Empty;


    /// <summary>
    /// Gets or sets the full stack trace string.
    /// </summary>
    [JsonPropertyName("stackTrace")]
    public string StackTrace { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the exception fingerprint (hash) for grouping similar errors.
    /// </summary>
    [JsonPropertyName("exceptionFingerprint")]
    public string ExceptionFingerprint { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the list of inner exceptions in hierarchical order.
    /// </summary>
    [JsonPropertyName("innerExceptions")]
    public List<StackTraceInfo> InnerExceptions { get; set; } = new();

    /// <summary>
    /// Gets or sets the declaring type of the method where exception occurred.
    /// </summary>
    [JsonPropertyName("declaringType")]
    public string DeclaringType { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets additional data from the exception's Data dictionary.
    /// </summary>
    [JsonPropertyName("additionalData")]
    public Dictionary<string, string> AdditionalData { get; set; } = new();

    /// <summary>
    /// Gets or sets the timestamp when the exception was captured.
    /// </summary>
    [JsonPropertyName("capturedAt")]
    public DateTime CapturedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Gets the total depth of the exception chain including inner exceptions.
    /// </summary>
    [JsonPropertyName("exceptionDepth")]
    public int ExceptionDepth => 1 + (InnerExceptions.Count > 0 
        ? InnerExceptions.Max(e => e.ExceptionDepth) 
        : 0);
}
