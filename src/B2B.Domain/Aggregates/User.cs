using B2B.Domain.Entities;
using B2B.Domain.Events;
using B2B.Domain.Exceptions;

namespace B2B.Domain.Aggregates;

/// <summary>
/// User aggregate root representing a system user.
/// </summary>
public class User : AggregateRoot
{
    /// <summary>
    /// Gets the email address of the user.
    /// </summary>
    public string Email { get; private set; } = string.Empty;

    /// <summary>
    /// Gets the hashed password of the user.
    /// </summary>
    public string PasswordHash { get; private set; } = string.Empty;

    /// <summary>
    /// Gets the first name of the user.
    /// </summary>
    public string FirstName { get; private set; } = string.Empty;

    /// <summary>
    /// Gets the last name of the user.
    /// </summary>
    public string LastName { get; private set; } = string.Empty;

    /// <summary>
    /// Gets the full name of the user.
    /// </summary>
    public string FullName => $"{FirstName} {LastName}".Trim();

    /// <summary>
    /// Gets a value indicating whether the user is active.
    /// </summary>
    public bool IsActive { get; private set; }

    /// <summary>
    /// Gets the timestamp of the user's last login.
    /// </summary>
    public DateTime? LastLoginAt { get; private set; }

    /// <summary>
    /// Gets the collection of user roles.
    /// </summary>
    public ICollection<UserRole> UserRoles { get; private set; } = new List<UserRole>();

    /// <summary>
    /// Gets the collection of direct user permissions.
    /// </summary>
    public ICollection<UserPermission> UserPermissions { get; private set; } = new List<UserPermission>();

    /// <summary>
    /// Private constructor for EF Core.
    /// </summary>
    private User() { }

    /// <summary>
    /// Creates a new user.
    /// </summary>
    /// <param name="email">The email address.</param>
    /// <param name="passwordHash">The hashed password.</param>
    /// <param name="firstName">The first name.</param>
    /// <param name="lastName">The last name.</param>
    /// <param name="tenantId">The tenant identifier.</param>
    /// <param name="correlationId">The correlation ID for tracing.</param>
    /// <returns>A new User instance.</returns>
    public static User Create(
        string email,
        string passwordHash,
        string firstName,
        string lastName,
        string tenantId,
        string correlationId = "")
    {
        if (string.IsNullOrWhiteSpace(email))
            throw new ValidationException(nameof(email), "Email is required.");

        if (string.IsNullOrWhiteSpace(passwordHash))
            throw new ValidationException(nameof(passwordHash), "Password is required.");

        var user = new User
        {
            Email = email.ToLowerInvariant(),
            PasswordHash = passwordHash,
            FirstName = firstName,
            LastName = lastName,
            IsActive = true
        };
        user.SetTenantId(tenantId);

        user.AddDomainEvent(new UserCreatedEvent(
            user.Id,
            user.Email,
            user.FullName,
            new List<string>(),
            correlationId,
            tenantId));

        return user;
    }

    /// <summary>
    /// Updates the user's profile information.
    /// </summary>
    /// <param name="firstName">The new first name.</param>
    /// <param name="lastName">The new last name.</param>
    public void UpdateProfile(string firstName, string lastName)
    {
        FirstName = firstName;
        LastName = lastName;
    }

    /// <summary>
    /// Updates the user's password.
    /// </summary>
    /// <param name="newPasswordHash">The new hashed password.</param>
    public void UpdatePassword(string newPasswordHash)
    {
        if (string.IsNullOrWhiteSpace(newPasswordHash))
            throw new ValidationException(nameof(newPasswordHash), "Password is required.");

        PasswordHash = newPasswordHash;
    }

    /// <summary>
    /// Records a successful login.
    /// </summary>
    public void RecordLogin()
    {
        LastLoginAt = DateTime.UtcNow;
    }

    /// <summary>
    /// Activates the user account.
    /// </summary>
    public void Activate()
    {
        IsActive = true;
    }

    /// <summary>
    /// Deactivates the user account.
    /// </summary>
    public void Deactivate()
    {
        IsActive = false;
    }

    /// <summary>
    /// Assigns a role to the user.
    /// </summary>
    /// <param name="role">The role to assign.</param>
    /// <param name="assignedBy">The identifier of the user making the assignment.</param>
    /// <param name="correlationId">The correlation ID for tracing.</param>
    public void AssignRole(Role role, string assignedBy = "", string correlationId = "")
    {
        if (UserRoles.Any(ur => ur.RoleId == role.Id))
            return;

        var oldRoles = UserRoles.Select(ur => ur.Role?.Name ?? string.Empty).ToList();
        UserRoles.Add(UserRole.Create(Id, role.Id, assignedBy));
        var newRoles = oldRoles.Append(role.Name).ToList();

        AddDomainEvent(new UserRolesUpdatedEvent(
            Id,
            oldRoles,
            newRoles,
            correlationId,
            TenantId));
    }

    /// <summary>
    /// Removes a role from the user.
    /// </summary>
    /// <param name="roleId">The ID of the role to remove.</param>
    /// <param name="correlationId">The correlation ID for tracing.</param>
    public void RemoveRole(Guid roleId, string correlationId = "")
    {
        var userRole = UserRoles.FirstOrDefault(ur => ur.RoleId == roleId);
        if (userRole == null)
            return;

        var oldRoles = UserRoles.Select(ur => ur.Role?.Name ?? string.Empty).ToList();
        UserRoles.Remove(userRole);
        var newRoles = UserRoles.Select(ur => ur.Role?.Name ?? string.Empty).ToList();

        AddDomainEvent(new UserRolesUpdatedEvent(
            Id,
            oldRoles,
            newRoles,
            correlationId,
            TenantId));
    }

    /// <summary>
    /// Assigns a direct permission to the user.
    /// </summary>
    /// <param name="permission">The permission to assign.</param>
    /// <param name="assignedBy">The identifier of the user making the assignment.</param>
    /// <param name="correlationId">The correlation ID for tracing.</param>
    public void AssignPermission(Permission permission, string assignedBy = "", string correlationId = "")
    {
        if (UserPermissions.Any(up => up.PermissionId == permission.Id))
            return;

        UserPermissions.Add(UserPermission.Create(Id, permission.Id, assignedBy));

        AddDomainEvent(new PermissionChangedEvent(
            Id,
            TenantId,
            correlationId));
    }

    /// <summary>
    /// Removes a direct permission from the user.
    /// </summary>
    /// <param name="permissionId">The ID of the permission to remove.</param>
    /// <param name="correlationId">The correlation ID for tracing.</param>
    public void RemovePermission(Guid permissionId, string correlationId = "")
    {
        var userPermission = UserPermissions.FirstOrDefault(up => up.PermissionId == permissionId);
        if (userPermission == null)
            return;

        UserPermissions.Remove(userPermission);

        AddDomainEvent(new PermissionChangedEvent(
            Id,
            TenantId,
            correlationId));
    }

    /// <summary>
    /// Gets all role names assigned to this user.
    /// </summary>
    /// <returns>A list of role names.</returns>
    public IReadOnlyList<string> GetRoleNames()
    {
        return UserRoles
            .Where(ur => ur.Role != null)
            .Select(ur => ur.Role!.Name)
            .ToList()
            .AsReadOnly();
    }
}
