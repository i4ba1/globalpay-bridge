namespace GlobalPay.Application.Modules.Payments;

public static class PaymentsOutboxMessageTypes
{
    public const string BatchDispatch = "payments.batch.dispatch";
}

public sealed record PaymentBatchDispatchMessage(Guid PaymentBatchId, Guid TenantId);
