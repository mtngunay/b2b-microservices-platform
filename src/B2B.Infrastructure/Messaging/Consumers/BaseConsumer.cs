using B2B.Application.Interfaces.Services;
using B2B.Domain.Interfaces;
using MassTransit;
using Microsoft.Extensions.Logging;
using SerilogContext = Serilog.Context.LogContext;

namespace B2B.Infrastructure.Messaging.Consumers;

/// <summary>
/// Base consumer class that provides idempotency checking and common functionality.
/// </summary>
/// <typeparam name="TEvent">The type of event to consume.</typeparam>
public abstract class BaseConsumer<TEvent> : IConsumer<TEvent>
    where TEvent : class, IDomainEvent
{
    private readonly ICacheService _cacheService;
    private readonly ICorrelationIdAccessor _correlationIdAccessor;
    private readonly ILogger _logger;

    private const string IdempotencyKeyPrefix = "consumer:idempotency";
    private static readonly TimeSpan IdempotencyExpiry = TimeSpan.FromHours(24);

    /// <summary>
    /// Initializes a new instance of BaseConsumer.
    /// </summary>
    protected BaseConsumer(
        ICacheService cacheService,
        ICorrelationIdAccessor correlationIdAccessor,
        ILogger logger)
    {
        _cacheService = cacheService ?? throw new ArgumentNullException(nameof(cacheService));
        _correlationIdAccessor = correlationIdAccessor ?? throw new ArgumentNullException(nameof(correlationIdAccessor));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Consumes the event with idempotency checking.
    /// </summary>
    public async Task Consume(ConsumeContext<TEvent> context)
    {
        var eventType = typeof(TEvent).Name;
        var eventId = context.Message.EventId;
        var correlationId = ExtractCorrelationId(context);

        // Set correlation ID for logging context
        _correlationIdAccessor.SetCorrelationId(correlationId);

        using (SerilogContext.PushProperty("CorrelationId", correlationId))
        using (SerilogContext.PushProperty("EventId", eventId))
        using (SerilogContext.PushProperty("EventType", eventType))
        using (SerilogContext.PushProperty("TenantId", context.Message.TenantId))
        {
            _logger.LogInformation(
                "Consuming event {EventType} with EventId {EventId}",
                eventType,
                eventId);

            try
            {
                // Check for idempotency
                var idempotencyKey = BuildIdempotencyKey(eventType, eventId);
                var alreadyProcessed = await _cacheService.ExistsAsync(idempotencyKey);

                if (alreadyProcessed)
                {
                    _logger.LogWarning(
                        "Event {EventType} with EventId {EventId} has already been processed. Skipping.",
                        eventType,
                        eventId);
                    return;
                }

                // Process the event
                await HandleAsync(context.Message, context.CancellationToken);

                // Mark as processed
                await _cacheService.SetAsync(
                    idempotencyKey,
                    DateTime.UtcNow.ToString("O"),
                    IdempotencyExpiry);

                _logger.LogInformation(
                    "Successfully processed event {EventType} with EventId {EventId}",
                    eventType,
                    eventId);
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "Error processing event {EventType} with EventId {EventId}",
                    eventType,
                    eventId);
                throw;
            }
        }
    }

    /// <summary>
    /// Handles the event. Override this method to implement event-specific logic.
    /// </summary>
    /// <param name="event">The event to handle.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    protected abstract Task HandleAsync(TEvent @event, CancellationToken cancellationToken);

    /// <summary>
    /// Extracts the correlation ID from the consume context.
    /// </summary>
    private static string ExtractCorrelationId(ConsumeContext<TEvent> context)
    {
        // Try to get from headers first
        if (context.Headers.TryGetHeader("X-Correlation-Id", out var headerValue) && 
            headerValue is string correlationId)
        {
            return correlationId;
        }

        // Fall back to MassTransit correlation ID
        if (context.CorrelationId.HasValue)
        {
            return context.CorrelationId.Value.ToString();
        }

        // Fall back to event correlation ID
        if (!string.IsNullOrEmpty(context.Message.CorrelationId))
        {
            return context.Message.CorrelationId;
        }

        // Generate new if none found
        return Guid.NewGuid().ToString();
    }

    /// <summary>
    /// Builds the idempotency key for the event.
    /// </summary>
    private static string BuildIdempotencyKey(string eventType, Guid eventId)
    {
        return $"{IdempotencyKeyPrefix}:{eventType}:{eventId}";
    }
}
