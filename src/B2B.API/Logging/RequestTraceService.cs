using System.Diagnostics;

namespace B2B.API.Logging;

/// <summary>
/// Implementation of request trace service using AsyncLocal for request-scoped storage.
/// </summary>
public class RequestTraceService : IRequestTraceService
{
    private static readonly AsyncLocal<StackTraceInfo?> _currentTrace = new();

    /// <inheritdoc />
    public StackTraceInfo? CurrentTrace => _currentTrace.Value;

    /// <inheritdoc />
    public StackTraceInfo StartTrace(string httpMethod, string requestPath, string? correlationId = null)
    {
        var activity = Activity.Current;
        
        var trace = new StackTraceInfo
        {
            TraceId = correlationId ?? activity?.TraceId.ToString() ?? Guid.NewGuid().ToString("N"),
            SpanId = activity?.SpanId.ToString() ?? Guid.NewGuid().ToString("N")[..16],
            ParentSpanId = activity?.ParentSpanId.ToString(),
            HttpMethod = httpMethod,
            RequestPath = requestPath,
            Category = LogCategories.GetCategoryFromMethod(httpMethod),
            OperationName = GetOperationName(httpMethod, requestPath),
            StartTime = DateTime.UtcNow
        };

        trace.AddStep("RequestReceived", $"HTTP {httpMethod} {requestPath} received", "Middleware");

        _currentTrace.Value = trace;
        return trace;
    }

    /// <inheritdoc />
    public void AddStep(string stepName, string description, string? component = null)
    {
        _currentTrace.Value?.AddStep(stepName, description, component);
    }

    /// <inheritdoc />
    public void SetControllerAction(string controller, string action)
    {
        if (_currentTrace.Value != null)
        {
            _currentTrace.Value.Controller = controller;
            _currentTrace.Value.Action = action;
            _currentTrace.Value.OperationName = $"{controller}.{action}";
        }
    }

    /// <inheritdoc />
    public void SetUserInfo(string? userId, string? tenantId)
    {
        if (_currentTrace.Value != null)
        {
            _currentTrace.Value.UserId = userId;
            _currentTrace.Value.TenantId = tenantId;
        }
    }

    /// <inheritdoc />
    public void AddMetadata(string key, object value)
    {
        if (_currentTrace.Value != null)
        {
            _currentTrace.Value.Metadata[key] = value;
        }
    }

    /// <inheritdoc />
    public void CompleteTrace(int statusCode, string? errorMessage = null, Exception? exception = null)
    {
        var trace = _currentTrace.Value;
        if (trace != null)
        {
            trace.AddStep("RequestCompleted", $"Request completed with status {statusCode}", "Middleware");
            trace.Complete(statusCode, errorMessage, exception);
        }
    }

    private static string GetOperationName(string httpMethod, string requestPath)
    {
        // Extract meaningful operation name from path
        var segments = requestPath.Split('/', StringSplitOptions.RemoveEmptyEntries);
        
        if (segments.Length == 0)
            return $"{httpMethod}_Root";

        // Skip 'api' and version segments
        var meaningfulSegments = segments
            .Where(s => !s.Equals("api", StringComparison.OrdinalIgnoreCase) 
                     && !s.StartsWith("v", StringComparison.OrdinalIgnoreCase))
            .ToList();

        if (meaningfulSegments.Count == 0)
            return $"{httpMethod}_Api";

        var resource = meaningfulSegments[0];
        
        // Determine operation type based on method and path structure
        return httpMethod.ToUpperInvariant() switch
        {
            "GET" when meaningfulSegments.Count == 1 => $"List{resource}",
            "GET" when meaningfulSegments.Count > 1 => $"Get{resource}ById",
            "POST" => $"Create{resource}",
            "PUT" => $"Update{resource}",
            "PATCH" => $"Patch{resource}",
            "DELETE" => $"Delete{resource}",
            _ => $"{httpMethod}_{resource}"
        };
    }
}
