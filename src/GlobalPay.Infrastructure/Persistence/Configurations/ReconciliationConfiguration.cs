using GlobalPay.Domain.Modules.Reconciliation;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GlobalPay.Infrastructure.Persistence.Configurations;

internal sealed class ReconciliationCaseConfiguration : IEntityTypeConfiguration<ReconciliationCase>
{
    public void Configure(EntityTypeBuilder<ReconciliationCase> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("ReconciliationCase");
        builder.HasKey(reconciliationCase => reconciliationCase.Id);
        builder.Property(reconciliationCase => reconciliationCase.Reason).HasConversion<int>();
        builder.Property(reconciliationCase => reconciliationCase.Status).HasConversion<int>();
        builder.Property(reconciliationCase => reconciliationCase.Detail).HasMaxLength(1000).IsRequired();
        builder.Property(reconciliationCase => reconciliationCase.Owner).HasMaxLength(200);
        builder.Property(reconciliationCase => reconciliationCase.ResolutionNote).HasMaxLength(1000);
        builder.Property(reconciliationCase => reconciliationCase.OpenedAtUtc).HasColumnType("datetimeoffset(3)");
        builder.Property(reconciliationCase => reconciliationCase.ResolvedAtUtc).HasColumnType("datetimeoffset(3)");
        builder.Property(reconciliationCase => reconciliationCase.RowVersion).IsRowVersion();

        builder
            .HasIndex(reconciliationCase => new { reconciliationCase.TenantId, reconciliationCase.OpenedAtUtc })
            .HasDatabaseName("IX_ReconciliationCase_Tenant_OpenedAt")
            .IsDescending(false, true);

        builder
            .HasIndex(reconciliationCase => reconciliationCase.PaymentInstructionId)
            .HasDatabaseName("IX_ReconciliationCase_Instruction");
    }
}
