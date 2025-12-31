using B2B.Domain.Interfaces;

namespace B2B.Domain.Entities;

/// <summary>
/// Base abstract class for all domain entities with audit fields and soft delete support.
/// </summary>
public abstract class BaseEntity : IEntity<Guid>, ITenantEntity
{
    /// <summary>
    /// Gets the unique identifier of the entity.
    /// </summary>
    public Guid Id { get; protected set; } = Guid.NewGuid();

    /// <summary>
    /// Gets the tenant identifier this entity belongs to.
    /// </summary>
    public string TenantId { get; protected set; } = string.Empty;

    /// <summary>
    /// Gets the timestamp when the entity was created.
    /// </summary>
    public DateTime CreatedAt { get; protected set; } = DateTime.UtcNow;

    /// <summary>
    /// Gets the timestamp when the entity was last updated.
    /// </summary>
    public DateTime? UpdatedAt { get; protected set; }

    /// <summary>
    /// Gets the identifier of the user who created this entity.
    /// </summary>
    public string CreatedBy { get; protected set; } = string.Empty;

    /// <summary>
    /// Gets the identifier of the user who last updated this entity.
    /// </summary>
    public string? UpdatedBy { get; protected set; }

    /// <summary>
    /// Gets a value indicating whether this entity has been soft deleted.
    /// </summary>
    public bool IsDeleted { get; protected set; }

    /// <summary>
    /// Sets the tenant identifier for this entity.
    /// </summary>
    /// <param name="tenantId">The tenant identifier.</param>
    public void SetTenantId(string tenantId)
    {
        TenantId = tenantId;
    }

    /// <summary>
    /// Sets the audit information for entity creation.
    /// </summary>
    /// <param name="userId">The identifier of the user creating the entity.</param>
    public void SetCreatedBy(string userId)
    {
        CreatedBy = userId;
        CreatedAt = DateTime.UtcNow;
    }

    /// <summary>
    /// Sets the audit information for entity update.
    /// </summary>
    /// <param name="userId">The identifier of the user updating the entity.</param>
    public void SetUpdatedBy(string userId)
    {
        UpdatedBy = userId;
        UpdatedAt = DateTime.UtcNow;
    }

    /// <summary>
    /// Marks the entity as soft deleted.
    /// </summary>
    public void MarkAsDeleted()
    {
        IsDeleted = true;
        UpdatedAt = DateTime.UtcNow;
    }
}
