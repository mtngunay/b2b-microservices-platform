using B2B.Application.DTOs;
using B2B.Application.Interfaces.CQRS;

namespace B2B.Application.Commands.Permissions;

/// <summary>
/// Command to create a new permission.
/// </summary>
public record CreatePermissionCommand(
    string Name,
    string Resource,
    string Action,
    string Description) : ICommand<PermissionDto>;
