using B2B.Application.Interfaces.CQRS;
using MediatR;

namespace B2B.Application.Commands.Roles;

/// <summary>
/// Command to delete a role.
/// </summary>
public record DeleteRoleCommand(Guid Id) : ICommand;
