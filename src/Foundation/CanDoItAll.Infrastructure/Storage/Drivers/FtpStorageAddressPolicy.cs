namespace CanDoItAll.Infrastructure.Storage;

public static class FtpStorageAddressPolicy
{
    public static Uri ResolveObjectUri(
        StorageCatalogRecord storage,
        string remotePath)
    {
        ArgumentNullException.ThrowIfNull(storage);
        if (string.IsNullOrWhiteSpace(storage.EndpointOrRoot))
        {
            throw new InvalidOperationException("FTP storage requires a host or ftp:// endpoint.");
        }

        var endpoint = storage.EndpointOrRoot.StartsWith("ftp://", StringComparison.OrdinalIgnoreCase) ||
                       storage.EndpointOrRoot.StartsWith("ftps://", StringComparison.OrdinalIgnoreCase)
            ? storage.EndpointOrRoot
            : $"ftp://{storage.EndpointOrRoot.Trim()}";
        var configuration = StorageJson.ParseProviderConfiguration(storage.ConfigJson);
        var builder = new UriBuilder(endpoint);
        if (configuration.Port.HasValue)
        {
            builder.Port = configuration.Port.Value;
        }

        builder.Path = string.Join('/', new[]
        {
            builder.Path.Trim('/'),
            configuration.BasePath.Trim('/'),
            remotePath.Trim('/')
        }.Where(segment => segment.Length > 0));
        return builder.Uri;
    }
}
