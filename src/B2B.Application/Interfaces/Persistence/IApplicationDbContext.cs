using B2B.Domain.Aggregates;
using B2B.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace B2B.Application.Interfaces.Persistence;

/// <summary>
/// Interface for the application database context.
/// Provides access to entity sets for CRUD operations.
/// </summary>
public interface IApplicationDbContext
{
    /// <summary>
    /// Gets the Users DbSet.
    /// </summary>
    DbSet<User> Users { get; }

    /// <summary>
    /// Gets the Roles DbSet.
    /// </summary>
    DbSet<Role> Roles { get; }

    /// <summary>
    /// Gets the Permissions DbSet.
    /// </summary>
    DbSet<Permission> Permissions { get; }

    /// <summary>
    /// Gets the Tenants DbSet.
    /// </summary>
    DbSet<Tenant> Tenants { get; }

    /// <summary>
    /// Gets the UserRoles DbSet.
    /// </summary>
    DbSet<UserRole> UserRoles { get; }

    /// <summary>
    /// Gets the UserPermissions DbSet.
    /// </summary>
    DbSet<UserPermission> UserPermissions { get; }

    /// <summary>
    /// Gets the RolePermissions DbSet.
    /// </summary>
    DbSet<RolePermission> RolePermissions { get; }

    /// <summary>
    /// Saves all changes made in this context to the database.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The number of state entries written to the database.</returns>
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
