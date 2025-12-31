using B2B.Application.Interfaces.CQRS;
using B2B.Application.Interfaces.Persistence;
using B2B.Application.Interfaces.Services;
using B2B.Domain.Entities;
using B2B.Domain.Events;
using B2B.Domain.Exceptions;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace B2B.Application.Commands.Roles;

/// <summary>
/// Handler for DeleteRoleCommand.
/// </summary>
public class DeleteRoleCommandHandler : ICommandHandler<DeleteRoleCommand>
{
    private readonly IApplicationDbContext _dbContext;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUserService _currentUserService;
    private readonly IOutboxService _outboxService;
    private readonly ICorrelationIdAccessor _correlationIdAccessor;
    private readonly ILogger<DeleteRoleCommandHandler> _logger;

    public DeleteRoleCommandHandler(
        IApplicationDbContext dbContext,
        IUnitOfWork unitOfWork,
        ICurrentUserService currentUserService,
        IOutboxService outboxService,
        ICorrelationIdAccessor correlationIdAccessor,
        ILogger<DeleteRoleCommandHandler> logger)
    {
        _dbContext = dbContext;
        _unitOfWork = unitOfWork;
        _currentUserService = currentUserService;
        _outboxService = outboxService;
        _correlationIdAccessor = correlationIdAccessor;
        _logger = logger;
    }

    public async Task<Unit> Handle(
        DeleteRoleCommand request,
        CancellationToken cancellationToken)
    {
        var tenantId = _currentUserService.TenantId 
            ?? throw new InvalidOperationException("Tenant context is required");

        var role = await _dbContext.Roles
            .Include(r => r.UserRoles)
            .FirstOrDefaultAsync(r => r.Id == request.Id, cancellationToken)
            ?? throw new NotFoundException(nameof(Role), request.Id);

        if (role.IsSystemRole)
        {
            throw new ForbiddenException("System roles cannot be deleted");
        }

        // Get affected users before deletion
        var affectedUserIds = role.UserRoles.Select(ur => ur.UserId).ToList();
        var correlationId = _correlationIdAccessor.CorrelationId ?? Guid.NewGuid().ToString();

        // Soft delete the role
        _dbContext.Roles.Remove(role);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        // Publish PermissionChangedEvent for all affected users
        foreach (var userId in affectedUserIds)
        {
            var permissionChangedEvent = new PermissionChangedEvent(userId, tenantId, correlationId);
            await _outboxService.AddEventAsync(permissionChangedEvent, cancellationToken);
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Role {RoleId} deleted. {AffectedUserCount} users affected.",
            request.Id, affectedUserIds.Count);

        return Unit.Value;
    }
}
