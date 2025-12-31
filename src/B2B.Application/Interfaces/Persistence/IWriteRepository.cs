using B2B.Domain.Interfaces;
using System.Linq.Expressions;

namespace B2B.Application.Interfaces.Persistence;

/// <summary>
/// Generic repository interface for write operations on aggregate roots.
/// </summary>
/// <typeparam name="TEntity">The aggregate root entity type.</typeparam>
/// <typeparam name="TId">The entity identifier type.</typeparam>
public interface IWriteRepository<TEntity, TId> where TEntity : class, IAggregateRoot<TId>
{
    /// <summary>
    /// Gets an entity by its identifier.
    /// </summary>
    /// <param name="id">The entity identifier.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The entity if found; otherwise, null.</returns>
    Task<TEntity?> GetByIdAsync(TId id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets an entity by its identifier with related entities.
    /// </summary>
    /// <param name="id">The entity identifier.</param>
    /// <param name="includes">Navigation properties to include.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The entity if found; otherwise, null.</returns>
    Task<TEntity?> GetByIdWithIncludesAsync(
        TId id,
        CancellationToken cancellationToken = default,
        params Expression<Func<TEntity, object>>[] includes);

    /// <summary>
    /// Finds entities matching the specified predicate.
    /// </summary>
    /// <param name="predicate">The filter predicate.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A collection of matching entities.</returns>
    Task<IEnumerable<TEntity>> FindAsync(
        Expression<Func<TEntity, bool>> predicate,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the first entity matching the specified predicate.
    /// </summary>
    /// <param name="predicate">The filter predicate.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The first matching entity if found; otherwise, null.</returns>
    Task<TEntity?> FirstOrDefaultAsync(
        Expression<Func<TEntity, bool>> predicate,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks if any entity matches the specified predicate.
    /// </summary>
    /// <param name="predicate">The filter predicate.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>True if any entity matches; otherwise, false.</returns>
    Task<bool> AnyAsync(
        Expression<Func<TEntity, bool>> predicate,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Adds a new entity to the repository.
    /// </summary>
    /// <param name="entity">The entity to add.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task AddAsync(TEntity entity, CancellationToken cancellationToken = default);

    /// <summary>
    /// Adds multiple entities to the repository.
    /// </summary>
    /// <param name="entities">The entities to add.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task AddRangeAsync(IEnumerable<TEntity> entities, CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates an existing entity in the repository.
    /// </summary>
    /// <param name="entity">The entity to update.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task UpdateAsync(TEntity entity, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes an entity from the repository (soft delete if supported).
    /// </summary>
    /// <param name="entity">The entity to delete.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task DeleteAsync(TEntity entity, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the queryable for advanced queries.
    /// </summary>
    /// <returns>An IQueryable for the entity type.</returns>
    IQueryable<TEntity> AsQueryable();
}
