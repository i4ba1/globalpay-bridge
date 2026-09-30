using GlobalPay.Domain.Modules.Workforce;

namespace GlobalPay.Domain.Modules.Payroll;

public sealed record PaymentLineSpec(
    string ComponentCode,
    CompensationType Type,
    CalculationRule Rule,
    decimal Amount);
