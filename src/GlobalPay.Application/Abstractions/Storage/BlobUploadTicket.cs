namespace GlobalPay.Application.Abstractions.Storage;

public sealed record BlobUploadTicket(
    Uri UploadUri,
    string Container,
    string BlobName,
    DateTimeOffset ExpiresAtUtc);
