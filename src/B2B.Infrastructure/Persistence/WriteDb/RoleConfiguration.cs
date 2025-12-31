using B2B.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace B2B.Infrastructure.Persistence.WriteDb;

/// <summary>
/// EF Core configuration for the Role entity.
/// </summary>
public class RoleConfiguration : IEntityTypeConfiguration<Role>
{
    public void Configure(EntityTypeBuilder<Role> builder)
    {
        builder.ToTable("Roles");

        builder.HasKey(r => r.Id);

        builder.Property(r => r.Id)
            .ValueGeneratedNever();

        builder.Property(r => r.Name)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(r => r.Description)
            .HasMaxLength(500);

        builder.Property(r => r.TenantId)
            .IsRequired()
            .HasMaxLength(50);

        builder.Property(r => r.CreatedBy)
            .IsRequired()
            .HasMaxLength(256);

        builder.Property(r => r.UpdatedBy)
            .HasMaxLength(256);

        // Indexes
        builder.HasIndex(r => new { r.TenantId, r.Name })
            .IsUnique()
            .HasDatabaseName("IX_Roles_TenantId_Name");

        builder.HasIndex(r => r.TenantId)
            .HasDatabaseName("IX_Roles_TenantId");

        builder.HasIndex(r => r.IsDeleted)
            .HasDatabaseName("IX_Roles_IsDeleted");

        // Relationships
        builder.HasMany(r => r.RolePermissions)
            .WithOne(rp => rp.Role)
            .HasForeignKey(rp => rp.RoleId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(r => r.UserRoles)
            .WithOne(ur => ur.Role)
            .HasForeignKey(ur => ur.RoleId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
