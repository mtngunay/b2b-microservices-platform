using B2B.Domain.Interfaces;

namespace B2B.Domain.Events;

/// <summary>
/// Base record for integration events that are published to the message bus.
/// </summary>
public abstract record IntegrationEvent : IDomainEvent
{
    /// <summary>
    /// Gets the unique identifier of the event.
    /// </summary>
    public Guid EventId { get; init; } = Guid.NewGuid();

    /// <summary>
    /// Gets the timestamp when the event occurred.
    /// </summary>
    public DateTime OccurredAt { get; init; } = DateTime.UtcNow;

    /// <summary>
    /// Gets the correlation ID for distributed tracing.
    /// </summary>
    public string CorrelationId { get; init; } = string.Empty;

    /// <summary>
    /// Gets the tenant ID for multi-tenancy support.
    /// </summary>
    public string TenantId { get; init; } = string.Empty;
}
