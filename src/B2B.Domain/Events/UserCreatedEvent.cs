namespace B2B.Domain.Events;

/// <summary>
/// Event raised when a new user is created.
/// </summary>
/// <param name="UserId">The unique identifier of the created user.</param>
/// <param name="Email">The email address of the user.</param>
/// <param name="FullName">The full name of the user.</param>
/// <param name="Roles">The initial roles assigned to the user.</param>
/// <param name="CorrelationId">The correlation ID for distributed tracing.</param>
/// <param name="TenantId">The tenant ID for multi-tenancy support.</param>
public record UserCreatedEvent(
    Guid UserId,
    string Email,
    string FullName,
    List<string> Roles,
    string CorrelationId,
    string TenantId) : IntegrationEvent
{
    /// <summary>
    /// Gets the correlation ID for distributed tracing.
    /// </summary>
    public new string CorrelationId { get; init; } = CorrelationId;

    /// <summary>
    /// Gets the tenant ID for multi-tenancy support.
    /// </summary>
    public new string TenantId { get; init; } = TenantId;
}
