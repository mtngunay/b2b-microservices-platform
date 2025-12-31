namespace B2B.Application.Interfaces.Services;

/// <summary>
/// Service for managing and checking user permissions.
/// </summary>
public interface IPermissionService
{
    /// <summary>
    /// Gets all permissions for a user within a tenant.
    /// </summary>
    /// <param name="userId">The user identifier.</param>
    /// <param name="tenantId">The tenant identifier.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A collection of permission names.</returns>
    Task<IEnumerable<string>> GetUserPermissionsAsync(
        string userId,
        string tenantId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks if a user has a specific permission within a tenant.
    /// </summary>
    /// <param name="userId">The user identifier.</param>
    /// <param name="tenantId">The tenant identifier.</param>
    /// <param name="permission">The permission to check.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>True if the user has the permission; otherwise, false.</returns>
    Task<bool> HasPermissionAsync(
        string userId,
        string tenantId,
        string permission,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Invalidates the cached permissions for a user.
    /// </summary>
    /// <param name="userId">The user identifier.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task InvalidatePermissionCacheAsync(
        string userId,
        CancellationToken cancellationToken = default);
}
