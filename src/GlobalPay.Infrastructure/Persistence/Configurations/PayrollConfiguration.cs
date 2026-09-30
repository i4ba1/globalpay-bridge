using GlobalPay.Domain.Modules.Payroll;
using GlobalPay.Infrastructure.Persistence.Conversions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GlobalPay.Infrastructure.Persistence.Configurations;

internal sealed class PayrollRunConfiguration : IEntityTypeConfiguration<PayrollRun>
{
    public void Configure(EntityTypeBuilder<PayrollRun> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("PayrollRun");
        builder.HasKey(run => run.Id);
        builder.Property(run => run.PeriodStart).HasColumnType("date");
        builder.Property(run => run.PeriodEnd).HasColumnType("date");
        builder.Property(run => run.Currency).HasConversion(new CurrencyConverter()).HasMaxLength(3).IsRequired();
        builder.Property(run => run.Status).HasConversion<int>();
        builder.Property(run => run.CreatedBy).HasMaxLength(200).IsRequired();
        builder.Property(run => run.Note).HasMaxLength(500);
        builder.Property(run => run.DecidedBy).HasMaxLength(200);
        builder.Property(run => run.DecisionReason).HasMaxLength(500);
        builder.Property(run => run.CreatedAtUtc).HasColumnType("datetimeoffset(3)");
        builder.Property(run => run.SubmittedAtUtc).HasColumnType("datetimeoffset(3)");
        builder.Property(run => run.DecidedAtUtc).HasColumnType("datetimeoffset(3)");
        builder.Property(run => run.RowVersion).IsRowVersion();

        builder.PrimitiveCollection(run => run.SourceImportIds)
            .HasColumnName("SourceImportIds")
            .HasColumnType("nvarchar(max)");

        builder
            .HasIndex(run => new { run.TenantId, run.PeriodEnd })
            .HasDatabaseName("IX_PayrollRun_Tenant_PeriodEnd")
            .IsDescending(false, true)
            .IncludeProperties(run => new { run.Status, run.CurrentVersion });
    }
}

internal sealed class PayrollRunVersionConfiguration : IEntityTypeConfiguration<PayrollRunVersion>
{
    public void Configure(EntityTypeBuilder<PayrollRunVersion> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("PayrollRunVersion");
        builder.HasKey(version => version.Id);
        builder.Property(version => version.RuleVersion).HasMaxLength(50).IsRequired();
        builder.Property(version => version.Currency).HasConversion(new CurrencyConverter()).HasMaxLength(3).IsRequired();
        builder.Property(version => version.GrossTotal).HasPrecision(19, 4);
        builder.Property(version => version.DeductionTotal).HasPrecision(19, 4);
        builder.Property(version => version.NetTotal).HasPrecision(19, 4);
        builder.Property(version => version.CalculatedAtUtc).HasColumnType("datetimeoffset(3)");

        builder
            .HasIndex(version => new { version.PayrollRunId, version.Version })
            .HasDatabaseName("UX_PayrollRunVersion_Run_Version")
            .IsUnique();

        builder
            .HasMany(version => version.Payments)
            .WithOne()
            .HasForeignKey(payment => payment.RunVersionId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Navigation(version => version.Payments).UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}

internal sealed class WorkerPaymentConfiguration : IEntityTypeConfiguration<WorkerPayment>
{
    public void Configure(EntityTypeBuilder<WorkerPayment> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("WorkerPayment");
        builder.HasKey(payment => payment.Id);
        builder.Property(payment => payment.Currency).HasConversion(new CurrencyConverter()).HasMaxLength(3).IsRequired();
        builder.Property(payment => payment.Gross).HasPrecision(19, 4);
        builder.Property(payment => payment.Deduction).HasPrecision(19, 4);
        builder.Property(payment => payment.Net).HasPrecision(19, 4);

        builder
            .HasIndex(payment => new { payment.RunVersionId, payment.WorkerId })
            .HasDatabaseName("UX_WorkerPayment_Version_Worker")
            .IsUnique();

        builder
            .HasMany(payment => payment.Lines)
            .WithOne()
            .HasForeignKey(line => line.WorkerPaymentId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Navigation(payment => payment.Lines).UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}

internal sealed class WorkerPaymentLineConfiguration : IEntityTypeConfiguration<WorkerPaymentLine>
{
    public void Configure(EntityTypeBuilder<WorkerPaymentLine> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("WorkerPaymentLine");
        builder.HasKey(line => line.Id);
        builder.Property(line => line.ComponentCode).HasMaxLength(50).IsRequired();
        builder.Property(line => line.Type).HasConversion<int>();
        builder.Property(line => line.Rule).HasConversion<int>();
        builder.Property(line => line.Amount).HasPrecision(19, 4);

        builder
            .HasIndex(line => line.WorkerPaymentId)
            .HasDatabaseName("IX_WorkerPaymentLine_Payment");
    }
}
