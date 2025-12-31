namespace B2B.Domain.Entities;

/// <summary>
/// Represents a permission that can be assigned to roles or users.
/// </summary>
public class Permission : BaseEntity
{
    /// <summary>
    /// Gets the unique name of the permission (e.g., "orders.create").
    /// </summary>
    public string Name { get; private set; } = string.Empty;

    /// <summary>
    /// Gets the resource this permission applies to (e.g., "orders").
    /// </summary>
    public string Resource { get; private set; } = string.Empty;

    /// <summary>
    /// Gets the action this permission allows (e.g., "create", "read", "update", "delete").
    /// </summary>
    public string Action { get; private set; } = string.Empty;

    /// <summary>
    /// Gets the description of the permission.
    /// </summary>
    public string Description { get; private set; } = string.Empty;

    /// <summary>
    /// Gets the collection of role permissions associated with this permission.
    /// </summary>
    public ICollection<RolePermission> RolePermissions { get; private set; } = new List<RolePermission>();

    /// <summary>
    /// Gets the collection of user permissions associated with this permission.
    /// </summary>
    public ICollection<UserPermission> UserPermissions { get; private set; } = new List<UserPermission>();

    /// <summary>
    /// Private constructor for EF Core.
    /// </summary>
    private Permission() { }

    /// <summary>
    /// Creates a new permission.
    /// </summary>
    /// <param name="name">The unique name of the permission.</param>
    /// <param name="resource">The resource this permission applies to.</param>
    /// <param name="action">The action this permission allows.</param>
    /// <param name="description">The description of the permission.</param>
    /// <param name="tenantId">The tenant identifier.</param>
    /// <returns>A new Permission instance.</returns>
    public static Permission Create(string name, string resource, string action, string description, string tenantId)
    {
        var permission = new Permission
        {
            Name = name,
            Resource = resource,
            Action = action,
            Description = description
        };
        permission.SetTenantId(tenantId);
        return permission;
    }

    /// <summary>
    /// Updates the permission details.
    /// </summary>
    /// <param name="description">The new description.</param>
    public void Update(string description)
    {
        Description = description;
    }
}
