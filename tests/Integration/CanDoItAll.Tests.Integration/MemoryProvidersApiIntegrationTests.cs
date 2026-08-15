using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using CanDoItAll.Memory.Abstractions;
using CanDoItAll.Memory.Application;
using CanDoItAll.Modules.Memory.Services;
using CanDoItAll.Modules.Workspace.ApiAccess;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace CanDoItAll.Tests.Integration.Memory;

public sealed class MemoryProvidersApiIntegrationTests
{
    [Fact]
    public async Task Profile_routes_round_trip_safe_configuration_and_retired_cognitive_memory_route_is_absent()
    {
        await using var host = await ApiTestHost.CreateAsync(
            jwtEnabled: false,
            useInMemoryDatabase: true);
        const string providerId = "provider.api-test";

        using var saveResponse = await host.Client.PutAsJsonAsync(
            $"/api/memory-providers/{providerId}",
            CreateMockProfileRequest());
        Assert.Equal(HttpStatusCode.OK, saveResponse.StatusCode);
        using var saved = JsonDocument.Parse(await saveResponse.Content.ReadAsStringAsync());
        Assert.Equal(providerId, saved.RootElement.GetProperty("providerId").GetString());
        Assert.Equal("Mock", saved.RootElement.GetProperty("driverKind").GetString());
        Assert.True(saved.RootElement
            .GetProperty("capabilities")
            .GetProperty("supportsSynchronousQueries")
            .GetBoolean());
        Assert.False(saved.RootElement
            .GetProperty("capabilities")
            .GetProperty("supportsRclUi")
            .GetBoolean());
        Assert.False(saved.RootElement
            .GetProperty("capabilities")
            .GetProperty("supportsIframeUi")
            .GetBoolean());

        using var listResponse = await host.Client.GetAsync("/api/memory-providers");
        Assert.Equal(HttpStatusCode.OK, listResponse.StatusCode);
        using var listed = JsonDocument.Parse(await listResponse.Content.ReadAsStringAsync());
        Assert.Contains(
            listed.RootElement.EnumerateArray(),
            provider => provider.GetProperty("providerId").GetString() == providerId);

        using var getResponse = await host.Client.GetAsync($"/api/memory-providers/{providerId}");
        Assert.Equal(HttpStatusCode.OK, getResponse.StatusCode);

        using var retiredResponse = await host.Client.GetAsync("/api/cognitive-memory/contract");
        Assert.Equal(HttpStatusCode.NotFound, retiredResponse.StatusCode);

        using var openApi = JsonDocument.Parse(
            await host.Client.GetStringAsync("/openapi/v1.json"));
        var paths = openApi.RootElement.GetProperty("paths");
        Assert.True(paths.TryGetProperty("/api/memory-providers", out _));
        Assert.True(paths.TryGetProperty("/api/memory-providers/{providerId}", out _));
        Assert.True(paths.TryGetProperty("/api/memory-providers/{providerId}/queries", out _));
        Assert.True(paths.TryGetProperty("/api/memory-providers/operations/{operationId}", out _));
        Assert.False(paths.TryGetProperty("/api/cognitive-memory/contract", out _));
    }

    [Fact]
    public async Task Profile_route_rejects_unmapped_raw_credentials()
    {
        await using var host = await ApiTestHost.CreateAsync(
            jwtEnabled: false,
            useInMemoryDatabase: true);
        const string requestJson = """
            {
              "displayName": "Unsafe HTTP provider",
              "driverKind": "Http",
              "isEnabled": true,
              "fallbackBehavior": "DenyImplicitFallback",
              "providerKind": "memory.http",
              "selectionTags": [],
              "capabilities": {
                "supportsSynchronousQueries": true,
                "supportsAsynchronousQueries": false,
                "supportsOperationStatus": false
              },
              "http": {
                "baseUrl": "https://memory.example.test",
                "queryPath": "/query",
                "healthPath": "/health",
                "apiKeyEnvironmentVariable": "MEMORY_API_KEY",
                "apiKey": "must-not-be-accepted",
                "authHeaderName": "Authorization",
                "authScheme": "Bearer",
                "timeoutMilliseconds": 30000,
                "maxRetryAttempts": 0
              },
              "mcp": null
            }
            """;
        using var content = new StringContent(requestJson, Encoding.UTF8, "application/json");

        using var response = await host.Client.PutAsync(
            "/api/memory-providers/provider.unsafe",
            content);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var profiles = await host.Client.GetFromJsonAsync<JsonElement>("/api/memory-providers");
        Assert.DoesNotContain(
            profiles.EnumerateArray(),
            provider => provider.GetProperty("providerId").GetString() == "provider.unsafe");
    }

    [Fact]
    public async Task Profile_route_rejects_writable_ui_capability_claims()
    {
        await using var host = await ApiTestHost.CreateAsync(
            jwtEnabled: false,
            useInMemoryDatabase: true);
        var request = new
        {
            displayName = "Unsafe UI provider",
            driverKind = "Mock",
            isEnabled = true,
            fallbackBehavior = "DenyImplicitFallback",
            providerKind = "memory.mock",
            selectionTags = Array.Empty<string>(),
            capabilities = new
            {
                supportsSynchronousQueries = true,
                supportsAsynchronousQueries = false,
                supportsOperationStatus = false,
                supportsRclUi = true
            },
            http = (object?)null,
            mcp = (object?)null
        };

        using var response = await host.Client.PutAsJsonAsync(
            "/api/memory-providers/provider.unsafe-ui",
            request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Profile_route_preserves_existing_read_only_ui_configuration()
    {
        await using var host = await ApiTestHost.CreateAsync(
            jwtEnabled: false,
            useInMemoryDatabase: true);
        const string providerId = "provider.ui-preserved";
        await SeedProfileAsync(host, CreateUiProfile(providerId));

        using var response = await host.Client.PutAsJsonAsync(
            $"/api/memory-providers/{providerId}",
            CreateMockProfileRequest());

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var capabilities = body.RootElement.GetProperty("capabilities");
        Assert.True(capabilities.GetProperty("supportsRclUi").GetBoolean());
        Assert.True(capabilities.GetProperty("supportsIframeUi").GetBoolean());

        await using var scope = host.App.Services.CreateAsyncScope();
        var profiles = await scope.ServiceProvider
            .GetRequiredService<IMemoryProviderProfileStore>()
            .ListAsync();
        var saved = Assert.Single(profiles, profile => profile.InstanceId.Value == providerId);
        Assert.Contains(
            saved.Manifest.UiSurfaces,
            surface => surface.CapabilityId == MemoryCapabilityIds.UiRcl &&
                       surface.ComponentKey == MemoryProviderUiSurfaceKeys.MockProviderPanelComponent);
        Assert.Contains(
            saved.Manifest.UiSurfaces,
            surface => surface.CapabilityId == MemoryCapabilityIds.UiIframe &&
                       surface.UrlSettingKey == MemoryProviderUiSurfaceKeys.ProviderVendorUiUrlExtension);
        Assert.Equal(
            "https://memory.example.test/provider-ui",
            saved.Manifest.Extensions.Values[MemoryProviderUiSurfaceKeys.ProviderVendorUiUrlExtension].GetString());
    }

    [Theory]
    [InlineData("Mock", true, false)]
    [InlineData("Mock", false, true)]
    [InlineData("Http", false, false)]
    [InlineData("Http", true, true)]
    [InlineData("NativeRemote", false, false)]
    [InlineData("NativeRemote", true, true)]
    [InlineData("Mcp", false, false)]
    [InlineData("Mcp", true, true)]
    public async Task Profile_route_rejects_transport_blocks_that_do_not_match_driver(
        string driverKind,
        bool includeHttp,
        bool includeMcp)
    {
        await using var host = await ApiTestHost.CreateAsync(
            jwtEnabled: false,
            useInMemoryDatabase: true);

        using var response = await host.Client.PutAsJsonAsync(
            "/api/memory-providers/provider.invalid-transport",
            CreateProfileRequest(
                driverKind,
                "DenyImplicitFallback",
                includeHttp,
                includeMcp));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var profiles = await host.Client.GetFromJsonAsync<JsonElement>("/api/memory-providers");
        Assert.DoesNotContain(
            profiles.EnumerateArray(),
            provider => provider.GetProperty("providerId").GetString() == "provider.invalid-transport");
    }

    [Fact]
    public async Task Profile_route_rejects_unknown_request_enum_values()
    {
        await using var host = await ApiTestHost.CreateAsync(
            jwtEnabled: false,
            useInMemoryDatabase: true);

        using var driverResponse = await host.Client.PutAsJsonAsync(
            "/api/memory-providers/provider.invalid-driver",
            CreateProfileRequest(
                driverKind: 4,
                fallbackBehavior: "DenyImplicitFallback",
                includeHttp: false,
                includeMcp: false));
        using var fallbackResponse = await host.Client.PutAsJsonAsync(
            "/api/memory-providers/provider.invalid-fallback",
            CreateProfileRequest(
                driverKind: "Mock",
                fallbackBehavior: 2,
                includeHttp: false,
                includeMcp: false));

        Assert.Equal(HttpStatusCode.BadRequest, driverResponse.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, fallbackResponse.StatusCode);
    }

    [Fact]
    public async Task Profile_route_rejects_async_queries_without_operation_status()
    {
        await using var host = await ApiTestHost.CreateAsync(
            jwtEnabled: false,
            useInMemoryDatabase: true);

        using var response = await host.Client.PutAsJsonAsync(
            "/api/memory-providers/provider.invalid-async",
            CreateProfileRequest(
                driverKind: "Mcp",
                fallbackBehavior: "DenyImplicitFallback",
                includeHttp: false,
                includeMcp: true,
                supportsAsynchronousQueries: true,
                supportsOperationStatus: false));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Query_and_status_routes_use_authenticated_subject_and_enforce_operation_ownership()
    {
        var handler = new RecordingMemoryOperationHandler();
        await using var host = await ApiTestHost.CreateAsync(
            jwtEnabled: true,
            configureServices: services =>
                services.Replace(ServiceDescriptor.Singleton<IMemoryOperationHandler>(handler)),
            useInMemoryDatabase: true);
        SetBearerToken(host, "memory-api-owner");

        using var queryResponse = await host.Client.PostAsJsonAsync(
            "/api/memory-providers/provider.test/queries",
            new
            {
                query = "Find the current delivery context.",
                mode = "Asynchronous"
            });
        Assert.Equal(HttpStatusCode.Accepted, queryResponse.StatusCode);
        Assert.NotNull(handler.QueryRequest);
        Assert.Equal(MemoryOperationCallerKind.ApiEndpoint, handler.QueryRequest.Caller.Kind);
        Assert.Equal("memory-api-owner", handler.QueryRequest.Caller.Requester.RequesterId);
        Assert.Null(handler.QueryRequest.Caller.Requester.SessionId);
        Assert.Equal(
            MemoryCapabilityIds.ContextQueryAsync,
            handler.QueryRequest.SelectionPolicy.RequiredCapability);
        using var queryBody = JsonDocument.Parse(await queryResponse.Content.ReadAsStringAsync());
        var operationId = queryBody.RootElement
            .GetProperty("acceptedOperation")
            .GetProperty("operationId")
            .GetGuid();

        using var ownerStatusResponse = await host.Client.GetAsync(
            $"/api/memory-providers/operations/{operationId:D}");
        Assert.Equal(HttpStatusCode.OK, ownerStatusResponse.StatusCode);
        Assert.Equal("memory-api-owner", handler.StatusRequest?.Caller.Requester.RequesterId);

        SetBearerToken(host, "memory-api-other");
        using var otherStatusResponse = await host.Client.GetAsync(
            $"/api/memory-providers/operations/{operationId:D}");
        Assert.Equal(HttpStatusCode.Forbidden, otherStatusResponse.StatusCode);
    }

    [Fact]
    public async Task Memory_provider_routes_enforce_specific_api_scopes()
    {
        await using var host = await ApiTestHost.CreateAsync(
            jwtEnabled: true,
            useInMemoryDatabase: true);
        SetBearerToken(
            host,
            "memory-api-reader",
            ApiAccessScopeNames.ReadMemoryProviders);

        using var listResponse = await host.Client.GetAsync("/api/memory-providers");
        using var writeResponse = await host.Client.PutAsJsonAsync(
            "/api/memory-providers/provider.read-only",
            CreateMockProfileRequest());
        using var queryResponse = await host.Client.PostAsJsonAsync(
            "/api/memory-providers/provider.read-only/queries",
            new
            {
                query = "This request must not reach the provider.",
                mode = "Synchronous"
            });

        Assert.Equal(HttpStatusCode.OK, listResponse.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, writeResponse.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, queryResponse.StatusCode);
    }

    private static object CreateMockProfileRequest() =>
        CreateProfileRequest(
            "Mock",
            "DenyImplicitFallback",
            includeHttp: false,
            includeMcp: false);

    private static object CreateProfileRequest(
        object driverKind,
        object fallbackBehavior,
        bool includeHttp,
        bool includeMcp,
        bool supportsAsynchronousQueries = false,
        bool supportsOperationStatus = false) =>
        new
        {
            displayName = "API test memory",
            driverKind,
            isEnabled = true,
            fallbackBehavior,
            providerKind = "memory.mock",
            selectionTags = new[] { "api-test" },
            capabilities = new
            {
                supportsSynchronousQueries = true,
                supportsAsynchronousQueries,
                supportsOperationStatus
            },
            http = includeHttp ? CreateHttpTransport() : null,
            mcp = includeMcp ? CreateMcpTransport() : null
        };

    private static object CreateHttpTransport() =>
        new
        {
            baseUrl = "https://memory.example.test",
            queryPath = "/query",
            healthPath = "/health",
            apiKeyEnvironmentVariable = "MEMORY_API_KEY",
            authHeaderName = "Authorization",
            authScheme = "Bearer",
            timeoutMilliseconds = 30_000,
            maxRetryAttempts = 0
        };

    private static object CreateMcpTransport() =>
        new
        {
            descriptorKind = "remote-http",
            serverKey = "memory-test",
            displayName = "Memory test MCP",
            description = "Test MCP provider",
            remoteEndpoint = "https://memory.example.test/mcp",
            authHeaderName = "Authorization",
            authHeaderEnvironmentVariable = "MEMORY_MCP_API_KEY",
            contextQueryTool = "memory.query",
            operationStatusTool = "memory.status"
        };

    private static async Task SeedProfileAsync(ApiTestHost host, MemoryProviderProfile profile)
    {
        await using var scope = host.App.Services.CreateAsyncScope();
        await scope.ServiceProvider
            .GetRequiredService<IMemoryProviderProfileStore>()
            .UpsertAsync(profile, DateTimeOffset.UtcNow);
    }

    private static MemoryProviderProfile CreateUiProfile(string providerId) =>
        new(
            MemoryProviderInstanceId.Parse(providerId),
            "UI-enabled memory provider",
            MemoryProviderDriverKind.Mock,
            IsEnabled: true,
            MemoryProviderHealthState.Healthy,
            MemoryProviderWorkspaceScope.AllWorkspaces,
            SelectionTags: ["ui"],
            MemoryProviderProfilePolicy.Default,
            new MemoryProviderManifest(
                MemoryProviderKind.Parse("memory.mock"),
                MemoryProtocolVersion.Current,
                [
                    new MemoryCapabilityDescriptor(
                        MemoryCapabilityIds.ContextQuerySync,
                        Version: "1",
                        Supported: true),
                    new MemoryCapabilityDescriptor(
                        MemoryCapabilityIds.UiRcl,
                        Version: "1",
                        Supported: true),
                    new MemoryCapabilityDescriptor(
                        MemoryCapabilityIds.UiIframe,
                        Version: "1",
                        Supported: true)
                ],
                MemoryProviderInteractionSupport.SyncQueryOnly,
                [
                    new MemoryProviderUiSurface(
                        MemoryProviderUiSurfaceKind.RazorComponentLibrary,
                        "Provider panel",
                        MemoryProviderUiSurfaceKeys.MockProviderPanelComponent,
                        UrlSettingKey: null,
                        MemoryCapabilityIds.UiRcl),
                    new MemoryProviderUiSurface(
                        MemoryProviderUiSurfaceKind.Iframe,
                        "Provider console",
                        ComponentKey: null,
                        MemoryProviderUiSurfaceKeys.ProviderVendorUiUrlExtension,
                        MemoryCapabilityIds.UiIframe)
                ],
                MemoryProviderLimits.Default,
                MemoryExtensionData.From((
                    MemoryProviderUiSurfaceKeys.ProviderVendorUiUrlExtension,
                    JsonSerializer.SerializeToElement("https://memory.example.test/provider-ui")))));

    private static void SetBearerToken(
        ApiTestHost host,
        string subject,
        params string[] scopes)
    {
        var request = new ApiTokenIssueRequest
        {
            Subject = subject,
            DisplayName = subject
        };
        if (scopes.Length > 0)
        {
            request.Scopes = scopes.ToList();
        }

        var token = host.App.Services
            .GetRequiredService<IApiTokenService>()
            .IssueToken(request);
        host.Client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", token.Token);
    }

    private sealed class RecordingMemoryOperationHandler : IMemoryOperationHandler
    {
        private static readonly MemoryProviderProfile Provider = CreateProvider();
        private MemoryLedgerRequester? owner;
        private MemoryOperationRecord? operation;

        public MemoryOperationHandlerRequest<MemoryContextQueryRequest>? QueryRequest { get; private set; }

        public MemoryOperationHandlerRequest<MemoryOperationStatusRequest>? StatusRequest { get; private set; }

        public Task<MemoryOperationHandlerResult<MemoryContextPack>> ExecuteQueryAsync(
            MemoryOperationHandlerRequest<MemoryContextQueryRequest> request,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            QueryRequest = request;
            owner = request.Caller.Requester;
            var now = DateTimeOffset.UtcNow;
            var operationId = MemoryOperationId.New();
            operation = MemoryOperationRecord.Create(
                MemoryOperationRecordId.New(),
                operationId,
                Provider.InstanceId,
                request.SelectionPolicy.RequiredCapability,
                MemoryOperationKind.ContextQuery,
                owner,
                request.CorrelationId,
                request.CausationId,
                [],
                request.Retention,
                now);
            var accepted = new MemoryOperationAccepted(
                operationId,
                $"/api/memory-providers/operations/{operationId.Value:D}",
                now.AddMinutes(10),
                TimeSpan.FromSeconds(1),
                CallbackAvailable: false);
            var result = new MemoryOperationHandlerResult<MemoryContextPack>(
                MemoryOperationHandlerStatus.Accepted,
                MemoryProviderSelectionResult.Selected(
                    Provider,
                    MemoryProviderSelectionReason.ExplicitProvider,
                    request.SelectionPolicy.RequiredCapability),
                operation,
                Output: null,
                accepted,
                FeedbackHandle: null,
                DriverDispatchAttempted: true,
                "Accepted by the integration test provider.");
            return Task.FromResult(result);
        }

        public Task<MemoryOperationHandlerResult<MemoryOperationRecord>> GetStatusAsync(
            MemoryOperationHandlerRequest<MemoryOperationStatusRequest> request,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            StatusRequest = request;
            if (operation is null ||
                owner is null ||
                request.Payload.OperationId != operation.OperationId)
            {
                return Task.FromResult(NotFound(request));
            }

            if (request.Caller.Requester != owner)
            {
                return Task.FromResult(AccessDenied(request));
            }

            var current = operation with
            {
                Status = MemoryLedgerStatus.Running,
                UpdatedAtUtc = DateTimeOffset.UtcNow,
                StatusReason = "running"
            };
            var result = new MemoryOperationHandlerResult<MemoryOperationRecord>(
                MemoryOperationHandlerStatus.Completed,
                MemoryProviderSelectionResult.Selected(
                    Provider,
                    MemoryProviderSelectionReason.ExplicitProvider,
                    MemoryCapabilityIds.OperationStatus),
                current,
                current,
                AcceptedOperation: null,
                FeedbackHandle: null,
                DriverDispatchAttempted: false,
                "Operation status returned.");
            return Task.FromResult(result);
        }

        public Task<MemoryOperationHandlerResult<MemorySourceCaptureOperationResult>> CaptureSourceForIngestionAsync(
            MemoryOperationHandlerRequest<MemorySourceCaptureOperationRequest> request,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<MemoryOperationHandlerResult<MemoryFeedbackRecord>> SubmitFeedbackAsync(
            MemoryOperationHandlerRequest<MemoryFeedbackOperationRequest> request,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<MemoryOperationHandlerResult<MemoryOperationRecord>> CancelAsync(
            MemoryOperationHandlerRequest<MemoryOperationCancellationRequest> request,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<MemoryOperationHandlerResult<MemoryEventOutboxRecord>> AcknowledgeEventAsync(
            MemoryOperationHandlerRequest<MemoryEventAcknowledgeRequest> request,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        private static MemoryOperationHandlerResult<MemoryOperationRecord> NotFound(
            MemoryOperationHandlerRequest<MemoryOperationStatusRequest> request) =>
            new(
                MemoryOperationHandlerStatus.NotFound,
                MemoryProviderSelectionResult.Rejected(
                    MemoryProviderSelectionStatus.ProviderNotFound,
                    MemoryProviderSelectionReason.None,
                    request.SelectionPolicy.RequiredCapability,
                    "Operation was not found.",
                    []),
                OperationRecord: null,
                Output: null,
                AcceptedOperation: null,
                FeedbackHandle: null,
                DriverDispatchAttempted: false,
                "Operation was not found.");

        private static MemoryOperationHandlerResult<MemoryOperationRecord> AccessDenied(
            MemoryOperationHandlerRequest<MemoryOperationStatusRequest> request) =>
            new(
                MemoryOperationHandlerStatus.AccessDenied,
                MemoryProviderSelectionResult.Rejected(
                    MemoryProviderSelectionStatus.ProviderDenied,
                    MemoryProviderSelectionReason.None,
                    request.SelectionPolicy.RequiredCapability,
                    "Operation ownership did not match.",
                    []),
                OperationRecord: null,
                Output: null,
                AcceptedOperation: null,
                FeedbackHandle: null,
                DriverDispatchAttempted: false,
                "Operation ownership did not match.");

        private static MemoryProviderProfile CreateProvider() =>
            new(
                MemoryProviderInstanceId.Parse("provider.test"),
                "Test memory provider",
                MemoryProviderDriverKind.Mock,
                IsEnabled: true,
                MemoryProviderHealthState.Healthy,
                MemoryProviderWorkspaceScope.AllWorkspaces,
                SelectionTags: [],
                MemoryProviderProfilePolicy.Default,
                new MemoryProviderManifest(
                    MemoryProviderKind.Parse("memory.mock"),
                    MemoryProtocolVersion.Current,
                    [
                        new MemoryCapabilityDescriptor(MemoryCapabilityIds.ContextQueryAsync, "1", Supported: true),
                        new MemoryCapabilityDescriptor(MemoryCapabilityIds.OperationStatus, "1", Supported: true)
                    ],
                    new MemoryProviderInteractionSupport(
                        SupportsSynchronousQueries: false,
                        SupportsAsynchronousOperations: true,
                        SupportsSourceRequests: false,
                        SupportsFeedback: false,
                        SupportsProviderEvents: false),
                    UiSurfaces: [],
                    MemoryProviderLimits.Default,
                    MemoryExtensionData.Empty));
    }
}
