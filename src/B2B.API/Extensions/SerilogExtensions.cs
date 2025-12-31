using Serilog;
using Serilog.Events;
using Serilog.Formatting.Json;
using Serilog.Sinks.Elasticsearch;

namespace B2B.API.Extensions;

/// <summary>
/// Extension methods for configuring Serilog with structured logging.
/// </summary>
public static class SerilogExtensions
{
    /// <summary>
    /// Configures Serilog with structured logging, JSON formatting for ELK,
    /// and enrichers for machine name, environment, and correlation ID.
    /// </summary>
    public static WebApplicationBuilder AddSerilogLogging(this WebApplicationBuilder builder)
    {
        var configuration = builder.Configuration;
        var environment = builder.Environment;

        Log.Logger = new LoggerConfiguration()
            .ReadFrom.Configuration(configuration)
            .MinimumLevel.Debug()
            .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
            .MinimumLevel.Override("Microsoft.AspNetCore", LogEventLevel.Warning)
            .MinimumLevel.Override("Microsoft.EntityFrameworkCore", LogEventLevel.Warning)
            .MinimumLevel.Override("System", LogEventLevel.Warning)
            .Enrich.FromLogContext()
            .Enrich.WithMachineName()
            .Enrich.WithEnvironmentName()
            .Enrich.WithProperty("Application", "B2B.API")
            .Enrich.WithProperty("Environment", environment.EnvironmentName)
            .Enrich.WithProperty("Version", "1.0.0")
            .WriteTo.Console(
                outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] [{Category}] {Message:lj} {Properties:j}{NewLine}{Exception}")
            .ConfigureElasticsearchSink(configuration, environment)
            .CreateLogger();

        builder.Host.UseSerilog();

        return builder;
    }

    private static LoggerConfiguration ConfigureElasticsearchSink(
        this LoggerConfiguration loggerConfiguration,
        IConfiguration configuration,
        IWebHostEnvironment environment)
    {
        var elasticsearchUri = configuration["Elasticsearch:Uri"];
        var indexFormat = configuration["Elasticsearch:IndexFormat"] ?? "b2b-api-logs-{0:yyyy.MM.dd}";

        if (!string.IsNullOrEmpty(elasticsearchUri))
        {
            loggerConfiguration.WriteTo.Elasticsearch(new ElasticsearchSinkOptions(new Uri(elasticsearchUri))
            {
                AutoRegisterTemplate = true,
                AutoRegisterTemplateVersion = AutoRegisterTemplateVersion.ESv7,
                IndexFormat = indexFormat,
                MinimumLogEventLevel = environment.IsDevelopment() 
                    ? LogEventLevel.Debug 
                    : LogEventLevel.Information,
                NumberOfShards = 2,
                NumberOfReplicas = 1,
                EmitEventFailure = EmitEventFailureHandling.WriteToSelfLog |
                                   EmitEventFailureHandling.RaiseCallback,
                FailureCallback = (e, ex) => Console.WriteLine($"Unable to submit event to Elasticsearch: {e.MessageTemplate}. Error: {ex?.Message}"),
                ModifyConnectionSettings = conn => conn.BasicAuthentication(
                    configuration["Elasticsearch:Username"] ?? "",
                    configuration["Elasticsearch:Password"] ?? ""),
                // Custom index naming for different log categories
                CustomFormatter = new Serilog.Formatting.Elasticsearch.ElasticsearchJsonFormatter(
                    renderMessage: true,
                    inlineFields: true)
            });
        }

        return loggerConfiguration;
    }

    /// <summary>
    /// Adds Serilog request logging middleware with enhanced configuration.
    /// </summary>
    public static WebApplication UseSerilogRequestLoggingMiddleware(this WebApplication app)
    {
        app.UseSerilogRequestLogging(options =>
        {
            options.MessageTemplate = "[{Category}] HTTP {RequestMethod} {RequestPath} responded {StatusCode} in {Elapsed:0.0000} ms";
            options.GetLevel = (httpContext, elapsed, ex) => GetLogLevel(httpContext, elapsed, ex);
            options.EnrichDiagnosticContext = EnrichFromRequest;
        });

        return app;
    }

    private static LogEventLevel GetLogLevel(HttpContext httpContext, double elapsed, Exception? ex)
    {
        if (ex != null)
            return LogEventLevel.Error;

        if (httpContext.Response.StatusCode >= 500)
            return LogEventLevel.Error;

        if (httpContext.Response.StatusCode >= 400)
            return LogEventLevel.Warning;

        if (elapsed > 5000) // Slow request threshold: 5 seconds
            return LogEventLevel.Warning;

        // Health check endpoints at Debug level
        if (httpContext.Request.Path.StartsWithSegments("/health"))
            return LogEventLevel.Debug;

        return LogEventLevel.Information;
    }

    private static void EnrichFromRequest(IDiagnosticContext diagnosticContext, HttpContext httpContext)
    {
        var request = httpContext.Request;

        // Add category based on HTTP method
        var category = request.Method.ToUpperInvariant() switch
        {
            "GET" => "READ",
            "POST" => "CREATE",
            "PUT" => "UPDATE",
            "PATCH" => "UPDATE",
            "DELETE" => "DELETE",
            _ => "SYSTEM"
        };
        diagnosticContext.Set("Category", category);

        diagnosticContext.Set("Host", request.Host.Value);
        diagnosticContext.Set("Protocol", request.Protocol);
        diagnosticContext.Set("Scheme", request.Scheme);
        diagnosticContext.Set("QueryString", request.QueryString.HasValue ? request.QueryString.Value : string.Empty);
        diagnosticContext.Set("ContentType", request.ContentType ?? string.Empty);
        diagnosticContext.Set("ContentLength", request.ContentLength ?? 0);

        // Add user information if authenticated
        if (httpContext.User.Identity?.IsAuthenticated == true)
        {
            diagnosticContext.Set("UserId", httpContext.User.FindFirst("sub")?.Value ?? "unknown");
            diagnosticContext.Set("TenantId", httpContext.User.FindFirst("tenant_id")?.Value ?? "unknown");
        }

        // Add correlation ID
        if (httpContext.Items.TryGetValue("CorrelationId", out var correlationId))
        {
            diagnosticContext.Set("CorrelationId", correlationId?.ToString() ?? string.Empty);
        }

        // Add trace ID
        if (httpContext.Items.TryGetValue("TraceId", out var traceId))
        {
            diagnosticContext.Set("TraceId", traceId?.ToString() ?? string.Empty);
        }

        // Add client IP
        diagnosticContext.Set("ClientIp", httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown");

        // Add response status code
        diagnosticContext.Set("StatusCode", httpContext.Response.StatusCode);

        // Add request timing
        diagnosticContext.Set("RequestTimestamp", DateTime.UtcNow.ToString("O"));
    }
}
