using B2B.Application.Interfaces.Services;
using B2B.Domain.Events;
using Microsoft.Extensions.Logging;

namespace B2B.Infrastructure.Messaging.Consumers;

/// <summary>
/// Consumer for PermissionChangedEvent that invalidates the permission cache.
/// </summary>
public class PermissionChangedEventConsumer : BaseConsumer<PermissionChangedEvent>
{
    private readonly IPermissionService _permissionService;
    private readonly ILogger<PermissionChangedEventConsumer> _logger;

    /// <summary>
    /// Initializes a new instance of PermissionChangedEventConsumer.
    /// </summary>
    public PermissionChangedEventConsumer(
        IPermissionService permissionService,
        ICacheService cacheService,
        ICorrelationIdAccessor correlationIdAccessor,
        ILogger<PermissionChangedEventConsumer> logger)
        : base(cacheService, correlationIdAccessor, logger)
    {
        _permissionService = permissionService ?? throw new ArgumentNullException(nameof(permissionService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc />
    protected override async Task HandleAsync(
        PermissionChangedEvent @event,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation(
            "Processing PermissionChangedEvent for user {UserId} in tenant {TenantId}",
            @event.UserId,
            @event.TenantId);

        // Invalidate the permission cache for the user
        await _permissionService.InvalidatePermissionCacheAsync(
            @event.UserId.ToString(),
            cancellationToken);

        _logger.LogInformation(
            "Successfully invalidated permission cache for user {UserId}",
            @event.UserId);
    }
}
