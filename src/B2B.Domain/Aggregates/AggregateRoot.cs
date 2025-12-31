using B2B.Domain.Entities;
using B2B.Domain.Interfaces;

namespace B2B.Domain.Aggregates;

/// <summary>
/// Base abstract class for aggregate roots that can raise domain events.
/// </summary>
public abstract class AggregateRoot : BaseEntity, IAggregateRoot<Guid>
{
    private readonly List<IDomainEvent> _domainEvents = new();

    /// <summary>
    /// Gets the collection of domain events raised by this aggregate.
    /// </summary>
    public IReadOnlyList<IDomainEvent> DomainEvents => _domainEvents.AsReadOnly();

    /// <summary>
    /// Adds a domain event to the aggregate's event collection.
    /// </summary>
    /// <param name="domainEvent">The domain event to add.</param>
    protected void AddDomainEvent(IDomainEvent domainEvent)
    {
        _domainEvents.Add(domainEvent);
    }

    /// <summary>
    /// Removes a domain event from the aggregate's event collection.
    /// </summary>
    /// <param name="domainEvent">The domain event to remove.</param>
    protected void RemoveDomainEvent(IDomainEvent domainEvent)
    {
        _domainEvents.Remove(domainEvent);
    }

    /// <summary>
    /// Clears all domain events from the aggregate.
    /// </summary>
    public void ClearDomainEvents()
    {
        _domainEvents.Clear();
    }
}
