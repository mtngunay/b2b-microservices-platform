using B2B.Application.Interfaces.CQRS;
using MediatR;

namespace B2B.Application.Commands.Permissions;

/// <summary>
/// Command to delete a permission.
/// </summary>
public record DeletePermissionCommand(Guid Id) : ICommand;
