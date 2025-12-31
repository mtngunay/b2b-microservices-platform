using MediatR;

namespace B2B.Application.Interfaces.CQRS;

/// <summary>
/// Handler interface for queries that return data.
/// </summary>
/// <typeparam name="TQuery">The type of query to handle.</typeparam>
/// <typeparam name="TResponse">The type of response returned.</typeparam>
public interface IQueryHandler<in TQuery, TResponse> : IRequestHandler<TQuery, TResponse>
    where TQuery : IQuery<TResponse>
{
}
