using B2B.Application.Interfaces.Services;
using B2B.Domain.Interfaces;
using MassTransit;
using Microsoft.Extensions.Logging;

namespace B2B.Infrastructure.Messaging;

/// <summary>
/// MassTransit implementation of the message publisher.
/// </summary>
public class MassTransitMessagePublisher : IMessagePublisher
{
    private readonly IPublishEndpoint _publishEndpoint;
    private readonly ISendEndpointProvider _sendEndpointProvider;
    private readonly ICorrelationIdAccessor _correlationIdAccessor;
    private readonly ILogger<MassTransitMessagePublisher> _logger;

    /// <summary>
    /// Initializes a new instance of MassTransitMessagePublisher.
    /// </summary>
    public MassTransitMessagePublisher(
        IPublishEndpoint publishEndpoint,
        ISendEndpointProvider sendEndpointProvider,
        ICorrelationIdAccessor correlationIdAccessor,
        ILogger<MassTransitMessagePublisher> logger)
    {
        _publishEndpoint = publishEndpoint ?? throw new ArgumentNullException(nameof(publishEndpoint));
        _sendEndpointProvider = sendEndpointProvider ?? throw new ArgumentNullException(nameof(sendEndpointProvider));
        _correlationIdAccessor = correlationIdAccessor ?? throw new ArgumentNullException(nameof(correlationIdAccessor));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc />
    public async Task PublishAsync<TEvent>(
        TEvent @event,
        CancellationToken cancellationToken = default)
        where TEvent : class, IDomainEvent
    {
        await PublishAsync(@event, new Dictionary<string, object>(), cancellationToken);
    }

    /// <inheritdoc />
    public async Task PublishAsync<TEvent>(
        TEvent @event,
        IDictionary<string, object> headers,
        CancellationToken cancellationToken = default)
        where TEvent : class, IDomainEvent
    {
        var eventType = typeof(TEvent).Name;
        var correlationId = @event.CorrelationId ?? _correlationIdAccessor.CorrelationId;

        _logger.LogInformation(
            "Publishing event {EventType} with EventId {EventId} and CorrelationId {CorrelationId}",
            eventType,
            @event.EventId,
            correlationId);

        try
        {
            await _publishEndpoint.Publish(@event, context =>
            {
                // Set correlation ID for distributed tracing
                context.CorrelationId = Guid.TryParse(correlationId, out var guid) 
                    ? guid 
                    : Guid.NewGuid();

                // Add custom headers
                context.Headers.Set("X-Correlation-Id", correlationId);
                context.Headers.Set("X-Tenant-Id", @event.TenantId);
                context.Headers.Set("X-Event-Type", eventType);
                context.Headers.Set("X-Event-Id", @event.EventId.ToString());
                context.Headers.Set("X-Occurred-At", @event.OccurredAt.ToString("O"));

                // Add any additional custom headers
                foreach (var header in headers)
                {
                    context.Headers.Set(header.Key, header.Value);
                }
            }, cancellationToken);

            _logger.LogDebug(
                "Successfully published event {EventType} with EventId {EventId}",
                eventType,
                @event.EventId);
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Failed to publish event {EventType} with EventId {EventId}",
                eventType,
                @event.EventId);
            throw;
        }
    }

    /// <inheritdoc />
    public async Task SendAsync<TCommand>(
        TCommand command,
        Uri destinationAddress,
        CancellationToken cancellationToken = default)
        where TCommand : class
    {
        var commandType = typeof(TCommand).Name;
        var correlationId = _correlationIdAccessor.CorrelationId;

        _logger.LogInformation(
            "Sending command {CommandType} to {Destination} with CorrelationId {CorrelationId}",
            commandType,
            destinationAddress,
            correlationId);

        try
        {
            var endpoint = await _sendEndpointProvider.GetSendEndpoint(destinationAddress);

            await endpoint.Send(command, context =>
            {
                context.CorrelationId = Guid.TryParse(correlationId, out var guid) 
                    ? guid 
                    : Guid.NewGuid();
                context.Headers.Set("X-Correlation-Id", correlationId);
            }, cancellationToken);

            _logger.LogDebug(
                "Successfully sent command {CommandType} to {Destination}",
                commandType,
                destinationAddress);
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Failed to send command {CommandType} to {Destination}",
                commandType,
                destinationAddress);
            throw;
        }
    }
}
