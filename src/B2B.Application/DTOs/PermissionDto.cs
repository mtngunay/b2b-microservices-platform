namespace B2B.Application.DTOs;

/// <summary>
/// Data transfer object for Permission entity.
/// </summary>
public class PermissionDto
{
    /// <summary>
    /// Gets or sets the permission identifier.
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// Gets or sets the unique permission name (e.g., "orders.create").
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the resource this permission applies to (e.g., "orders").
    /// </summary>
    public string Resource { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the action this permission allows (e.g., "create").
    /// </summary>
    public string Action { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the permission description.
    /// </summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the tenant identifier.
    /// </summary>
    public string TenantId { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the timestamp when the permission was created.
    /// </summary>
    public DateTime CreatedAt { get; set; }
}
