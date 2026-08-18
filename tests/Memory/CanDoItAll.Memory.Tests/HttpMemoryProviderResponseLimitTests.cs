using System.Net;
using System.Text.Json;
using CanDoItAll.Memory.Abstractions;
using CanDoItAll.Memory.Application;
using CanDoItAll.Memory.Drivers.CognitiveMemory;
using CanDoItAll.Memory.Http;

namespace CanDoItAll.Memory.Tests.Providers;

public sealed class HttpMemoryProviderResponseLimitTests
{
    private const int ResponseLimitBytes = 128;

    [Fact]
    public async Task Http_driver_rejects_declared_oversized_body_without_reading_it()
    {
        var stream = new TrackingResponseStream(ResponseLimitBytes + 1);
        using var content = CreateStreamingContent(
            stream,
            declaredLength: ResponseLimitBytes + 1);
        var driver = CreateDriver(MemoryProviderDriverKind.Http, content);

        var result = await driver.ExecuteContextQueryAsync(
            CreateProfile(MemoryProviderDriverKind.Http),
            CreateOperation(),
            CreateQuery());

        Assert.Equal(MemoryProviderDriverResultKind.ProviderError, result.Kind);
        Assert.Contains("128 bytes", result.Diagnostic, StringComparison.Ordinal);
        Assert.Equal(0, stream.TotalBytesRead);
    }

    [Fact]
    public async Task Http_driver_stops_chunked_oversized_body_at_limit_plus_one()
    {
        var stream = new TrackingResponseStream(ResponseLimitBytes + 64);
        using var content = CreateStreamingContent(stream, declaredLength: null);
        var driver = CreateDriver(MemoryProviderDriverKind.Http, content);

        var result = await driver.ExecuteContextQueryAsync(
            CreateProfile(MemoryProviderDriverKind.Http),
            CreateOperation(),
            CreateQuery());

        Assert.Equal(MemoryProviderDriverResultKind.ProviderError, result.Kind);
        Assert.Contains("128 bytes", result.Diagnostic, StringComparison.Ordinal);
        Assert.Equal(ResponseLimitBytes + 1, stream.TotalBytesRead);
    }

    [Fact]
    public async Task Native_remote_driver_applies_the_same_declared_body_limit()
    {
        var stream = new TrackingResponseStream(ResponseLimitBytes + 1);
        using var content = CreateStreamingContent(
            stream,
            declaredLength: ResponseLimitBytes + 1);
        var driver = CreateDriver(MemoryProviderDriverKind.NativeRemote, content);

        var result = await driver.ExecuteContextQueryAsync(
            CreateProfile(MemoryProviderDriverKind.NativeRemote),
            CreateOperation(),
            CreateQuery());

        Assert.Equal(MemoryProviderDriverResultKind.ProviderError, result.Kind);
        Assert.Contains("128 bytes", result.Diagnostic, StringComparison.Ordinal);
        Assert.Equal(0, stream.TotalBytesRead);
    }

    private static IMemoryProviderDriver CreateDriver(
        MemoryProviderDriverKind driverKind,
        HttpContent content)
    {
        var factory = new StaticHttpClientFactory(new ResponseHandler(content));
        var sizeLimit = new MemoryProviderResponseSizeLimit(ResponseLimitBytes);
        return driverKind == MemoryProviderDriverKind.Http
            ? new HttpMemoryProviderDriver(
                factory,
                new HttpMemoryProviderOptions { ResponseSizeLimit = sizeLimit })
            : new NativeRemoteMemoryProviderDriver(
                factory,
                new NativeRemoteMemoryProviderOptions { ResponseSizeLimit = sizeLimit });
    }

    private static StreamContent CreateStreamingContent(
        TrackingResponseStream stream,
        long? declaredLength)
    {
        var content = new StreamContent(stream);
        content.Headers.ContentLength = declaredLength;
        return content;
    }

    private static MemoryProviderProfile CreateProfile(MemoryProviderDriverKind driverKind)
    {
        var isNative = driverKind == MemoryProviderDriverKind.NativeRemote;
        var baseUrlKey = isNative
            ? NativeRemoteMemoryProviderConfigurationKeys.ServiceBaseUrl
            : HttpMemoryProviderConfigurationKeys.BaseUrl;
        return new MemoryProviderProfile(
            MemoryProviderInstanceId.Parse("provider.response-limit"),
            "Response limit provider",
            driverKind,
            IsEnabled: true,
            MemoryProviderHealthState.Healthy,
            MemoryProviderWorkspaceScope.AllWorkspaces,
            SelectionTags: [],
            MemoryProviderProfilePolicy.Default,
            new MemoryProviderManifest(
                MemoryProviderKind.Parse(isNative ? "memory.native" : "memory.http"),
                MemoryProtocolVersion.Current,
                [new MemoryCapabilityDescriptor(MemoryCapabilityIds.ContextQuerySync, "1", Supported: true)],
                MemoryProviderInteractionSupport.SyncQueryOnly,
                UiSurfaces: [],
                MemoryProviderLimits.Default,
                MemoryExtensionData.From((
                    baseUrlKey,
                    JsonSerializer.SerializeToElement("https://memory.example.test")))));
    }

    private static MemoryOperationRecord CreateOperation()
    {
        var now = DateTimeOffset.Parse("2026-07-12T12:00:00Z");
        return MemoryOperationRecord.Create(
            MemoryOperationRecordId.New(),
            MemoryOperationId.New(),
            MemoryProviderInstanceId.Parse("provider.response-limit"),
            MemoryCapabilityIds.ContextQuerySync,
            MemoryOperationKind.ContextQuery,
            new MemoryLedgerRequester("test", null, null, null, null, null, null, null),
            MemoryCorrelationId.New(),
            MemoryCausationId.New(),
            [],
            MemoryLedgerRetentionPolicy.Expiring(now.AddHours(1), now.AddHours(2)),
            now);
    }

    private static MemoryContextQueryRequest CreateQuery() =>
        new("query", [MemoryCapabilityIds.ContextQuerySync], MemorySourceProvenance.None);

    private sealed class StaticHttpClientFactory(HttpMessageHandler handler) : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => new(handler, disposeHandler: false);
    }

    private sealed class ResponseHandler(HttpContent content) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = content });
    }

    private sealed class TrackingResponseStream(int length) : Stream
    {
        private int remaining = length;

        public int TotalBytesRead { get; private set; }
        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => throw new NotSupportedException();
        public override long Position
        {
            get => throw new NotSupportedException();
            set => throw new NotSupportedException();
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            var read = Math.Min(count, remaining);
            Array.Fill(buffer, (byte)'x', offset, read);
            remaining -= read;
            TotalBytesRead += read;
            return read;
        }

        public override ValueTask<int> ReadAsync(
            Memory<byte> buffer,
            CancellationToken cancellationToken = default)
        {
            var read = Math.Min(buffer.Length, remaining);
            buffer.Span[..read].Fill((byte)'x');
            remaining -= read;
            TotalBytesRead += read;
            return ValueTask.FromResult(read);
        }

        public override void Flush() { }
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }
}
