namespace B2B.Domain.Exceptions;

/// <summary>
/// Exception thrown when the user is authenticated but lacks permission to access a resource.
/// </summary>
public class ForbiddenException : DomainException
{
    /// <summary>
    /// Gets the required permission that was missing.
    /// </summary>
    public string? RequiredPermission { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="ForbiddenException"/> class.
    /// </summary>
    public ForbiddenException()
        : base("You do not have permission to access this resource.", "FORBIDDEN")
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="ForbiddenException"/> class.
    /// </summary>
    /// <param name="message">The error message.</param>
    public ForbiddenException(string message)
        : base(message, "FORBIDDEN")
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="ForbiddenException"/> class.
    /// </summary>
    /// <param name="message">The error message.</param>
    /// <param name="requiredPermission">The permission that was required.</param>
    public ForbiddenException(string message, string requiredPermission)
        : base(message, "FORBIDDEN")
    {
        RequiredPermission = requiredPermission;
    }
}
