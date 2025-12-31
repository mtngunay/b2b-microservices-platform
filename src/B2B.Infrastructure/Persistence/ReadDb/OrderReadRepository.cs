using B2B.Infrastructure.Persistence.ReadDb.ReadModels;
using MongoDB.Driver;

namespace B2B.Infrastructure.Persistence.ReadDb;

/// <summary>
/// Repository for Order read model operations.
/// </summary>
public class OrderReadRepository : ReadRepository<OrderReadModel, string>
{
    /// <summary>
    /// Initializes a new instance of OrderReadRepository.
    /// </summary>
    public OrderReadRepository(MongoDbContext context)
        : base(context, "orders")
    {
    }

    /// <summary>
    /// Gets an order by order number.
    /// </summary>
    public async Task<OrderReadModel?> GetByOrderNumberAsync(
        string orderNumber,
        CancellationToken cancellationToken = default)
    {
        var filter = Builders<OrderReadModel>.Filter.Eq(o => o.OrderNumber, orderNumber);
        filter = CombineWithDefaultFilter(filter);

        return await Collection
            .Find(filter)
            .FirstOrDefaultAsync(cancellationToken);
    }

    /// <summary>
    /// Gets orders by customer ID.
    /// </summary>
    public async Task<IEnumerable<OrderReadModel>> GetByCustomerIdAsync(
        string customerId,
        int skip,
        int take,
        CancellationToken cancellationToken = default)
    {
        var filter = Builders<OrderReadModel>.Filter.Eq("customer.id", customerId);
        filter = CombineWithDefaultFilter(filter);

        return await Collection
            .Find(filter)
            .SortByDescending(o => o.CreatedAt)
            .Skip(skip)
            .Limit(take)
            .ToListAsync(cancellationToken);
    }

    /// <summary>
    /// Gets orders by status.
    /// </summary>
    public async Task<IEnumerable<OrderReadModel>> GetByStatusAsync(
        string status,
        int skip,
        int take,
        CancellationToken cancellationToken = default)
    {
        var filter = Builders<OrderReadModel>.Filter.Eq(o => o.Status, status);
        filter = CombineWithDefaultFilter(filter);

        return await Collection
            .Find(filter)
            .SortByDescending(o => o.CreatedAt)
            .Skip(skip)
            .Limit(take)
            .ToListAsync(cancellationToken);
    }

    /// <summary>
    /// Gets orders within a date range.
    /// </summary>
    public async Task<IEnumerable<OrderReadModel>> GetByDateRangeAsync(
        DateTime startDate,
        DateTime endDate,
        int skip,
        int take,
        CancellationToken cancellationToken = default)
    {
        var filter = Builders<OrderReadModel>.Filter.And(
            Builders<OrderReadModel>.Filter.Gte(o => o.CreatedAt, startDate),
            Builders<OrderReadModel>.Filter.Lte(o => o.CreatedAt, endDate)
        );
        filter = CombineWithDefaultFilter(filter);

        return await Collection
            .Find(filter)
            .SortByDescending(o => o.CreatedAt)
            .Skip(skip)
            .Limit(take)
            .ToListAsync(cancellationToken);
    }

    /// <summary>
    /// Gets the total order amount for a customer.
    /// </summary>
    public async Task<decimal> GetTotalAmountByCustomerAsync(
        string customerId,
        CancellationToken cancellationToken = default)
    {
        var filter = Builders<OrderReadModel>.Filter.Eq("customer.id", customerId);
        filter = CombineWithDefaultFilter(filter);

        var result = await Collection
            .Aggregate()
            .Match(filter)
            .Group(o => 1, g => new { Total = g.Sum(o => o.TotalAmount) })
            .FirstOrDefaultAsync(cancellationToken);

        return result?.Total ?? 0;
    }

    /// <summary>
    /// Upserts an order read model.
    /// </summary>
    public async Task UpsertAsync(
        OrderReadModel order,
        CancellationToken cancellationToken = default)
    {
        var filter = Builders<OrderReadModel>.Filter.Eq(o => o.Id, order.Id);

        await Collection.ReplaceOneAsync(
            filter,
            order,
            new ReplaceOptions { IsUpsert = true },
            cancellationToken);
    }

    /// <summary>
    /// Updates order status.
    /// </summary>
    public async Task UpdateStatusAsync(
        string id,
        string newStatus,
        CancellationToken cancellationToken = default)
    {
        var filter = Builders<OrderReadModel>.Filter.Eq(o => o.Id, id);
        var update = Builders<OrderReadModel>.Update
            .Set(o => o.Status, newStatus)
            .Set(o => o.UpdatedAt, DateTime.UtcNow);

        await Collection.UpdateOneAsync(filter, update, cancellationToken: cancellationToken);
    }

    /// <summary>
    /// Soft deletes an order.
    /// </summary>
    public async Task SoftDeleteAsync(
        string id,
        CancellationToken cancellationToken = default)
    {
        var filter = Builders<OrderReadModel>.Filter.Eq(o => o.Id, id);
        var update = Builders<OrderReadModel>.Update
            .Set(o => o.IsDeleted, true)
            .Set(o => o.UpdatedAt, DateTime.UtcNow);

        await Collection.UpdateOneAsync(filter, update, cancellationToken: cancellationToken);
    }
}
