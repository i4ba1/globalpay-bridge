namespace GlobalPay.Contracts.Payments;

public static class PaymentProviderEventTypes
{
    public const string Accepted = "payment.accepted";

    public const string Failed = "payment.failed";

    public const string Settled = "payment.settled";
}

public sealed record PaymentProviderWebhookPayload(
    string EventType,
    string PaymentReference,
    decimal Amount,
    string Currency,
    DateTimeOffset OccurredAt);
