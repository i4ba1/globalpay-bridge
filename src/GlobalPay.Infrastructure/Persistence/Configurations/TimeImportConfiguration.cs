using GlobalPay.Domain.Modules.TimeImport;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GlobalPay.Infrastructure.Persistence.Configurations;

internal sealed class TimesheetImportConfiguration : IEntityTypeConfiguration<TimesheetImport>
{
    public void Configure(EntityTypeBuilder<TimesheetImport> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("TimesheetImport");
        builder.HasKey(import => import.Id);
        builder.Property(import => import.BlobName).HasMaxLength(1024).IsRequired();
        builder.Property(import => import.Sha256).HasMaxLength(64).IsRequired();
        builder.Property(import => import.PeriodStart).HasColumnType("date");
        builder.Property(import => import.PeriodEnd).HasColumnType("date");
        builder.Property(import => import.Status).HasConversion<int>();
        builder.Property(import => import.ErrorReportBlobName).HasMaxLength(1024);
        builder.Property(import => import.FailureReason).HasMaxLength(2000);
        builder.Property(import => import.CreatedAtUtc).HasColumnType("datetimeoffset(3)");
        builder.Property(import => import.CompletedAtUtc).HasColumnType("datetimeoffset(3)");
        builder.Property(import => import.RowVersion).IsRowVersion();

        builder
            .HasIndex(import => new { import.TenantId, import.PeriodStart, import.PeriodEnd, import.Sha256 })
            .HasDatabaseName("UX_TimesheetImport_Tenant_Period_Sha256")
            .IsUnique();
    }
}

internal sealed class TimeEntryConfiguration : IEntityTypeConfiguration<TimeEntry>
{
    public void Configure(EntityTypeBuilder<TimeEntry> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("TimeEntry");
        builder.HasKey(entry => entry.Id);
        builder.Property(entry => entry.WorkDate).HasColumnType("date");
        builder.Property(entry => entry.Units).HasPrecision(19, 4);
        builder.Property(entry => entry.ComponentCode).HasMaxLength(50);

        builder
            .HasIndex(entry => new { entry.TenantId, entry.ImportId, entry.WorkerId, entry.WorkDate })
            .HasDatabaseName("IX_TimeEntry_Tenant_Import_Worker_Date");

        builder
            .HasIndex(entry => new { entry.TenantId, entry.WorkerId, entry.WorkDate })
            .HasDatabaseName("IX_TimeEntry_Tenant_Worker_Date");
    }
}
