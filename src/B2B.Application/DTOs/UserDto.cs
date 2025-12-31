namespace B2B.Application.DTOs;

/// <summary>
/// Data transfer object for User entity.
/// </summary>
public class UserDto
{
    /// <summary>
    /// Gets or sets the user identifier.
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// Gets or sets the user's email address.
    /// </summary>
    public string Email { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the user's first name.
    /// </summary>
    public string FirstName { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the user's last name.
    /// </summary>
    public string LastName { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the user's full name.
    /// </summary>
    public string FullName { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets a value indicating whether the user is active.
    /// </summary>
    public bool IsActive { get; set; }

    /// <summary>
    /// Gets or sets the tenant identifier.
    /// </summary>
    public string TenantId { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the user's roles.
    /// </summary>
    public List<string> Roles { get; set; } = new();

    /// <summary>
    /// Gets or sets the user's direct permissions.
    /// </summary>
    public List<string> Permissions { get; set; } = new();

    /// <summary>
    /// Gets or sets the timestamp when the user was created.
    /// </summary>
    public DateTime CreatedAt { get; set; }

    /// <summary>
    /// Gets or sets the timestamp of the user's last login.
    /// </summary>
    public DateTime? LastLoginAt { get; set; }
}
