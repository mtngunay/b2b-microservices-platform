using MediatR;

namespace B2B.Application.Interfaces.CQRS;

/// <summary>
/// Marker interface for queries that read data without modifying state.
/// Queries represent read operations in the CQRS pattern.
/// </summary>
/// <typeparam name="TResponse">The type of response returned by the query.</typeparam>
public interface IQuery<TResponse> : IRequest<TResponse>
{
}
