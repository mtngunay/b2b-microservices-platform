namespace B2B.Domain.Interfaces;

/// <summary>
/// Interface for domain events that occur within the domain layer.
/// </summary>
public interface IDomainEvent
{
    /// <summary>
    /// Gets the unique identifier of the event.
    /// </summary>
    Guid EventId { get; }

    /// <summary>
    /// Gets the timestamp when the event occurred.
    /// </summary>
    DateTime OccurredAt { get; }

    /// <summary>
    /// Gets the correlation ID for distributed tracing.
    /// </summary>
    string CorrelationId { get; }

    /// <summary>
    /// Gets the tenant ID for multi-tenancy support.
    /// </summary>
    string TenantId { get; }
}
