namespace GlobalPay.Infrastructure.Storage;

/// <summary>
/// Blob storage settings. <see cref="PublicEndpoint"/> is the address clients should use to reach blobs;
/// it differs from the service endpoint when the application talks to storage over a private network
/// (Docker Compose service name, or an Azure private endpoint) but hands out links to the public address.
/// </summary>
public sealed class BlobStorageOptions
{
    public const string SectionName = "Blobs";

    /// <summary>
    /// Public base address used when generating time-limited links, for example
    /// <c>http://localhost:10000/devstoreaccount1</c> for Azurite or
    /// <c>https://account.blob.core.windows.net</c> in Azure. When empty, links use the service endpoint.
    /// </summary>
    public string? PublicEndpoint { get; set; }
}
