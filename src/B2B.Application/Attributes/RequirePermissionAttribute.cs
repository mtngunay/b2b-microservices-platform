namespace B2B.Application.Attributes;

/// <summary>
/// Attribute to specify required permissions for a command or query.
/// Used by the AuthorizationBehavior to enforce RBAC/ABAC.
/// </summary>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = true, Inherited = true)]
public class RequirePermissionAttribute : Attribute
{
    /// <summary>
    /// Gets the required permission name.
    /// </summary>
    public string Permission { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="RequirePermissionAttribute"/> class.
    /// </summary>
    /// <param name="permission">The required permission name (e.g., "orders.create").</param>
    public RequirePermissionAttribute(string permission)
    {
        Permission = permission;
    }
}
