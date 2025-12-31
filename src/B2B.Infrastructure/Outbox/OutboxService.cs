using System.Text.Json;
using B2B.Application.Interfaces.Services;
using B2B.Domain.Interfaces;
using B2B.Infrastructure.Persistence.WriteDb;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace B2B.Infrastructure.Outbox;

/// <summary>
/// Service for managing the outbox pattern for reliable event publishing.
/// Events are stored in the same transaction as business data to ensure atomicity.
/// </summary>
public class OutboxService : IOutboxService
{
    private readonly WriteDbContext _dbContext;
    private readonly ICorrelationIdAccessor _correlationIdAccessor;
    private readonly ICurrentUserService _currentUserService;
    private readonly ILogger<OutboxService> _logger;

    private const int MaxRetryCount = 5;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false
    };

    /// <summary>
    /// Initializes a new instance of OutboxService.
    /// </summary>
    public OutboxService(
        WriteDbContext dbContext,
        ICorrelationIdAccessor correlationIdAccessor,
        ICurrentUserService currentUserService,
        ILogger<OutboxService> logger)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        _correlationIdAccessor = correlationIdAccessor ?? throw new ArgumentNullException(nameof(correlationIdAccessor));
        _currentUserService = currentUserService ?? throw new ArgumentNullException(nameof(currentUserService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc />
    public async Task AddEventAsync(
        IDomainEvent domainEvent,
        CancellationToken cancellationToken = default)
    {
        var eventType = domainEvent.GetType().AssemblyQualifiedName 
            ?? domainEvent.GetType().FullName 
            ?? domainEvent.GetType().Name;

        var payload = JsonSerializer.Serialize(domainEvent, domainEvent.GetType(), JsonOptions);
        var correlationId = domainEvent.CorrelationId ?? _correlationIdAccessor.CorrelationId;
        var tenantId = domainEvent.TenantId ?? _currentUserService.TenantId ?? string.Empty;

        var outboxMessage = new OutboxMessage
        {
            Id = Guid.NewGuid(),
            EventType = eventType,
            Payload = payload,
            CorrelationId = correlationId,
            TenantId = tenantId,
            CreatedAt = DateTime.UtcNow,
            Status = OutboxMessageStatus.Pending,
            RetryCount = 0
        };

        await _dbContext.OutboxMessages.AddAsync(outboxMessage, cancellationToken);

        _logger.LogDebug(
            "Added event {EventType} to outbox with Id {MessageId} and CorrelationId {CorrelationId}",
            domainEvent.GetType().Name,
            outboxMessage.Id,
            correlationId);
    }

    /// <inheritdoc />
    public async Task<IEnumerable<OutboxMessage>> GetPendingMessagesAsync(
        int batchSize,
        CancellationToken cancellationToken = default)
    {
        var messages = await _dbContext.OutboxMessages
            .Where(m => m.Status == OutboxMessageStatus.Pending && m.RetryCount < MaxRetryCount)
            .OrderBy(m => m.CreatedAt)
            .Take(batchSize)
            .ToListAsync(cancellationToken);

        // Mark messages as processing to prevent concurrent processing
        foreach (var message in messages)
        {
            message.Status = OutboxMessageStatus.Processing;
        }

        if (messages.Count > 0)
        {
            await _dbContext.SaveChangesAsync(cancellationToken);

            _logger.LogDebug(
                "Retrieved {Count} pending messages from outbox",
                messages.Count);
        }

        return messages;
    }

    /// <inheritdoc />
    public async Task MarkAsProcessedAsync(
        Guid messageId,
        CancellationToken cancellationToken = default)
    {
        var message = await _dbContext.OutboxMessages
            .FirstOrDefaultAsync(m => m.Id == messageId, cancellationToken);

        if (message == null)
        {
            _logger.LogWarning(
                "Attempted to mark non-existent outbox message {MessageId} as processed",
                messageId);
            return;
        }

        message.Status = OutboxMessageStatus.Processed;
        message.ProcessedAt = DateTime.UtcNow;
        message.Error = null;

        await _dbContext.SaveChangesAsync(cancellationToken);

        _logger.LogDebug(
            "Marked outbox message {MessageId} as processed",
            messageId);
    }

    /// <inheritdoc />
    public async Task MarkAsFailedAsync(
        Guid messageId,
        string error,
        CancellationToken cancellationToken = default)
    {
        var message = await _dbContext.OutboxMessages
            .FirstOrDefaultAsync(m => m.Id == messageId, cancellationToken);

        if (message == null)
        {
            _logger.LogWarning(
                "Attempted to mark non-existent outbox message {MessageId} as failed",
                messageId);
            return;
        }

        message.RetryCount++;
        message.Error = error;

        // If max retries exceeded, mark as failed; otherwise, set back to pending for retry
        if (message.RetryCount >= MaxRetryCount)
        {
            message.Status = OutboxMessageStatus.Failed;
            _logger.LogError(
                "Outbox message {MessageId} exceeded max retry count ({MaxRetries}). Error: {Error}",
                messageId,
                MaxRetryCount,
                error);
        }
        else
        {
            message.Status = OutboxMessageStatus.Pending;
            _logger.LogWarning(
                "Outbox message {MessageId} failed (attempt {RetryCount}/{MaxRetries}). Error: {Error}",
                messageId,
                message.RetryCount,
                MaxRetryCount,
                error);
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task<IEnumerable<OutboxMessage>> GetFailedMessagesAsync(
        int batchSize,
        CancellationToken cancellationToken = default)
    {
        var messages = await _dbContext.OutboxMessages
            .Where(m => m.Status == OutboxMessageStatus.Failed)
            .OrderBy(m => m.CreatedAt)
            .Take(batchSize)
            .ToListAsync(cancellationToken);

        _logger.LogDebug(
            "Retrieved {Count} failed messages from outbox",
            messages.Count);

        return messages;
    }
}
