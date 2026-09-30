namespace GlobalPay.Api.Composition;

internal static class Policies
{
    public const string Admin = "Admin";

    public const string Approver = "Approver";

    public const string Finance = "Finance";

    public const string Auditor = "Auditor";

    // Composite policies. Note that RequireAuthorization(A, B) requires *both* policies, so endpoints that
    // should accept either role must reference a single policy that lists the roles.
    public const string AdminOrApprover = "AdminOrApprover";

    public const string FinanceOrApprover = "FinanceOrApprover";

    public const string FinanceOrAuditor = "FinanceOrAuditor";
}
