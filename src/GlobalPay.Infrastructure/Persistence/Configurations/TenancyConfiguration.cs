using GlobalPay.Domain.Modules.Tenancy;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GlobalPay.Infrastructure.Persistence.Configurations;

internal sealed class TenantConfiguration : IEntityTypeConfiguration<Tenant>
{
    public void Configure(EntityTypeBuilder<Tenant> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("Tenant");
        builder.HasKey(tenant => tenant.Id);
        builder.Property(tenant => tenant.Name).HasMaxLength(200).IsRequired();
        builder.Property(tenant => tenant.Status).HasConversion<int>();
        builder.Property(tenant => tenant.CreatedAtUtc).HasColumnType("datetimeoffset(3)");
        builder.Property(tenant => tenant.RowVersion).IsRowVersion();
    }
}

internal sealed class UserTenantRoleConfiguration : IEntityTypeConfiguration<UserTenantRole>
{
    public void Configure(EntityTypeBuilder<UserTenantRole> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("UserTenantRole");
        builder.HasKey(role => role.Id);
        builder.Property(role => role.UserId).HasMaxLength(200).IsRequired();
        builder.Property(role => role.RoleCode).HasMaxLength(50).IsRequired();

        builder
            .HasIndex(role => new { role.TenantId, role.UserId, role.RoleCode })
            .HasDatabaseName("UX_UserTenantRole_Tenant_User_Role")
            .IsUnique();
    }
}
