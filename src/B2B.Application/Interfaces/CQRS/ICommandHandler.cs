using MediatR;

namespace B2B.Application.Interfaces.CQRS;

/// <summary>
/// Handler interface for commands that return a response.
/// </summary>
/// <typeparam name="TCommand">The type of command to handle.</typeparam>
/// <typeparam name="TResponse">The type of response returned.</typeparam>
public interface ICommandHandler<in TCommand, TResponse> : IRequestHandler<TCommand, TResponse>
    where TCommand : ICommand<TResponse>
{
}

/// <summary>
/// Handler interface for commands that do not return a response.
/// </summary>
/// <typeparam name="TCommand">The type of command to handle.</typeparam>
public interface ICommandHandler<in TCommand> : IRequestHandler<TCommand, Unit>
    where TCommand : ICommand
{
}
