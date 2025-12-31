namespace B2B.Domain.Exceptions;

/// <summary>
/// Exception thrown when validation fails for a domain operation.
/// </summary>
public class ValidationException : DomainException
{
    /// <summary>
    /// Gets the validation errors associated with this exception.
    /// </summary>
    public IDictionary<string, string[]> Errors { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="ValidationException"/> class.
    /// </summary>
    /// <param name="message">The error message.</param>
    public ValidationException(string message)
        : base(message, "VALIDATION_ERROR")
    {
        Errors = new Dictionary<string, string[]>();
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="ValidationException"/> class.
    /// </summary>
    /// <param name="errors">The validation errors.</param>
    public ValidationException(IDictionary<string, string[]> errors)
        : base("One or more validation errors occurred.", "VALIDATION_ERROR")
    {
        Errors = errors;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="ValidationException"/> class.
    /// </summary>
    /// <param name="propertyName">The name of the property that failed validation.</param>
    /// <param name="errorMessage">The validation error message.</param>
    public ValidationException(string propertyName, string errorMessage)
        : base($"Validation failed for {propertyName}: {errorMessage}", "VALIDATION_ERROR")
    {
        Errors = new Dictionary<string, string[]>
        {
            { propertyName, new[] { errorMessage } }
        };
    }
}
