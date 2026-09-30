using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Azure.Storage.Sas;
using GlobalPay.Application.Abstractions.Storage;
using Microsoft.Extensions.Options;

namespace GlobalPay.Infrastructure.Storage;

internal sealed class AzureBlobStorage : IBlobStorage
{
    private readonly BlobServiceClient _serviceClient;
    private readonly BlobStorageOptions _options;

    public AzureBlobStorage(BlobServiceClient serviceClient, IOptions<BlobStorageOptions> options)
    {
        _serviceClient = serviceClient;
        _options = options.Value;
    }

    public async Task<BlobUploadTicket> CreateUploadTicketAsync(
        string container,
        string blobName,
        TimeSpan lifetime,
        CancellationToken cancellationToken = default)
    {
        var blobClient = await GetBlobClientForWriteAsync(container, blobName, cancellationToken).ConfigureAwait(false);

        if (!blobClient.CanGenerateSasUri)
        {
            throw new InvalidOperationException(
                "The configured blob credential cannot generate shared access signatures.");
        }

        var expiresAt = DateTimeOffset.UtcNow.Add(lifetime);
        var sasBuilder = new BlobSasBuilder(BlobSasPermissions.Write, expiresAt)
        {
            BlobContainerName = container,
            BlobName = blobName,
            Resource = "b",
        };

        return new BlobUploadTicket(
            ToPublicUri(blobClient.GenerateSasUri(sasBuilder)),
            container,
            blobName,
            expiresAt);
    }

    public async Task UploadAsync(
        string container,
        string blobName,
        Stream content,
        string contentType,
        CancellationToken cancellationToken = default)
    {
        var blobClient = await GetBlobClientForWriteAsync(container, blobName, cancellationToken).ConfigureAwait(false);
        var options = new BlobUploadOptions
        {
            HttpHeaders = new BlobHttpHeaders { ContentType = contentType },
        };

        await blobClient.UploadAsync(content, options, cancellationToken).ConfigureAwait(false);
    }

    public async Task<Stream> OpenReadAsync(
        string container,
        string blobName,
        CancellationToken cancellationToken = default)
    {
        var blobClient = _serviceClient.GetBlobContainerClient(container).GetBlobClient(blobName);

        return await blobClient.OpenReadAsync(cancellationToken: cancellationToken).ConfigureAwait(false);
    }

    public Task<Uri> CreateReadUrlAsync(
        string container,
        string blobName,
        TimeSpan lifetime,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var blobClient = _serviceClient.GetBlobContainerClient(container).GetBlobClient(blobName);

        if (!blobClient.CanGenerateSasUri)
        {
            throw new InvalidOperationException(
                "The configured blob credential cannot generate shared access signatures.");
        }

        var expiresAt = DateTimeOffset.UtcNow.Add(lifetime);
        var sasBuilder = new BlobSasBuilder(BlobSasPermissions.Read, expiresAt)
        {
            BlobContainerName = container,
            BlobName = blobName,
            Resource = "b",
        };

        return Task.FromResult(ToPublicUri(blobClient.GenerateSasUri(sasBuilder)));
    }

    private async Task<BlobClient> GetBlobClientForWriteAsync(
        string container,
        string blobName,
        CancellationToken cancellationToken)
    {
        var containerClient = _serviceClient.GetBlobContainerClient(container);
        await containerClient.CreateIfNotExistsAsync(cancellationToken: cancellationToken).ConfigureAwait(false);

        return containerClient.GetBlobClient(blobName);
    }

    /// <summary>
    /// Rewrites the host of a generated SAS URI to the public endpoint while keeping the blob path and
    /// signature, so clients outside the private network can use the link.
    /// </summary>
    private Uri ToPublicUri(Uri generated)
    {
        if (string.IsNullOrWhiteSpace(_options.PublicEndpoint))
        {
            return generated;
        }

        var publicEndpoint = new Uri(_options.PublicEndpoint, UriKind.Absolute);
        var builder = new UriBuilder(generated)
        {
            Scheme = publicEndpoint.Scheme,
            Host = publicEndpoint.Host,
            Port = publicEndpoint.IsDefaultPort ? -1 : publicEndpoint.Port,
        };

        return builder.Uri;
    }
}
