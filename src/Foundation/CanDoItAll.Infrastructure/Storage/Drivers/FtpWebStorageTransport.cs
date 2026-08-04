using System.Net;

namespace CanDoItAll.Infrastructure.Storage;

public sealed class FtpWebStorageTransport : IFtpStorageTransport
{
    private const long MaximumContentBytes = 256L * 1024 * 1024;

    public async Task<string?> TestConnectionAsync(
        StorageCatalogRecord storage,
        string? password,
        CancellationToken cancellationToken)
    {
        FtpWebRequest request = FtpWebRequestFactory.Create(
            storage,
            password,
            string.Empty,
            WebRequestMethods.Ftp.ListDirectory);
        using FtpWebResponse response = await FtpWebRequestFactory.GetResponseAsync(request, cancellationToken);
        return string.IsNullOrWhiteSpace(response.StatusDescription)
            ? null
            : response.StatusDescription.Trim();
    }

    public async Task UploadAsync(
        StorageCatalogRecord storage,
        string? password,
        string remotePath,
        ReadOnlyMemory<byte> content,
        CancellationToken cancellationToken)
    {
        if (content.Length > MaximumContentBytes)
        {
            throw new StorageBrowseException(new StorageBrowseError(
                StorageBrowseErrorCode.BudgetExceeded,
                "The FTP upload exceeds the configured byte limit."));
        }

        await FtpWebRequestFactory.EnsureParentDirectoriesAsync(
            storage,
            password,
            remotePath,
            cancellationToken);
        FtpWebRequest request = FtpWebRequestFactory.Create(
            storage,
            password,
            remotePath,
            WebRequestMethods.Ftp.UploadFile);
        using CancellationTokenRegistration registration = cancellationToken.Register(request.Abort);
        await using (Stream requestStream = await request.GetRequestStreamAsync().WaitAsync(cancellationToken))
        {
            await requestStream.WriteAsync(content, cancellationToken);
        }

        using FtpWebResponse response = await FtpWebRequestFactory.GetResponseAsync(request, cancellationToken);
    }

    public async Task<Stream> OpenReadAsync(
        StorageCatalogRecord storage,
        string? password,
        string remotePath,
        CancellationToken cancellationToken)
    {
        FtpWebRequest request = FtpWebRequestFactory.Create(
            storage,
            password,
            remotePath,
            WebRequestMethods.Ftp.DownloadFile);
        FtpWebResponse response = await FtpWebRequestFactory.GetResponseAsync(request, cancellationToken);
        try
        {
            if (response.ContentLength > MaximumContentBytes)
            {
                throw new StorageBrowseException(new StorageBrowseError(
                    StorageBrowseErrorCode.BudgetExceeded,
                    "The FTP content exceeds the configured stream byte limit."));
            }

            Stream stream = response.GetResponseStream()
                ?? throw new InvalidOperationException("FTP download did not return a response stream.");
            return new OwnedBoundedReadStream(stream, response, MaximumContentBytes);
        }
        catch
        {
            response.Dispose();
            throw;
        }
    }

    public async Task DeleteAsync(
        StorageCatalogRecord storage,
        string? password,
        string remotePath,
        CancellationToken cancellationToken)
    {
        FtpWebRequest request = FtpWebRequestFactory.Create(
            storage,
            password,
            remotePath,
            WebRequestMethods.Ftp.DeleteFile);
        try
        {
            using FtpWebResponse response = await FtpWebRequestFactory.GetResponseAsync(
                request,
                cancellationToken);
        }
        catch (WebException exception) when (
            FtpWebRequestFactory.IsFileNotFound(exception))
        {
            exception.Response?.Dispose();
        }
    }

    public async Task<RemoteBrowseTransportPage> BrowseAsync(
        StorageCatalogRecord storage,
        string? password,
        string remotePath,
        RemoteBrowseTransportRequest browseRequest,
        CancellationToken cancellationToken)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(browseRequest.MaximumDuration);
        const string machineListMethod = "MLSD";
        FtpWebRequest request = FtpWebRequestFactory.Create(storage, password, remotePath, machineListMethod);
        FtpWebResponse response;
        try
        {
            response = await FtpWebRequestFactory.GetResponseAsync(request, timeout.Token);
        }
        catch (WebException exception)
        {
            using (exception.Response)
            {
                if (FtpWebRequestFactory.IsMachineListingUnsupported(exception))
                {
                    throw new StorageBrowseException(
                        new StorageBrowseError(
                            StorageBrowseErrorCode.UnsupportedOperation,
                            "The FTP server does not provide a reliable machine-readable directory listing."),
                        exception);
                }

                throw;
            }
        }

        using (response)
        await using (Stream raw = response.GetResponseStream()
            ?? throw new InvalidOperationException("FTP browse did not return a response stream."))
        await using (var bounded = new OwnedBoundedReadStream(raw, NoopDisposable.Instance, browseRequest.MaximumResponseBytes))
        using (var reader = new StreamReader(bounded))
        {
            var entries = new List<RemoteBrowseTransportEntry>(browseRequest.Limit);
            int inspected = 0;
            bool hasMore = false;
            bool reliable = true;
            while (inspected < browseRequest.MaximumInspectedItems)
            {
                timeout.Token.ThrowIfCancellationRequested();
                string? line = await reader.ReadLineAsync(timeout.Token);
                if (line is null)
                {
                    break;
                }

                if (!FtpMachineListEntryParser.TryParse(
                    remotePath,
                    line,
                    out RemoteBrowseTransportEntry? entry))
                {
                    reliable = false;
                    break;
                }

                if (entry is null)
                {
                    continue;
                }

                int currentIndex = inspected++;
                if (currentIndex < browseRequest.Offset)
                {
                    continue;
                }

                if (entries.Count == browseRequest.Limit)
                {
                    hasMore = true;
                    break;
                }

                entries.Add(entry);
            }

            if (inspected >= browseRequest.MaximumInspectedItems)
            {
                hasMore = true;
            }

            return new RemoteBrowseTransportPage(
                entries,
                inspected,
                hasMore,
                bounded.Position,
                RequestCount: 1,
                ClassificationReliable: reliable);
        }
    }

    private sealed class NoopDisposable : IDisposable
    {
        public static NoopDisposable Instance { get; } = new();

        public void Dispose()
        {
        }
    }
}
