using System.Diagnostics;
using System.Text.Json;
using B2B.Application.Interfaces.Services;
using MediatR;
using Microsoft.Extensions.Logging;

namespace B2B.Application.Behaviors;

/// <summary>
/// MediatR pipeline behavior that logs request execution with detailed stack trace.
/// Provides category-based logging (COMMAND, QUERY) with full execution details.
/// </summary>
/// <typeparam name="TRequest">The type of request being handled.</typeparam>
/// <typeparam name="TResponse">The type of response returned.</typeparam>
public class LoggingBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    private readonly ILogger<LoggingBehavior<TRequest, TResponse>> _logger;
    private readonly ICorrelationIdAccessor _correlationIdAccessor;
    private readonly ICurrentUserService _currentUserService;

    public LoggingBehavior(
        ILogger<LoggingBehavior<TRequest, TResponse>> logger,
        ICorrelationIdAccessor correlationIdAccessor,
        ICurrentUserService currentUserService)
    {
        _logger = logger;
        _correlationIdAccessor = correlationIdAccessor;
        _currentUserService = currentUserService;
    }

    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        var requestName = typeof(TRequest).Name;
        var correlationId = _correlationIdAccessor.CorrelationId;
        var category = GetRequestCategory(requestName);
        var userId = _currentUserService.UserId ?? "anonymous";
        var tenantId = _currentUserService.TenantId ?? "none";

        // Create execution context for detailed logging
        var executionContext = new
        {
            RequestName = requestName,
            Category = category,
            CorrelationId = correlationId,
            UserId = userId,
            TenantId = tenantId,
            RequestType = typeof(TRequest).FullName,
            ResponseType = typeof(TResponse).FullName,
            StartTime = DateTime.UtcNow
        };

        _logger.LogInformation(
            "[{Category}] Starting {RequestName} | CorrelationId: {CorrelationId} | User: {UserId} | Tenant: {TenantId}",
            category,
            requestName,
            correlationId,
            userId,
            tenantId);

        // Log request details (masked for sensitive data)
        LogRequestDetails(request, requestName, category, correlationId);

        var stopwatch = Stopwatch.StartNew();

        try
        {
            _logger.LogDebug(
                "[{Category}] Step 1: Validation pipeline | {RequestName} | CorrelationId: {CorrelationId}",
                category, requestName, correlationId);

            var response = await next();
            stopwatch.Stop();

            _logger.LogDebug(
                "[{Category}] Step 2: Handler execution completed | {RequestName} | CorrelationId: {CorrelationId}",
                category, requestName, correlationId);

            // Log success with detailed metrics
            LogSuccessfulExecution(requestName, category, correlationId, stopwatch.ElapsedMilliseconds, userId, tenantId, response);

            return response;
        }
        catch (Exception ex)
        {
            stopwatch.Stop();

            // Log error with full stack trace
            LogFailedExecution(requestName, category, correlationId, stopwatch.ElapsedMilliseconds, userId, tenantId, ex);

            throw;
        }
    }

    private void LogRequestDetails(TRequest request, string requestName, string category, string correlationId)
    {
        try
        {
            // Serialize request for logging (with sensitive data masking)
            var requestJson = JsonSerializer.Serialize(request, new JsonSerializerOptions
            {
                WriteIndented = false,
                MaxDepth = 3
            });

            // Mask sensitive fields
            requestJson = MaskSensitiveFields(requestJson);

            _logger.LogDebug(
                "[{Category}] Request payload for {RequestName} | CorrelationId: {CorrelationId} | Payload: {Payload}",
                category,
                requestName,
                correlationId,
                requestJson.Length > 1000 ? requestJson[..1000] + "...[truncated]" : requestJson);
        }
        catch
        {
            _logger.LogDebug(
                "[{Category}] Could not serialize request payload for {RequestName} | CorrelationId: {CorrelationId}",
                category, requestName, correlationId);
        }
    }

    private void LogSuccessfulExecution(string requestName, string category, string correlationId, 
        long elapsedMs, string userId, string tenantId, TResponse? response)
    {
        // Determine if this is a CREATE operation
        var isCreateOperation = requestName.Contains("Create", StringComparison.OrdinalIgnoreCase) ||
                                requestName.Contains("Add", StringComparison.OrdinalIgnoreCase) ||
                                requestName.Contains("Register", StringComparison.OrdinalIgnoreCase);

        if (isCreateOperation)
        {
            _logger.LogInformation(
                "[{Category}] Resource created successfully via {RequestName} | " +
                "Duration: {DurationMs}ms | CorrelationId: {CorrelationId} | User: {UserId} | Tenant: {TenantId}",
                "CREATE",
                requestName,
                elapsedMs,
                correlationId,
                userId,
                tenantId);

            // Log response details for CREATE operations
            LogResponseDetails(response, requestName, "CREATE", correlationId);
        }
        else
        {
            _logger.LogInformation(
                "[{Category}] Completed {RequestName} | Duration: {DurationMs}ms | CorrelationId: {CorrelationId}",
                category,
                requestName,
                elapsedMs,
                correlationId);
        }

        // Log execution summary as structured data
        _logger.LogInformation(
            "[TRACE] Execution summary | Request: {RequestName} | Category: {Category} | " +
            "Duration: {DurationMs}ms | Status: Success | CorrelationId: {CorrelationId} | " +
            "User: {UserId} | Tenant: {TenantId} | Timestamp: {Timestamp}",
            requestName,
            category,
            elapsedMs,
            correlationId,
            userId,
            tenantId,
            DateTime.UtcNow.ToString("O"));
    }

    private void LogFailedExecution(string requestName, string category, string correlationId,
        long elapsedMs, string userId, string tenantId, Exception ex)
    {
        _logger.LogError(ex,
            "[{Category}] Failed {RequestName} | Duration: {DurationMs}ms | " +
            "CorrelationId: {CorrelationId} | User: {UserId} | Tenant: {TenantId} | " +
            "Error: {ErrorMessage} | ExceptionType: {ExceptionType}",
            category,
            requestName,
            elapsedMs,
            correlationId,
            userId,
            tenantId,
            ex.Message,
            ex.GetType().Name);

        // Log full stack trace for debugging
        _logger.LogError(
            "[TRACE] Full stack trace for {RequestName} | CorrelationId: {CorrelationId} | StackTrace: {StackTrace}",
            requestName,
            correlationId,
            ex.ToString());
    }

    private void LogResponseDetails(TResponse? response, string requestName, string category, string correlationId)
    {
        if (response == null) return;

        try
        {
            var responseJson = JsonSerializer.Serialize(response, new JsonSerializerOptions
            {
                WriteIndented = false,
                MaxDepth = 3
            });

            responseJson = MaskSensitiveFields(responseJson);

            _logger.LogDebug(
                "[{Category}] Response for {RequestName} | CorrelationId: {CorrelationId} | Response: {Response}",
                category,
                requestName,
                correlationId,
                responseJson.Length > 500 ? responseJson[..500] + "...[truncated]" : responseJson);
        }
        catch
        {
            // Ignore serialization errors
        }
    }

    private static string GetRequestCategory(string requestName)
    {
        if (requestName.EndsWith("Query", StringComparison.OrdinalIgnoreCase) ||
            requestName.StartsWith("Get", StringComparison.OrdinalIgnoreCase) ||
            requestName.StartsWith("List", StringComparison.OrdinalIgnoreCase))
        {
            return "QUERY";
        }

        if (requestName.Contains("Create", StringComparison.OrdinalIgnoreCase) ||
            requestName.Contains("Add", StringComparison.OrdinalIgnoreCase) ||
            requestName.Contains("Register", StringComparison.OrdinalIgnoreCase))
        {
            return "COMMAND_CREATE";
        }

        if (requestName.Contains("Update", StringComparison.OrdinalIgnoreCase) ||
            requestName.Contains("Modify", StringComparison.OrdinalIgnoreCase))
        {
            return "COMMAND_UPDATE";
        }

        if (requestName.Contains("Delete", StringComparison.OrdinalIgnoreCase) ||
            requestName.Contains("Remove", StringComparison.OrdinalIgnoreCase))
        {
            return "COMMAND_DELETE";
        }

        return "COMMAND";
    }

    private static string MaskSensitiveFields(string json)
    {
        var sensitiveFields = new[] { "password", "secret", "token", "apiKey", "api_key", "authorization", "passwordHash" };

        foreach (var field in sensitiveFields)
        {
            json = System.Text.RegularExpressions.Regex.Replace(
                json,
                $"\"{field}\"\\s*:\\s*\"[^\"]*\"",
                $"\"{field}\":\"***MASKED***\"",
                System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        }

        return json;
    }
}
