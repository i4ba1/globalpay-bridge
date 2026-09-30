namespace GlobalPay.Application.Abstractions.Storage;

public interface IBlobStorage
{
    Task<BlobUploadTicket> CreateUploadTicketAsync(
        string container,
        string blobName,
        TimeSpan lifetime,
        CancellationToken cancellationToken = default);

    Task UploadAsync(
        string container,
        string blobName,
        Stream content,
        string contentType,
        CancellationToken cancellationToken = default);

    Task<Stream> OpenReadAsync(
        string container,
        string blobName,
        CancellationToken cancellationToken = default);

    Task<Uri> CreateReadUrlAsync(
        string container,
        string blobName,
        TimeSpan lifetime,
        CancellationToken cancellationToken = default);
}
