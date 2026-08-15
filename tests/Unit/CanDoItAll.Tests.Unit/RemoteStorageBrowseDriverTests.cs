using CanDoItAll.Infrastructure.Storage;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using System.Net;
using System.Text;
using Xunit.Abstractions;

namespace CanDoItAll.Tests.Unit.Storage;

public sealed class RemoteStorageBrowseDriverTests(ITestOutputHelper output)
{
    [Fact]
    public async Task Ipfs_cid_and_mfs_use_distinct_typed_addresses_and_consistency()
    {
        var transport = new FakeIpfsTransport();
        var driver = CreateIpfsDriver(transport);
        StorageCatalogRecord storage = CreateStorage(StorageProviderKind.Ipfs);

        StorageBrowsePage cidPage = await driver.BrowseAsync(
            storage,
            new StorageBrowseRequest(new StorageBrowseContainer("cid:bafy-root"), pageSize: 2));
        StorageBrowsePage mfsPage = await driver.BrowseAsync(
            storage,
            new StorageBrowseRequest(new StorageBrowseContainer("mfs:/projects"), pageSize: 2));

        Assert.Equal(IpfsBrowseAddressKind.ContentAddress, transport.Addresses[0].Kind);
        Assert.Equal("bafy-root", cidPage.Consistency?.Value);
        Assert.Equal(IpfsBrowseAddressKind.MutableFileSystem, transport.Addresses[1].Kind);
        Assert.Equal("mfs-revision-1", mfsPage.Consistency?.Value);
        Assert.StartsWith("cid:", cidPage.Entries[0].Id.Value, StringComparison.Ordinal);
        Assert.StartsWith("mfs:", mfsPage.Entries[0].Id.Value, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Ipfs_mutable_revision_change_invalidates_continuation()
    {
        var transport = new FakeIpfsTransport { HasMore = true };
        var driver = CreateIpfsDriver(transport);
        StorageCatalogRecord storage = CreateStorage(StorageProviderKind.Ipfs);
        var request = new StorageBrowseRequest(
            new StorageBrowseContainer("mfs:/projects"),
            pageSize: 1);
        StorageBrowsePage first = await driver.BrowseAsync(storage, request);
        transport.SourceRevision = "mfs-revision-2";

        StorageBrowseException exception = await Assert.ThrowsAsync<StorageBrowseException>(() =>
            driver.BrowseAsync(
                storage,
                new StorageBrowseRequest(
                    request.Container,
                    pageSize: 1,
                    cursor: first.NextCursor)));

        Assert.Equal(StorageBrowseErrorCode.SourceChanged, exception.Error.Code);
    }

    [Fact]
    public async Task Ipfs_transport_facts_outside_budget_are_rejected()
    {
        var transport = new FakeIpfsTransport { ReportedInspectedItems = 20_000 };
        var driver = CreateIpfsDriver(transport);

        StorageBrowseException exception = await Assert.ThrowsAsync<StorageBrowseException>(() =>
            driver.BrowseAsync(
                CreateStorage(StorageProviderKind.Ipfs),
                new StorageBrowseRequest(new StorageBrowseContainer("cid:bafy-root"))));

        Assert.Equal(StorageBrowseErrorCode.ProviderUnavailable, exception.Error.Code);
    }

    [Fact]
    public async Task Ipfs_cancellation_does_not_publish_completed_diagnostic()
    {
        var transport = new FakeIpfsTransport { WaitForCancellation = true };
        var logger = new ListLogger<IpfsStorageBrowseDriver>();
        var driver = CreateIpfsDriver(transport, logger);
        using var cancellation = new CancellationTokenSource(TimeSpan.FromMilliseconds(50));

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            driver.BrowseAsync(
                CreateStorage(StorageProviderKind.Ipfs),
                new StorageBrowseRequest(new StorageBrowseContainer("cid:bafy-root")),
                cancellation.Token));

        Assert.Contains(logger.Messages, message => message.Contains("cancelled", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(logger.Messages, message => message.Contains("completed", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task Ftp_reliable_facts_map_without_write_authority()
    {
        var transport = new FakeFtpTransport();
        var driver = CreateFtpDriver(transport);
        var request = new StorageBrowseRequest(
            StorageBrowseContainer.Root,
            pageSize: 2,
            metadata: StorageBrowseMetadataField.Size,
            budget: new StorageBrowseWorkBudget(
                maximumReturnedItems: 2,
                maximumInspectedItems: 8,
                maximumMetadataProbes: 2,
                maximumConcurrentMetadataProbes: 1));

        StorageBrowsePage page = await driver.BrowseAsync(
            CreateStorage(StorageProviderKind.Ftp),
            request);

        Assert.Equal(2, page.Entries.Count);
        Assert.Equal(StorageBrowseEntryKind.Container, page.Entries[0].Kind);
        Assert.Equal(StorageBrowseEntryCapability.Browse, page.Entries[0].Capabilities);
        Assert.Equal(StorageBrowseEntryCapability.Read, page.Entries[1].Capabilities);
        Assert.DoesNotContain(page.Entries, entry => entry.Capabilities.HasFlag(StorageBrowseEntryCapability.Write));
        Assert.Equal(2, page.Metrics.MetadataProbes);
    }

    [Fact]
    public async Task Ftp_ambiguous_classification_is_explicitly_unsupported()
    {
        var transport = new FakeFtpTransport { ClassificationReliable = false };
        var driver = CreateFtpDriver(transport);

        StorageBrowseException exception = await Assert.ThrowsAsync<StorageBrowseException>(() =>
            driver.BrowseAsync(
                CreateStorage(StorageProviderKind.Ftp),
                new StorageBrowseRequest(StorageBrowseContainer.Root)));

        Assert.Equal(StorageBrowseErrorCode.UnsupportedOperation, exception.Error.Code);
    }

    [Fact]
    public void Ftp_machine_facts_classify_only_standard_file_and_directory_entries()
    {
        bool directoryParsed = FtpMachineListEntryParser.TryParse(
            "root",
            "type=dir;modify=20260712010203; folder",
            out RemoteBrowseTransportEntry? directory);
        bool fileParsed = FtpMachineListEntryParser.TryParse(
            "root",
            "type=file;size=42;modify=20260712010203; file.txt",
            out RemoteBrowseTransportEntry? file);
        bool currentDirectoryParsed = FtpMachineListEntryParser.TryParse(
            "root",
            "type=cdir; .",
            out RemoteBrowseTransportEntry? currentDirectory);

        Assert.True(directoryParsed);
        Assert.Equal(StorageBrowseEntryKind.Container, directory?.Kind);
        Assert.True(fileParsed);
        Assert.Equal(StorageBrowseEntryKind.File, file?.Kind);
        Assert.Equal(42, file?.Size);
        Assert.True(currentDirectoryParsed);
        Assert.Null(currentDirectory);
    }

    [Theory]
    [InlineData("file.txt")]
    [InlineData("type=file;size=12; ../escape.txt")]
    [InlineData("type=file;type=dir; duplicate")]
    [InlineData("type=unknown; item")]
    public void Ftp_malformed_or_ambiguous_machine_facts_are_rejected(string line)
    {
        bool parsed = FtpMachineListEntryParser.TryParse(
            "root",
            line,
            out RemoteBrowseTransportEntry? entry);

        Assert.False(parsed);
        Assert.Null(entry);
    }

    [Fact]
    public async Task Remote_content_drivers_return_unread_owned_streams_without_bridge_buffering()
    {
        var ipfsTransport = new FakeIpfsTransport();
        var ftpTransport = new FakeFtpTransport();
        var secretResolver = new StaticSecretResolver();
        var ipfsDriver = new IpfsStorageDriver(
            NullLogger<IpfsStorageDriver>.Instance,
            secretResolver,
            ipfsTransport);
        var ftpDriver = new FtpStorageDriver(
            secretResolver,
            ftpTransport,
            NullLogger<FtpStorageDriver>.Instance);

        await using Stream ipfsStream = await ipfsDriver.OpenReadAsync(
            CreateStorage(StorageProviderKind.Ipfs),
            CreateReference(StorageProviderKind.Ipfs));
        await using Stream ftpStream = await ftpDriver.OpenReadAsync(
            CreateStorage(StorageProviderKind.Ftp),
            CreateReference(StorageProviderKind.Ftp));

        Assert.Same(ipfsTransport.ContentStream, ipfsStream);
        Assert.Same(ftpTransport.ContentStream, ftpStream);
        Assert.Equal(0, ipfsTransport.ContentStream.ReadCount);
        Assert.Equal(0, ftpTransport.ContentStream.ReadCount);
    }

    [Fact]
    public async Task Remote_failure_masks_endpoint_credential_and_raw_transport_message()
    {
        const string secret = "credential-secret";
        const string endpoint = "https://private.example.test:5001";
        var transport = new FakeIpfsTransport
        {
            Failure = new IOException($"{secret} at {endpoint}")
        };
        var logger = new ListLogger<IpfsStorageBrowseDriver>();
        var driver = new IpfsStorageBrowseDriver(
            new StaticSecretResolver(secret),
            transport,
            logger);
        StorageCatalogRecord storage = new()
        {
            Id = Guid.NewGuid(),
            Name = "IPFS",
            ProviderKind = StorageProviderKind.Ipfs,
            EndpointOrRoot = endpoint
        };

        StorageBrowseException exception = await Assert.ThrowsAsync<StorageBrowseException>(() =>
            driver.BrowseAsync(
                storage,
                new StorageBrowseRequest(new StorageBrowseContainer("cid:bafy-root"))));

        Assert.Equal(StorageBrowseErrorCode.ProviderUnavailable, exception.Error.Code);
        Assert.DoesNotContain(secret, exception.Message, StringComparison.Ordinal);
        Assert.DoesNotContain(endpoint, exception.Message, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(logger.Messages, message => message.Contains(secret, StringComparison.Ordinal));
        Assert.DoesNotContain(logger.Messages, message => message.Contains(endpoint, StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task Ipfs_http_reuses_injected_client_and_leaves_content_unread_until_consumer_reads()
    {
        var contentStream = new TrackingStream(Encoding.UTF8.GetBytes("streamed-content"));
        var handler = new RecordingHttpHandler(request =>
        {
            if (request.RequestUri?.AbsolutePath.EndsWith("/cat", StringComparison.Ordinal) == true)
            {
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StreamContent(contentStream)
                };
            }

            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("{}")
            };
        });
        using var client = new HttpClient(handler);
        var transport = new IpfsHttpStorageTransport(client);
        StorageCatalogRecord storage = CreateStorage(StorageProviderKind.Ipfs);

        await transport.TestConnectionAsync(storage, "token-one", CancellationToken.None);
        await transport.TestConnectionAsync(storage, "token-two", CancellationToken.None);
        await using Stream stream = await transport.OpenReadAsync(
            storage,
            "token-three",
            "bafy-content",
            route: string.Empty,
            CancellationToken.None);

        Assert.Equal(3, handler.RequestCount);
        Assert.Equal(0, contentStream.ReadCount);
        byte[] buffer = new byte[7];
        int read = await stream.ReadAsync(buffer);
        Assert.Equal(7, read);
        Assert.True(contentStream.ReadCount > 0);
        Assert.Equal(["token-one", "token-two", "token-three"], handler.BearerTokens);
    }

    [Fact]
    public async Task Ipfs_http_mfs_entry_id_reads_through_mutable_file_endpoint()
    {
        HttpMethod? observedMethod = null;
        Uri? observedUri = null;
        var handler = new RecordingHttpHandler(request =>
        {
            observedMethod = request.Method;
            observedUri = request.RequestUri;
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("mutable-content")
            };
        });
        using var client = new HttpClient(handler);
        var transport = new IpfsHttpStorageTransport(client);

        await using Stream stream = await transport.OpenReadAsync(
            CreateStorage(StorageProviderKind.Ipfs),
            bearerToken: null,
            "mfs:/projects/readme.txt",
            route: string.Empty,
            CancellationToken.None);

        Assert.Equal(HttpMethod.Post, observedMethod);
        Assert.EndsWith("/files/read", observedUri?.AbsolutePath, StringComparison.Ordinal);
        Assert.Equal("?arg=/projects/readme.txt", Uri.UnescapeDataString(observedUri?.Query ?? string.Empty));
    }

    [Fact]
    public async Task Ipfs_http_mfs_browse_uses_before_and_after_revision_checks()
    {
        var handler = new RecordingHttpHandler(request =>
        {
            string path = request.RequestUri?.AbsolutePath ?? string.Empty;
            string json = path.EndsWith("/files/stat", StringComparison.Ordinal)
                ? "{\"Hash\":\"mfs-revision\"}"
                : "{\"Entries\":[{\"Name\":\"folder\",\"Type\":1,\"Size\":0,\"Hash\":\"bafy-folder\"}]}";
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(json, Encoding.UTF8, "application/json")
            };
        });
        using var client = new HttpClient(handler);
        var transport = new IpfsHttpStorageTransport(client);

        RemoteBrowseTransportPage page = await transport.BrowseAsync(
            CreateStorage(StorageProviderKind.Ipfs),
            bearerToken: null,
            new IpfsBrowseAddress(IpfsBrowseAddressKind.MutableFileSystem, "/projects"),
            new RemoteBrowseTransportRequest(
                Offset: 0,
                Limit: 10,
                MaximumInspectedItems: 20,
                MaximumResponseBytes: 64 * 1024,
                MaximumDuration: TimeSpan.FromSeconds(2)),
            CancellationToken.None);

        Assert.Equal(3, handler.RequestCount);
        Assert.Equal(3, page.RequestCount);
        Assert.Equal("mfs-revision", page.SourceRevision);
        Assert.Single(page.Entries);
        Assert.Equal(StorageBrowseEntryKind.Container, page.Entries[0].Kind);
    }

    [Fact]
    public async Task Ipfs_http_large_listing_first_page_stops_before_consuming_whole_response()
    {
        var json = new StringBuilder("{\"Objects\":[{\"Links\":[");
        for (int index = 0; index < 10_000; index++)
        {
            if (index > 0)
            {
                json.Append(',');
            }

            json.Append("{\"Name\":\"file-")
                .Append(index)
                .Append("\",\"Type\":2,\"Size\":12,\"Hash\":\"bafy-")
                .Append(index)
                .Append("\"}");
        }

        json.Append("]}]}");
        byte[] payload = Encoding.UTF8.GetBytes(json.ToString());
        var listingStream = new TrackingStream(payload);
        var handler = new RecordingHttpHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StreamContent(listingStream)
        });
        using var client = new HttpClient(handler);
        var transport = new IpfsHttpStorageTransport(client);

        RemoteBrowseTransportPage page = await transport.BrowseAsync(
            CreateStorage(StorageProviderKind.Ipfs),
            bearerToken: null,
            new IpfsBrowseAddress(IpfsBrowseAddressKind.ContentAddress, "bafy-root"),
            new RemoteBrowseTransportRequest(
                Offset: 0,
                Limit: 1,
                MaximumInspectedItems: 8,
                MaximumResponseBytes: 2 * 1024 * 1024,
                MaximumDuration: TimeSpan.FromSeconds(2)),
            CancellationToken.None);

        Assert.Single(page.Entries);
        Assert.True(page.HasMore);
        Assert.Equal(2, page.InspectedItems);
        Assert.True(listingStream.PositionAtDispose < listingStream.OriginalLength);
        Assert.True(page.ResponseBytes < payload.Length);
        output.WriteLine(
            "IPFS streaming total-bytes={0} consumed-bytes={1} reported-bytes={2} inspected={3} returned={4}",
            payload.Length,
            listingStream.PositionAtDispose,
            page.ResponseBytes,
            page.InspectedItems,
            page.Entries.Count);
    }

    [Fact]
    public async Task Ipfs_http_oversized_content_is_rejected_before_body_read()
    {
        var contentStream = new TrackingStream([1, 2, 3]);
        var handler = new RecordingHttpHandler(_ =>
        {
            var content = new StreamContent(contentStream);
            content.Headers.ContentLength = 300L * 1024 * 1024;
            return new HttpResponseMessage(HttpStatusCode.OK) { Content = content };
        });
        using var client = new HttpClient(handler);
        var transport = new IpfsHttpStorageTransport(client);

        StorageBrowseException exception = await Assert.ThrowsAsync<StorageBrowseException>(() =>
            transport.OpenReadAsync(
                CreateStorage(StorageProviderKind.Ipfs),
                bearerToken: null,
                "bafy-content",
                route: string.Empty,
                CancellationToken.None));

        Assert.Equal(StorageBrowseErrorCode.BudgetExceeded, exception.Error.Code);
        Assert.Equal(0, contentStream.ReadCount);
    }

    private static IpfsStorageBrowseDriver CreateIpfsDriver(
        FakeIpfsTransport transport,
        ILogger<IpfsStorageBrowseDriver>? logger = null)
        => new(
            new StaticSecretResolver(),
            transport,
            logger ?? NullLogger<IpfsStorageBrowseDriver>.Instance);

    private static FtpStorageBrowseDriver CreateFtpDriver(FakeFtpTransport transport)
        => new(
            new StaticSecretResolver(),
            transport,
            NullLogger<FtpStorageBrowseDriver>.Instance);

    private static StorageCatalogRecord CreateStorage(StorageProviderKind providerKind)
        => new()
        {
            Id = Guid.NewGuid(),
            Name = providerKind.ToString(),
            ProviderKind = providerKind,
            EndpointOrRoot = providerKind == StorageProviderKind.Ipfs
                ? "https://ipfs.example.test/api/v0/"
                : "ftp://ftp.example.test/"
        };

    private static StorageObjectReference CreateReference(StorageProviderKind providerKind)
        => new(
            Guid.NewGuid(),
            providerKind,
            providerKind == StorageProviderKind.Ipfs
                ? StorageLocatorKind.ContentAddress
                : StorageLocatorKind.RemotePath,
            providerKind == StorageProviderKind.Ipfs ? "bafy-content" : "folder/file.txt");

    private sealed class StaticSecretResolver(string? secret = null) : IStorageSecretResolver
    {
        public Task<string?> ResolveCredentialAsync(
            Guid? secretId,
            CancellationToken cancellationToken = default)
            => Task.FromResult(secret);
    }

    private sealed class FakeIpfsTransport : IIpfsStorageTransport
    {
        public List<IpfsBrowseAddress> Addresses { get; } = [];

        public TrackingStream ContentStream { get; } = new();

        public bool HasMore { get; set; }

        public int? ReportedInspectedItems { get; set; }

        public string SourceRevision { get; set; } = "mfs-revision-1";

        public bool WaitForCancellation { get; set; }

        public Exception? Failure { get; set; }

        public Task TestConnectionAsync(
            StorageCatalogRecord storage,
            string? bearerToken,
            CancellationToken cancellationToken) => Task.CompletedTask;

        public Task<IpfsAddResult> AddAsync(
            StorageCatalogRecord storage,
            string? bearerToken,
            string fileName,
            ReadOnlyMemory<byte> content,
            CancellationToken cancellationToken) => Task.FromResult(new IpfsAddResult("bafy-added"));

        public Task PinAsync(
            StorageCatalogRecord storage,
            string? bearerToken,
            string contentId,
            CancellationToken cancellationToken) => Task.CompletedTask;

        public Task<Stream> OpenReadAsync(
            StorageCatalogRecord storage,
            string? bearerToken,
            string locator,
            string route,
            CancellationToken cancellationToken) => Task.FromResult<Stream>(ContentStream);

        public async Task<RemoteBrowseTransportPage> BrowseAsync(
            StorageCatalogRecord storage,
            string? bearerToken,
            IpfsBrowseAddress address,
            RemoteBrowseTransportRequest request,
            CancellationToken cancellationToken)
        {
            if (Failure is not null)
            {
                throw Failure;
            }

            if (WaitForCancellation)
            {
                await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
            }

            Addresses.Add(address);
            string locator = address.Kind == IpfsBrowseAddressKind.ContentAddress
                ? "cid:bafy-child"
                : "mfs:/projects/child";
            var entries = new[]
            {
                new RemoteBrowseTransportEntry(
                    "child",
                    locator,
                    StorageBrowseEntryKind.Container,
                    42,
                    ContentVersion: "bafy-child")
            };
            return new RemoteBrowseTransportPage(
                entries,
                ReportedInspectedItems ?? entries.Length,
                HasMore,
                ResponseBytes: 256,
                RequestCount: 1,
                address.Kind == IpfsBrowseAddressKind.ContentAddress ? address.Value : SourceRevision);
        }
    }

    private sealed class FakeFtpTransport : IFtpStorageTransport
    {
        public TrackingStream ContentStream { get; } = new();

        public bool ClassificationReliable { get; set; } = true;

        public Task<string?> TestConnectionAsync(
            StorageCatalogRecord storage,
            string? password,
            CancellationToken cancellationToken) => Task.FromResult<string?>("FTP ready.");

        public Task UploadAsync(
            StorageCatalogRecord storage,
            string? password,
            string remotePath,
            ReadOnlyMemory<byte> content,
            CancellationToken cancellationToken) => Task.CompletedTask;

        public Task<Stream> OpenReadAsync(
            StorageCatalogRecord storage,
            string? password,
            string remotePath,
            CancellationToken cancellationToken) => Task.FromResult<Stream>(ContentStream);

        public Task DeleteAsync(
            StorageCatalogRecord storage,
            string? password,
            string remotePath,
            CancellationToken cancellationToken) => Task.CompletedTask;

        public Task<RemoteBrowseTransportPage> BrowseAsync(
            StorageCatalogRecord storage,
            string? password,
            string remotePath,
            RemoteBrowseTransportRequest request,
            CancellationToken cancellationToken)
        {
            RemoteBrowseTransportEntry[] entries =
            [
                new("folder", "folder", StorageBrowseEntryKind.Container, 0),
                new("file.txt", "file.txt", StorageBrowseEntryKind.File, 12)
            ];
            return Task.FromResult(new RemoteBrowseTransportPage(
                entries,
                InspectedItems: 2,
                HasMore: false,
                ResponseBytes: 180,
                RequestCount: 1,
                ClassificationReliable: ClassificationReliable));
        }
    }

    private sealed class TrackingStream : MemoryStream
    {
        public TrackingStream()
        {
        }

        public TrackingStream(byte[] buffer) : base(buffer)
        {
            OriginalLength = buffer.LongLength;
        }

        public int ReadCount { get; private set; }

        public long OriginalLength { get; }

        public long PositionAtDispose { get; private set; }

        private bool IsDisposed { get; set; }

        public override int Read(byte[] buffer, int offset, int count)
        {
            ReadCount++;
            return base.Read(buffer, offset, count);
        }

        public override ValueTask<int> ReadAsync(
            Memory<byte> buffer,
            CancellationToken cancellationToken = default)
        {
            ReadCount++;
            return base.ReadAsync(buffer, cancellationToken);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing && !IsDisposed)
            {
                PositionAtDispose = Position;
                IsDisposed = true;
            }

            base.Dispose(disposing);
        }
    }

    private sealed class RecordingHttpHandler(
        Func<HttpRequestMessage, HttpResponseMessage> responseFactory) : HttpMessageHandler
    {
        public int RequestCount { get; private set; }

        public List<string?> BearerTokens { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            RequestCount++;
            BearerTokens.Add(request.Headers.Authorization?.Parameter);
            return Task.FromResult(responseFactory(request));
        }
    }

    private sealed class ListLogger<T> : ILogger<T>
    {
        public List<string> Messages { get; } = [];

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            Messages.Add(formatter(state, exception));
        }
    }
}
