using B2B.Application.Interfaces.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace B2B.Infrastructure.Persistence.WriteDb;

/// <summary>
/// EF Core configuration for the OutboxMessage entity.
/// </summary>
public class OutboxMessageConfiguration : IEntityTypeConfiguration<OutboxMessage>
{
    public void Configure(EntityTypeBuilder<OutboxMessage> builder)
    {
        builder.ToTable("OutboxMessages");

        builder.HasKey(o => o.Id);

        builder.Property(o => o.Id)
            .ValueGeneratedNever();

        builder.Property(o => o.EventType)
            .IsRequired()
            .HasMaxLength(500);

        builder.Property(o => o.Payload)
            .IsRequired()
            .HasColumnType("nvarchar(max)");

        builder.Property(o => o.CorrelationId)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(o => o.TenantId)
            .IsRequired()
            .HasMaxLength(50);

        builder.Property(o => o.Error)
            .HasMaxLength(2000);

        builder.Property(o => o.Status)
            .IsRequired()
            .HasConversion<int>();

        // Indexes for efficient polling
        builder.HasIndex(o => o.Status)
            .HasDatabaseName("IX_OutboxMessages_Status");

        builder.HasIndex(o => new { o.Status, o.CreatedAt })
            .HasDatabaseName("IX_OutboxMessages_Status_CreatedAt");

        builder.HasIndex(o => o.TenantId)
            .HasDatabaseName("IX_OutboxMessages_TenantId");

        builder.HasIndex(o => o.CorrelationId)
            .HasDatabaseName("IX_OutboxMessages_CorrelationId");

        builder.HasIndex(o => o.ProcessedAt)
            .HasDatabaseName("IX_OutboxMessages_ProcessedAt");
    }
}
