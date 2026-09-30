using System.Globalization;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using GlobalPay.Application.Abstractions.Payments;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace GlobalPay.Infrastructure.Payments;

internal sealed class PaymentProviderOptions
{
    public string BaseAddress { get; set; } = "http://localhost:5090";

    public string SubmitPath { get; set; } = "/api/v1/payments";

    public string WebhookSecret { get; set; } = "local-dev-webhook-secret";

    public int WebhookToleranceSeconds { get; set; } = 300;
}

internal sealed class PaymentProviderClient : IPaymentProvider
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<PaymentProviderClient> _logger;

    public PaymentProviderClient(HttpClient httpClient, ILogger<PaymentProviderClient> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    public async Task<PaymentSubmissionResult> SubmitAsync(
        PaymentSubmission submission,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(submission);

        var request = new ProviderPaymentRequest(
            submission.PaymentBatchId.ToString("D"),
            submission.TotalAmount,
            submission.CurrencyCode,
            submission.Instructions.Count.ToString(CultureInfo.InvariantCulture));

        using var message = new HttpRequestMessage(HttpMethod.Post, "api/v1/payments")
        {
            Content = JsonContent.Create(request),
        };

        message.Headers.Add("Idempotency-Key", submission.PaymentBatchId.ToString("D"));

        try
        {
            using var response = await _httpClient
                .SendAsync(message, HttpCompletionOption.ResponseHeadersRead, cancellationToken)
                .ConfigureAwait(false);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning(
                    "Provider rejected payment batch {BatchId} with status {StatusCode}",
                    submission.PaymentBatchId,
                    (int)response.StatusCode);

                return new PaymentSubmissionResult(
                    false,
                    null,
                    $"provider.http_{(int)response.StatusCode}",
                    "The payment provider rejected the submission.");
            }

            var payload = await response.Content
                .ReadFromJsonAsync<ProviderPaymentResponse>(cancellationToken)
                .ConfigureAwait(false);

            return new PaymentSubmissionResult(true, payload?.ProviderReference, null, null);
        }
        catch (HttpRequestException exception)
        {
            _logger.LogWarning(exception, "Payment provider call failed for batch {BatchId}", submission.PaymentBatchId);

            return new PaymentSubmissionResult(false, null, "provider.unavailable", exception.Message);
        }
        catch (TaskCanceledException exception) when (!cancellationToken.IsCancellationRequested)
        {
            _logger.LogWarning(exception, "Payment provider call timed out for batch {BatchId}", submission.PaymentBatchId);

            return new PaymentSubmissionResult(false, null, "provider.timeout", "The payment provider timed out.");
        }
    }

    public Task<PaymentStatusResult> GetStatusAsync(
        string providerReference,
        CancellationToken cancellationToken = default) =>
        Task.FromResult(new PaymentStatusResult("unknown", providerReference, null));

    private sealed record ProviderPaymentRequest(
        string PaymentBatchId,
        decimal Amount,
        string Currency,
        string Destination);

    private sealed record ProviderPaymentResponse(
        string ProviderReference,
        string Status,
        string PaymentBatchId);
}

internal sealed class HmacWebhookSignatureVerifier : IWebhookSignatureVerifier
{
    private readonly PaymentProviderOptions _options;
    private readonly TimeProvider _timeProvider;

    public HmacWebhookSignatureVerifier(IOptions<PaymentProviderOptions> options, TimeProvider timeProvider)
    {
        _options = options.Value;
        _timeProvider = timeProvider;
    }

    public bool Verify(string signature, long timestamp, ReadOnlySpan<byte> rawBody)
    {
        if (string.IsNullOrWhiteSpace(signature))
        {
            return false;
        }

        var now = _timeProvider.GetUtcNow().ToUnixTimeSeconds();

        if (Math.Abs(now - timestamp) > _options.WebhookToleranceSeconds)
        {
            return false;
        }

        var payload = Encoding.UTF8.GetBytes(
            string.Create(CultureInfo.InvariantCulture, $"{timestamp}.") + Encoding.UTF8.GetString(rawBody));

        var expected = HMACSHA256.HashData(Encoding.UTF8.GetBytes(_options.WebhookSecret), payload);

        byte[] provided;

        try
        {
            provided = Convert.FromHexString(signature);
        }
        catch (FormatException)
        {
            return false;
        }

        return CryptographicOperations.FixedTimeEquals(expected, provided);
    }
}
