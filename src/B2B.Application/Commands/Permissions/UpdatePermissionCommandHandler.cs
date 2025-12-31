using AutoMapper;
using B2B.Application.DTOs;
using B2B.Application.Interfaces.CQRS;
using B2B.Application.Interfaces.Persistence;
using B2B.Application.Interfaces.Services;
using B2B.Domain.Entities;
using B2B.Domain.Exceptions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace B2B.Application.Commands.Permissions;

/// <summary>
/// Handler for UpdatePermissionCommand.
/// </summary>
public class UpdatePermissionCommandHandler : ICommandHandler<UpdatePermissionCommand, PermissionDto>
{
    private readonly IApplicationDbContext _dbContext;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IMapper _mapper;
    private readonly ILogger<UpdatePermissionCommandHandler> _logger;

    public UpdatePermissionCommandHandler(
        IApplicationDbContext dbContext,
        IUnitOfWork unitOfWork,
        IMapper mapper,
        ILogger<UpdatePermissionCommandHandler> logger)
    {
        _dbContext = dbContext;
        _unitOfWork = unitOfWork;
        _mapper = mapper;
        _logger = logger;
    }

    public async Task<PermissionDto> Handle(
        UpdatePermissionCommand request,
        CancellationToken cancellationToken)
    {
        var permission = await _dbContext.Permissions
            .FirstOrDefaultAsync(p => p.Id == request.Id, cancellationToken)
            ?? throw new NotFoundException(nameof(Permission), request.Id);

        permission.Update(request.Description);

        _dbContext.Permissions.Update(permission);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Permission {PermissionId} updated",
            permission.Id);

        return _mapper.Map<PermissionDto>(permission);
    }
}
