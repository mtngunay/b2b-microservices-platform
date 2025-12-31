using Asp.Versioning;

namespace B2B.API.Extensions;

/// <summary>
/// Extension methods for configuring API versioning.
/// </summary>
public static class ApiVersioningExtensions
{
    /// <summary>
    /// Adds API versioning services to the DI container.
    /// Supports URL-based versioning (/api/v1/) and header-based versioning (api-version header).
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddApiVersioningConfiguration(this IServiceCollection services)
    {
        services.AddApiVersioning(options =>
        {
            // Set default version when not specified
            options.DefaultApiVersion = new ApiVersion(1, 0);
            
            // Assume default version when not specified
            options.AssumeDefaultVersionWhenUnspecified = true;
            
            // Report API versions in response headers
            options.ReportApiVersions = true;
            
            // Configure version readers (URL segment and header-based)
            options.ApiVersionReader = ApiVersionReader.Combine(
                new UrlSegmentApiVersionReader(),
                new HeaderApiVersionReader("api-version"),
                new QueryStringApiVersionReader("api-version")
            );
        })
        .AddApiExplorer(options =>
        {
            // Format the version as "'v'major[.minor][-status]"
            options.GroupNameFormat = "'v'VVV";
            
            // Substitute the version in the URL
            options.SubstituteApiVersionInUrl = true;
        });

        return services;
    }
}
