using B2B.Application.Interfaces.Services;
using B2B.Infrastructure.Persistence.ReadDb.ReadModels;
using Microsoft.Extensions.Options;
using MongoDB.Driver;

namespace B2B.Infrastructure.Persistence.ReadDb;

/// <summary>
/// MongoDB context for read operations.
/// </summary>
public class MongoDbContext
{
    private readonly IMongoDatabase _database;
    private readonly ICurrentUserService? _currentUserService;
    private readonly string? _tenantId;

    /// <summary>
    /// Gets the Users collection.
    /// </summary>
    public IMongoCollection<UserReadModel> Users => _database.GetCollection<UserReadModel>("users");

    /// <summary>
    /// Gets the Orders collection.
    /// </summary>
    public IMongoCollection<OrderReadModel> Orders => _database.GetCollection<OrderReadModel>("orders");

    /// <summary>
    /// Initializes a new instance of MongoDbContext.
    /// </summary>
    public MongoDbContext(IOptions<MongoDbSettings> settings)
    {
        var client = new MongoClient(settings.Value.ConnectionString);
        _database = client.GetDatabase(settings.Value.DatabaseName);
    }

    /// <summary>
    /// Initializes a new instance of MongoDbContext with current user service.
    /// </summary>
    public MongoDbContext(
        IOptions<MongoDbSettings> settings,
        ICurrentUserService currentUserService)
    {
        var client = new MongoClient(settings.Value.ConnectionString);
        _database = client.GetDatabase(settings.Value.DatabaseName);
        _currentUserService = currentUserService;
        _tenantId = currentUserService.TenantId;
    }

    /// <summary>
    /// Gets a collection by name.
    /// </summary>
    public IMongoCollection<T> GetCollection<T>(string name)
    {
        return _database.GetCollection<T>(name);
    }

    /// <summary>
    /// Gets the current tenant ID for filtering.
    /// </summary>
    public string? CurrentTenantId => _tenantId;

    /// <summary>
    /// Creates a tenant filter for queries.
    /// </summary>
    public FilterDefinition<T> CreateTenantFilter<T>() where T : class
    {
        if (string.IsNullOrEmpty(_tenantId))
        {
            return Builders<T>.Filter.Empty;
        }

        return Builders<T>.Filter.Eq("tenantId", _tenantId);
    }

    /// <summary>
    /// Creates a combined filter with tenant isolation and soft delete.
    /// </summary>
    public FilterDefinition<T> CreateDefaultFilter<T>() where T : class
    {
        var builder = Builders<T>.Filter;
        var filters = new List<FilterDefinition<T>>
        {
            builder.Eq("isDeleted", false)
        };

        if (!string.IsNullOrEmpty(_tenantId))
        {
            filters.Add(builder.Eq("tenantId", _tenantId));
        }

        return builder.And(filters);
    }

    /// <summary>
    /// Ensures indexes are created for all collections.
    /// </summary>
    public async Task EnsureIndexesAsync()
    {
        await CreateUserIndexesAsync();
        await CreateOrderIndexesAsync();
    }

    private async Task CreateUserIndexesAsync()
    {
        var indexKeys = Builders<UserReadModel>.IndexKeys;

        var indexes = new List<CreateIndexModel<UserReadModel>>
        {
            new(indexKeys.Ascending(u => u.TenantId)),
            new(indexKeys.Ascending(u => u.Email)),
            new(indexKeys.Combine(
                indexKeys.Ascending(u => u.TenantId),
                indexKeys.Ascending(u => u.Email)),
                new CreateIndexOptions { Unique = true }),
            new(indexKeys.Ascending(u => u.IsDeleted)),
            new(indexKeys.Ascending(u => u.IsActive))
        };

        await Users.Indexes.CreateManyAsync(indexes);
    }

    private async Task CreateOrderIndexesAsync()
    {
        var indexKeys = Builders<OrderReadModel>.IndexKeys;

        var indexes = new List<CreateIndexModel<OrderReadModel>>
        {
            new(indexKeys.Ascending(o => o.TenantId)),
            new(indexKeys.Ascending(o => o.OrderNumber)),
            new(indexKeys.Combine(
                indexKeys.Ascending(o => o.TenantId),
                indexKeys.Ascending(o => o.OrderNumber)),
                new CreateIndexOptions { Unique = true }),
            new(indexKeys.Ascending(o => o.Status)),
            new(indexKeys.Ascending(o => o.CreatedAt)),
            new(indexKeys.Ascending(o => o.IsDeleted)),
            new(indexKeys.Ascending("customer.id"))
        };

        await Orders.Indexes.CreateManyAsync(indexes);
    }
}

/// <summary>
/// MongoDB connection settings.
/// </summary>
public class MongoDbSettings
{
    /// <summary>
    /// Gets or sets the connection string.
    /// </summary>
    public string ConnectionString { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the database name.
    /// </summary>
    public string DatabaseName { get; set; } = string.Empty;
}
