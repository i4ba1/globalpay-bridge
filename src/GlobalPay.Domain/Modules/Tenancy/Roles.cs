namespace GlobalPay.Domain.Modules.Tenancy;

public static class Roles
{
    public const string Admin = "Admin";

    public const string Approver = "Approver";

    public const string Finance = "Finance";

    public const string Auditor = "Auditor";

    public const string IntegrationPartner = "IntegrationPartner";

    public const string PlatformOperator = "PlatformOperator";

    public static readonly IReadOnlyCollection<string> All =
    [
        Admin, Approver, Finance, Auditor, IntegrationPartner, PlatformOperator,
    ];
}
