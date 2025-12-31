using System.Net;
using System.Text.Json;
using B2B.Application.DTOs;
using B2B.Application.Interfaces.Services;
using B2B.Domain.Exceptions;

namespace B2B.API.Middleware;

/// <summary>
/// Global exception handler middleware that catches all unhandled exceptions
/// and returns standardized error responses with correlation IDs.
/// </summary>
public class GlobalExceptionHandlerMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<GlobalExceptionHandlerMiddleware> _logger;
    private readonly IHostEnvironment _environment;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false
    };

    public GlobalExceptionHandlerMiddleware(
        RequestDelegate next,
        ILogger<GlobalExceptionHandlerMiddleware> logger,
        IHostEnvironment environment)
    {
        _next = next ?? throw new ArgumentNullException(nameof(next));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _environment = environment ?? throw new ArgumentNullException(nameof(environment));
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (Exception ex)
        {
            await HandleExceptionAsync(context, ex);
        }
    }

    private async Task HandleExceptionAsync(HttpContext context, Exception exception)
    {
        var correlationId = context.Items["CorrelationId"]?.ToString() ?? Guid.NewGuid().ToString();

        _logger.LogError(
            exception,
            "Unhandled exception occurred. CorrelationId: {CorrelationId}, Path: {Path}, Method: {Method}",
            correlationId,
            context.Request.Path,
            context.Request.Method);

        var (statusCode, errorResponse) = exception switch
        {
            ValidationException validationEx => (
                HttpStatusCode.BadRequest,
                ApiErrorResponse.ValidationError(correlationId, 
                    validationEx.Errors.ToDictionary(k => k.Key, v => v.Value))),

            NotFoundException notFoundEx => (
                HttpStatusCode.NotFound,
                ApiErrorResponse.NotFound(correlationId, notFoundEx.Message)),

            UnauthorizedException unauthorizedEx => (
                HttpStatusCode.Unauthorized,
                ApiErrorResponse.Unauthorized(correlationId, unauthorizedEx.Message)),

            ForbiddenException forbiddenEx => (
                HttpStatusCode.Forbidden,
                ApiErrorResponse.Forbidden(correlationId, forbiddenEx.Message)),

            ConflictException conflictEx => (
                HttpStatusCode.Conflict,
                ApiErrorResponse.Conflict(correlationId, conflictEx.Message)),

            DomainException domainEx => (
                HttpStatusCode.BadRequest,
                new ApiErrorResponse
                {
                    CorrelationId = correlationId,
                    ErrorCode = domainEx.ErrorCode,
                    Message = domainEx.Message,
                    Timestamp = DateTime.UtcNow
                }),

            OperationCanceledException => (
                HttpStatusCode.BadRequest,
                new ApiErrorResponse
                {
                    CorrelationId = correlationId,
                    ErrorCode = "REQUEST_CANCELLED",
                    Message = "The request was cancelled.",
                    Timestamp = DateTime.UtcNow
                }),

            _ => (
                HttpStatusCode.InternalServerError,
                CreateInternalErrorResponse(correlationId, exception))
        };

        context.Response.StatusCode = (int)statusCode;
        context.Response.ContentType = "application/json";

        var json = JsonSerializer.Serialize(errorResponse, JsonOptions);
        await context.Response.WriteAsync(json);
    }

    private ApiErrorResponse CreateInternalErrorResponse(string correlationId, Exception exception)
    {
        var response = ApiErrorResponse.InternalError(correlationId);

        // Include exception details only in development
        if (_environment.IsDevelopment())
        {
            response.Details = exception.ToString();
        }

        return response;
    }
}

/// <summary>
/// Extension methods for GlobalExceptionHandlerMiddleware.
/// </summary>
public static class GlobalExceptionHandlerMiddlewareExtensions
{
    /// <summary>
    /// Adds the global exception handler middleware to the application pipeline.
    /// </summary>
    public static IApplicationBuilder UseGlobalExceptionHandler(this IApplicationBuilder builder)
    {
        return builder.UseMiddleware<GlobalExceptionHandlerMiddleware>();
    }
}
