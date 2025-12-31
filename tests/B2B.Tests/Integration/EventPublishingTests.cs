using B2B.Application.Interfaces.Services;
using B2B.Domain.Aggregates;
using B2B.Domain.Events;
using B2B.Infrastructure.Persistence.WriteDb;
using B2B.Tests.Integration.Fixtures;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace B2B.Tests.Integration;

/// <summary>
/// Integration tests for event publishing via the Outbox pattern.
/// </summary>
[Collection("Integration")]
public class EventPublishingTests : IntegrationTestBase
{
    public EventPublishingTests(B2BWebApplicationFactory factory) : base(factory)
    {
    }

    [Fact]
    public async Task CreateUser_ShouldAddUserCreatedEventToOutbox()
    {
        // Arrange
        using var scope = Factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<WriteDbContext>();
        var outboxService = scope.ServiceProvider.GetRequiredService<IOutboxService>();

        var email = "outbox-test@example.com";
        var tenantId = "test-tenant";
        var correlationId = Guid.NewGuid().ToString();

        // Act
        var user = User.Create(
            email,
            ComputeHash("Test@123"),
            "Outbox",
            "Test",
            tenantId,
            correlationId);

        dbContext.Users.Add(user);

        // Add domain events to outbox
        foreach (var domainEvent in user.DomainEvents)
        {
            await outboxService.AddEventAsync(domainEvent);
        }

        await dbContext.SaveChangesAsync();
        user.ClearDomainEvents();

        // Assert
        var outboxMessages = await dbContext.OutboxMessages
            .Where(m => m.CorrelationId == correlationId)
            .ToListAsync();

        outboxMessages.Should().HaveCount(1);
        outboxMessages[0].EventType.Should().Contain("UserCreatedEvent");
        outboxMessages[0].Status.Should().Be(OutboxMessageStatus.Pending);
        outboxMessages[0].TenantId.Should().Be(tenantId);
    }

    [Fact]
    public async Task AssignRole_ShouldAddUserRolesUpdatedEventToOutbox()
    {
        // Arrange
        using var scope = Factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<WriteDbContext>();
        var outboxService = scope.ServiceProvider.GetRequiredService<IOutboxService>();

        var tenantId = "test-tenant";
        var correlationId = Guid.NewGuid().ToString();

        // Create user
        var user = User.Create(
            "role-test@example.com",
            ComputeHash("Test@123"),
            "Role",
            "Test",
            tenantId,
            correlationId);

        dbContext.Users.Add(user);
        await dbContext.SaveChangesAsync();
        user.ClearDomainEvents();

        // Create role
        var role = B2B.Domain.Entities.Role.Create("TestRole", "Test Role", tenantId);
        dbContext.Roles.Add(role);
        await dbContext.SaveChangesAsync();

        // Act - Assign role
        var roleCorrelationId = Guid.NewGuid().ToString();
        user.AssignRole(role, "system", roleCorrelationId);

        foreach (var domainEvent in user.DomainEvents)
        {
            await outboxService.AddEventAsync(domainEvent);
        }

        await dbContext.SaveChangesAsync();

        // Assert
        var outboxMessages = await dbContext.OutboxMessages
            .Where(m => m.CorrelationId == roleCorrelationId)
            .ToListAsync();

        outboxMessages.Should().HaveCount(1);
        outboxMessages[0].EventType.Should().Contain("UserRolesUpdatedEvent");
        outboxMessages[0].Status.Should().Be(OutboxMessageStatus.Pending);
    }

    [Fact]
    public async Task OutboxService_GetPendingMessages_ReturnsUnprocessedMessages()
    {
        // Arrange
        using var scope = Factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<WriteDbContext>();
        var outboxService = scope.ServiceProvider.GetRequiredService<IOutboxService>();

        var tenantId = "test-tenant";
        var correlationId = Guid.NewGuid().ToString();

        // Create user to generate event
        var user = User.Create(
            "pending-test@example.com",
            ComputeHash("Test@123"),
            "Pending",
            "Test",
            tenantId,
            correlationId);

        dbContext.Users.Add(user);

        foreach (var domainEvent in user.DomainEvents)
        {
            await outboxService.AddEventAsync(domainEvent);
        }

        await dbContext.SaveChangesAsync();

        // Act
        var pendingMessages = await outboxService.GetPendingMessagesAsync(10);

        // Assert
        pendingMessages.Should().NotBeEmpty();
        pendingMessages.Should().Contain(m => m.CorrelationId == correlationId);
    }

    [Fact]
    public async Task OutboxService_MarkAsProcessed_UpdatesMessageStatus()
    {
        // Arrange
        using var scope = Factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<WriteDbContext>();
        var outboxService = scope.ServiceProvider.GetRequiredService<IOutboxService>();

        var tenantId = "test-tenant";
        var correlationId = Guid.NewGuid().ToString();

        // Create user to generate event
        var user = User.Create(
            "processed-test@example.com",
            ComputeHash("Test@123"),
            "Processed",
            "Test",
            tenantId,
            correlationId);

        dbContext.Users.Add(user);

        foreach (var domainEvent in user.DomainEvents)
        {
            await outboxService.AddEventAsync(domainEvent);
        }

        await dbContext.SaveChangesAsync();

        // Get the message
        var message = await dbContext.OutboxMessages
            .FirstAsync(m => m.CorrelationId == correlationId);

        // Act
        await outboxService.MarkAsProcessedAsync(message.Id);

        // Assert
        var updatedMessage = await dbContext.OutboxMessages
            .FirstAsync(m => m.Id == message.Id);

        updatedMessage.Status.Should().Be(OutboxMessageStatus.Processed);
        updatedMessage.ProcessedAt.Should().NotBeNull();
    }

    [Fact]
    public async Task OutboxService_MarkAsFailed_IncrementsRetryCount()
    {
        // Arrange
        using var scope = Factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<WriteDbContext>();
        var outboxService = scope.ServiceProvider.GetRequiredService<IOutboxService>();

        var tenantId = "test-tenant";
        var correlationId = Guid.NewGuid().ToString();

        // Create user to generate event
        var user = User.Create(
            "failed-test@example.com",
            ComputeHash("Test@123"),
            "Failed",
            "Test",
            tenantId,
            correlationId);

        dbContext.Users.Add(user);

        foreach (var domainEvent in user.DomainEvents)
        {
            await outboxService.AddEventAsync(domainEvent);
        }

        await dbContext.SaveChangesAsync();

        // Get the message
        var message = await dbContext.OutboxMessages
            .FirstAsync(m => m.CorrelationId == correlationId);

        // Act
        await outboxService.MarkAsFailedAsync(message.Id, "Test error");

        // Assert
        var updatedMessage = await dbContext.OutboxMessages
            .FirstAsync(m => m.Id == message.Id);

        updatedMessage.RetryCount.Should().Be(1);
        updatedMessage.Error.Should().Be("Test error");
        updatedMessage.Status.Should().Be(OutboxMessageStatus.Pending); // Still pending for retry
    }

    [Fact]
    public async Task OutboxService_MarkAsFailed_ExceedsMaxRetries_SetsStatusToFailed()
    {
        // Arrange
        using var scope = Factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<WriteDbContext>();
        var outboxService = scope.ServiceProvider.GetRequiredService<IOutboxService>();

        var tenantId = "test-tenant";
        var correlationId = Guid.NewGuid().ToString();

        // Create user to generate event
        var user = User.Create(
            "maxretry-test@example.com",
            ComputeHash("Test@123"),
            "MaxRetry",
            "Test",
            tenantId,
            correlationId);

        dbContext.Users.Add(user);

        foreach (var domainEvent in user.DomainEvents)
        {
            await outboxService.AddEventAsync(domainEvent);
        }

        await dbContext.SaveChangesAsync();

        // Get the message
        var message = await dbContext.OutboxMessages
            .FirstAsync(m => m.CorrelationId == correlationId);

        // Act - Fail 5 times (max retry count)
        for (int i = 0; i < 5; i++)
        {
            await outboxService.MarkAsFailedAsync(message.Id, $"Test error {i + 1}");
        }

        // Assert
        var updatedMessage = await dbContext.OutboxMessages
            .FirstAsync(m => m.Id == message.Id);

        updatedMessage.RetryCount.Should().Be(5);
        updatedMessage.Status.Should().Be(OutboxMessageStatus.Failed);
    }

    [Fact]
    public async Task DomainEvent_ContainsCorrectCorrelationIdAndTenantId()
    {
        // Arrange
        using var scope = Factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<WriteDbContext>();
        var outboxService = scope.ServiceProvider.GetRequiredService<IOutboxService>();

        var tenantId = "specific-tenant";
        var correlationId = Guid.NewGuid().ToString();

        // Act
        var user = User.Create(
            "correlation-test@example.com",
            ComputeHash("Test@123"),
            "Correlation",
            "Test",
            tenantId,
            correlationId);

        dbContext.Users.Add(user);

        foreach (var domainEvent in user.DomainEvents)
        {
            await outboxService.AddEventAsync(domainEvent);
        }

        await dbContext.SaveChangesAsync();

        // Assert
        var outboxMessage = await dbContext.OutboxMessages
            .FirstAsync(m => m.CorrelationId == correlationId);

        outboxMessage.CorrelationId.Should().Be(correlationId);
        outboxMessage.TenantId.Should().Be(tenantId);
        outboxMessage.Payload.Should().Contain(correlationId);
        outboxMessage.Payload.Should().Contain(tenantId);
    }
}
