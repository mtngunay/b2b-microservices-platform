namespace B2B.Domain.Exceptions;

/// <summary>
/// Exception thrown when a requested resource is not found.
/// </summary>
public class NotFoundException : DomainException
{
    /// <summary>
    /// Gets the name of the resource that was not found.
    /// </summary>
    public string ResourceName { get; }

    /// <summary>
    /// Gets the identifier of the resource that was not found.
    /// </summary>
    public object? ResourceId { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="NotFoundException"/> class.
    /// </summary>
    /// <param name="message">The error message.</param>
    public NotFoundException(string message)
        : base(message, "NOT_FOUND")
    {
        ResourceName = string.Empty;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="NotFoundException"/> class.
    /// </summary>
    /// <param name="resourceName">The name of the resource that was not found.</param>
    /// <param name="resourceId">The identifier of the resource that was not found.</param>
    public NotFoundException(string resourceName, object resourceId)
        : base($"{resourceName} with ID '{resourceId}' was not found.", "NOT_FOUND")
    {
        ResourceName = resourceName;
        ResourceId = resourceId;
    }
}
