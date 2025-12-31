using B2B.Application.Interfaces.CQRS;
using B2B.Application.Interfaces.Persistence;
using B2B.Domain.Entities;
using B2B.Domain.Exceptions;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace B2B.Application.Commands.Permissions;

/// <summary>
/// Handler for DeletePermissionCommand.
/// </summary>
public class DeletePermissionCommandHandler : ICommandHandler<DeletePermissionCommand>
{
    private readonly IApplicationDbContext _dbContext;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<DeletePermissionCommandHandler> _logger;

    public DeletePermissionCommandHandler(
        IApplicationDbContext dbContext,
        IUnitOfWork unitOfWork,
        ILogger<DeletePermissionCommandHandler> logger)
    {
        _dbContext = dbContext;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public async Task<Unit> Handle(
        DeletePermissionCommand request,
        CancellationToken cancellationToken)
    {
        var permission = await _dbContext.Permissions
            .FirstOrDefaultAsync(p => p.Id == request.Id, cancellationToken)
            ?? throw new NotFoundException(nameof(Permission), request.Id);

        _dbContext.Permissions.Remove(permission);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Permission {PermissionId} deleted", request.Id);

        return Unit.Value;
    }
}
