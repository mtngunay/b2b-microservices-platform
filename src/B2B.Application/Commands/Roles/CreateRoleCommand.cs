using B2B.Application.DTOs;
using B2B.Application.Interfaces.CQRS;

namespace B2B.Application.Commands.Roles;

/// <summary>
/// Command to create a new role.
/// </summary>
public record CreateRoleCommand(
    string Name,
    string Description,
    List<Guid> PermissionIds) : ICommand<RoleDto>;
