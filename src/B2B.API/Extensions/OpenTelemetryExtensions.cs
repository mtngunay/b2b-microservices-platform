using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

namespace B2B.API.Extensions;

/// <summary>
/// Extension methods for configuring OpenTelemetry tracing and metrics.
/// </summary>
public static class OpenTelemetryExtensions
{
    /// <summary>
    /// Adds OpenTelemetry tracing with Jaeger exporter and instrumentation
    /// for HTTP, EF Core, Redis, and RabbitMQ.
    /// </summary>
    public static IServiceCollection AddOpenTelemetryTracing(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var serviceName = configuration["OpenTelemetry:ServiceName"] ?? "B2B.API";
        var jaegerEndpoint = configuration["OpenTelemetry:JaegerEndpoint"];

        services.AddOpenTelemetry()
            .ConfigureResource(resource => resource
                .AddService(
                    serviceName: serviceName,
                    serviceVersion: typeof(OpenTelemetryExtensions).Assembly.GetName().Version?.ToString() ?? "1.0.0",
                    serviceInstanceId: Environment.MachineName)
                .AddAttributes(new Dictionary<string, object>
                {
                    ["deployment.environment"] = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Production",
                    ["host.name"] = Environment.MachineName
                }))
            .WithTracing(tracing =>
            {
                tracing
                    // ASP.NET Core instrumentation
                    .AddAspNetCoreInstrumentation(options =>
                    {
                        options.RecordException = true;
                        options.Filter = httpContext =>
                        {
                            // Filter out health check endpoints from tracing
                            var path = httpContext.Request.Path.Value;
                            return path != null && 
                                   !path.StartsWith("/health", StringComparison.OrdinalIgnoreCase) &&
                                   !path.StartsWith("/metrics", StringComparison.OrdinalIgnoreCase);
                        };
                        options.EnrichWithHttpRequest = (activity, request) =>
                        {
                            // Add correlation ID to trace
                            if (request.HttpContext.Items.TryGetValue("CorrelationId", out var correlationId))
                            {
                                activity.SetTag("correlation.id", correlationId?.ToString());
                            }
                        };
                        options.EnrichWithHttpResponse = (activity, response) =>
                        {
                            activity.SetTag("http.response.status_code", response.StatusCode);
                        };
                    })
                    // HTTP client instrumentation
                    .AddHttpClientInstrumentation(options =>
                    {
                        options.RecordException = true;
                        options.FilterHttpRequestMessage = request =>
                        {
                            // Filter out health check calls
                            return request.RequestUri?.PathAndQuery.StartsWith("/health") != true;
                        };
                    })
                    // SQL Client instrumentation
                    .AddSqlClientInstrumentation(options =>
                    {
                        options.SetDbStatementForText = true;
                        options.RecordException = true;
                    })
                    // Add custom activity source for application-specific traces
                    .AddSource("B2B.API")
                    .AddSource("B2B.Application")
                    .AddSource("B2B.Infrastructure");

                // Configure Jaeger exporter if endpoint is provided
                if (!string.IsNullOrEmpty(jaegerEndpoint))
                {
                    tracing.AddJaegerExporter(options =>
                    {
                        var uri = new Uri(jaegerEndpoint);
                        options.AgentHost = uri.Host;
                        options.AgentPort = uri.Port > 0 ? uri.Port : 6831;
                    });
                }
            })
            .WithMetrics(metrics =>
            {
                metrics
                    .AddAspNetCoreInstrumentation()
                    .AddHttpClientInstrumentation()
                    // Add custom meters
                    .AddMeter("B2B.API")
                    .AddMeter("B2B.Application")
                    .AddMeter("B2B.Infrastructure");

                // Add Prometheus exporter
                metrics.AddPrometheusExporter();
            });

        return services;
    }

    /// <summary>
    /// Maps the Prometheus metrics endpoint.
    /// </summary>
    public static IEndpointRouteBuilder MapPrometheusMetrics(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPrometheusScrapingEndpoint("/metrics");
        return endpoints;
    }
}
