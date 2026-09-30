using GlobalPay.Domain.Modules.Workforce;
using GlobalPay.Infrastructure.Persistence.Conversions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GlobalPay.Infrastructure.Persistence.Configurations;

internal sealed class WorkerConfiguration : IEntityTypeConfiguration<Worker>
{
    public void Configure(EntityTypeBuilder<Worker> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("Worker");
        builder.HasKey(worker => worker.Id);
        builder.Property(worker => worker.ExternalRef).HasMaxLength(100).IsRequired();
        builder.Property(worker => worker.DisplayName).HasMaxLength(200).IsRequired();
        builder.Property(worker => worker.Email).HasMaxLength(320);
        builder.Property(worker => worker.Status).HasConversion<int>();
        builder.Property(worker => worker.CreatedAtUtc).HasColumnType("datetimeoffset(3)");
        builder.Property(worker => worker.RowVersion).IsRowVersion();

        builder
            .HasIndex(worker => new { worker.TenantId, worker.ExternalRef })
            .HasDatabaseName("UX_Worker_Tenant_ExternalRef")
            .IsUnique()
            .IncludeProperties(worker => new { worker.Status, worker.DisplayName });
    }
}

internal sealed class ContractConfiguration : IEntityTypeConfiguration<Contract>
{
    public void Configure(EntityTypeBuilder<Contract> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("Contract");
        builder.HasKey(contract => contract.Id);
        builder.Property(contract => contract.StartDate).HasColumnType("date");
        builder.Property(contract => contract.EndDate).HasColumnType("date");
        builder.Property(contract => contract.Currency).HasConversion(new CurrencyConverter()).HasMaxLength(3).IsRequired();
        builder.Property(contract => contract.CreatedAtUtc).HasColumnType("datetimeoffset(3)");
        builder.Property(contract => contract.RowVersion).IsRowVersion();

        builder
            .HasIndex(contract => new { contract.TenantId, contract.WorkerId, contract.StartDate })
            .HasDatabaseName("IX_Contract_Tenant_Worker_Start");

        builder
            .HasMany(contract => contract.Components)
            .WithOne()
            .HasForeignKey(component => component.ContractId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Navigation(contract => contract.Components).UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}

internal sealed class CompensationComponentConfiguration : IEntityTypeConfiguration<CompensationComponent>
{
    public void Configure(EntityTypeBuilder<CompensationComponent> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("CompensationComponent");
        builder.HasKey(component => component.Id);
        builder.Property(component => component.Type).HasConversion<int>();
        builder.Property(component => component.Code).HasMaxLength(50).IsRequired();
        builder.Property(component => component.Rule).HasConversion<int>();
        builder.Property(component => component.Amount).HasPrecision(19, 4);

        builder
            .HasIndex(component => component.ContractId)
            .HasDatabaseName("IX_CompensationComponent_Contract");
    }
}
