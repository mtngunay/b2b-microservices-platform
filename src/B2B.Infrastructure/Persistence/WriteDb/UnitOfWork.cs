using B2B.Application.Interfaces.Persistence;
using B2B.Application.Interfaces.Services;
using B2B.Domain.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using System.Text.Json;

namespace B2B.Infrastructure.Persistence.WriteDb;

/// <summary>
/// Unit of Work implementation for managing database transactions.
/// </summary>
public class UnitOfWork : IUnitOfWork
{
    private readonly WriteDbContext _context;
    private readonly ICorrelationIdAccessor _correlationIdAccessor;
    private readonly ICurrentUserService _currentUserService;
    private IDbContextTransaction? _currentTransaction;
    private bool _disposed;

    /// <summary>
    /// Initializes a new instance of the UnitOfWork.
    /// </summary>
    public UnitOfWork(
        WriteDbContext context,
        ICorrelationIdAccessor correlationIdAccessor,
        ICurrentUserService currentUserService)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _correlationIdAccessor = correlationIdAccessor ?? throw new ArgumentNullException(nameof(correlationIdAccessor));
        _currentUserService = currentUserService ?? throw new ArgumentNullException(nameof(currentUserService));
    }

    /// <inheritdoc />
    public bool HasActiveTransaction => _currentTransaction != null;

    /// <inheritdoc />
    public async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        // Collect domain events before saving
        await ProcessDomainEventsAsync(cancellationToken);

        return await _context.SaveChangesAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task BeginTransactionAsync(CancellationToken cancellationToken = default)
    {
        if (_currentTransaction != null)
        {
            return;
        }

        _currentTransaction = await _context.Database.BeginTransactionAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task CommitTransactionAsync(CancellationToken cancellationToken = default)
    {
        if (_currentTransaction == null)
        {
            throw new InvalidOperationException("No active transaction to commit.");
        }

        try
        {
            await _context.SaveChangesAsync(cancellationToken);
            await _currentTransaction.CommitAsync(cancellationToken);
        }
        catch
        {
            await RollbackTransactionAsync(cancellationToken);
            throw;
        }
        finally
        {
            await DisposeTransactionAsync();
        }
    }

    /// <inheritdoc />
    public async Task RollbackTransactionAsync(CancellationToken cancellationToken = default)
    {
        if (_currentTransaction == null)
        {
            return;
        }

        try
        {
            await _currentTransaction.RollbackAsync(cancellationToken);
        }
        finally
        {
            await DisposeTransactionAsync();
        }
    }

    /// <summary>
    /// Processes domain events from aggregate roots and adds them to the outbox.
    /// </summary>
    private async Task ProcessDomainEventsAsync(CancellationToken cancellationToken)
    {
        var aggregatesWithEvents = _context.ChangeTracker
            .Entries<IAggregateRoot<Guid>>()
            .Where(e => e.Entity.DomainEvents.Any())
            .Select(e => e.Entity)
            .ToList();

        var correlationId = _correlationIdAccessor.CorrelationId ?? Guid.NewGuid().ToString();
        var tenantId = _currentUserService.TenantId ?? string.Empty;

        foreach (var aggregate in aggregatesWithEvents)
        {
            foreach (var domainEvent in aggregate.DomainEvents)
            {
                var outboxMessage = new OutboxMessage
                {
                    Id = Guid.NewGuid(),
                    EventType = domainEvent.GetType().AssemblyQualifiedName ?? domainEvent.GetType().FullName ?? "Unknown",
                    Payload = JsonSerializer.Serialize(domainEvent, domainEvent.GetType()),
                    CorrelationId = correlationId,
                    TenantId = tenantId,
                    CreatedAt = DateTime.UtcNow,
                    Status = OutboxMessageStatus.Pending
                };

                await _context.OutboxMessages.AddAsync(outboxMessage, cancellationToken);
            }

            aggregate.ClearDomainEvents();
        }
    }

    private async Task DisposeTransactionAsync()
    {
        if (_currentTransaction != null)
        {
            await _currentTransaction.DisposeAsync();
            _currentTransaction = null;
        }
    }

    /// <inheritdoc />
    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (!_disposed && disposing)
        {
            _currentTransaction?.Dispose();
            _context.Dispose();
        }

        _disposed = true;
    }
}
