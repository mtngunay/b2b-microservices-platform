using B2B.Domain.Interfaces;

namespace B2B.Infrastructure.Messaging;

/// <summary>
/// Interface for publishing messages to the message bus.
/// </summary>
public interface IMessagePublisher
{
    /// <summary>
    /// Publishes a domain event to the message bus.
    /// </summary>
    /// <typeparam name="TEvent">The type of the event.</typeparam>
    /// <param name="event">The event to publish.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task PublishAsync<TEvent>(
        TEvent @event,
        CancellationToken cancellationToken = default)
        where TEvent : class, IDomainEvent;

    /// <summary>
    /// Publishes a domain event with custom headers.
    /// </summary>
    /// <typeparam name="TEvent">The type of the event.</typeparam>
    /// <param name="event">The event to publish.</param>
    /// <param name="headers">Custom headers to include.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task PublishAsync<TEvent>(
        TEvent @event,
        IDictionary<string, object> headers,
        CancellationToken cancellationToken = default)
        where TEvent : class, IDomainEvent;

    /// <summary>
    /// Sends a command to a specific endpoint.
    /// </summary>
    /// <typeparam name="TCommand">The type of the command.</typeparam>
    /// <param name="command">The command to send.</param>
    /// <param name="destinationAddress">The destination endpoint address.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task SendAsync<TCommand>(
        TCommand command,
        Uri destinationAddress,
        CancellationToken cancellationToken = default)
        where TCommand : class;
}
