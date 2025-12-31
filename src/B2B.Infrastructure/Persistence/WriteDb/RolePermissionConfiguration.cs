using B2B.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace B2B.Infrastructure.Persistence.WriteDb;

/// <summary>
/// EF Core configuration for the RolePermission join entity.
/// </summary>
public class RolePermissionConfiguration : IEntityTypeConfiguration<RolePermission>
{
    public void Configure(EntityTypeBuilder<RolePermission> builder)
    {
        builder.ToTable("RolePermissions");

        // Composite primary key
        builder.HasKey(rp => new { rp.RoleId, rp.PermissionId });

        // Indexes
        builder.HasIndex(rp => rp.RoleId)
            .HasDatabaseName("IX_RolePermissions_RoleId");

        builder.HasIndex(rp => rp.PermissionId)
            .HasDatabaseName("IX_RolePermissions_PermissionId");

        // Relationships are configured in Role and Permission configurations
    }
}
