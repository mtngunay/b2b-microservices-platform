using Asp.Versioning;
using Microsoft.AspNetCore.Mvc;

namespace B2B.API.Controllers;

/// <summary>
/// Base controller for all API controllers with versioning support.
/// </summary>
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/[controller]")]
public abstract class ApiControllerBase : ControllerBase
{
    /// <summary>
    /// Adds deprecation headers to the response.
    /// </summary>
    /// <param name="sunsetDate">The date when the API version will be sunset.</param>
    /// <param name="deprecationMessage">Optional deprecation message.</param>
    protected void AddDeprecationHeaders(DateTimeOffset sunsetDate, string? deprecationMessage = null)
    {
        Response.Headers.Append("Sunset", sunsetDate.ToString("R"));
        Response.Headers.Append("Deprecation", "true");
        
        if (!string.IsNullOrEmpty(deprecationMessage))
        {
            Response.Headers.Append("X-Deprecation-Message", deprecationMessage);
        }
    }

    /// <summary>
    /// Adds a link header pointing to the newer API version.
    /// </summary>
    /// <param name="newVersionUrl">The URL of the newer API version.</param>
    protected void AddUpgradeLink(string newVersionUrl)
    {
        Response.Headers.Append("Link", $"<{newVersionUrl}>; rel=\"successor-version\"");
    }
}
