using B2B.Application.DTOs;
using B2B.Application.Interfaces.CQRS;

namespace B2B.Application.Commands.Permissions;

/// <summary>
/// Command to update an existing permission.
/// </summary>
public record UpdatePermissionCommand(
    Guid Id,
    string Description) : ICommand<PermissionDto>;
