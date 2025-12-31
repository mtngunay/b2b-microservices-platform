using B2B.Application.DTOs;
using B2B.Application.Interfaces.CQRS;

namespace B2B.Application.Queries.Permissions;

/// <summary>
/// Query to get all permissions for the current tenant.
/// </summary>
public record GetAllPermissionsQuery : IQuery<IEnumerable<PermissionDto>>;
