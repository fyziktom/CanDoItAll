using System.Net;

namespace CanDoItAll.Infrastructure.Storage;

public sealed class FtpStorageDriver(IStorageSecretResolver secretResolver) : IStorageDriver
{
    public StorageProviderKind ProviderKind => StorageProviderKind.Ftp;

    public StorageCapability SupportedCapabilities =>
        StorageCapability.Read |
        StorageCapability.Write |
        StorageCapability.Delete |
        StorageCapability.Download |
        StorageCapability.BatchFolderUpload |
        StorageCapability.BatchTransfer |
        StorageCapability.ConnectionTest;

    public async Task<StorageConnectionTestResult> TestConnectionAsync(
        StorageCatalogRecord storage,
        string? secretValue,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(storage);

        try
        {
            var request = CreateRequest(storage, secretValue, string.Empty, WebRequestMethods.Ftp.ListDirectory);
            using var response = (FtpWebResponse)await request.GetResponseAsync();

            return new StorageConnectionTestResult(
                true,
                $"FTP server responded with status '{response.StatusDescription?.Trim() ?? "OK"}'.",
                StorageHealthStatus.Healthy,
                SupportedCapabilities,
                DateTimeOffset.UtcNow);
        }
        catch (Exception ex)
        {
            return new StorageConnectionTestResult(
                false,
                $"FTP storage is unavailable: {ex.Message}",
                StorageHealthStatus.Unavailable,
                SupportedCapabilities & ~StorageCapability.ConnectionTest,
                DateTimeOffset.UtcNow);
        }
    }

    public async Task<StorageWriteResult> SaveAsync(
        StorageCatalogRecord storage,
        StorageWriteRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(storage);
        ArgumentNullException.ThrowIfNull(request);

        var remotePath = NormalizeRemotePath(request.RelativePathHint, request.FileName);
        var secretValue = await secretResolver.ResolveCredentialAsync(storage.CredentialSecretId, cancellationToken);
        await EnsureParentDirectoriesAsync(storage, secretValue, remotePath, cancellationToken);

        var uploadRequest = CreateRequest(storage, secretValue, remotePath, WebRequestMethods.Ftp.UploadFile);
        await using (var requestStream = await uploadRequest.GetRequestStreamAsync())
        {
            await requestStream.WriteAsync(request.Content, cancellationToken);
        }

        using var uploadResponse = (FtpWebResponse)await uploadRequest.GetResponseAsync();
        var reference = new StorageObjectReference(
            storage.Id,
            ProviderKind,
            StorageLocatorKind.RemotePath,
            remotePath,
            request.FileName,
            string.IsNullOrWhiteSpace(request.ContentType) ? "application/octet-stream" : request.ContentType,
            request.Content.LongLength);

        return new StorageWriteResult(
            reference,
            new StorageAccessDescriptor(
                string.Empty,
                StorageJson.BuildDownloadUrl(reference),
                null,
                false,
                true,
                false,
                string.IsNullOrWhiteSpace(request.FileName) ? remotePath : request.FileName,
                reference.ContentType,
                reference.ContentLength,
                "FTP storage supports download but not inline preview or local open."));
    }

    public async Task<Stream> OpenReadAsync(
        StorageCatalogRecord storage,
        StorageObjectReference reference,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(storage);
        ArgumentNullException.ThrowIfNull(reference);

        var secretValue = await secretResolver.ResolveCredentialAsync(storage.CredentialSecretId, cancellationToken);
        var request = CreateRequest(storage, secretValue, reference.Locator, WebRequestMethods.Ftp.DownloadFile);
        using var response = (FtpWebResponse)await request.GetResponseAsync();
        await using var responseStream = response.GetResponseStream()
            ?? throw new InvalidOperationException("FTP download did not return a response stream.");

        var buffer = new MemoryStream();
        await responseStream.CopyToAsync(buffer, cancellationToken);
        buffer.Position = 0;
        return buffer;
    }

    public async Task DeleteAsync(
        StorageCatalogRecord storage,
        StorageObjectReference reference,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(storage);
        ArgumentNullException.ThrowIfNull(reference);

        var secretValue = await secretResolver.ResolveCredentialAsync(storage.CredentialSecretId, cancellationToken);
        var request = CreateRequest(storage, secretValue, reference.Locator, WebRequestMethods.Ftp.DeleteFile);
        using var response = (FtpWebResponse)await request.GetResponseAsync();
    }

    private async Task EnsureParentDirectoriesAsync(
        StorageCatalogRecord storage,
        string? secretValue,
        string remotePath,
        CancellationToken cancellationToken)
    {
        var segments = remotePath.Split('/', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (segments.Length <= 1)
        {
            return;
        }

        var currentPath = string.Empty;
        for (var index = 0; index < segments.Length - 1; index++)
        {
            currentPath = string.IsNullOrWhiteSpace(currentPath)
                ? segments[index]
                : $"{currentPath}/{segments[index]}";

            try
            {
                var request = CreateRequest(storage, secretValue, currentPath, WebRequestMethods.Ftp.MakeDirectory);
                using var response = (FtpWebResponse)await request.GetResponseAsync();
            }
            catch (WebException ex) when (ex.Response is FtpWebResponse ftpResponse &&
                                          ftpResponse.StatusCode == FtpStatusCode.ActionNotTakenFileUnavailable)
            {
            }
        }
    }

    private static string NormalizeRemotePath(string? relativePathHint, string fileName)
    {
        if (!string.IsNullOrWhiteSpace(relativePathHint))
        {
            return relativePathHint.Trim().Replace('\\', '/').TrimStart('/');
        }

        var sanitizedFileName = string.Concat(fileName.Select(character =>
            Path.GetInvalidFileNameChars().Contains(character) ? '-' : character));
        return string.IsNullOrWhiteSpace(sanitizedFileName)
            ? "artifact.bin"
            : sanitizedFileName;
    }

    private static FtpWebRequest CreateRequest(
        StorageCatalogRecord storage,
        string? secretValue,
        string remotePath,
        string method)
    {
        var configuration = StorageJson.ParseProviderConfiguration(storage.ConfigJson);
        var requestUri = BuildRequestUri(storage.EndpointOrRoot, configuration, remotePath);

#pragma warning disable SYSLIB0014
        var request = (FtpWebRequest)WebRequest.Create(requestUri);
#pragma warning restore SYSLIB0014

        request.Method = method;
        request.UseBinary = true;
        request.UsePassive = configuration.UsePassiveMode;
        request.EnableSsl = configuration.UseSsl;
        request.KeepAlive = false;

        if (!string.IsNullOrWhiteSpace(configuration.Username))
        {
            request.Credentials = new NetworkCredential(configuration.Username, secretValue ?? string.Empty);
        }

        return request;
    }

    private static Uri BuildRequestUri(
        string endpointOrRoot,
        StorageProviderConfiguration configuration,
        string remotePath)
    {
        if (string.IsNullOrWhiteSpace(endpointOrRoot))
        {
            throw new InvalidOperationException("FTP storage requires a host or ftp:// endpoint.");
        }

        var endpoint = endpointOrRoot.StartsWith("ftp://", StringComparison.OrdinalIgnoreCase) ||
                       endpointOrRoot.StartsWith("ftps://", StringComparison.OrdinalIgnoreCase)
            ? endpointOrRoot
            : $"ftp://{endpointOrRoot.Trim()}";
        var builder = new UriBuilder(endpoint);
        if (configuration.Port.HasValue)
        {
            builder.Port = configuration.Port.Value;
        }

        var pathSegments = new[]
            {
                builder.Path.Trim('/'),
                configuration.BasePath.Trim('/'),
                remotePath.Trim('/')
            }
            .Where(segment => !string.IsNullOrWhiteSpace(segment));
        builder.Path = string.Join('/', pathSegments);
        return builder.Uri;
    }
}
