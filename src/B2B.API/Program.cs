using B2B.API.Extensions;
using B2B.API.Logging;
using B2B.API.Middleware;
using Serilog;

// Configure Serilog early to capture startup errors
Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Debug()
    .WriteTo.Console()
    .CreateBootstrapLogger();

try
{
    Log.Information("Starting B2B API application");

    var builder = WebApplication.CreateBuilder(args);

    // Configure Serilog with structured logging
    builder.AddSerilogLogging();

    // Add services to the container
    builder.Services.AddOpenApi();
    builder.Services.AddControllers();
    builder.Services.AddEndpointsApiExplorer();

    // Add API versioning
    builder.Services.AddApiVersioningConfiguration();

    // Add Swagger/OpenAPI
    builder.Services.AddSwaggerGen(c =>
    {
        c.SwaggerDoc("v1", new Microsoft.OpenApi.Models.OpenApiInfo
        {
            Title = "B2B Platform API",
            Version = "v1",
            Description = "B2B Platform REST API with CQRS and Event Sourcing",
            Contact = new Microsoft.OpenApi.Models.OpenApiContact
            {
                Name = "B2B Platform Team",
                Email = "support@b2b-platform.com"
            }
        });
        
        // Add JWT Authentication to Swagger
        c.AddSecurityDefinition("Bearer", new Microsoft.OpenApi.Models.OpenApiSecurityScheme
        {
            Description = "JWT Authorization header using the Bearer scheme. Enter 'Bearer' [space] and then your token.",
            Name = "Authorization",
            In = Microsoft.OpenApi.Models.ParameterLocation.Header,
            Type = Microsoft.OpenApi.Models.SecuritySchemeType.ApiKey,
            Scheme = "Bearer"
        });
        
        c.AddSecurityRequirement(new Microsoft.OpenApi.Models.OpenApiSecurityRequirement
        {
            {
                new Microsoft.OpenApi.Models.OpenApiSecurityScheme
                {
                    Reference = new Microsoft.OpenApi.Models.OpenApiReference
                    {
                        Type = Microsoft.OpenApi.Models.ReferenceType.SecurityScheme,
                        Id = "Bearer"
                    }
                },
                Array.Empty<string>()
            }
        });
    });

    // Add CorrelationId services
    builder.Services.AddCorrelationId();

    // Add OpenTelemetry tracing and metrics
    builder.Services.AddOpenTelemetryTracing(builder.Configuration);

    // Add application services (MediatR, FluentValidation, AutoMapper)
    builder.Services.AddApplicationServices(builder.Configuration);

    // Add infrastructure services (EF Core, MongoDB, Redis, Repositories)
    builder.Services.AddInfrastructureServices(builder.Configuration);

    // Add JWT authentication
    builder.Services.AddJwtAuthentication(builder.Configuration);

    // Add response compression
    builder.Services.AddCustomResponseCompression();

    // Add health checks
    builder.Services.AddCustomHealthChecks(builder.Configuration);

    // Add rate limiting
    builder.Services.AddCustomRateLimiting(builder.Configuration);

    // Add request trace service for detailed logging
    builder.Services.AddScoped<IRequestTraceService, RequestTraceService>();

    var app = builder.Build();

    // Configure the HTTP request pipeline
    // Enable Swagger in all environments
    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "B2B Platform API v1");
        c.RoutePrefix = "swagger";
        c.DocumentTitle = "B2B Platform API";
    });
    
    if (app.Environment.IsDevelopment())
    {
        app.MapOpenApi();
    }

    // Response compression (should be early in pipeline)
    app.UseResponseCompression();

    // Global exception handler (should be early to catch all exceptions)
    app.UseGlobalExceptionHandler();

    // Add CorrelationId middleware (should be early in the pipeline)
    app.UseCorrelationId();

    // Add detailed logging middleware (after correlation ID)
    app.UseDetailedLogging();

    // Add custom metrics middleware
    app.UseMetricsMiddleware();

    // Add Serilog request logging
    app.UseSerilogRequestLoggingMiddleware();

    // Rate limiting middleware
    var rateLimitingOptions = app.Services.GetRequiredService<RateLimitingOptions>();
    app.UseCustomRateLimiting(rateLimitingOptions);

    app.UseHttpsRedirection();

    // Authentication & Authorization
    app.UseAuthentication();
    app.UseAuthorization();

    app.MapControllers();

    // Map Prometheus metrics endpoint
    app.MapPrometheusMetrics();

    // Map health check endpoints
    app.MapHealthCheckEndpoints();

    app.Run();
}
catch (Exception ex)
{
    Log.Fatal(ex, "Application terminated unexpectedly");
}
finally
{
    Log.CloseAndFlush();
}


// Make the implicit Program class public so test projects can access it
public partial class Program { }
