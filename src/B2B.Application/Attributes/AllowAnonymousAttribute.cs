namespace B2B.Application.Attributes;

/// <summary>
/// Attribute to allow anonymous access to a command or query.
/// Bypasses the AuthorizationBehavior checks.
/// </summary>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = true)]
public class AllowAnonymousAttribute : Attribute
{
}
