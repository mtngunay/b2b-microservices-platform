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
/// Handler for UpdateRoleCommand.
/// </summary>
public class UpdateRoleCommandHandler : ICommandHandler<UpdateRoleCommand, RoleDto>
{
    private readonly IApplicationDbContext _dbContext;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUserService _currentUserService;
    private readonly IOutboxService _outboxService;
    private readonly ICorrelationIdAccessor _correlationIdAccessor;
    private readonly IMapper _mapper;
    private readonly ILogger<UpdateRoleCommandHandler> _logger;

    public UpdateRoleCommandHandler(
        IApplicationDbContext dbContext,
        IUnitOfWork unitOfWork,
        ICurrentUserService currentUserService,
        IOutboxService outboxService,
        ICorrelationIdAccessor correlationIdAccessor,
        IMapper mapper,
        ILogger<UpdateRoleCommandHandler> logger)
    {
        _dbContext = dbContext;
        _unitOfWork = unitOfWork;
        _currentUserService = currentUserService;
        _outboxService = outboxService;
        _correlationIdAccessor = correlationIdAccessor;
        _mapper = mapper;
        _logger = logger;
    }

    public async Task<RoleDto> Handle(
        UpdateRoleCommand request,
        CancellationToken cancellationToken)
    {
        var tenantId = _currentUserService.TenantId 
            ?? throw new InvalidOperationException("Tenant context is required");

        var role = await _dbContext.Roles
            .Include(r => r.RolePermissions)
                .ThenInclude(rp => rp.Permission)
            .Include(r => r.UserRoles)
            .FirstOrDefaultAsync(r => r.Id == request.Id, cancellationToken)
            ?? throw new NotFoundException(nameof(Role), request.Id);

        if (role.IsSystemRole)
        {
            throw new ForbiddenException("System roles cannot be modified");
        }

        // Check if new name conflicts with existing role
        if (role.Name != request.Name)
        {
            var existingRole = await _dbContext.Roles
                .FirstOrDefaultAsync(r => r.Name == request.Name && r.Id != request.Id, cancellationToken);

            if (existingRole != null)
            {
                throw new ConflictException($"Role with name '{request.Name}' already exists");
            }
        }

        role.Update(request.Name, request.Description);

        // Update permissions
        var currentPermissionIds = role.RolePermissions.Select(rp => rp.PermissionId).ToList();
        var permissionsToRemove = currentPermissionIds.Except(request.PermissionIds).ToList();
        var permissionsToAdd = request.PermissionIds.Except(currentPermissionIds).ToList();

        foreach (var permissionId in permissionsToRemove)
        {
            role.RemovePermission(permissionId);
        }

        if (permissionsToAdd.Any())
        {
            var newPermissions = await _dbContext.Permissions
                .Where(p => permissionsToAdd.Contains(p.Id))
                .ToListAsync(cancellationToken);

            foreach (var permission in newPermissions)
            {
                role.AddPermission(permission);
            }
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        // Publish PermissionChangedEvent for all users with this role
        var affectedUserIds = role.UserRoles.Select(ur => ur.UserId).ToList();
        var correlationId = _correlationIdAccessor.CorrelationId ?? Guid.NewGuid().ToString();

        foreach (var userId in affectedUserIds)
        {
            var permissionChangedEvent = new PermissionChangedEvent(userId, tenantId, correlationId);
            await _outboxService.AddEventAsync(permissionChangedEvent, cancellationToken);
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Role {RoleId} updated. {AffectedUserCount} users affected.",
            role.Id, affectedUserIds.Count);

        var dto = _mapper.Map<RoleDto>(role);
        dto.Permissions = role.RolePermissions
            .Select(rp => rp.Permission?.Name ?? string.Empty)
            .Where(n => !string.IsNullOrEmpty(n))
            .ToList();

        return dto;
    }
}
