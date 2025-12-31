using B2B.Domain.Aggregates;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace B2B.Infrastructure.Persistence.WriteDb;

/// <summary>
/// EF Core configuration for the User entity.
/// </summary>
public class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> builder)
    {
        builder.ToTable("Users");

        builder.HasKey(u => u.Id);

        builder.Property(u => u.Id)
            .ValueGeneratedNever();

        builder.Property(u => u.Email)
            .IsRequired()
            .HasMaxLength(256);

        builder.Property(u => u.PasswordHash)
            .IsRequired()
            .HasMaxLength(512);

        builder.Property(u => u.FirstName)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(u => u.LastName)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(u => u.TenantId)
            .IsRequired()
            .HasMaxLength(50);

        builder.Property(u => u.CreatedBy)
            .IsRequired()
            .HasMaxLength(256);

        builder.Property(u => u.UpdatedBy)
            .HasMaxLength(256);

        // Indexes
        builder.HasIndex(u => u.Email)
            .IsUnique()
            .HasDatabaseName("IX_Users_Email");

        builder.HasIndex(u => u.TenantId)
            .HasDatabaseName("IX_Users_TenantId");

        builder.HasIndex(u => new { u.TenantId, u.Email })
            .HasDatabaseName("IX_Users_TenantId_Email");

        builder.HasIndex(u => u.IsDeleted)
            .HasDatabaseName("IX_Users_IsDeleted");

        // Ignore domain events (not persisted)
        builder.Ignore(u => u.DomainEvents);

        // Relationships
        builder.HasMany(u => u.UserRoles)
            .WithOne(ur => ur.User)
            .HasForeignKey(ur => ur.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(u => u.UserPermissions)
            .WithOne(up => up.User)
            .HasForeignKey(up => up.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
