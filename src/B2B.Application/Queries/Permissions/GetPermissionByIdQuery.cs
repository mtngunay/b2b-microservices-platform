using B2B.Application.DTOs;
using B2B.Application.Interfaces.CQRS;

namespace B2B.Application.Queries.Permissions;

/// <summary>
/// Query to get a permission by ID.
/// </summary>
public record GetPermissionByIdQuery(Guid Id) : IQuery<PermissionDto?>;
