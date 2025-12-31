using AutoMapper;
using B2B.Application.DTOs;
using B2B.Application.Interfaces.CQRS;
using B2B.Application.Interfaces.Persistence;
using Microsoft.EntityFrameworkCore;

namespace B2B.Application.Queries.Roles;

/// <summary>
/// Handler for GetAllRolesQuery.
/// </summary>
public class GetAllRolesQueryHandler : IQueryHandler<GetAllRolesQuery, IEnumerable<RoleDto>>
{
    private readonly IApplicationDbContext _dbContext;
    private readonly IMapper _mapper;

    public GetAllRolesQueryHandler(
        IApplicationDbContext dbContext,
        IMapper mapper)
    {
        _dbContext = dbContext;
        _mapper = mapper;
    }

    public async Task<IEnumerable<RoleDto>> Handle(
        GetAllRolesQuery request,
        CancellationToken cancellationToken)
    {
        var roles = await _dbContext.Roles
            .AsNoTracking()
            .Include(r => r.RolePermissions)
                .ThenInclude(rp => rp.Permission)
            .OrderBy(r => r.Name)
            .ToListAsync(cancellationToken);

        return roles.Select(role =>
        {
            var dto = _mapper.Map<RoleDto>(role);
            dto.Permissions = role.RolePermissions
                .Select(rp => rp.Permission?.Name ?? string.Empty)
                .Where(n => !string.IsNullOrEmpty(n))
                .ToList();
            return dto;
        });
    }
}
