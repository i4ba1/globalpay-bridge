using GlobalPay.Infrastructure.Persistence.Inbox;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GlobalPay.Infrastructure.Persistence.Configurations;

internal sealed class InboxMessageConfiguration : IEntityTypeConfiguration<InboxMessage>
{
    public void Configure(EntityTypeBuilder<InboxMessage> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("ProviderEventInbox");
        builder.HasKey(message => message.Id);
        builder.Property(message => message.ProviderCode).HasMaxLength(50).IsRequired();
        builder.Property(message => message.ProviderEventId).HasMaxLength(200).IsRequired();
        builder.Property(message => message.PayloadHash).HasMaxLength(64).IsRequired();
        builder.Property(message => message.ReceivedAtUtc).HasColumnType("datetimeoffset(3)");
        builder.Property(message => message.ProcessedAtUtc).HasColumnType("datetimeoffset(3)");

        builder
            .HasIndex(message => new { message.ProviderCode, message.ProviderEventId })
            .HasDatabaseName("UX_ProviderEventInbox_Provider_Event")
            .IsUnique();
    }
}
