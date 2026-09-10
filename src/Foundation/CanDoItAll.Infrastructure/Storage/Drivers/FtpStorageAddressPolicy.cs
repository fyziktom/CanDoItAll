namespace CanDoItAll.Infrastructure.Storage;

public static class FtpStorageAddressPolicy {
    public static Uri ResolveObjectUri(StorageCatalogRecord storage, string remotePath) {
        ArgumentNullException.ThrowIfNull(storage);
        return ResolveObjectUri(storage.EndpointOrRoot, remotePath, () => {
            var configuration = StorageJson.ParseProviderConfiguration(storage.ConfigJson);
            return new(configuration.Port, configuration.BasePath);
        });
    }

    public static Uri ResolveObjectUriFromFacts(StorageCatalogPlanningFact storage, string remotePath) {
        ArgumentNullException.ThrowIfNull(storage);
        return ResolveObjectUri(storage.EndpointOrRoot, remotePath, () => storage.FtpAddressing
            ?? throw new InvalidOperationException("FTP addressing facts were not requested for the referenced storage."));
    }

    private static Uri ResolveObjectUri(string endpointOrRoot, string remotePath, Func<StorageFtpAddressingFact> readConfiguration) {
        if (string.IsNullOrWhiteSpace(endpointOrRoot)) {
            throw new InvalidOperationException("FTP storage requires a host or ftp:// endpoint.");
        }
        var endpoint = endpointOrRoot.StartsWith("ftp://", StringComparison.OrdinalIgnoreCase) ||
                       endpointOrRoot.StartsWith("ftps://", StringComparison.OrdinalIgnoreCase)
            ? endpointOrRoot
            : $"ftp://{endpointOrRoot.Trim()}";
        var configuration = readConfiguration();
        var builder = new UriBuilder(endpoint);
        if (configuration.Port.HasValue) {
            builder.Port = configuration.Port.Value;
        }
        builder.Path = string.Join('/', new[] {
            builder.Path.Trim('/'), configuration.BasePath.Trim('/'), remotePath.Trim('/')
        }.Where(segment => segment.Length > 0));
        return builder.Uri;
    }
}
