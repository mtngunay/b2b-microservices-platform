using B2B.Domain.Aggregates;

namespace B2B.Domain.Entities;

/// <summary>
/// Join entity representing the many-to-many relationship between users and roles.
/// </summary>
public class UserRole
{
    /// <summary>
    /// Gets the user identifier.
    /// </summary>
    public Guid UserId { get; private set; }

    /// <summary>
    /// Gets the role identifier.
    /// </summary>
    public Guid RoleId { get; private set; }

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
    /// Gets the associated role.
    /// </summary>
    public Role? Role { get; private set; }

    /// <summary>
    /// Private constructor for EF Core.
    /// </summary>
    private UserRole() { }

    /// <summary>
    /// Creates a new user-role assignment.
    /// </summary>
    /// <param name="userId">The user identifier.</param>
    /// <param name="roleId">The role identifier.</param>
    /// <param name="assignedBy">The identifier of the user making the assignment.</param>
    /// <returns>A new UserRole instance.</returns>
    public static UserRole Create(Guid userId, Guid roleId, string assignedBy = "")
    {
        return new UserRole
        {
            UserId = userId,
            RoleId = roleId,
            AssignedAt = DateTime.UtcNow,
            AssignedBy = assignedBy
        };
    }
}
