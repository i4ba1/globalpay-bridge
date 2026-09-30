using GlobalPay.Infrastructure.Persistence.Idempotency;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GlobalPay.Infrastructure.Persistence.Configurations;

internal sealed class IdempotencyRecordConfiguration : IEntityTypeConfiguration<IdempotencyRecordEntry>
{
    public void Configure(EntityTypeBuilder<IdempotencyRecordEntry> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("IdempotencyRecord");
        builder.HasKey(record => record.Id);
        builder.Property(record => record.OperationCode).HasMaxLength(100).IsRequired();
        builder.Property(record => record.IdempotencyKey).HasMaxLength(200).IsRequired();
        builder.Property(record => record.RequestFingerprint).HasMaxLength(128).IsRequired();
        builder.Property(record => record.ResponseBody);
        builder.Property(record => record.CreatedAtUtc).HasColumnType("datetimeoffset(3)");
        builder.Property(record => record.CompletedAtUtc).HasColumnType("datetimeoffset(3)");

        builder
            .HasIndex(record => new { record.TenantId, record.OperationCode, record.IdempotencyKey })
            .HasDatabaseName("UX_IdempotencyRecord_Tenant_Operation_Key")
            .IsUnique();
    }
}
