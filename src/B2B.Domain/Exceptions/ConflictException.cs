namespace B2B.Domain.Exceptions;

/// <summary>
/// Exception thrown when a resource conflict occurs (e.g., duplicate entry, version mismatch).
/// </summary>
public class ConflictException : DomainException
{
    /// <summary>
    /// Gets the name of the resource that caused the conflict.
    /// </summary>
    public string? ResourceName { get; }

    /// <summary>
    /// Gets the identifier of the conflicting resource.
    /// </summary>
    public object? ResourceId { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="ConflictException"/> class.
    /// </summary>
    /// <param name="message">The error message.</param>
    public ConflictException(string message)
        : base(message, "CONFLICT")
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="ConflictException"/> class.
    /// </summary>
    /// <param name="resourceName">The name of the resource that caused the conflict.</param>
    /// <param name="resourceId">The identifier of the conflicting resource.</param>
    public ConflictException(string resourceName, object resourceId)
        : base($"A conflict occurred with {resourceName} having ID '{resourceId}'.", "CONFLICT")
    {
        ResourceName = resourceName;
        ResourceId = resourceId;
    }

    /// <summary>
    /// Creates a ConflictException for a duplicate resource.
    /// </summary>
    /// <param name="resourceName">The name of the resource.</param>
    /// <param name="propertyName">The name of the property that caused the duplicate.</param>
    /// <param name="propertyValue">The value of the property that caused the duplicate.</param>
    /// <returns>A new ConflictException instance.</returns>
    public static ConflictException Duplicate(string resourceName, string propertyName, object propertyValue)
    {
        return new ConflictException($"{resourceName} with {propertyName} '{propertyValue}' already exists.");
    }
}
