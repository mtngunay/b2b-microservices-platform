using B2B.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace B2B.Infrastructure.Persistence.WriteDb;

/// <summary>
/// EF Core configuration for the UserPermission join entity.
/// </summary>
public class UserPermissionConfiguration : IEntityTypeConfiguration<UserPermission>
{
    public void Configure(EntityTypeBuilder<UserPermission> builder)
    {
        builder.ToTable("UserPermissions");

        // Composite primary key
        builder.HasKey(up => new { up.UserId, up.PermissionId });

        builder.Property(up => up.AssignedBy)
            .HasMaxLength(256);

        // Indexes
        builder.HasIndex(up => up.UserId)
            .HasDatabaseName("IX_UserPermissions_UserId");

        builder.HasIndex(up => up.PermissionId)
            .HasDatabaseName("IX_UserPermissions_PermissionId");

        // Relationships are configured in User and Permission configurations
    }
}
