using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using CanDoItAll.Memory.Abstractions;
using CanDoItAll.Memory.Application;
using CanDoItAll.Memory.Http;
using CanDoItAll.Memory.Protocol.Http;

namespace CanDoItAll.Memory.Tests;

public sealed class MemoryHttpDriverTests
{
    private const string ApiKeyEnvironmentVariable = "CANDOITALL_MEMORY_HTTP_TEST_KEY";
    private static readonly DateTimeOffset Now = DateTimeOffset.Parse("2026-07-05T12:00:00Z");

    [Fact]
    public async Task HTTP001_Sync_context_pack_posts_plain_query_and_structured_envelope()
    {
        using var handler = new CapturingHandler((request, _) =>
            JsonResponse(HttpMemoryProviderResponse.FromContextPack(CreateContextPack())));
        var driver = CreateDriver(handler);
        var profile = CreateHttpProfile();
        var operation = CreateOperation();

        var result = await driver.ExecuteContextQueryAsync(profile, operation, CreateQuery());

        Assert.Equal(MemoryProviderDriverResultKind.ContextPack, result.Kind);
        Assert.Equal(MemoryLedgerStatus.Completed, result.LedgerStatus);
        Assert.Equal("HTTP context", result.ContextPack?.Summary);
        Assert.Equal(new Uri("https://memory.example.test/memory/query"), handler.RequestUri);
        Assert.Equal("Bearer provider-secret", handler.Authorization);
        using var body = JsonDocument.Parse(handler.RequestBody);
        Assert.Equal("payment integration", body.RootElement.GetProperty("query").GetString());
        Assert.Equal(operation.OperationId.Value.ToString("D"), body.RootElement.GetProperty("operationId").GetString());
        Assert.Equal(operation.CorrelationId.Value.ToString("D"), body.RootElement.GetProperty("correlationId").GetString());
        Assert.Equal(operation.RequestedCapability.Value, body.RootElement.GetProperty("capabilityId").GetString());
        Assert.True(body.RootElement.TryGetProperty("envelope", out var envelope));
        Assert.Equal(MemoryProtocolVersion.Current.Value, envelope.GetProperty("memoryProtocolVersion").GetProperty("value").GetString());
        Assert.Equal("workspace-1", envelope.GetProperty("workspaceContext").GetProperty("workspaceId").GetString());
        Assert.Equal("11111111-1111-1111-1111-111111111111", envelope.GetProperty("executionContext").GetProperty("projectId").GetString());
    }

    [Fact]
    public async Task HTTP002_Async_accepted_response_is_rejected_without_status_driver()
    {
        using var handler = new CapturingHandler((_, _) =>
            JsonResponse(HttpMemoryProviderResponse.FromAccepted(new MemoryOperationAccepted(
                new MemoryOperationId(Guid.Parse("11111111-1111-1111-1111-111111111111")),
                "/memory/operations/11111111-1111-1111-1111-111111111111",
                Now.AddMinutes(5),
                TimeSpan.FromSeconds(2),
                CallbackAvailable: false))));
        var driver = CreateDriver(handler);

        var result = await driver.ExecuteContextQueryAsync(CreateHttpProfile(), CreateOperation(), CreateQuery());

        Assert.Equal(MemoryProviderDriverResultKind.UnsupportedCapability, result.Kind);
        Assert.Equal(MemoryLedgerStatus.Failed, result.LedgerStatus);
        Assert.Null(result.AcceptedOperation);
        Assert.Null(result.ContextPack);
        Assert.Contains("operation-status polling is not implemented", result.Diagnostic, StringComparison.Ordinal);
    }

    [Fact]
    public async Task HTTP003_Timeout_budget_maps_to_timed_out_result()
    {
        using var handler = new CapturingHandler(async (_, cancellationToken) =>
        {
            await Task.Delay(TimeSpan.FromSeconds(10), cancellationToken);
            return JsonResponse(HttpMemoryProviderResponse.FromContextPack(CreateContextPack()));
        });
        var driver = CreateDriver(handler);
        var profile = CreateHttpProfile(timeoutMilliseconds: 20);

        var result = await driver.ExecuteContextQueryAsync(profile, CreateOperation(), CreateQuery());

        Assert.Equal(MemoryProviderDriverResultKind.Timeout, result.Kind);
        Assert.Equal(MemoryLedgerStatus.TimedOut, result.LedgerStatus);
        Assert.Contains("timed out", result.Diagnostic, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task HTTP004_Caller_cancellation_is_propagated()
    {
        using var handler = new CapturingHandler(async (_, cancellationToken) =>
        {
            await Task.Delay(TimeSpan.FromSeconds(10), cancellationToken);
            return JsonResponse(HttpMemoryProviderResponse.FromContextPack(CreateContextPack()));
        });
        var driver = CreateDriver(handler);
        using var cancellation = new CancellationTokenSource();

        var task = driver.ExecuteContextQueryAsync(CreateHttpProfile(), CreateOperation(), CreateQuery(), cancellation.Token);
        await cancellation.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => task);
        Assert.True(handler.CapturedCancellationToken.IsCancellationRequested);
    }

    [Fact]
    public async Task HTTP005_Health_degraded_response_is_returned()
    {
        var health = new MemoryProviderHealth(
            MemoryProviderHealthStatus.Degraded,
            LastErrorCategory: "warming-up",
            CreateHttpProfile().Manifest);
        using var handler = new CapturingHandler((request, _) =>
            request.RequestUri?.AbsolutePath == "/memory/health"
                ? JsonResponse(health)
                : JsonResponse(HttpMemoryProviderResponse.FromContextPack(CreateContextPack())));
        var driver = CreateDriver(handler);

        var result = await driver.GetHealthAsync(CreateHttpProfile());

        Assert.Equal(MemoryProviderHealthStatus.Degraded, result.Status);
        Assert.Equal("warming-up", result.LastErrorCategory);
    }

    [Fact]
    public async Task HTTP006_Malformed_response_maps_to_provider_error()
    {
        using var handler = new CapturingHandler((_, _) => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("not-json")
        });
        var driver = CreateDriver(handler);

        var result = await driver.ExecuteContextQueryAsync(CreateHttpProfile(), CreateOperation(), CreateQuery());

        Assert.Equal(MemoryProviderDriverResultKind.ProviderError, result.Kind);
        Assert.Equal(MemoryLedgerStatus.Failed, result.LedgerStatus);
        Assert.Contains("Malformed", result.Diagnostic, StringComparison.Ordinal);
    }

    [Fact]
    public async Task HTTP007_Unsupported_capability_response_is_typed()
    {
        using var handler = new CapturingHandler((_, _) =>
            JsonResponse(HttpMemoryProviderResponse.UnsupportedCapability("context.query.sync is disabled")));
        var driver = CreateDriver(handler);

        var result = await driver.ExecuteContextQueryAsync(CreateHttpProfile(), CreateOperation(), CreateQuery());

        Assert.Equal(MemoryProviderDriverResultKind.UnsupportedCapability, result.Kind);
        Assert.Equal(MemoryLedgerStatus.Failed, result.LedgerStatus);
        Assert.Contains("disabled", result.Diagnostic, StringComparison.Ordinal);
    }

    [Fact]
    public async Task HTTP008_Unavailable_response_is_typed_without_retry_by_default()
    {
        using var handler = new CapturingHandler((_, _) => new HttpResponseMessage(HttpStatusCode.ServiceUnavailable)
        {
            Content = JsonContent.Create(HttpMemoryProviderResponse.ProviderError("provider-offline", "Provider is offline."))
        });
        var driver = CreateDriver(handler);

        var result = await driver.ExecuteContextQueryAsync(CreateHttpProfile(), CreateOperation(), CreateQuery());

        Assert.Equal(MemoryProviderDriverResultKind.Unavailable, result.Kind);
        Assert.Equal(MemoryLedgerStatus.Failed, result.LedgerStatus);
        Assert.Equal(1, handler.RequestCount);
    }

    [Fact]
    public async Task HTTP009_Missing_credential_environment_variable_fails_before_dispatch()
    {
        using var handler = new CapturingHandler((_, _) =>
            JsonResponse(HttpMemoryProviderResponse.FromContextPack(CreateContextPack())));
        var driver = CreateDriver(handler);
        var profile = CreateHttpProfile();
        Environment.SetEnvironmentVariable(ApiKeyEnvironmentVariable, null);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            driver.ExecuteContextQueryAsync(profile, CreateOperation(), CreateQuery()));

        Assert.Contains(ApiKeyEnvironmentVariable, exception.Message, StringComparison.Ordinal);
        Assert.Equal(0, handler.RequestCount);
    }

    [Fact]
    public async Task HTTP010_Persisted_raw_credential_is_rejected()
    {
        using var handler = new CapturingHandler((_, _) =>
            JsonResponse(HttpMemoryProviderResponse.FromContextPack(CreateContextPack())));
        var driver = CreateDriver(handler);
        var profile = CreateHttpProfile();
        var values = profile.Manifest.Extensions.Values.ToDictionary();
        values[HttpMemoryProviderConfigurationKeys.LegacyRawApiKey] =
            JsonSerializer.SerializeToElement("must-not-be-stored");
        profile = profile with
        {
            Manifest = profile.Manifest with
            {
                Extensions = new MemoryExtensionData(values)
            }
        };

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            driver.ExecuteContextQueryAsync(profile, CreateOperation(), CreateQuery()));

        Assert.Contains("raw credential", exception.Message, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("must-not-be-stored", exception.Message, StringComparison.Ordinal);
        Assert.Equal(0, handler.RequestCount);
    }

    [Fact]
    public async Task HTTP011_Cleartext_non_loopback_endpoint_is_rejected()
    {
        using var handler = new CapturingHandler((_, _) =>
            JsonResponse(HttpMemoryProviderResponse.FromContextPack(CreateContextPack())));
        var driver = CreateDriver(handler);
        var profile = CreateHttpProfile();
        var values = profile.Manifest.Extensions.Values.ToDictionary();
        values[HttpMemoryProviderConfigurationKeys.BaseUrl] =
            JsonSerializer.SerializeToElement("http://memory.example.test");
        profile = profile with
        {
            Manifest = profile.Manifest with
            {
                Extensions = new MemoryExtensionData(values)
            }
        };

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            driver.ExecuteContextQueryAsync(profile, CreateOperation(), CreateQuery()));

        Assert.Contains("HTTPS", exception.Message, StringComparison.Ordinal);
        Assert.Equal(0, handler.RequestCount);
    }

    [Theory]
    [InlineData(HttpMemoryProviderConfigurationKeys.BaseUrl, "https://memory.example.test?token=secret")]
    [InlineData(HttpMemoryProviderConfigurationKeys.QueryPath, "/v1/context/query?token=secret")]
    public async Task HTTP011A_Secret_bearing_url_locations_are_rejected(
        string extensionKey,
        string extensionValue)
    {
        using var handler = new CapturingHandler((_, _) =>
            JsonResponse(HttpMemoryProviderResponse.FromContextPack(CreateContextPack())));
        var driver = CreateDriver(handler);
        var profile = WithExtension(CreateHttpProfile(), extensionKey, extensionValue);

        var exception = await Record.ExceptionAsync(() =>
            driver.ExecuteContextQueryAsync(profile, CreateOperation(), CreateQuery()));

        var rejection = Assert.IsAssignableFrom<Exception>(exception);
        Assert.True(rejection is ArgumentException or InvalidOperationException);
        Assert.DoesNotContain("secret", rejection.Message, StringComparison.Ordinal);
        Assert.Equal(0, handler.RequestCount);
    }

    [Fact]
    public async Task HTTP012_Invalid_credential_environment_variable_reference_is_rejected()
    {
        using var handler = new CapturingHandler((_, _) =>
            JsonResponse(HttpMemoryProviderResponse.FromContextPack(CreateContextPack())));
        var driver = CreateDriver(handler);
        var profile = WithExtension(
            CreateHttpProfile(),
            HttpMemoryProviderConfigurationKeys.ApiKeyEnvironmentVariable,
            "INVALID-VARIABLE-NAME");

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            driver.ExecuteContextQueryAsync(profile, CreateOperation(), CreateQuery()));

        Assert.Contains("environment-variable identifier", exception.Message, StringComparison.Ordinal);
        Assert.Equal(0, handler.RequestCount);
    }

    [Fact]
    public async Task HTTP013_Invalid_authentication_header_name_is_rejected()
    {
        using var handler = new CapturingHandler((_, _) =>
            JsonResponse(HttpMemoryProviderResponse.FromContextPack(CreateContextPack())));
        var driver = CreateDriver(handler);
        var profile = WithExtension(
            CreateHttpProfile(),
            HttpMemoryProviderConfigurationKeys.AuthHeaderName,
            "X-Memory:Injected");

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            driver.ExecuteContextQueryAsync(profile, CreateOperation(), CreateQuery()));

        Assert.Contains("header name", exception.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(0, handler.RequestCount);
    }

    [Fact]
    public async Task HTTP014_Credential_header_injection_is_rejected_before_dispatch()
    {
        using var handler = new CapturingHandler((_, _) =>
            JsonResponse(HttpMemoryProviderResponse.FromContextPack(CreateContextPack())));
        var driver = CreateDriver(handler);
        var profile = CreateHttpProfile();
        Environment.SetEnvironmentVariable(ApiKeyEnvironmentVariable, "provider-secret\r\nX-Injected: true");

        try
        {
            var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
                driver.ExecuteContextQueryAsync(profile, CreateOperation(), CreateQuery()));

            Assert.Contains("invalid header value", exception.Message, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("provider-secret", exception.Message, StringComparison.Ordinal);
            Assert.Equal(0, handler.RequestCount);
        }
        finally
        {
            Environment.SetEnvironmentVariable(ApiKeyEnvironmentVariable, "provider-secret");
        }
    }

    [Theory]
    [InlineData("memory/query")]
    [InlineData("//attacker.example.test/collect")]
    [InlineData("https://attacker.example.test/collect")]
    [InlineData("/memory\\query")]
    [InlineData("/memory//query")]
    [InlineData("/memory/query\r\nX-Injected:true")]
    [InlineData("//user:password@attacker.example.test/collect")]
    public async Task HTTP015_Invalid_query_path_is_rejected_before_authenticated_dispatch(string queryPath)
    {
        using var handler = new CapturingHandler((_, _) =>
            JsonResponse(HttpMemoryProviderResponse.FromContextPack(CreateContextPack())));
        var driver = CreateDriver(handler);
        var profile = WithExtension(
            CreateHttpProfile(),
            HttpMemoryProviderConfigurationKeys.QueryPath,
            queryPath);

        await Assert.ThrowsAsync<ArgumentException>(() =>
            driver.ExecuteContextQueryAsync(profile, CreateOperation(), CreateQuery()));

        Assert.Equal(0, handler.RequestCount);
        Assert.Null(handler.RequestUri);
        Assert.Null(handler.Authorization);
    }

    [Fact]
    public async Task HTTP016_Network_health_path_is_rejected_before_authenticated_dispatch()
    {
        using var handler = new CapturingHandler((_, _) =>
            JsonResponse(new MemoryProviderHealth(
                MemoryProviderHealthStatus.Reachable,
                LastErrorCategory: null,
                CreateHttpProfile().Manifest)));
        var driver = CreateDriver(handler);
        var profile = WithExtension(
            CreateHttpProfile(),
            HttpMemoryProviderConfigurationKeys.HealthPath,
            "//user:password@attacker.example.test/collect");

        await Assert.ThrowsAsync<ArgumentException>(() => driver.GetHealthAsync(profile));

        Assert.Equal(0, handler.RequestCount);
        Assert.Null(handler.RequestUri);
        Assert.Null(handler.Authorization);
    }

    private static MemoryProviderProfile WithExtension(
        MemoryProviderProfile profile,
        string key,
        string value)
    {
        var values = profile.Manifest.Extensions.Values.ToDictionary();
        values[key] = JsonSerializer.SerializeToElement(value);
        return profile with
        {
            Manifest = profile.Manifest with
            {
                Extensions = new MemoryExtensionData(values)
            }
        };
    }

    private static HttpMemoryProviderDriver CreateDriver(CapturingHandler handler)
    {
        return new HttpMemoryProviderDriver(
            new StaticHttpClientFactory(handler),
            new HttpMemoryProviderOptions());
    }

    private static MemoryProviderProfile CreateHttpProfile(int? timeoutMilliseconds = null)
    {
        Environment.SetEnvironmentVariable(ApiKeyEnvironmentVariable, "provider-secret");
        var extensions = new List<(string Key, JsonElement Value)>
        {
            (HttpMemoryProviderConfigurationKeys.BaseUrl, JsonSerializer.SerializeToElement("https://memory.example.test")),
            (HttpMemoryProviderConfigurationKeys.ApiKeyEnvironmentVariable, JsonSerializer.SerializeToElement(ApiKeyEnvironmentVariable))
        };
        if (timeoutMilliseconds.HasValue)
        {
            extensions.Add((HttpMemoryProviderConfigurationKeys.TimeoutMilliseconds, JsonSerializer.SerializeToElement(timeoutMilliseconds.Value)));
        }

        return new MemoryProviderProfile(
            MemoryProviderInstanceId.Parse("provider.http"),
            DisplayName: "HTTP memory",
            MemoryProviderDriverKind.Http,
            IsEnabled: true,
            MemoryProviderHealthState.Healthy,
            MemoryProviderWorkspaceScope.AllWorkspaces,
            SelectionTags: ["http"],
            MemoryProviderProfilePolicy.Default,
            new MemoryProviderManifest(
                MemoryProviderKind.Parse("memory.http"),
                MemoryProtocolVersion.Current,
                [new MemoryCapabilityDescriptor(MemoryCapabilityId.Parse("context.query.sync"), Version: "1", Supported: true)],
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
            MemoryProviderInstanceId.Parse("provider.http"),
            MemoryCapabilityId.Parse("context.query.sync"),
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
            [MemorySourceSnapshotId.Parse("snapshot.project.1")],
            MemoryLedgerRetentionPolicy.Expiring(Now.AddDays(1), Now.AddDays(7)),
            Now);
    }

    private static MemoryContextQueryRequest CreateQuery()
    {
        return new MemoryContextQueryRequest(
            "payment integration",
            [MemoryCapabilityId.Parse("context.query.sync")],
            new MemorySourceProvenance(
                MemorySourceSnapshotId.Parse("snapshot.project.1"),
                SourceModule: nameof(MemorySourceKind.Project),
                SourceRecordIds: ["project-1"],
                Citations: ["Project 1"]))
        {
            Context = new MemoryRequestContext(
                new MemoryWorkspaceContext("workspace-1", "Workspace 1", CustomerId: null, Domain: null, Tags: []),
                new MemoryExecutionContext(
                    ProjectId: "11111111-1111-1111-1111-111111111111",
                    ProjectName: "Project 1",
                    ProcessId: "process-1",
                    ProcessStepId: "step-1",
                    ProcessStepName: null,
                    WorkflowId: "workflow-1",
                    WorkflowNodeId: "node-1",
                    ArtifactIds: []),
                MemoryPolicyContext.InternalDefault,
                MemoryBudget.Default,
                MemoryExtensionData.Empty)
        };
    }

    private static MemoryContextPack CreateContextPack()
    {
        return new MemoryContextPack(
            MemoryContextPackId.New(),
            "HTTP context",
            [
                new MemoryContextSection(
                    "Relevant memory",
                    "Use provider-neutral HTTP memory.",
                    [new MemoryCitation("memory://http/1", "HTTP memory")],
                    Confidence: 0.91m)
            ],
            Warnings: [],
            ProviderConfidence: 0.91m,
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

        public CancellationToken CapturedCancellationToken { get; private set; }

        public int RequestCount { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            RequestCount++;
            CapturedCancellationToken = cancellationToken;
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
