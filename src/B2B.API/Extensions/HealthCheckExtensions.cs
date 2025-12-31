using System.Text.Json;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace B2B.API.Extensions;

/// <summary>
/// Extension methods for configuring health check endpoints.
/// </summary>
public static class HealthCheckExtensions
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true
    };

    /// <summary>
    /// Maps health check endpoints for liveness and readiness probes.
    /// </summary>
    public static IEndpointRouteBuilder MapHealthCheckEndpoints(this IEndpointRouteBuilder endpoints)
    {
        // Liveness probe - checks if the application is running
        endpoints.MapHealthChecks("/health/live", new HealthCheckOptions
        {
            Predicate = _ => false, // No dependency checks for liveness
            ResponseWriter = WriteLivenessResponse
        });

        // Readiness probe - checks if the application can handle requests
        endpoints.MapHealthChecks("/health/ready", new HealthCheckOptions
        {
            Predicate = check => check.Tags.Contains("ready"),
            ResponseWriter = WriteReadinessResponse
        });

        // Detailed health check endpoint (for debugging/monitoring)
        endpoints.MapHealthChecks("/health/details", new HealthCheckOptions
        {
            ResponseWriter = WriteDetailedResponse
        });

        return endpoints;
    }

    private static Task WriteLivenessResponse(HttpContext context, HealthReport report)
    {
        context.Response.ContentType = "application/json";

        var response = new
        {
            Status = "Healthy",
            Timestamp = DateTime.UtcNow
        };

        return context.Response.WriteAsync(JsonSerializer.Serialize(response, JsonOptions));
    }

    private static Task WriteReadinessResponse(HttpContext context, HealthReport report)
    {
        context.Response.ContentType = "application/json";

        var response = new
        {
            Status = report.Status.ToString(),
            Timestamp = DateTime.UtcNow,
            TotalDuration = report.TotalDuration.TotalMilliseconds,
            Checks = report.Entries.Select(e => new
            {
                Name = e.Key,
                Status = e.Value.Status.ToString(),
                Duration = e.Value.Duration.TotalMilliseconds,
                Description = e.Value.Description,
                Tags = e.Value.Tags
            })
        };

        return context.Response.WriteAsync(JsonSerializer.Serialize(response, JsonOptions));
    }

    private static Task WriteDetailedResponse(HttpContext context, HealthReport report)
    {
        context.Response.ContentType = "application/json";

        var response = new
        {
            Status = report.Status.ToString(),
            Timestamp = DateTime.UtcNow,
            TotalDuration = report.TotalDuration.TotalMilliseconds,
            Checks = report.Entries.Select(e => new
            {
                Name = e.Key,
                Status = e.Value.Status.ToString(),
                Duration = e.Value.Duration.TotalMilliseconds,
                Description = e.Value.Description,
                Tags = e.Value.Tags,
                Exception = e.Value.Exception?.Message,
                Data = e.Value.Data
            })
        };

        return context.Response.WriteAsync(JsonSerializer.Serialize(response, JsonOptions));
    }
}
