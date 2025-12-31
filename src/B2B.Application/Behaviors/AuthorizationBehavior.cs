using System.Reflection;
using B2B.Application.Attributes;
using B2B.Application.Interfaces.Services;
using B2B.Domain.Exceptions;
using MediatR;
using Microsoft.Extensions.Logging;

namespace B2B.Application.Behaviors;

/// <summary>
/// MediatR pipeline behavior that enforces RBAC and ABAC authorization.
/// Checks user roles and permissions before allowing handler execution.
/// </summary>
/// <typeparam name="TRequest">The type of request being handled.</typeparam>
/// <typeparam name="TResponse">The type of response returned.</typeparam>
public class AuthorizationBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    private readonly ICurrentUserService _currentUserService;
    private readonly IPermissionService _permissionService;
    private readonly ILogger<AuthorizationBehavior<TRequest, TResponse>> _logger;

    public AuthorizationBehavior(
        ICurrentUserService currentUserService,
        IPermissionService permissionService,
        ILogger<AuthorizationBehavior<TRequest, TResponse>> logger)
    {
        _currentUserService = currentUserService;
        _permissionService = permissionService;
        _logger = logger;
    }

    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        var requestType = typeof(TRequest);

        // Check if anonymous access is allowed
        if (requestType.GetCustomAttribute<AllowAnonymousAttribute>() != null)
        {
            return await next();
        }

        // Get authorization requirements
        var requiredRoles = requestType
            .GetCustomAttributes<RequireRoleAttribute>()
            .Select(a => a.Role)
            .ToList();

        var requiredPermissions = requestType
            .GetCustomAttributes<RequirePermissionAttribute>()
            .Select(a => a.Permission)
            .ToList();

        // If no authorization requirements, proceed
        if (!requiredRoles.Any() && !requiredPermissions.Any())
        {
            return await next();
        }

        // Check if user is authenticated
        if (!_currentUserService.IsAuthenticated)
        {
            _logger.LogWarning(
                "Unauthorized access attempt to {RequestName}. User not authenticated.",
                requestType.Name);
            throw new UnauthorizedException("Authentication required.");
        }

        var userId = _currentUserService.UserId!;
        var tenantId = _currentUserService.TenantId ?? string.Empty;
        var userRoles = _currentUserService.Roles.ToList();

        // RBAC Check - Evaluate role requirements first
        if (requiredRoles.Any())
        {
            var hasRequiredRole = requiredRoles.Any(role => 
                userRoles.Contains(role, StringComparer.OrdinalIgnoreCase));

            if (!hasRequiredRole)
            {
                _logger.LogWarning(
                    "Authorization failed for {RequestName}. User {UserId} lacks required roles: {RequiredRoles}",
                    requestType.Name,
                    userId,
                    string.Join(", ", requiredRoles));
                throw new ForbiddenException($"Required roles: {string.Join(", ", requiredRoles)}");
            }
        }

        // ABAC Check - Evaluate permission requirements second
        if (requiredPermissions.Any())
        {
            foreach (var permission in requiredPermissions)
            {
                var hasPermission = await _permissionService.HasPermissionAsync(
                    userId,
                    tenantId,
                    permission,
                    cancellationToken);

                if (!hasPermission)
                {
                    _logger.LogWarning(
                        "Authorization failed for {RequestName}. User {UserId} lacks permission: {Permission}",
                        requestType.Name,
                        userId,
                        permission);
                    throw new ForbiddenException($"Required permission: {permission}");
                }
            }
        }

        _logger.LogDebug(
            "Authorization succeeded for {RequestName}. User: {UserId}, Tenant: {TenantId}",
            requestType.Name,
            userId,
            tenantId);

        return await next();
    }
}
