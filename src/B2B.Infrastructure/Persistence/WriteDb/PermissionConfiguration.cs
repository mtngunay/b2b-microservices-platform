using B2B.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace B2B.Infrastructure.Persistence.WriteDb;

/// <summary>
/// EF Core configuration for the Permission entity.
/// </summary>
public class PermissionConfiguration : IEntityTypeConfiguration<Permission>
{
    public void Configure(EntityTypeBuilder<Permission> builder)
    {
        builder.ToTable("Permissions");

        builder.HasKey(p => p.Id);

        builder.Property(p => p.Id)
            .ValueGeneratedNever();

        builder.Property(p => p.Name)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(p => p.Resource)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(p => p.Action)
            .IsRequired()
            .HasMaxLength(50);

        builder.Property(p => p.Description)
            .HasMaxLength(500);

        builder.Property(p => p.TenantId)
            .IsRequired()
            .HasMaxLength(50);

        builder.Property(p => p.CreatedBy)
            .IsRequired()
            .HasMaxLength(256);

        builder.Property(p => p.UpdatedBy)
            .HasMaxLength(256);

        // Indexes
        builder.HasIndex(p => new { p.TenantId, p.Name })
            .IsUnique()
            .HasDatabaseName("IX_Permissions_TenantId_Name");

        builder.HasIndex(p => p.TenantId)
            .HasDatabaseName("IX_Permissions_TenantId");

        builder.HasIndex(p => new { p.Resource, p.Action })
            .HasDatabaseName("IX_Permissions_Resource_Action");

        builder.HasIndex(p => p.IsDeleted)
            .HasDatabaseName("IX_Permissions_IsDeleted");

        // Relationships
        builder.HasMany(p => p.RolePermissions)
            .WithOne(rp => rp.Permission)
            .HasForeignKey(rp => rp.PermissionId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(p => p.UserPermissions)
            .WithOne(up => up.Permission)
            .HasForeignKey(up => up.PermissionId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
