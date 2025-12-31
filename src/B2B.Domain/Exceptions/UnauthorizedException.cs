namespace B2B.Domain.Exceptions;

/// <summary>
/// Exception thrown when authentication is required but not provided or invalid.
/// </summary>
public class UnauthorizedException : DomainException
{
    /// <summary>
    /// Initializes a new instance of the <see cref="UnauthorizedException"/> class.
    /// </summary>
    public UnauthorizedException()
        : base("Authentication is required to access this resource.", "UNAUTHORIZED")
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="UnauthorizedException"/> class.
    /// </summary>
    /// <param name="message">The error message.</param>
    public UnauthorizedException(string message)
        : base(message, "UNAUTHORIZED")
    {
    }
}
