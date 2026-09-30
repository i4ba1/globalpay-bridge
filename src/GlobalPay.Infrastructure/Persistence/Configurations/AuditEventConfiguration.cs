using GlobalPay.Domain.Modules.Audit;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GlobalPay.Infrastructure.Persistence.Configurations;

internal sealed class AuditEventConfiguration : IEntityTypeConfiguration<AuditEvent>
{
    public void Configure(EntityTypeBuilder<AuditEvent> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("AuditEvent");
        builder.HasKey(auditEvent => auditEvent.Id);
        builder.Property(auditEvent => auditEvent.Id).ValueGeneratedOnAdd();
        builder.Property(auditEvent => auditEvent.Actor).HasMaxLength(200).IsRequired();
        builder.Property(auditEvent => auditEvent.Action).HasMaxLength(100).IsRequired();
        builder.Property(auditEvent => auditEvent.TargetType).HasMaxLength(100).IsRequired();
        builder.Property(auditEvent => auditEvent.TraceId).HasMaxLength(100);
        builder.Property(auditEvent => auditEvent.Data).HasColumnType("nvarchar(max)");
        builder.Property(auditEvent => auditEvent.OccurredAtUtc).HasColumnType("datetimeoffset(3)");

        builder
            .HasIndex(auditEvent => new { auditEvent.TenantId, auditEvent.OccurredAtUtc })
            .HasDatabaseName("IX_AuditEvent_Tenant_OccurredAt")
            .IsDescending(false, true);
    }
}
