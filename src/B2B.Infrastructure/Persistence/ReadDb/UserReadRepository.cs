using B2B.Infrastructure.Persistence.ReadDb.ReadModels;
using MongoDB.Driver;

namespace B2B.Infrastructure.Persistence.ReadDb;

/// <summary>
/// Repository for User read model operations.
/// </summary>
public class UserReadRepository : ReadRepository<UserReadModel, string>
{
    /// <summary>
    /// Initializes a new instance of UserReadRepository.
    /// </summary>
    public UserReadRepository(MongoDbContext context)
        : base(context, "users")
    {
    }

    /// <summary>
    /// Gets a user by email address.
    /// </summary>
    public async Task<UserReadModel?> GetByEmailAsync(
        string email,
        CancellationToken cancellationToken = default)
    {
        var filter = Builders<UserReadModel>.Filter.Eq(u => u.Email, email.ToLowerInvariant());
        filter = CombineWithDefaultFilter(filter);

        return await Collection
            .Find(filter)
            .FirstOrDefaultAsync(cancellationToken);
    }

    /// <summary>
    /// Gets users by role name.
    /// </summary>
    public async Task<IEnumerable<UserReadModel>> GetByRoleAsync(
        string roleName,
        CancellationToken cancellationToken = default)
    {
        var filter = Builders<UserReadModel>.Filter.AnyEq(u => u.Roles, roleName);
        filter = CombineWithDefaultFilter(filter);

        return await Collection
            .Find(filter)
            .ToListAsync(cancellationToken);
    }

    /// <summary>
    /// Gets active users.
    /// </summary>
    public async Task<IEnumerable<UserReadModel>> GetActiveUsersAsync(
        int skip,
        int take,
        CancellationToken cancellationToken = default)
    {
        var filter = Builders<UserReadModel>.Filter.Eq(u => u.IsActive, true);
        filter = CombineWithDefaultFilter(filter);

        return await Collection
            .Find(filter)
            .Skip(skip)
            .Limit(take)
            .ToListAsync(cancellationToken);
    }

    /// <summary>
    /// Searches users by name or email.
    /// </summary>
    public async Task<IEnumerable<UserReadModel>> SearchAsync(
        string searchTerm,
        int skip,
        int take,
        CancellationToken cancellationToken = default)
    {
        var searchFilter = Builders<UserReadModel>.Filter.Or(
            Builders<UserReadModel>.Filter.Regex(u => u.FullName, new MongoDB.Bson.BsonRegularExpression(searchTerm, "i")),
            Builders<UserReadModel>.Filter.Regex(u => u.Email, new MongoDB.Bson.BsonRegularExpression(searchTerm, "i"))
        );

        var filter = CombineWithDefaultFilter(searchFilter);

        return await Collection
            .Find(filter)
            .Skip(skip)
            .Limit(take)
            .ToListAsync(cancellationToken);
    }

    /// <summary>
    /// Upserts a user read model.
    /// </summary>
    public async Task UpsertAsync(
        UserReadModel user,
        CancellationToken cancellationToken = default)
    {
        var filter = Builders<UserReadModel>.Filter.Eq(u => u.Id, user.Id);

        await Collection.ReplaceOneAsync(
            filter,
            user,
            new ReplaceOptions { IsUpsert = true },
            cancellationToken);
    }

    /// <summary>
    /// Soft deletes a user.
    /// </summary>
    public async Task SoftDeleteAsync(
        string id,
        CancellationToken cancellationToken = default)
    {
        var filter = Builders<UserReadModel>.Filter.Eq(u => u.Id, id);
        var update = Builders<UserReadModel>.Update
            .Set(u => u.IsDeleted, true)
            .Set(u => u.UpdatedAt, DateTime.UtcNow);

        await Collection.UpdateOneAsync(filter, update, cancellationToken: cancellationToken);
    }
}
