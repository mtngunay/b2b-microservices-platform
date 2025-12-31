using B2B.Domain.Interfaces;

namespace B2B.Application.Interfaces.Services;

/// <summary>
/// Service for managing the outbox pattern for reliable event publishing.
/// </summary>
public interface IOutboxService
{
    /// <summary>
    /// Adds a domain event to the outbox for later publishing.
    /// This should be called within the same transaction as the business data changes.
    /// </summary>
    /// <param name="domainEvent">The domain event to add.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task AddEventAsync(
        IDomainEvent domainEvent,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets pending messages from the outbox that need to be published.
    /// </summary>
    /// <param name="batchSize">The maximum number of messages to retrieve.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A collection of pending outbox messages.</returns>
    Task<IEnumerable<OutboxMessage>> GetPendingMessagesAsync(
        int batchSize,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Marks a message as successfully processed.
    /// </summary>
    /// <param name="messageId">The message identifier.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task MarkAsProcessedAsync(
        Guid messageId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Marks a message as failed with an error message.
    /// </summary>
    /// <param name="messageId">The message identifier.</param>
    /// <param name="error">The error message.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task MarkAsFailedAsync(
        Guid messageId,
        string error,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets failed messages that have exceeded the maximum retry count.
    /// </summary>
    /// <param name="batchSize">The maximum number of messages to retrieve.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A collection of failed outbox messages.</returns>
    Task<IEnumerable<OutboxMessage>> GetFailedMessagesAsync(
        int batchSize,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Represents a message in the outbox.
/// </summary>
public class OutboxMessage
{
    /// <summary>
    /// Gets or sets the unique identifier of the message.
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// Gets or sets the type of the event.
    /// </summary>
    public string EventType { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the serialized event payload.
    /// </summary>
    public string Payload { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the correlation ID for distributed tracing.
    /// </summary>
    public string CorrelationId { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the tenant identifier.
    /// </summary>
    public string TenantId { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the timestamp when the message was created.
    /// </summary>
    public DateTime CreatedAt { get; set; }

    /// <summary>
    /// Gets or sets the timestamp when the message was processed.
    /// </summary>
    public DateTime? ProcessedAt { get; set; }

    /// <summary>
    /// Gets or sets the number of retry attempts.
    /// </summary>
    public int RetryCount { get; set; }

    /// <summary>
    /// Gets or sets the error message if processing failed.
    /// </summary>
    public string? Error { get; set; }

    /// <summary>
    /// Gets or sets the status of the message.
    /// </summary>
    public OutboxMessageStatus Status { get; set; }
}

/// <summary>
/// Status of an outbox message.
/// </summary>
public enum OutboxMessageStatus
{
    /// <summary>
    /// Message is pending and waiting to be processed.
    /// </summary>
    Pending = 0,

    /// <summary>
    /// Message is currently being processed.
    /// </summary>
    Processing = 1,

    /// <summary>
    /// Message has been successfully processed.
    /// </summary>
    Processed = 2,

    /// <summary>
    /// Message processing failed.
    /// </summary>
    Failed = 3
}
