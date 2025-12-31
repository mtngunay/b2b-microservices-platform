using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace B2B.Infrastructure.Persistence.ReadDb.ReadModels;

/// <summary>
/// Read model for User entity optimized for queries.
/// </summary>
public class UserReadModel
{
    /// <summary>
    /// Gets or sets the unique identifier.
    /// </summary>
    [BsonId]
    [BsonRepresentation(BsonType.String)]
    public string Id { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the tenant identifier.
    /// </summary>
    [BsonElement("tenantId")]
    public string TenantId { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the email address.
    /// </summary>
    [BsonElement("email")]
    public string Email { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the full name.
    /// </summary>
    [BsonElement("fullName")]
    public string FullName { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the first name.
    /// </summary>
    [BsonElement("firstName")]
    public string FirstName { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the last name.
    /// </summary>
    [BsonElement("lastName")]
    public string LastName { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets whether the user is active.
    /// </summary>
    [BsonElement("isActive")]
    public bool IsActive { get; set; }

    /// <summary>
    /// Gets or sets the list of role names.
    /// </summary>
    [BsonElement("roles")]
    public List<string> Roles { get; set; } = new();

    /// <summary>
    /// Gets or sets the list of permission names.
    /// </summary>
    [BsonElement("permissions")]
    public List<string> Permissions { get; set; } = new();

    /// <summary>
    /// Gets or sets the creation timestamp.
    /// </summary>
    [BsonElement("createdAt")]
    public DateTime CreatedAt { get; set; }

    /// <summary>
    /// Gets or sets the last update timestamp.
    /// </summary>
    [BsonElement("updatedAt")]
    public DateTime? UpdatedAt { get; set; }

    /// <summary>
    /// Gets or sets the last login timestamp.
    /// </summary>
    [BsonElement("lastLoginAt")]
    public DateTime? LastLoginAt { get; set; }

    /// <summary>
    /// Gets or sets whether the user is deleted.
    /// </summary>
    [BsonElement("isDeleted")]
    public bool IsDeleted { get; set; }
}
