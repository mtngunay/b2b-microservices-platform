namespace B2B.Domain.Entities;

/// <summary>
/// Represents a role that can be assigned to users.
/// </summary>
public class Role : BaseEntity
{
    /// <summary>
    /// Gets the name of the role.
    /// </summary>
    public string Name { get; private set; } = string.Empty;

    /// <summary>
    /// Gets the description of the role.
    /// </summary>
    public string Description { get; private set; } = string.Empty;

    /// <summary>
    /// Gets a value indicating whether this is a system role that cannot be deleted.
    /// </summary>
    public bool IsSystemRole { get; private set; }

    /// <summary>
    /// Gets the collection of role permissions associated with this role.
    /// </summary>
    public ICollection<RolePermission> RolePermissions { get; private set; } = new List<RolePermission>();

    /// <summary>
    /// Gets the collection of user roles associated with this role.
    /// </summary>
    public ICollection<UserRole> UserRoles { get; private set; } = new List<UserRole>();

    /// <summary>
    /// Private constructor for EF Core.
    /// </summary>
    private Role() { }

    /// <summary>
    /// Creates a new role.
    /// </summary>
    /// <param name="name">The name of the role.</param>
    /// <param name="description">The description of the role.</param>
    /// <param name="tenantId">The tenant identifier.</param>
    /// <param name="isSystemRole">Whether this is a system role.</param>
    /// <returns>A new Role instance.</returns>
    public static Role Create(string name, string description, string tenantId, bool isSystemRole = false)
    {
        var role = new Role
        {
            Name = name,
            Description = description,
            IsSystemRole = isSystemRole
        };
        role.SetTenantId(tenantId);
        return role;
    }

    /// <summary>
    /// Updates the role details.
    /// </summary>
    /// <param name="name">The new name.</param>
    /// <param name="description">The new description.</param>
    public void Update(string name, string description)
    {
        Name = name;
        Description = description;
    }

    /// <summary>
    /// Adds a permission to this role.
    /// </summary>
    /// <param name="permission">The permission to add.</param>
    public void AddPermission(Permission permission)
    {
        if (!RolePermissions.Any(rp => rp.PermissionId == permission.Id))
        {
            RolePermissions.Add(RolePermission.Create(Id, permission.Id));
        }
    }

    /// <summary>
    /// Removes a permission from this role.
    /// </summary>
    /// <param name="permissionId">The ID of the permission to remove.</param>
    public void RemovePermission(Guid permissionId)
    {
        var rolePermission = RolePermissions.FirstOrDefault(rp => rp.PermissionId == permissionId);
        if (rolePermission != null)
        {
            RolePermissions.Remove(rolePermission);
        }
    }
}
