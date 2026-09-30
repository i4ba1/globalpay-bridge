namespace GlobalPay.Application.Abstractions.Payments;

public interface IWebhookSignatureVerifier
{
    bool Verify(string signature, long timestamp, ReadOnlySpan<byte> rawBody);
}
