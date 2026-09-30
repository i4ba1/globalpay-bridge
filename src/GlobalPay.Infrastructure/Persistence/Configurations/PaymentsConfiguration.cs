using GlobalPay.Domain.Modules.Payments;
using GlobalPay.Infrastructure.Persistence.Conversions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GlobalPay.Infrastructure.Persistence.Configurations;

internal sealed class PaymentBatchConfiguration : IEntityTypeConfiguration<PaymentBatch>
{
    public void Configure(EntityTypeBuilder<PaymentBatch> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("PaymentBatch");
        builder.HasKey(batch => batch.Id);
        builder.Property(batch => batch.Currency).HasConversion(new CurrencyConverter()).HasMaxLength(3).IsRequired();
        builder.Property(batch => batch.IdempotencyKey).HasMaxLength(200).IsRequired();
        builder.Property(batch => batch.CreatedBy).HasMaxLength(200).IsRequired();
        builder.Property(batch => batch.Status).HasConversion<int>();
        builder.Property(batch => batch.ProviderReference).HasMaxLength(200);
        builder.Property(batch => batch.CreatedAtUtc).HasColumnType("datetimeoffset(3)");
        builder.Property(batch => batch.SubmittedAtUtc).HasColumnType("datetimeoffset(3)");
        builder.Property(batch => batch.SettledAtUtc).HasColumnType("datetimeoffset(3)");
        builder.Property(batch => batch.RowVersion).IsRowVersion();

        builder
            .HasIndex(batch => new { batch.TenantId, batch.PayrollRunId })
            .HasDatabaseName("UX_PaymentBatch_Tenant_Run")
            .IsUnique();

        builder
            .HasIndex(batch => new { batch.TenantId, batch.ProviderReference })
            .HasDatabaseName("IX_PaymentBatch_Tenant_ProviderReference");

        builder
            .HasMany(batch => batch.Instructions)
            .WithOne()
            .HasForeignKey(instruction => instruction.BatchId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Navigation(batch => batch.Instructions).UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}

internal sealed class PaymentInstructionConfiguration : IEntityTypeConfiguration<PaymentInstruction>
{
    public void Configure(EntityTypeBuilder<PaymentInstruction> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("PaymentInstruction");
        builder.HasKey(instruction => instruction.Id);
        builder.Property(instruction => instruction.Amount).HasPrecision(19, 4);
        builder.Property(instruction => instruction.Currency).HasConversion(new CurrencyConverter()).HasMaxLength(3).IsRequired();
        builder.Property(instruction => instruction.Status).HasConversion<int>();
        builder.Property(instruction => instruction.ProviderReference).HasMaxLength(200);
        builder.Property(instruction => instruction.FailureCode).HasMaxLength(100);
        builder.Property(instruction => instruction.LastEventAtUtc).HasColumnType("datetimeoffset(3)");

        builder
            .HasIndex(instruction => new { instruction.BatchId, instruction.WorkerPaymentId })
            .HasDatabaseName("UX_PaymentInstruction_Batch_WorkerPayment")
            .IsUnique();

        builder
            .HasIndex(instruction => instruction.ProviderReference)
            .HasDatabaseName("IX_PaymentInstruction_ProviderReference");
    }
}
