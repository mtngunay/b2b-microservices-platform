namespace B2B.Domain.Events;

/// <summary>
/// Event raised when a user's permissions are changed.
/// </summary>
/// <param name="UserId">The unique identifier of the user whose permissions changed.</param>
/// <param name="TenantId">The tenant ID for multi-tenancy support.</param>
/// <param name="CorrelationId">The correlation ID for distributed tracing.</param>
public record PermissionChangedEvent(
    Guid UserId,
    string TenantId,
    string CorrelationId) : IntegrationEvent
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
