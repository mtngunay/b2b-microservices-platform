using B2B.Application.Interfaces.Services;
using B2B.Domain.Events;
using B2B.Infrastructure.Persistence.ReadDb;
using B2B.Infrastructure.Persistence.ReadDb.ReadModels;
using Microsoft.Extensions.Logging;

namespace B2B.Infrastructure.Messaging.Consumers;

/// <summary>
/// Consumer for UserCreatedEvent that updates the MongoDB read model.
/// </summary>
public class UserCreatedEventConsumer : BaseConsumer<UserCreatedEvent>
{
    private readonly UserReadRepository _userReadRepository;
    private readonly ILogger<UserCreatedEventConsumer> _logger;

    /// <summary>
    /// Initializes a new instance of UserCreatedEventConsumer.
    /// </summary>
    public UserCreatedEventConsumer(
        UserReadRepository userReadRepository,
        ICacheService cacheService,
        ICorrelationIdAccessor correlationIdAccessor,
        ILogger<UserCreatedEventConsumer> logger)
        : base(cacheService, correlationIdAccessor, logger)
    {
        _userReadRepository = userReadRepository ?? throw new ArgumentNullException(nameof(userReadRepository));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc />
    protected override async Task HandleAsync(
        UserCreatedEvent @event,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation(
            "Processing UserCreatedEvent for user {UserId} with email {Email}",
            @event.UserId,
            @event.Email);

        // Parse the full name into first and last name
        var nameParts = @event.FullName.Split(' ', 2, StringSplitOptions.RemoveEmptyEntries);
        var firstName = nameParts.Length > 0 ? nameParts[0] : string.Empty;
        var lastName = nameParts.Length > 1 ? nameParts[1] : string.Empty;

        // Create the read model
        var userReadModel = new UserReadModel
        {
            Id = @event.UserId.ToString(),
            TenantId = @event.TenantId,
            Email = @event.Email.ToLowerInvariant(),
            FullName = @event.FullName,
            FirstName = firstName,
            LastName = lastName,
            IsActive = true,
            Roles = @event.Roles ?? new List<string>(),
            Permissions = new List<string>(),
            CreatedAt = @event.OccurredAt,
            IsDeleted = false
        };

        // Upsert to MongoDB
        await _userReadRepository.UpsertAsync(userReadModel, cancellationToken);

        _logger.LogInformation(
            "Successfully created/updated user read model for user {UserId}",
            @event.UserId);
    }
}
