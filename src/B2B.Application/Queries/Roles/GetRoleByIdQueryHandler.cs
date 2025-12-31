using AutoMapper;
using B2B.Application.DTOs;
using B2B.Application.Interfaces.CQRS;
using B2B.Application.Interfaces.Persistence;
using Microsoft.EntityFrameworkCore;

namespace B2B.Application.Queries.Roles;

/// <summary>
/// Handler for GetRoleByIdQuery.
/// </summary>
public class GetRoleByIdQueryHandler : IQueryHandler<GetRoleByIdQuery, RoleDto?>
{
    private readonly IApplicationDbContext _dbContext;
    private readonly IMapper _mapper;

    public GetRoleByIdQueryHandler(
        IApplicationDbContext dbContext,
        IMapper mapper)
    {
        _dbContext = dbContext;
        _mapper = mapper;
    }

    public async Task<RoleDto?> Handle(
        GetRoleByIdQuery request,
        CancellationToken cancellationToken)
    {
        var role = await _dbContext.Roles
            .AsNoTracking()
            .Include(r => r.RolePermissions)
                .ThenInclude(rp => rp.Permission)
            .FirstOrDefaultAsync(r => r.Id == request.Id, cancellationToken);

        if (role == null)
        {
            return null;
        }

        var dto = _mapper.Map<RoleDto>(role);
        dto.Permissions = role.RolePermissions
            .Select(rp => rp.Permission?.Name ?? string.Empty)
            .Where(n => !string.IsNullOrEmpty(n))
            .ToList();

        return dto;
    }
}
