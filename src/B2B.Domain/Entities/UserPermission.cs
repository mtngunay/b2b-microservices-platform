using B2B.Domain.Aggregates;

namespace B2B.Domain.Entities;

/// <summary>
/// Join entity representing direct permission assignments to users (bypassing roles).
/// </summary>
public class UserPermission
{
    /// <summary>
    /// Gets the user identifier.
    /// </summary>
    public Guid UserId { get; private set; }

    /// <summary>
    /// Gets the permission identifier.
    /// </summary>
    public Guid PermissionId { get; private set; }

    /// <summary>
    /// Gets the timestamp when this assignment was created.
    /// </summary>
    public DateTime AssignedAt { get; private set; }

    /// <summary>
    /// Gets the identifier of the user who made this assignment.
    /// </summary>
    public string AssignedBy { get; private set; } = string.Empty;

    /// <summary>
    /// Gets the associated user.
    /// </summary>
    public User? User { get; private set; }

    /// <summary>
    /// Gets the associated permission.
    /// </summary>
    public Permission? Permission { get; private set; }

    /// <summary>
    /// Private constructor for EF Core.
    /// </summary>
    private UserPermission() { }

    /// <summary>
    /// Creates a new user-permission assignment.
    /// </summary>
    /// <param name="userId">The user identifier.</param>
    /// <param name="permissionId">The permission identifier.</param>
    /// <param name="assignedBy">The identifier of the user making the assignment.</param>
    /// <returns>A new UserPermission instance.</returns>
    public static UserPermission Create(Guid userId, Guid permissionId, string assignedBy = "")
    {
        return new UserPermission
        {
            UserId = userId,
            PermissionId = permissionId,
            AssignedAt = DateTime.UtcNow,
            AssignedBy = assignedBy
        };
    }
}
