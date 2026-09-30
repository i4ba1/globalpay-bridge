using GlobalPay.Infrastructure.Persistence.Outbox;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GlobalPay.Infrastructure.Persistence.Configurations;

internal sealed class OutboxMessageConfiguration : IEntityTypeConfiguration<OutboxMessage>
{
    public void Configure(EntityTypeBuilder<OutboxMessage> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("OutboxMessage");
        builder.HasKey(message => message.Id);
        builder.Property(message => message.Type).HasMaxLength(200).IsRequired();
        builder.Property(message => message.Payload).IsRequired();
        builder.Property(message => message.OccurredAtUtc).HasColumnType("datetimeoffset(3)");
        builder.Property(message => message.AvailableAtUtc).HasColumnType("datetimeoffset(3)");
        builder.Property(message => message.ProcessedAtUtc).HasColumnType("datetimeoffset(3)");
        builder.Property(message => message.LeaseUntilUtc).HasColumnType("datetimeoffset(3)");
        builder.Property(message => message.Error).HasMaxLength(2000);
        builder.Property(message => message.RowVersion).IsRowVersion();

        builder
            .HasIndex(message => message.AvailableAtUtc)
            .HasDatabaseName("IX_OutboxMessage_Pending")
            .HasFilter("[ProcessedAtUtc] IS NULL");
    }
}
