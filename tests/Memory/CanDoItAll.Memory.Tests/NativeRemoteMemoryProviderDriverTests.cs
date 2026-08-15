using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using CanDoItAll.Memory.Abstractions;
using CanDoItAll.Memory.Application;
using CanDoItAll.Memory.Drivers.CognitiveMemory;
using CanDoItAll.Memory.Http;
using CanDoItAll.Memory.Protocol.Http;

namespace CanDoItAll.Memory.Tests.Providers;

public sealed class NativeRemoteMemoryProviderDriverTests
{
    private const string ApiKeyEnvironmentVariable = "CANDOITALL_TEST_NATIVE_MEMORY_API_KEY";
    private static readonly DateTimeOffset Now = DateTimeOffset.Parse("2026-07-06T01:00:00Z");

    [Fact]
    public async Task NativeRemoteDriver_PostsGenericQueryToNativeProtocolEndpoint()
    {
        using var handler = new CapturingHandler((_, _) =>
            JsonResponse(HttpMemoryProviderResponse.FromContextPack(CreateContextPack())));
        var driver = CreateDriver(handler);
        var operation = CreateOperation();

        var result = await driver.ExecuteContextQueryAsync(CreateNativeProfile(), operation, CreateQuery());

        Assert.Equal(MemoryProviderDriverKind.NativeRemote, driver.DriverKind);
        Assert.Equal(MemoryProviderDriverResultKind.ContextPack, result.Kind);
        Assert.Equal("Native context", result.ContextPack?.Summary);
        Assert.Equal(new Uri("https://native-memory.example.test/memory/query"), handler.RequestUri);
        Assert.Equal("Bearer native-secret", handler.Authorization);
        using var body = JsonDocument.Parse(handler.RequestBody);
        Assert.Equal(operation.OperationId.Value.ToString("D"), body.RootElement.GetProperty("operationId").GetString());
        Assert.Equal(MemoryProtocolVersion.Current.Value, body.RootElement.GetProperty("memoryProtocolVersion").GetString());
        Assert.True(body.RootElement.TryGetProperty("envelope", out _));
    }

    [Fact]
    public async Task NativeRemoteDriver_HealthUsesNativeHealthPath()
    {
        var profile = CreateNativeProfile();
        var health = new MemoryProviderHealth(
            MemoryProviderHealthStatus.Reachable,
            LastErrorCategory: null,
            profile.Manifest);
        using var handler = new CapturingHandler((_, _) => JsonResponse(health));
        var driver = CreateDriver(handler);

        var result = await driver.GetHealthAsync(profile);

        Assert.Equal(MemoryProviderHealthStatus.Reachable, result.Status);
        Assert.Equal(new Uri("https://native-memory.example.test/memory/health"), handler.RequestUri);
    }

    [Fact]
    public async Task NativeRemoteDriver_MissingServiceBaseUrlFailsPredictably()
    {
        using var handler = new CapturingHandler((_, _) =>
            JsonResponse(HttpMemoryProviderResponse.FromContextPack(CreateContextPack())));
        var driver = CreateDriver(handler);
        var profile = CreateNativeProfile(withBaseUrl: false);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            driver.ExecuteContextQueryAsync(profile, CreateOperation(), CreateQuery()));

        Assert.Contains(NativeRemoteMemoryProviderConfigurationKeys.ServiceBaseUrl, exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task NativeRemoteDriver_TimeoutMapsToTimedOutResult()
    {
        using var handler = new CapturingHandler(async (_, cancellationToken) =>
        {
            await Task.Delay(TimeSpan.FromSeconds(10), cancellationToken);
            return JsonResponse(HttpMemoryProviderResponse.FromContextPack(CreateContextPack()));
        });
        var driver = CreateDriver(handler);

        var result = await driver.ExecuteContextQueryAsync(
            CreateNativeProfile(timeoutMilliseconds: 20),
            CreateOperation(),
            CreateQuery());

        Assert.Equal(MemoryProviderDriverResultKind.Timeout, result.Kind);
        Assert.Equal(MemoryLedgerStatus.TimedOut, result.LedgerStatus);
        Assert.Contains("timed out", result.Diagnostic, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task NativeRemoteDriver_MalformedResponseMapsToProviderError()
    {
        using var handler = new CapturingHandler((_, _) => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("not-json")
        });
        var driver = CreateDriver(handler);

        var result = await driver.ExecuteContextQueryAsync(CreateNativeProfile(), CreateOperation(), CreateQuery());

        Assert.Equal(MemoryProviderDriverResultKind.ProviderError, result.Kind);
        Assert.Equal(MemoryLedgerStatus.Failed, result.LedgerStatus);
        Assert.Contains("Malformed", result.Diagnostic, StringComparison.Ordinal);
    }

    [Fact]
    public async Task NativeRemoteDriver_MaliciousQueryPathCannotRedirectAuthenticatedRequest()
    {
        using var handler = new CapturingHandler((_, _) =>
            JsonResponse(HttpMemoryProviderResponse.FromContextPack(CreateContextPack())));
        var driver = CreateDriver(handler);
        var profile = CreateNativeProfile(
            queryPath: "//user:password@attacker.example.test/collect");

        await Assert.ThrowsAsync<ArgumentException>(() =>
            driver.ExecuteContextQueryAsync(profile, CreateOperation(), CreateQuery()));

        Assert.Equal(0, handler.RequestCount);
        Assert.Null(handler.RequestUri);
        Assert.Null(handler.Authorization);
    }

    private static NativeRemoteMemoryProviderDriver CreateDriver(CapturingHandler handler)
    {
        return new NativeRemoteMemoryProviderDriver(
            new StaticHttpClientFactory(handler),
            new NativeRemoteMemoryProviderOptions());
    }

    private static MemoryProviderProfile CreateNativeProfile(
        bool withBaseUrl = true,
        int? timeoutMilliseconds = null,
        string? queryPath = null)
    {
        Environment.SetEnvironmentVariable(ApiKeyEnvironmentVariable, "native-secret");
        var extensions = new List<(string Key, JsonElement Value)>();
        if (withBaseUrl)
        {
            extensions.Add((NativeRemoteMemoryProviderConfigurationKeys.ServiceBaseUrl, JsonSerializer.SerializeToElement("https://native-memory.example.test")));
        }

        extensions.Add((
            NativeRemoteMemoryProviderConfigurationKeys.ApiKeyEnvironmentVariable,
            JsonSerializer.SerializeToElement(ApiKeyEnvironmentVariable)));
        if (timeoutMilliseconds.HasValue)
        {
            extensions.Add((NativeRemoteMemoryProviderConfigurationKeys.TimeoutMilliseconds, JsonSerializer.SerializeToElement(timeoutMilliseconds.Value)));
        }

        if (queryPath is not null)
        {
            extensions.Add((NativeRemoteMemoryProviderConfigurationKeys.QueryPath, JsonSerializer.SerializeToElement(queryPath)));
        }

        return new MemoryProviderProfile(
            MemoryProviderInstanceId.Parse("provider.native"),
            DisplayName: "Native remote memory",
            MemoryProviderDriverKind.NativeRemote,
            IsEnabled: true,
            MemoryProviderHealthState.Healthy,
            MemoryProviderWorkspaceScope.AllWorkspaces,
            SelectionTags: ["native"],
            MemoryProviderProfilePolicy.Default,
            new MemoryProviderManifest(
                MemoryProviderKind.Parse("memory.cognitive-native"),
                MemoryProtocolVersion.Current,
                [new MemoryCapabilityDescriptor(MemoryCapabilityIds.ContextQuerySync, Version: "1", Supported: true)],
                MemoryProviderInteractionSupport.SyncQueryOnly,
                UiSurfaces: [],
                MemoryProviderLimits.Default,
                MemoryExtensionData.From(extensions.ToArray())));
    }

    private static MemoryOperationRecord CreateOperation()
    {
        return MemoryOperationRecord.Create(
            MemoryOperationRecordId.New(),
            MemoryOperationId.New(),
            MemoryProviderInstanceId.Parse("provider.native"),
            MemoryCapabilityIds.ContextQuerySync,
            MemoryOperationKind.ContextQuery,
            new MemoryLedgerRequester(
                RequesterId: "user-42",
                AgentId: "agent-1",
                AgentRole: "programmer",
                SessionId: "session-1",
                WorkflowId: "workflow-1",
                WorkflowNodeId: "node-1",
                ProcessId: "process-1",
                ProcessStepId: "step-1"),
            MemoryCorrelationId.New(),
            MemoryCausationId.New(),
            [MemorySourceSnapshotId.Parse("snapshot.native.1")],
            MemoryLedgerRetentionPolicy.Expiring(Now.AddDays(1), Now.AddDays(7)),
            Now);
    }

    private static MemoryContextQueryRequest CreateQuery()
    {
        return new MemoryContextQueryRequest(
            "native protocol",
            [MemoryCapabilityIds.ContextQuerySync],
            MemorySourceProvenance.None);
    }

    private static MemoryContextPack CreateContextPack()
    {
        return new MemoryContextPack(
            MemoryContextPackId.New(),
            "Native context",
            [
                new MemoryContextSection(
                    "Native memory",
                    "Use the native remote provider endpoint.",
                    [new MemoryCitation("native://memory/1", "Native memory")],
                    Confidence: 0.93m)
            ],
            Warnings: [],
            ProviderConfidence: 0.93m,
            FeedbackHandle: null);
    }

    private static HttpResponseMessage JsonResponse<T>(T value, HttpStatusCode statusCode = HttpStatusCode.OK)
    {
        return new HttpResponseMessage(statusCode)
        {
            Content = JsonContent.Create(value)
        };
    }

    private sealed class StaticHttpClientFactory(HttpMessageHandler handler) : IHttpClientFactory
    {
        public HttpClient CreateClient(string name)
        {
            return new HttpClient(handler, disposeHandler: false);
        }
    }

    private sealed class CapturingHandler(
        Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> respond) : HttpMessageHandler, IDisposable
    {
        private readonly Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> respond = respond;

        public CapturingHandler(Func<HttpRequestMessage, CancellationToken, HttpResponseMessage> respond)
            : this((request, cancellationToken) => Task.FromResult(respond(request, cancellationToken)))
        {
        }

        public Uri? RequestUri { get; private set; }

        public string? Authorization { get; private set; }

        public string RequestBody { get; private set; } = string.Empty;

        public int RequestCount { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            RequestCount++;
            RequestUri = request.RequestUri;
            Authorization = request.Headers.Authorization?.ToString();
            RequestBody = request.Content is null
                ? string.Empty
                : await request.Content.ReadAsStringAsync(cancellationToken);
            return await respond(request, cancellationToken);
        }

        void IDisposable.Dispose()
        {
            Dispose(disposing: true);
        }
    }
}
