using System.Text.Json;
using B2B.Application.Interfaces.Persistence;
using B2B.Application.Interfaces.Services;
using B2B.Domain.Aggregates;
using B2B.Domain.Entities;
using B2B.Domain.Interfaces;
using Microsoft.EntityFrameworkCore;
using OutboxMessage = B2B.Application.Interfaces.Services.OutboxMessage;

namespace B2B.Infrastructure.Persistence.WriteDb;

/// <summary>
/// EF Core DbContext for write operations (MSSQL).
/// Implements soft delete and tenant filtering via global query filters.
/// </summary>
public class WriteDbContext : DbContext, IApplicationDbContext
{
    private readonly ICurrentUserService? _currentUserService;
    private readonly string? _tenantId;

    /// <summary>
    /// Gets or sets the Users DbSet.
    /// </summary>
    public DbSet<User> Users => Set<User>();

    /// <summary>
    /// Gets or sets the Roles DbSet.
    /// </summary>
    public DbSet<Role> Roles => Set<Role>();

    /// <summary>
    /// Gets or sets the Permissions DbSet.
    /// </summary>
    public DbSet<Permission> Permissions => Set<Permission>();

    /// <summary>
    /// Gets or sets the Tenants DbSet.
    /// </summary>
    public DbSet<Tenant> Tenants => Set<Tenant>();

    /// <summary>
    /// Gets or sets the UserRoles DbSet.
    /// </summary>
    public DbSet<UserRole> UserRoles => Set<UserRole>();

    /// <summary>
    /// Gets or sets the UserPermissions DbSet.
    /// </summary>
    public DbSet<UserPermission> UserPermissions => Set<UserPermission>();

    /// <summary>
    /// Gets or sets the RolePermissions DbSet.
    /// </summary>
    public DbSet<RolePermission> RolePermissions => Set<RolePermission>();

    /// <summary>
    /// Gets or sets the OutboxMessages DbSet.
    /// </summary>
    public DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();

    /// <summary>
    /// Initializes a new instance of WriteDbContext.
    /// </summary>
    public WriteDbContext(DbContextOptions<WriteDbContext> options) : base(options)
    {
    }

    /// <summary>
    /// Initializes a new instance of WriteDbContext with current user service.
    /// </summary>
    public WriteDbContext(
        DbContextOptions<WriteDbContext> options,
        ICurrentUserService currentUserService) : base(options)
    {
        _currentUserService = currentUserService;
        _tenantId = currentUserService.TenantId;
    }

    /// <inheritdoc />
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Apply entity configurations
        modelBuilder.ApplyConfiguration(new UserConfiguration());
        modelBuilder.ApplyConfiguration(new RoleConfiguration());
        modelBuilder.ApplyConfiguration(new PermissionConfiguration());
        modelBuilder.ApplyConfiguration(new TenantConfiguration());
        modelBuilder.ApplyConfiguration(new UserRoleConfiguration());
        modelBuilder.ApplyConfiguration(new UserPermissionConfiguration());
        modelBuilder.ApplyConfiguration(new RolePermissionConfiguration());
        modelBuilder.ApplyConfiguration(new OutboxMessageConfiguration());

        // Apply global query filters for soft delete and tenant isolation
        ApplyGlobalFilters(modelBuilder);
    }

    /// <summary>
    /// Applies global query filters for soft delete and tenant isolation.
    /// </summary>
    private void ApplyGlobalFilters(ModelBuilder modelBuilder)
    {
        // User: soft delete + tenant filter
        modelBuilder.Entity<User>().HasQueryFilter(e => 
            !e.IsDeleted && (_tenantId == null || e.TenantId == _tenantId));

        // Role: soft delete + tenant filter
        modelBuilder.Entity<Role>().HasQueryFilter(e => 
            !e.IsDeleted && (_tenantId == null || e.TenantId == _tenantId));

        // Permission: soft delete + tenant filter
        modelBuilder.Entity<Permission>().HasQueryFilter(e => 
            !e.IsDeleted && (_tenantId == null || e.TenantId == _tenantId));

        // Tenant: soft delete only (no tenant filter on Tenant itself)
        modelBuilder.Entity<Tenant>().HasQueryFilter(e => !e.IsDeleted);
    }

    /// <inheritdoc />
    public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        UpdateAuditFields();
        await AddDomainEventsToOutboxAsync(cancellationToken);
        return await base.SaveChangesAsync(cancellationToken);
    }

    /// <inheritdoc />
    public override int SaveChanges()
    {
        UpdateAuditFields();
        AddDomainEventsToOutboxAsync(CancellationToken.None).GetAwaiter().GetResult();
        return base.SaveChanges();
    }

    /// <summary>
    /// Collects domain events from aggregate roots and adds them to the outbox.
    /// </summary>
    private async Task AddDomainEventsToOutboxAsync(CancellationToken cancellationToken)
    {
        var aggregateRoots = ChangeTracker.Entries<IAggregateRoot<Guid>>()
            .Where(e => e.Entity.DomainEvents.Any())
            .Select(e => e.Entity)
            .ToList();

        var domainEvents = aggregateRoots
            .SelectMany(ar => ar.DomainEvents)
            .ToList();

        foreach (var domainEvent in domainEvents)
        {
            var eventType = domainEvent.GetType().AssemblyQualifiedName
                ?? domainEvent.GetType().FullName
                ?? domainEvent.GetType().Name;

            var payload = JsonSerializer.Serialize(domainEvent, domainEvent.GetType(), new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                WriteIndented = false
            });

            var outboxMessage = new OutboxMessage
            {
                Id = Guid.NewGuid(),
                EventType = eventType,
                Payload = payload,
                CorrelationId = domainEvent.CorrelationId,
                TenantId = domainEvent.TenantId ?? _tenantId ?? string.Empty,
                CreatedAt = DateTime.UtcNow,
                Status = OutboxMessageStatus.Pending,
                RetryCount = 0
            };

            await OutboxMessages.AddAsync(outboxMessage, cancellationToken);
        }

        // Clear domain events after adding to outbox
        foreach (var aggregateRoot in aggregateRoots)
        {
            aggregateRoot.ClearDomainEvents();
        }
    }

    /// <summary>
    /// Updates audit fields (CreatedAt, UpdatedAt, CreatedBy, UpdatedBy) on tracked entities.
    /// </summary>
    private void UpdateAuditFields()
    {
        var entries = ChangeTracker.Entries()
            .Where(e => e.Entity is BaseEntity && 
                       (e.State == EntityState.Added || e.State == EntityState.Modified));

        foreach (var entry in entries)
        {
            var entity = (BaseEntity)entry.Entity;

            if (entry.State == EntityState.Added)
            {
                entity.SetCreatedBy(_currentUserService?.UserId ?? "system");
                if (string.IsNullOrEmpty(entity.TenantId) && !string.IsNullOrEmpty(_tenantId))
                {
                    entity.SetTenantId(_tenantId);
                }
            }
            else if (entry.State == EntityState.Modified)
            {
                entity.SetUpdatedBy(_currentUserService?.UserId ?? "system");
            }
        }
    }

    /// <summary>
    /// Ignores global query filters for the current query context.
    /// Useful for admin operations that need to access all tenants.
    /// </summary>
    public IQueryable<TEntity> IgnoreFilters<TEntity>() where TEntity : class
    {
        return Set<TEntity>().IgnoreQueryFilters();
    }
}
