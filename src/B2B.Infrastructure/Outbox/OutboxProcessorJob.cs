using System.Text.Json;
using B2B.Application.Interfaces.Services;
using B2B.Domain.Events;
using B2B.Domain.Interfaces;
using B2B.Infrastructure.Messaging;
using Hangfire;
using Microsoft.Extensions.Logging;

namespace B2B.Infrastructure.Outbox;

/// <summary>
/// Hangfire job that processes pending outbox messages and publishes them to RabbitMQ.
/// </summary>
public class OutboxProcessorJob
{
    private readonly IOutboxService _outboxService;
    private readonly IMessagePublisher _messagePublisher;
    private readonly ILogger<OutboxProcessorJob> _logger;

    private const int BatchSize = 100;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true
    };

    /// <summary>
    /// Initializes a new instance of OutboxProcessorJob.
    /// </summary>
    public OutboxProcessorJob(
        IOutboxService outboxService,
        IMessagePublisher messagePublisher,
        ILogger<OutboxProcessorJob> logger)
    {
        _outboxService = outboxService ?? throw new ArgumentNullException(nameof(outboxService));
        _messagePublisher = messagePublisher ?? throw new ArgumentNullException(nameof(messagePublisher));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Processes pending outbox messages.
    /// This method is called by Hangfire on a recurring schedule.
    /// </summary>
    [AutomaticRetry(Attempts = 3, DelaysInSeconds = new[] { 10, 30, 60 })]
    public async Task ProcessAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Starting outbox processor job");

        try
        {
            var messages = await _outboxService.GetPendingMessagesAsync(BatchSize, cancellationToken);
            var messageList = messages.ToList();

            if (messageList.Count == 0)
            {
                _logger.LogDebug("No pending outbox messages to process");
                return;
            }

            _logger.LogInformation(
                "Processing {Count} pending outbox messages",
                messageList.Count);

            var processedCount = 0;
            var failedCount = 0;

            foreach (var message in messageList)
            {
                try
                {
                    await ProcessMessageAsync(message, cancellationToken);
                    await _outboxService.MarkAsProcessedAsync(message.Id, cancellationToken);
                    processedCount++;
                }
                catch (Exception ex)
                {
                    _logger.LogError(
                        ex,
                        "Failed to process outbox message {MessageId} of type {EventType}",
                        message.Id,
                        message.EventType);

                    await _outboxService.MarkAsFailedAsync(
                        message.Id,
                        ex.Message,
                        cancellationToken);
                    failedCount++;
                }
            }

            _logger.LogInformation(
                "Outbox processor completed. Processed: {ProcessedCount}, Failed: {FailedCount}",
                processedCount,
                failedCount);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in outbox processor job");
            throw;
        }
    }

    /// <summary>
    /// Processes a single outbox message by deserializing and publishing it.
    /// </summary>
    private async Task ProcessMessageAsync(
        OutboxMessage message,
        CancellationToken cancellationToken)
    {
        _logger.LogDebug(
            "Processing outbox message {MessageId} of type {EventType}",
            message.Id,
            message.EventType);

        // Get the event type
        var eventType = Type.GetType(message.EventType);
        if (eventType == null)
        {
            // Try to find the type by name in known assemblies
            eventType = FindEventType(message.EventType);
        }

        if (eventType == null)
        {
            throw new InvalidOperationException(
                $"Could not resolve event type: {message.EventType}");
        }

        // Deserialize the event
        var domainEvent = JsonSerializer.Deserialize(message.Payload, eventType, JsonOptions);
        if (domainEvent == null)
        {
            throw new InvalidOperationException(
                $"Failed to deserialize event payload for message {message.Id}");
        }

        // Publish the event
        var publishMethod = typeof(IMessagePublisher)
            .GetMethod(nameof(IMessagePublisher.PublishAsync), new[] { eventType, typeof(CancellationToken) });

        if (publishMethod == null)
        {
            // Use the generic method
            var genericMethod = typeof(IMessagePublisher)
                .GetMethods()
                .First(m => m.Name == nameof(IMessagePublisher.PublishAsync) && 
                           m.GetParameters().Length == 2 &&
                           m.IsGenericMethod)
                .MakeGenericMethod(eventType);

            var task = (Task?)genericMethod.Invoke(_messagePublisher, new[] { domainEvent, cancellationToken });
            if (task != null)
            {
                await task;
            }
        }

        _logger.LogDebug(
            "Successfully published outbox message {MessageId} to message bus",
            message.Id);
    }

    /// <summary>
    /// Attempts to find the event type by searching known assemblies.
    /// </summary>
    private static Type? FindEventType(string typeName)
    {
        // Extract the type name without assembly info
        var simpleTypeName = typeName.Contains(',') 
            ? typeName.Split(',')[0].Trim() 
            : typeName;

        // Search in the Domain assembly
        var domainAssembly = typeof(IntegrationEvent).Assembly;
        var type = domainAssembly.GetTypes()
            .FirstOrDefault(t => t.FullName == simpleTypeName || t.Name == simpleTypeName);

        return type;
    }

    /// <summary>
    /// Registers the recurring job with Hangfire.
    /// </summary>
    public static void RegisterRecurringJob()
    {
        RecurringJob.AddOrUpdate<OutboxProcessorJob>(
            "outbox-processor",
            job => job.ProcessAsync(CancellationToken.None),
            "*/5 * * * * *"); // Every 5 seconds
    }
}
