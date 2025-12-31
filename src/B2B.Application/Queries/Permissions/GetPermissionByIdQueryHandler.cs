using AutoMapper;
using B2B.Application.DTOs;
using B2B.Application.Interfaces.CQRS;
using B2B.Application.Interfaces.Persistence;
using Microsoft.EntityFrameworkCore;

namespace B2B.Application.Queries.Permissions;

/// <summary>
/// Handler for GetPermissionByIdQuery.
/// </summary>
public class GetPermissionByIdQueryHandler : IQueryHandler<GetPermissionByIdQuery, PermissionDto?>
{
    private readonly IApplicationDbContext _dbContext;
    private readonly IMapper _mapper;

    public GetPermissionByIdQueryHandler(
        IApplicationDbContext dbContext,
        IMapper mapper)
    {
        _dbContext = dbContext;
        _mapper = mapper;
    }

    public async Task<PermissionDto?> Handle(
        GetPermissionByIdQuery request,
        CancellationToken cancellationToken)
    {
        var permission = await _dbContext.Permissions
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == request.Id, cancellationToken);

        return permission == null ? null : _mapper.Map<PermissionDto>(permission);
    }
}
