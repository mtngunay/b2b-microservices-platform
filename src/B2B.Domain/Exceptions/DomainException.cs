namespace B2B.Domain.Exceptions;

/// <summary>
/// Base exception class for all domain-specific exceptions.
/// </summary>
public class DomainException : Exception
{
    /// <summary>
    /// Gets the error code associated with this exception.
    /// </summary>
    public string ErrorCode { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="DomainException"/> class.
    /// </summary>
    /// <param name="message">The error message.</param>
    /// <param name="errorCode">The error code.</param>
    public DomainException(string message, string errorCode = "DOMAIN_ERROR")
        : base(message)
    {
        ErrorCode = errorCode;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="DomainException"/> class.
    /// </summary>
    /// <param name="message">The error message.</param>
    /// <param name="innerException">The inner exception.</param>
    /// <param name="errorCode">The error code.</param>
    public DomainException(string message, Exception innerException, string errorCode = "DOMAIN_ERROR")
        : base(message, innerException)
    {
        ErrorCode = errorCode;
    }
}
