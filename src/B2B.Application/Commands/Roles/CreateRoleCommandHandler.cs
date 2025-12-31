using AutoMapper;
using B2B.Application.DTOs;
using B2B.Application.Interfaces.CQRS;
using B2B.Application.Interfaces.Persistence;
using B2B.Application.Interfaces.Services;
using B2B.Domain.Entities;
using B2B.Domain.Exceptions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace B2B.Application.Commands.Roles;

/// <summary>
/// Handler for CreateRoleCommand.
/// </summary>
public class CreateRoleCommandHandler : ICommandHandler<CreateRoleCommand, RoleDto>
{
    private readonly IApplicationDbContext _dbContext;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUserService _currentUserService;
    private readonly IMapper _mapper;
    private readonly ILogger<CreateRoleCommandHandler> _logger;

    public CreateRoleCommandHandler(
        IApplicationDbContext dbContext,
        IUnitOfWork unitOfWork,
        ICurrentUserService currentUserService,
        IMapper mapper,
        ILogger<CreateRoleCommandHandler> logger)
    {
        _dbContext = dbContext;
        _unitOfWork = unitOfWork;
        _currentUserService = currentUserService;
        _mapper = mapper;
        _logger = logger;
    }

    public async Task<RoleDto> Handle(
        CreateRoleCommand request,
        CancellationToken cancellationToken)
    {
        var tenantId = _currentUserService.TenantId 
            ?? throw new InvalidOperationException("Tenant context is required");

        // Check if role name already exists
        var existingRole = await _dbContext.Roles
            .FirstOrDefaultAsync(r => r.Name == request.Name, cancellationToken);

        if (existingRole != null)
        {
            throw new ConflictException($"Role with name '{request.Name}' already exists");
        }

        var role = Role.Create(request.Name, request.Description, tenantId);

        // Add permissions to role
        if (request.PermissionIds.Any())
        {
            var permissions = await _dbContext.Permissions
                .Where(p => request.PermissionIds.Contains(p.Id))
                .ToListAsync(cancellationToken);

            foreach (var permission in permissions)
            {
                role.AddPermission(permission);
            }
        }

        await _dbContext.Roles.AddAsync(role, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Role {RoleName} created with ID {RoleId} in tenant {TenantId}",
            role.Name, role.Id, tenantId);

        var dto = _mapper.Map<RoleDto>(role);
        dto.Permissions = role.RolePermissions
            .Select(rp => rp.Permission?.Name ?? string.Empty)
            .Where(n => !string.IsNullOrEmpty(n))
            .ToList();

        return dto;
    }
}
