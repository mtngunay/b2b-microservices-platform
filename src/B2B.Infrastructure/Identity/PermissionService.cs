using B2B.Application.Interfaces.Services;
using B2B.Infrastructure.Persistence.WriteDb;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace B2B.Infrastructure.Identity;

/// <summary>
/// Permission service with Redis caching for efficient permission lookups.
/// </summary>
public class PermissionService : IPermissionService
{
    private readonly WriteDbContext _dbContext;
    private readonly ICacheService _cacheService;
    private readonly ILogger<PermissionService> _logger;

    private const string PermissionsCacheKeyPrefix = "permissions";
    private static readonly TimeSpan PermissionsCacheExpiry = TimeSpan.FromMinutes(15);

    /// <summary>
    /// Initializes a new instance of PermissionService.
    /// </summary>
    /// <param name="dbContext">The write database context.</param>
    /// <param name="cacheService">The cache service.</param>
    /// <param name="logger">Logger instance.</param>
    public PermissionService(
        WriteDbContext dbContext,
        ICacheService cacheService,
        ILogger<PermissionService> logger)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        _cacheService = cacheService ?? throw new ArgumentNullException(nameof(cacheService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc />
    public async Task<IEnumerable<string>> GetUserPermissionsAsync(
        string userId,
        string tenantId,
        CancellationToken cancellationToken = default)
    {
        var cacheKey = BuildPermissionsCacheKey(userId, tenantId);

        // Try to get from cache first
        var cachedPermissions = await _cacheService.GetAsync<List<string>>(cacheKey, cancellationToken);
        if (cachedPermissions != null)
        {
            _logger.LogDebug(
                "Permissions cache hit for user {UserId} in tenant {TenantId}",
                userId,
                tenantId);
            return cachedPermissions;
        }

        _logger.LogDebug(
            "Permissions cache miss for user {UserId} in tenant {TenantId}. Loading from database.",
            userId,
            tenantId);

        // Load permissions from database
        var permissions = await LoadUserPermissionsFromDatabaseAsync(userId, tenantId, cancellationToken);

        // Cache the permissions
        await _cacheService.SetAsync(
            cacheKey,
            permissions.ToList(),
            PermissionsCacheExpiry,
            cancellationToken);

        _logger.LogInformation(
            "Loaded and cached {Count} permissions for user {UserId} in tenant {TenantId}",
            permissions.Count(),
            userId,
            tenantId);

        return permissions;
    }

    /// <inheritdoc />
    public async Task<bool> HasPermissionAsync(
        string userId,
        string tenantId,
        string permission,
        CancellationToken cancellationToken = default)
    {
        var permissions = await GetUserPermissionsAsync(userId, tenantId, cancellationToken);
        var hasPermission = permissions.Contains(permission, StringComparer.OrdinalIgnoreCase);

        _logger.LogDebug(
            "Permission check for user {UserId}: {Permission} = {Result}",
            userId,
            permission,
            hasPermission);

        return hasPermission;
    }

    /// <inheritdoc />
    public async Task InvalidatePermissionCacheAsync(
        string userId,
        CancellationToken cancellationToken = default)
    {
        // Remove all permission cache entries for this user across all tenants
        var pattern = $"{PermissionsCacheKeyPrefix}:*:{userId}";
        await _cacheService.RemoveByPatternAsync(pattern, cancellationToken);

        _logger.LogInformation(
            "Invalidated permission cache for user {UserId}",
            userId);
    }

    /// <summary>
    /// Loads user permissions from the database.
    /// Combines direct user permissions and role-based permissions.
    /// </summary>
    private async Task<IEnumerable<string>> LoadUserPermissionsFromDatabaseAsync(
        string userId,
        string tenantId,
        CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(userId, out var userGuid))
        {
            _logger.LogWarning("Invalid user ID format: {UserId}", userId);
            return Enumerable.Empty<string>();
        }

        // Get direct user permissions
        var directPermissions = await _dbContext.UserPermissions
            .AsNoTracking()
            .Where(up => up.UserId == userGuid)
            .Include(up => up.Permission)
            .Where(up => up.Permission != null && up.Permission.TenantId == tenantId)
            .Select(up => up.Permission!.Name)
            .ToListAsync(cancellationToken);

        // Get role-based permissions
        var rolePermissions = await _dbContext.UserRoles
            .AsNoTracking()
            .Where(ur => ur.UserId == userGuid)
            .Include(ur => ur.Role)
                .ThenInclude(r => r!.RolePermissions)
                    .ThenInclude(rp => rp.Permission)
            .Where(ur => ur.Role != null && ur.Role.TenantId == tenantId)
            .SelectMany(ur => ur.Role!.RolePermissions
                .Where(rp => rp.Permission != null)
                .Select(rp => rp.Permission!.Name))
            .ToListAsync(cancellationToken);

        // Combine and deduplicate permissions
        var allPermissions = directPermissions
            .Union(rolePermissions, StringComparer.OrdinalIgnoreCase)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(p => p)
            .ToList();

        return allPermissions;
    }

    /// <summary>
    /// Builds the cache key for user permissions.
    /// </summary>
    private static string BuildPermissionsCacheKey(string userId, string tenantId)
    {
        return $"{PermissionsCacheKeyPrefix}:{tenantId}:{userId}";
    }
}
