namespace B2B.Application.Attributes;

/// <summary>
/// Attribute to specify required roles for a command or query.
/// Used by the AuthorizationBehavior to enforce RBAC.
/// </summary>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = true, Inherited = true)]
public class RequireRoleAttribute : Attribute
{
    /// <summary>
    /// Gets the required role name.
    /// </summary>
    public string Role { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="RequireRoleAttribute"/> class.
    /// </summary>
    /// <param name="role">The required role name (e.g., "Admin").</param>
    public RequireRoleAttribute(string role)
    {
        Role = role;
    }
}
