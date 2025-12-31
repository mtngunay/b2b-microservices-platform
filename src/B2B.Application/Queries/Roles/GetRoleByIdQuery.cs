using B2B.Application.DTOs;
using B2B.Application.Interfaces.CQRS;

namespace B2B.Application.Queries.Roles;

/// <summary>
/// Query to get a role by ID.
/// </summary>
public record GetRoleByIdQuery(Guid Id) : IQuery<RoleDto?>;
