using B2B.Domain.Entities;
using B2B.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using System.Text.Json;

namespace B2B.Infrastructure.Persistence.WriteDb;

/// <summary>
/// EF Core configuration for the Tenant entity.
/// </summary>
public class TenantConfiguration : IEntityTypeConfiguration<Tenant>
{
    public void Configure(EntityTypeBuilder<Tenant> builder)
    {
        builder.ToTable("Tenants");

        builder.HasKey(t => t.Id);

        builder.Property(t => t.Id)
            .ValueGeneratedNever();

        builder.Property(t => t.Name)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(t => t.Subdomain)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(t => t.ContactEmail)
            .IsRequired()
            .HasMaxLength(256);

        builder.Property(t => t.ContactPhone)
            .HasMaxLength(50);

        // Configure TenantSettings as owned entity stored as JSON
        builder.OwnsOne(t => t.Settings, settings =>
        {
            settings.ToJson();
        });

        // Indexes
        builder.HasIndex(t => t.Subdomain)
            .IsUnique()
            .HasDatabaseName("IX_Tenants_Subdomain");

        builder.HasIndex(t => t.Name)
            .HasDatabaseName("IX_Tenants_Name");

        builder.HasIndex(t => t.IsDeleted)
            .HasDatabaseName("IX_Tenants_IsDeleted");

        builder.HasIndex(t => t.IsActive)
            .HasDatabaseName("IX_Tenants_IsActive");
    }
}
