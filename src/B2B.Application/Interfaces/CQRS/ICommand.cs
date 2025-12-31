using MediatR;

namespace B2B.Application.Interfaces.CQRS;

/// <summary>
/// Marker interface for commands that modify state and return a response.
/// Commands represent write operations in the CQRS pattern.
/// </summary>
/// <typeparam name="TResponse">The type of response returned by the command.</typeparam>
public interface ICommand<TResponse> : IRequest<TResponse>
{
}

/// <summary>
/// Marker interface for commands that modify state without returning a response.
/// </summary>
public interface ICommand : IRequest<Unit>
{
}
