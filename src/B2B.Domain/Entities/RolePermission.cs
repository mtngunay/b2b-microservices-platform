namespace B2B.Domain.Entities;

/// <summary>
/// Join entity representing the many-to-many relationship between roles and permissions.
/// </summary>
public class RolePermission
{
    /// <summary>
    /// Gets the role identifier.
    /// </summary>
    public Guid RoleId { get; private set; }

    /// <summary>
    /// Gets the permission identifier.
    /// </summary>
    public Guid PermissionId { get; private set; }

    /// <summary>
    /// Gets the timestamp when this assignment was created.
    /// </summary>
    public DateTime AssignedAt { get; private set; }

    /// <summary>
    /// Gets the associated role.
    /// </summary>
    public Role? Role { get; private set; }

    /// <summary>
    /// Gets the associated permission.
    /// </summary>
    public Permission? Permission { get; private set; }

    /// <summary>
    /// Private constructor for EF Core.
    /// </summary>
    private RolePermission() { }

    /// <summary>
    /// Creates a new role-permission assignment.
    /// </summary>
    /// <param name="roleId">The role identifier.</param>
    /// <param name="permissionId">The permission identifier.</param>
    /// <returns>A new RolePermission instance.</returns>
    public static RolePermission Create(Guid roleId, Guid permissionId)
    {
        return new RolePermission
        {
            RoleId = roleId,
            PermissionId = permissionId,
            AssignedAt = DateTime.UtcNow
        };
    }
}
