namespace B2B.Application.Interfaces.Services;

/// <summary>
/// Provides access to the current authenticated user's information.
/// </summary>
public interface ICurrentUserService
{
    /// <summary>
    /// Gets the current user's identifier.
    /// </summary>
    string? UserId { get; }

    /// <summary>
    /// Gets the current user's tenant identifier.
    /// </summary>
    string? TenantId { get; }

    /// <summary>
    /// Gets the current user's roles.
    /// </summary>
    IEnumerable<string> Roles { get; }

    /// <summary>
    /// Gets a value indicating whether the user is authenticated.
    /// </summary>
    bool IsAuthenticated { get; }
}
