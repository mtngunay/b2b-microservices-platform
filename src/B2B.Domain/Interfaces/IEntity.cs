namespace B2B.Domain.Interfaces;

/// <summary>
/// Base interface for all domain entities with a typed identifier.
/// </summary>
/// <typeparam name="TId">The type of the entity identifier.</typeparam>
public interface IEntity<TId>
{
    /// <summary>
    /// Gets the unique identifier of the entity.
    /// </summary>
    TId Id { get; }

    /// <summary>
    /// Gets the timestamp when the entity was created.
    /// </summary>
    DateTime CreatedAt { get; }

    /// <summary>
    /// Gets the timestamp when the entity was last updated.
    /// </summary>
    DateTime? UpdatedAt { get; }
}
