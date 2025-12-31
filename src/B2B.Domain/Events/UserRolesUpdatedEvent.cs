namespace B2B.Domain.Events;

/// <summary>
/// Event raised when a user's roles are updated.
/// </summary>
/// <param name="UserId">The unique identifier of the user.</param>
/// <param name="OldRoles">The previous roles assigned to the user.</param>
/// <param name="NewRoles">The new roles assigned to the user.</param>
/// <param name="CorrelationId">The correlation ID for distributed tracing.</param>
/// <param name="TenantId">The tenant ID for multi-tenancy support.</param>
public record UserRolesUpdatedEvent(
    Guid UserId,
    List<string> OldRoles,
    List<string> NewRoles,
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
