using System.Linq.Expressions;

namespace B2B.Application.Interfaces.Persistence;

/// <summary>
/// Generic repository interface for read operations on read models.
/// </summary>
/// <typeparam name="TReadModel">The read model type.</typeparam>
/// <typeparam name="TId">The identifier type.</typeparam>
public interface IReadRepository<TReadModel, TId> where TReadModel : class
{
    /// <summary>
    /// Gets a read model by its identifier.
    /// </summary>
    /// <param name="id">The identifier.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The read model if found; otherwise, null.</returns>
    Task<TReadModel?> GetByIdAsync(TId id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets all read models.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A collection of all read models.</returns>
    Task<IEnumerable<TReadModel>> GetAllAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets all read models with pagination.
    /// </summary>
    /// <param name="skip">Number of items to skip.</param>
    /// <param name="take">Number of items to take.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A collection of read models.</returns>
    Task<IEnumerable<TReadModel>> GetAllAsync(
        int skip,
        int take,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Finds read models matching the specified predicate.
    /// </summary>
    /// <param name="predicate">The filter predicate.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A collection of matching read models.</returns>
    Task<IEnumerable<TReadModel>> FindAsync(
        Expression<Func<TReadModel, bool>> predicate,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Finds read models matching the specified predicate with pagination.
    /// </summary>
    /// <param name="predicate">The filter predicate.</param>
    /// <param name="skip">Number of items to skip.</param>
    /// <param name="take">Number of items to take.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A collection of matching read models.</returns>
    Task<IEnumerable<TReadModel>> FindAsync(
        Expression<Func<TReadModel, bool>> predicate,
        int skip,
        int take,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the first read model matching the specified predicate.
    /// </summary>
    /// <param name="predicate">The filter predicate.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The first matching read model if found; otherwise, null.</returns>
    Task<TReadModel?> FirstOrDefaultAsync(
        Expression<Func<TReadModel, bool>> predicate,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks if any read model matches the specified predicate.
    /// </summary>
    /// <param name="predicate">The filter predicate.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>True if any read model matches; otherwise, false.</returns>
    Task<bool> AnyAsync(
        Expression<Func<TReadModel, bool>> predicate,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Counts read models matching the specified predicate.
    /// </summary>
    /// <param name="predicate">The filter predicate.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The count of matching read models.</returns>
    Task<long> CountAsync(
        Expression<Func<TReadModel, bool>> predicate,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Counts all read models.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The total count of read models.</returns>
    Task<long> CountAsync(CancellationToken cancellationToken = default);
}
