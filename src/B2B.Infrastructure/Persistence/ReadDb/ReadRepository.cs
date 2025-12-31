using B2B.Application.Interfaces.Persistence;
using MongoDB.Driver;
using MongoDB.Driver.Linq;
using System.Linq.Expressions;

namespace B2B.Infrastructure.Persistence.ReadDb;

/// <summary>
/// Generic repository implementation for read operations using MongoDB.
/// Automatically applies tenant filtering.
/// </summary>
/// <typeparam name="TReadModel">The read model type.</typeparam>
/// <typeparam name="TId">The identifier type.</typeparam>
public class ReadRepository<TReadModel, TId> : IReadRepository<TReadModel, TId>
    where TReadModel : class
{
    protected readonly MongoDbContext _context;
    protected readonly IMongoCollection<TReadModel> _collection;
    private readonly string _tenantIdFieldName;

    /// <summary>
    /// Initializes a new instance of the ReadRepository.
    /// </summary>
    /// <param name="context">The MongoDB context.</param>
    /// <param name="collectionName">The collection name.</param>
    /// <param name="tenantIdFieldName">The tenant ID field name (default: "tenantId").</param>
    public ReadRepository(
        MongoDbContext context,
        string collectionName,
        string tenantIdFieldName = "tenantId")
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _collection = context.GetCollection<TReadModel>(collectionName);
        _tenantIdFieldName = tenantIdFieldName;
    }

    /// <inheritdoc />
    public virtual async Task<TReadModel?> GetByIdAsync(TId id, CancellationToken cancellationToken = default)
    {
        var filter = CreateIdFilter(id);
        filter = CombineWithTenantFilter(filter);

        return await _collection
            .Find(filter)
            .FirstOrDefaultAsync(cancellationToken);
    }

    /// <inheritdoc />
    public virtual async Task<IEnumerable<TReadModel>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        var filter = GetDefaultFilter();

        return await _collection
            .Find(filter)
            .ToListAsync(cancellationToken);
    }

    /// <inheritdoc />
    public virtual async Task<IEnumerable<TReadModel>> GetAllAsync(
        int skip,
        int take,
        CancellationToken cancellationToken = default)
    {
        var filter = GetDefaultFilter();

        return await _collection
            .Find(filter)
            .Skip(skip)
            .Limit(take)
            .ToListAsync(cancellationToken);
    }

    /// <inheritdoc />
    public virtual async Task<IEnumerable<TReadModel>> FindAsync(
        Expression<Func<TReadModel, bool>> predicate,
        CancellationToken cancellationToken = default)
    {
        var filter = Builders<TReadModel>.Filter.Where(predicate);
        filter = CombineWithDefaultFilter(filter);

        return await _collection
            .Find(filter)
            .ToListAsync(cancellationToken);
    }

    /// <inheritdoc />
    public virtual async Task<IEnumerable<TReadModel>> FindAsync(
        Expression<Func<TReadModel, bool>> predicate,
        int skip,
        int take,
        CancellationToken cancellationToken = default)
    {
        var filter = Builders<TReadModel>.Filter.Where(predicate);
        filter = CombineWithDefaultFilter(filter);

        return await _collection
            .Find(filter)
            .Skip(skip)
            .Limit(take)
            .ToListAsync(cancellationToken);
    }

    /// <inheritdoc />
    public virtual async Task<TReadModel?> FirstOrDefaultAsync(
        Expression<Func<TReadModel, bool>> predicate,
        CancellationToken cancellationToken = default)
    {
        var filter = Builders<TReadModel>.Filter.Where(predicate);
        filter = CombineWithDefaultFilter(filter);

        return await _collection
            .Find(filter)
            .FirstOrDefaultAsync(cancellationToken);
    }

    /// <inheritdoc />
    public virtual async Task<bool> AnyAsync(
        Expression<Func<TReadModel, bool>> predicate,
        CancellationToken cancellationToken = default)
    {
        var filter = Builders<TReadModel>.Filter.Where(predicate);
        filter = CombineWithDefaultFilter(filter);

        return await _collection
            .Find(filter)
            .AnyAsync(cancellationToken);
    }

    /// <inheritdoc />
    public virtual async Task<long> CountAsync(
        Expression<Func<TReadModel, bool>> predicate,
        CancellationToken cancellationToken = default)
    {
        var filter = Builders<TReadModel>.Filter.Where(predicate);
        filter = CombineWithDefaultFilter(filter);

        return await _collection.CountDocumentsAsync(filter, cancellationToken: cancellationToken);
    }

    /// <inheritdoc />
    public virtual async Task<long> CountAsync(CancellationToken cancellationToken = default)
    {
        var filter = GetDefaultFilter();
        return await _collection.CountDocumentsAsync(filter, cancellationToken: cancellationToken);
    }

    /// <summary>
    /// Creates a filter for the ID field.
    /// </summary>
    protected virtual FilterDefinition<TReadModel> CreateIdFilter(TId id)
    {
        return Builders<TReadModel>.Filter.Eq("_id", id);
    }

    /// <summary>
    /// Gets the default filter (tenant + soft delete).
    /// </summary>
    protected virtual FilterDefinition<TReadModel> GetDefaultFilter()
    {
        return _context.CreateDefaultFilter<TReadModel>();
    }

    /// <summary>
    /// Combines a filter with the tenant filter.
    /// </summary>
    protected virtual FilterDefinition<TReadModel> CombineWithTenantFilter(FilterDefinition<TReadModel> filter)
    {
        var tenantFilter = _context.CreateTenantFilter<TReadModel>();
        if (tenantFilter == Builders<TReadModel>.Filter.Empty)
        {
            return filter;
        }

        return Builders<TReadModel>.Filter.And(filter, tenantFilter);
    }

    /// <summary>
    /// Combines a filter with the default filter (tenant + soft delete).
    /// </summary>
    protected virtual FilterDefinition<TReadModel> CombineWithDefaultFilter(FilterDefinition<TReadModel> filter)
    {
        var defaultFilter = GetDefaultFilter();
        if (defaultFilter == Builders<TReadModel>.Filter.Empty)
        {
            return filter;
        }

        return Builders<TReadModel>.Filter.And(filter, defaultFilter);
    }

    /// <summary>
    /// Gets the underlying MongoDB collection for advanced queries.
    /// </summary>
    protected IMongoCollection<TReadModel> Collection => _collection;
}
