namespace B2B.Domain.Interfaces;

/// <summary>
/// Interface for entities that belong to a specific tenant.
/// </summary>
public interface ITenantEntity
{
    /// <summary>
    /// Gets the tenant identifier this entity belongs to.
    /// </summary>
    string TenantId { get; }
}
