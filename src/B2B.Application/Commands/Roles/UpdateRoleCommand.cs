using B2B.Application.DTOs;
using B2B.Application.Interfaces.CQRS;

namespace B2B.Application.Commands.Roles;

/// <summary>
/// Command to update an existing role.
/// </summary>
public record UpdateRoleCommand(
    Guid Id,
    string Name,
    string Description,
    List<Guid> PermissionIds) : ICommand<RoleDto>;
