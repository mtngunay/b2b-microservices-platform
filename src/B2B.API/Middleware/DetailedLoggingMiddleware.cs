using System.Diagnostics;
using System.Text;
using System.Text.Json;
using B2B.API.Logging;
using Serilog;
using Serilog.Context;
using Serilog.Events;

namespace B2B.API.Middleware;

/// <summary>
/// Middleware that provides detailed stack trace logging for each request.
/// Logs are categorized by HTTP method (READ, CREATE, UPDATE, DELETE).
/// </summary>
public class DetailedLoggingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<DetailedLoggingMiddleware> _logger;

    public DetailedLoggingMiddleware(RequestDelegate next, ILogger<DetailedLoggingMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context, IRequestTraceService traceService)
    {
        // Skip health check endpoints for detailed logging
        if (context.Request.Path.StartsWithSegments("/health") ||
            context.Request.Path.StartsWithSegments("/metrics"))
        {
            await _next(context);
            return;
        }

        var correlationId = context.Items.TryGetValue("CorrelationId", out var corrId) 
            ? corrId?.ToString() 
            : Guid.NewGuid().ToString();

        var trace = traceService.StartTrace(
            context.Request.Method,
            context.Request.Path,
            correlationId);

        // Set client IP
        trace.ClientIp = context.Connection.RemoteIpAddress?.ToString();

        // Push log context properties
        using (LogContext.PushProperty("TraceId", trace.TraceId))
        using (LogContext.PushProperty("SpanId", trace.SpanId))
        using (LogContext.PushProperty("Category", trace.Category))
        using (LogContext.PushProperty("OperationName", trace.OperationName))
        using (LogContext.PushProperty("HttpMethod", trace.HttpMethod))
        using (LogContext.PushProperty("RequestPath", trace.RequestPath))
        {
            // Log request start based on category
            LogRequestStart(trace);

            var stopwatch = Stopwatch.StartNew();

            try
            {
                // Capture request body for POST/PUT/PATCH
                if (ShouldCaptureRequestBody(context.Request.Method))
                {
                    await CaptureRequestBody(context, trace, traceService);
                }

                traceService.AddStep("MiddlewarePipeline", "Entering middleware pipeline", "ASP.NET Core");

                await _next(context);

                stopwatch.Stop();

                // Set user info after authentication
                if (context.User.Identity?.IsAuthenticated == true)
                {
                    var userId = context.User.FindFirst("sub")?.Value;
                    var tenantId = context.User.FindFirst("tenant_id")?.Value;
                    traceService.SetUserInfo(userId, tenantId);
                }

                traceService.CompleteTrace(context.Response.StatusCode);

                // Log completion based on category and status
                LogRequestCompletion(trace, stopwatch.ElapsedMilliseconds);
            }
            catch (Exception ex)
            {
                stopwatch.Stop();
                traceService.CompleteTrace(500, ex.Message, ex);

                // Log error with full stack trace
                LogRequestError(trace, ex, stopwatch.ElapsedMilliseconds);

                throw;
            }
        }
    }

    private void LogRequestStart(StackTraceInfo trace)
    {
        var logLevel = trace.Category switch
        {
            LogCategories.Read => LogLevel.Information,
            LogCategories.Create => LogLevel.Information,
            LogCategories.Update => LogLevel.Information,
            LogCategories.Delete => LogLevel.Warning, // Delete operations are more critical
            _ => LogLevel.Information
        };

        _logger.Log(logLevel,
            "[{Category}] Request started: {HttpMethod} {RequestPath} | TraceId: {TraceId}",
            trace.Category,
            trace.HttpMethod,
            trace.RequestPath,
            trace.TraceId);
    }

    private void LogRequestCompletion(StackTraceInfo trace, long elapsedMs)
    {
        var logLevel = GetLogLevelForStatus(trace.StatusCode ?? 200, trace.Category);

        // For CREATE operations, log detailed trace
        if (trace.Category == LogCategories.Create && trace.StatusCode is >= 200 and < 300)
        {
            _logger.Log(logLevel,
                "[{Category}] Resource created successfully: {HttpMethod} {RequestPath} | " +
                "Status: {StatusCode} | Duration: {DurationMs}ms | TraceId: {TraceId} | " +
                "Steps: {StepCount} | User: {UserId} | Tenant: {TenantId}",
                trace.Category,
                trace.HttpMethod,
                trace.RequestPath,
                trace.StatusCode,
                elapsedMs,
                trace.TraceId,
                trace.Steps.Count,
                trace.UserId ?? "anonymous",
                trace.TenantId ?? "none");

            // Log detailed steps for CREATE operations
            LogDetailedSteps(trace);
        }
        else
        {
            _logger.Log(logLevel,
                "[{Category}] Request completed: {HttpMethod} {RequestPath} | " +
                "Status: {StatusCode} | Duration: {DurationMs}ms | TraceId: {TraceId}",
                trace.Category,
                trace.HttpMethod,
                trace.RequestPath,
                trace.StatusCode,
                elapsedMs,
                trace.TraceId);
        }

        // Always log full trace as structured data for ELK
        LogStructuredTrace(trace);
    }

    private void LogRequestError(StackTraceInfo trace, Exception ex, long elapsedMs)
    {
        _logger.LogError(ex,
            "[{Category}] Request failed: {HttpMethod} {RequestPath} | " +
            "Duration: {DurationMs}ms | TraceId: {TraceId} | Error: {ErrorMessage}",
            trace.Category,
            trace.HttpMethod,
            trace.RequestPath,
            elapsedMs,
            trace.TraceId,
            ex.Message);

        // Log full trace with exception details
        LogStructuredTrace(trace);
    }

    private void LogDetailedSteps(StackTraceInfo trace)
    {
        foreach (var step in trace.Steps)
        {
            _logger.LogDebug(
                "[{Category}] Step {StepNumber}: {StepName} - {Description} | " +
                "Component: {Component} | ElapsedMs: {ElapsedMs} | TraceId: {TraceId}",
                trace.Category,
                step.StepNumber,
                step.StepName,
                step.Description,
                step.Component ?? "Unknown",
                step.ElapsedFromStart,
                trace.TraceId);
        }
    }

    private void LogStructuredTrace(StackTraceInfo trace)
    {
        // Log the complete trace as a structured object for ELK indexing
        using (LogContext.PushProperty("StackTrace", trace.ToDictionary(), destructureObjects: true))
        {
            _logger.LogInformation(
                "[TRACE] Complete request trace for {TraceId}",
                trace.TraceId);
        }
    }

    private static LogLevel GetLogLevelForStatus(int statusCode, string category)
    {
        return statusCode switch
        {
            >= 500 => LogLevel.Error,
            >= 400 => LogLevel.Warning,
            _ => category == LogCategories.Create ? LogLevel.Information : LogLevel.Information
        };
    }

    private static bool ShouldCaptureRequestBody(string method)
    {
        return method.Equals("POST", StringComparison.OrdinalIgnoreCase) ||
               method.Equals("PUT", StringComparison.OrdinalIgnoreCase) ||
               method.Equals("PATCH", StringComparison.OrdinalIgnoreCase);
    }

    private async Task CaptureRequestBody(HttpContext context, StackTraceInfo trace, IRequestTraceService traceService)
    {
        try
        {
            context.Request.EnableBuffering();

            using var reader = new StreamReader(
                context.Request.Body,
                Encoding.UTF8,
                detectEncodingFromByteOrderMarks: false,
                bufferSize: 1024,
                leaveOpen: true);

            var body = await reader.ReadToEndAsync();
            context.Request.Body.Position = 0;

            if (!string.IsNullOrWhiteSpace(body) && body.Length < 10000) // Limit body size
            {
                // Mask sensitive fields
                var maskedBody = MaskSensitiveData(body);
                traceService.AddMetadata("RequestBody", maskedBody);
            }
        }
        catch
        {
            // Ignore body capture errors
        }
    }

    private static string MaskSensitiveData(string body)
    {
        try
        {
            var sensitiveFields = new[] { "password", "secret", "token", "apikey", "api_key", "authorization" };
            
            foreach (var field in sensitiveFields)
            {
                // Simple regex-like replacement for JSON
                var patterns = new[]
                {
                    $"\"{field}\"\\s*:\\s*\"[^\"]*\"",
                    $"\"{field}\"\\s*:\\s*'[^']*'"
                };

                foreach (var pattern in patterns)
                {
                    body = System.Text.RegularExpressions.Regex.Replace(
                        body,
                        pattern,
                        $"\"{field}\":\"***MASKED***\"",
                        System.Text.RegularExpressions.RegexOptions.IgnoreCase);
                }
            }

            return body;
        }
        catch
        {
            return "[Body masking failed]";
        }
    }
}

/// <summary>
/// Extension methods for DetailedLoggingMiddleware.
/// </summary>
public static class DetailedLoggingMiddlewareExtensions
{
    /// <summary>
    /// Adds detailed logging middleware to the pipeline.
    /// </summary>
    public static IApplicationBuilder UseDetailedLogging(this IApplicationBuilder app)
    {
        return app.UseMiddleware<DetailedLoggingMiddleware>();
    }
}
