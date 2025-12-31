namespace B2B.Domain.Interfaces;

/// <summary>
/// Interface for aggregate roots that can raise domain events.
/// </summary>
/// <typeparam name="TId">The type of the aggregate root identifier.</typeparam>
public interface IAggregateRoot<TId> : IEntity<TId>
{
    /// <summary>
    /// Gets the collection of domain events raised by this aggregate.
    /// </summary>
    IReadOnlyList<IDomainEvent> DomainEvents { get; }

    /// <summary>
    /// Clears all domain events from the aggregate.
    /// </summary>
    void ClearDomainEvents();
}
