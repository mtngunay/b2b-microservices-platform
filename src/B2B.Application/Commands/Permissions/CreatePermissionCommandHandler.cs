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
/// Handler for CreatePermissionCommand.
/// </summary>
public class CreatePermissionCommandHandler : ICommandHandler<CreatePermissionCommand, PermissionDto>
{
    private readonly IApplicationDbContext _dbContext;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUserService _currentUserService;
    private readonly IMapper _mapper;
    private readonly ILogger<CreatePermissionCommandHandler> _logger;

    public CreatePermissionCommandHandler(
        IApplicationDbContext dbContext,
        IUnitOfWork unitOfWork,
        ICurrentUserService currentUserService,
        IMapper mapper,
        ILogger<CreatePermissionCommandHandler> logger)
    {
        _dbContext = dbContext;
        _unitOfWork = unitOfWork;
        _currentUserService = currentUserService;
        _mapper = mapper;
        _logger = logger;
    }

    public async Task<PermissionDto> Handle(
        CreatePermissionCommand request,
        CancellationToken cancellationToken)
    {
        var tenantId = _currentUserService.TenantId 
            ?? throw new InvalidOperationException("Tenant context is required");

        // Check if permission name already exists
        var existingPermission = await _dbContext.Permissions
            .FirstOrDefaultAsync(p => p.Name == request.Name, cancellationToken);

        if (existingPermission != null)
        {
            throw new ConflictException($"Permission with name '{request.Name}' already exists");
        }

        var permission = Permission.Create(
            request.Name,
            request.Resource,
            request.Action,
            request.Description,
            tenantId);

        await _dbContext.Permissions.AddAsync(permission, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Permission {PermissionName} created with ID {PermissionId} in tenant {TenantId}",
            permission.Name, permission.Id, tenantId);

        return _mapper.Map<PermissionDto>(permission);
    }
}
