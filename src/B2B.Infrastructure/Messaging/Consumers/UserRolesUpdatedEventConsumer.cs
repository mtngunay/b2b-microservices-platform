using B2B.Application.Interfaces.Services;
using B2B.Domain.Events;
using B2B.Infrastructure.Persistence.ReadDb;
using Microsoft.Extensions.Logging;
using MongoDB.Driver;

namespace B2B.Infrastructure.Messaging.Consumers;

/// <summary>
/// Consumer for UserRolesUpdatedEvent that updates the MongoDB read model and invalidates cache.
/// </summary>
public class UserRolesUpdatedEventConsumer : BaseConsumer<UserRolesUpdatedEvent>
{
    private readonly UserReadRepository _userReadRepository;
    private readonly IPermissionService _permissionService;
    private readonly ILogger<UserRolesUpdatedEventConsumer> _logger;

    /// <summary>
    /// Initializes a new instance of UserRolesUpdatedEventConsumer.
    /// </summary>
    public UserRolesUpdatedEventConsumer(
        UserReadRepository userReadRepository,
        IPermissionService permissionService,
        ICacheService cacheService,
        ICorrelationIdAccessor correlationIdAccessor,
        ILogger<UserRolesUpdatedEventConsumer> logger)
        : base(cacheService, correlationIdAccessor, logger)
    {
        _userReadRepository = userReadRepository ?? throw new ArgumentNullException(nameof(userReadRepository));
        _permissionService = permissionService ?? throw new ArgumentNullException(nameof(permissionService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc />
    protected override async Task HandleAsync(
        UserRolesUpdatedEvent @event,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation(
            "Processing UserRolesUpdatedEvent for user {UserId}. Old roles: [{OldRoles}], New roles: [{NewRoles}]",
            @event.UserId,
            string.Join(", ", @event.OldRoles),
            string.Join(", ", @event.NewRoles));

        // Get the existing user read model
        var userId = @event.UserId.ToString();
        var existingUser = await _userReadRepository.GetByIdAsync(userId, cancellationToken);

        if (existingUser != null)
        {
            // Update the roles in the read model
            existingUser.Roles = @event.NewRoles ?? new List<string>();
            existingUser.UpdatedAt = DateTime.UtcNow;

            await _userReadRepository.UpsertAsync(existingUser, cancellationToken);

            _logger.LogInformation(
                "Updated roles in read model for user {UserId}",
                @event.UserId);
        }
        else
        {
            _logger.LogWarning(
                "User read model not found for user {UserId}. Roles update skipped.",
                @event.UserId);
        }

        // Invalidate the permission cache since roles affect permissions
        await _permissionService.InvalidatePermissionCacheAsync(userId, cancellationToken);

        _logger.LogInformation(
            "Successfully processed UserRolesUpdatedEvent for user {UserId}",
            @event.UserId);
    }
}
