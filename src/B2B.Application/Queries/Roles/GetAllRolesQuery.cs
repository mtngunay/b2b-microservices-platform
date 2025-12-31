using B2B.Application.DTOs;
using B2B.Application.Interfaces.CQRS;

namespace B2B.Application.Queries.Roles;

/// <summary>
/// Query to get all roles for the current tenant.
/// </summary>
public record GetAllRolesQuery : IQuery<IEnumerable<RoleDto>>;
