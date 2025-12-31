using B2B.Application.DTOs;
using B2B.Application.Interfaces.CQRS;

namespace B2B.Application.Commands.Roles;

/// <summary>
/// Command to assign permissions to a role.
/// </summary>
public record AssignPermissionsToRoleCommand(
    Guid RoleId,
    List<Guid> PermissionIds) : ICommand<RoleDto>;
