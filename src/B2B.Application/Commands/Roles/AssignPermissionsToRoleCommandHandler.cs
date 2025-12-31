using AutoMapper;
using B2B.Application.DTOs;
using B2B.Application.Interfaces.CQRS;
using B2B.Application.Interfaces.Persistence;
using B2B.Application.Interfaces.Services;
using B2B.Domain.Entities;
using B2B.Domain.Events;
using B2B.Domain.Exceptions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace B2B.Application.Commands.Roles;

/// <summary>
/// Handler for AssignPermissionsToRoleCommand.
/// </summary>
public class AssignPermissionsToRoleCommandHandler : ICommandHandler<AssignPermissionsToRoleCommand, RoleDto>
{
    private readonly IApplicationDbContext _dbContext;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUserService _currentUserService;
    private readonly IOutboxService _outboxService;
    private readonly ICorrelationIdAccessor _correlationIdAccessor;
    private readonly IPermissionService _permissionService;
    private readonly IMapper _mapper;
    private readonly ILogger<AssignPermissionsToRoleCommandHandler> _logger;

    public AssignPermissionsToRoleCommandHandler(
        IApplicationDbContext dbContext,
        IUnitOfWork unitOfWork,
        ICurrentUserService currentUserService,
        IOutboxService outboxService,
        ICorrelationIdAccessor correlationIdAccessor,
        IPermissionService permissionService,
        IMapper mapper,
        ILogger<AssignPermissionsToRoleCommandHandler> logger)
    {
        _dbContext = dbContext;
        _unitOfWork = unitOfWork;
        _currentUserService = currentUserService;
        _outboxService = outboxService;
        _correlationIdAccessor = correlationIdAccessor;
        _permissionService = permissionService;
        _mapper = mapper;
        _logger = logger;
    }

    public async Task<RoleDto> Handle(
        AssignPermissionsToRoleCommand request,
        CancellationToken cancellationToken)
    {
        var tenantId = _currentUserService.TenantId 
            ?? throw new InvalidOperationException("Tenant context is required");

        var role = await _dbContext.Roles
            .Include(r => r.RolePermissions)
                .ThenInclude(rp => rp.Permission)
            .Include(r => r.UserRoles)
            .FirstOrDefaultAsync(r => r.Id == request.RoleId, cancellationToken)
            ?? throw new NotFoundException(nameof(Role), request.RoleId);

        // Get permissions to add
        var permissions = await _dbContext.Permissions
            .Where(p => request.PermissionIds.Contains(p.Id))
            .ToListAsync(cancellationToken);

        foreach (var permission in permissions)
        {
            role.AddPermission(permission);
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        // Publish PermissionChangedEvent for all users with this role
        var affectedUserIds = role.UserRoles.Select(ur => ur.UserId).ToList();
        var correlationId = _correlationIdAccessor.CorrelationId ?? Guid.NewGuid().ToString();

        foreach (var userId in affectedUserIds)
        {
            var permissionChangedEvent = new PermissionChangedEvent(userId, tenantId, correlationId);
            await _outboxService.AddEventAsync(permissionChangedEvent, cancellationToken);
            
            // Invalidate permission cache immediately
            await _permissionService.InvalidatePermissionCacheAsync(userId.ToString(), cancellationToken);
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Assigned {PermissionCount} permissions to role {RoleId}. {AffectedUserCount} users affected.",
            permissions.Count, role.Id, affectedUserIds.Count);

        var dto = _mapper.Map<RoleDto>(role);
        dto.Permissions = role.RolePermissions
            .Select(rp => rp.Permission?.Name ?? string.Empty)
            .Where(n => !string.IsNullOrEmpty(n))
            .ToList();

        return dto;
    }
}
