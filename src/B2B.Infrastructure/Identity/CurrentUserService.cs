using System.Security.Claims;
using B2B.Application.Interfaces.Services;
using Microsoft.AspNetCore.Http;

namespace B2B.Infrastructure.Identity;

/// <summary>
/// Provides access to the current authenticated user's information from HttpContext.
/// </summary>
public class CurrentUserService : ICurrentUserService
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    /// <summary>
    /// Initializes a new instance of CurrentUserService.
    /// </summary>
    /// <param name="httpContextAccessor">The HTTP context accessor.</param>
    public CurrentUserService(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor ?? throw new ArgumentNullException(nameof(httpContextAccessor));
    }

    /// <inheritdoc />
    public string? UserId => _httpContextAccessor.HttpContext?.User?.FindFirstValue(ClaimTypes.NameIdentifier)
        ?? _httpContextAccessor.HttpContext?.User?.FindFirstValue("sub");

    /// <inheritdoc />
    public string? TenantId => _httpContextAccessor.HttpContext?.User?.FindFirstValue("tenant_id");

    /// <inheritdoc />
    public IEnumerable<string> Roles => _httpContextAccessor.HttpContext?.User?
        .FindAll(ClaimTypes.Role)
        .Select(c => c.Value) ?? Enumerable.Empty<string>();

    /// <inheritdoc />
    public bool IsAuthenticated => _httpContextAccessor.HttpContext?.User?.Identity?.IsAuthenticated ?? false;
}
