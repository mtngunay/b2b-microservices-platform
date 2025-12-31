using B2B.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace B2B.Infrastructure.Persistence.WriteDb;

/// <summary>
/// EF Core configuration for the UserRole join entity.
/// </summary>
public class UserRoleConfiguration : IEntityTypeConfiguration<UserRole>
{
    public void Configure(EntityTypeBuilder<UserRole> builder)
    {
        builder.ToTable("UserRoles");

        // Composite primary key
        builder.HasKey(ur => new { ur.UserId, ur.RoleId });

        builder.Property(ur => ur.AssignedBy)
            .HasMaxLength(256);

        // Indexes
        builder.HasIndex(ur => ur.UserId)
            .HasDatabaseName("IX_UserRoles_UserId");

        builder.HasIndex(ur => ur.RoleId)
            .HasDatabaseName("IX_UserRoles_RoleId");

        // Relationships are configured in User and Role configurations
    }
}
