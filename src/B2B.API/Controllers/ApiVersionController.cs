using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace B2B.API.Controllers;

/// <summary>
/// Controller for API version discovery.
/// </summary>
[ApiController]
[ApiVersionNeutral]
[Route("api/versions")]
[AllowAnonymous]
public class ApiVersionController : ControllerBase
{
    /// <summary>
    /// Gets information about available API versions.
    /// </summary>
    /// <returns>List of available API versions with their status.</returns>
    [HttpGet]
    [ProducesResponseType(typeof(ApiVersionInfo), StatusCodes.Status200OK)]
    public ActionResult<ApiVersionInfo> GetVersions()
    {
        var versionInfo = new ApiVersionInfo
        {
            CurrentVersion = "1.0",
            SupportedVersions = new List<VersionDetail>
            {
                new()
                {
                    Version = "1.0",
                    Status = "Current",
                    ReleaseDate = new DateTimeOffset(2024, 1, 1, 0, 0, 0, TimeSpan.Zero),
                    SunsetDate = null,
                    Description = "Initial stable API version"
                },
                new()
                {
                    Version = "2.0",
                    Status = "Preview",
                    ReleaseDate = new DateTimeOffset(2025, 1, 1, 0, 0, 0, TimeSpan.Zero),
                    SunsetDate = null,
                    Description = "Enhanced API with additional features and improved response models"
                }
            },
            DeprecatedVersions = new List<VersionDetail>()
        };

        return Ok(versionInfo);
    }
}

/// <summary>
/// Information about available API versions.
/// </summary>
public class ApiVersionInfo
{
    /// <summary>
    /// The current recommended API version.
    /// </summary>
    public string CurrentVersion { get; set; } = string.Empty;

    /// <summary>
    /// List of supported API versions.
    /// </summary>
    public List<VersionDetail> SupportedVersions { get; set; } = new();

    /// <summary>
    /// List of deprecated API versions.
    /// </summary>
    public List<VersionDetail> DeprecatedVersions { get; set; } = new();
}

/// <summary>
/// Details about a specific API version.
/// </summary>
public class VersionDetail
{
    /// <summary>
    /// The version number.
    /// </summary>
    public string Version { get; set; } = string.Empty;

    /// <summary>
    /// The status of this version (Current, Preview, Deprecated, Sunset).
    /// </summary>
    public string Status { get; set; } = string.Empty;

    /// <summary>
    /// The date this version was released.
    /// </summary>
    public DateTimeOffset ReleaseDate { get; set; }

    /// <summary>
    /// The date this version will be sunset (if deprecated).
    /// </summary>
    public DateTimeOffset? SunsetDate { get; set; }

    /// <summary>
    /// Description of this version.
    /// </summary>
    public string Description { get; set; } = string.Empty;
}
