using AutoMapper;
using B2B.Application.DTOs;
using B2B.Application.Interfaces.CQRS;
using B2B.Application.Interfaces.Persistence;
using Microsoft.EntityFrameworkCore;

namespace B2B.Application.Queries.Permissions;

/// <summary>
/// Handler for GetAllPermissionsQuery.
/// </summary>
public class GetAllPermissionsQueryHandler : IQueryHandler<GetAllPermissionsQuery, IEnumerable<PermissionDto>>
{
    private readonly IApplicationDbContext _dbContext;
    private readonly IMapper _mapper;

    public GetAllPermissionsQueryHandler(
        IApplicationDbContext dbContext,
        IMapper mapper)
    {
        _dbContext = dbContext;
        _mapper = mapper;
    }

    public async Task<IEnumerable<PermissionDto>> Handle(
        GetAllPermissionsQuery request,
        CancellationToken cancellationToken)
    {
        var permissions = await _dbContext.Permissions
            .AsNoTracking()
            .OrderBy(p => p.Resource)
            .ThenBy(p => p.Action)
            .ToListAsync(cancellationToken);

        return _mapper.Map<IEnumerable<PermissionDto>>(permissions);
    }
}
