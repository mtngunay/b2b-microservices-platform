namespace B2B.Application.DTOs;

/// <summary>
/// Data transfer object for Role entity.
/// </summary>
public class RoleDto
{
    /// <summary>
    /// Gets or sets the role identifier.
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// Gets or sets the role name.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the role description.
    /// </summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets a value indicating whether this is a system role.
    /// </summary>
    public bool IsSystemRole { get; set; }

    /// <summary>
    /// Gets or sets the tenant identifier.
    /// </summary>
    public string TenantId { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the permissions associated with this role.
    /// </summary>
    public List<string> Permissions { get; set; } = new();

    /// <summary>
    /// Gets or sets the timestamp when the role was created.
    /// </summary>
    public DateTime CreatedAt { get; set; }
}
