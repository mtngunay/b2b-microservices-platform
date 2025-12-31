using B2B.Application.DTOs;
using B2B.Application.Interfaces.CQRS;

namespace B2B.Application.Commands.Roles;

/// <summary>
/// Command to remove permissions from a role.
/// </summary>
public record RemovePermissionsFromRoleCommand(
    Guid RoleId,
    List<Guid> PermissionIds) : ICommand<RoleDto>;
