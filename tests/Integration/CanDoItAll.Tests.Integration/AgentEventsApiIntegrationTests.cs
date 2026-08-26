using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Reflection;
using System.Text.Json;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Infrastructure.ControlPlane;
using CanDoItAll.Modules.AgentFramework;
using CanDoItAll.Modules.AgentFramework.Hosting;
using IProviderRuntimeAdministrationService = CanDoItAll.Modules.AgentFramework.ProviderManagement.IProviderRuntimeAdministrationService;
using CanDoItAll.Modules.Workspace.ApiAccess;
using CanDoItAll.SharedKernel.Streaming;
using CanDoItAll.Web.Api;
using CanDoItAll.Web.Api.Streaming;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace CanDoItAll.Tests.Integration.AgentFramework;

public sealed class AgentEventsApiIntegrationTests
{
    [Fact]
    public async Task Unknown_agent_operation_stream_returns_not_found_before_starting_sse()
    {
        await using var host = await ApiTestHost.CreateAsync(
            jwtEnabled: false,
            useInMemoryDatabase: true);

        using var response = await host.Client.GetAsync(
            $"/api/agents/execution-operations/{Guid.NewGuid():D}/events/stream",
            HttpCompletionOption.ResponseHeadersRead);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("application/json", response.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task Invalid_agent_operation_cursor_returns_structured_bad_request()
    {
        await using var host = await ApiTestHost.CreateAsync(
            jwtEnabled: false,
            useInMemoryDatabase: true);

        using var response = await host.Client.GetAsync(
            $"/api/agents/execution-operations/{Guid.NewGuid():D}/events/stream?after=invalid");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        using var payload = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal(
            ServerSentEventResponseWriter.InvalidCursorCode,
            payload.RootElement.GetProperty("errors")[0].GetProperty("code").GetString());
    }

    [Fact]
    public async Task Image_attachment_upload_uses_bounded_workspace_staging_service()
    {
        await using var host = await ApiTestHost.CreateAsync(
            jwtEnabled: false,
            useInMemoryDatabase: true);
        using var request = new MultipartFormDataContent();
        using var image = new ByteArrayContent([0x89, 0x50, 0x4E, 0x47]);
        image.Headers.ContentType = new MediaTypeHeaderValue("image/png");
        request.Add(image, "file", "evidence.png");

        using var response = await host.Client.PostAsync(
            "/api/agents/attachments/images",
            request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<AgentChatAttachmentStagingResult>();
        Assert.NotNull(result);
        Assert.Equal("image/png", result.ContentType);
        Assert.Equal(4, result.SizeBytes);
        Assert.StartsWith("artifacts/chat-attachments/", result.RelativePath, StringComparison.Ordinal);
        using var scope = host.App.Services.CreateScope();
        var pathResolver = scope.ServiceProvider
            .GetRequiredService<IWorkspacePathResolutionService>();
        var resolved = pathResolver.ResolveFilePath(
            result.RelativePath,
            allowMissing: false);
        Assert.True(resolved.IsWorkspacePath);
        Assert.True(File.Exists(resolved.FullPath));
    }

    [Fact]
    public async Task OpenApi_exposes_agent_provider_and_run_sse_contracts()
    {
        await using var host = await ApiTestHost.CreateAsync(
            jwtEnabled: false,
            useInMemoryDatabase: true);

        using var openApi = JsonDocument.Parse(
            await host.Client.GetStringAsync("/openapi/v1.json"));
        var paths = openApi.RootElement.GetProperty("paths");

        AssertSsePost(paths, "/api/agents/{agentId}/chat/stream");
        AssertSsePost(paths, "/api/agents/execution-runs/stream");
        AssertSsePost(paths, "/api/agents/{agentId}/execution-runs/stream");
        AssertSsePost(
            paths,
            "/api/agents/execution-runs/{executionRunId}/pending-approvals/stream");
        AssertSsePost(
            paths,
            "/api/agents/providers/{providerId}/chat-completions/stream");
        Assert.True(paths.TryGetProperty(
            "/api/agents/execution-operations/{operationId}/events/stream",
            out _));
        Assert.True(paths.TryGetProperty(
            "/api/agents/attachments/images",
            out _));
    }

    [Fact]
    public async Task Unknown_provider_stream_returns_not_found_before_starting_sse()
    {
        await using var host = await ApiTestHost.CreateAsync(
            jwtEnabled: false,
            useInMemoryDatabase: true);

        using var response = await host.Client.PostAsJsonAsync(
            $"/api/agents/providers/{Guid.NewGuid():D}/chat-completions/stream",
            new
            {
                model = "test-model",
                systemPrompt = string.Empty,
                messages = Array.Empty<object>(),
                prompt = "Test the provider."
            });

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("application/json", response.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task Provider_stream_cancels_and_observes_dispatch_when_running_frame_fails()
    {
        var provider = CreateProvider();
        var providerAdministration =
            DispatchProxy.Create<IProviderRuntimeAdministrationService, ProviderAdministrationProxy>();
        using var providerAdministrationProxy =
            (ProviderAdministrationProxy)(object)providerAdministration;
        var context = new DefaultHttpContext();
        await using var body = new ThrowOnRunningFrameStream();
        context.Response.Body = body;

        await Assert.ThrowsAsync<IOException>(() =>
            AgentProviderEventsApi.StreamChatCompletionAsync(
                provider.Id,
                new ProviderChatCompletionApiRequest(
                    provider.DefaultModel,
                    string.Empty,
                    [],
                    "Test cancellation ownership."),
                context,
                providerAdministration,
                new StaticProviderSource(provider),
                Options.Create(new ApiAccessOptions()),
                NullLogger<ProviderChatCompletionApiRequest>.Instance));

        await providerAdministrationProxy.Cancelled.Task.WaitAsync(TimeSpan.FromSeconds(1));
        Assert.True(providerAdministrationProxy.Completion.Task.IsCanceled);
    }

    [Fact]
    public async Task Provider_heartbeat_wait_returns_success_when_completion_races_the_tick()
    {
        for (var attempt = 0; attempt < 25; attempt++)
        {
            var context = new DefaultHttpContext();
            await using var body = new MemoryStream();
            context.Response.Body = body;
            var completion = new TaskCompletionSource<string>(
                TaskCreationOptions.RunContinuationsAsynchronously);
            var wait = AgentProviderEventsApi.AwaitWithHeartbeatsAsync(
                context,
                completion.Task,
                TimeSpan.FromMilliseconds(1));

            await Task.Delay(TimeSpan.FromMilliseconds(1));
            completion.TrySetResult("completed");

            Assert.Equal("completed", await wait);
        }
    }

    [Fact]
    public async Task Duplicate_client_operation_id_returns_conflict_before_sse_starts()
    {
        await using var host = await ApiTestHost.CreateAsync(
            jwtEnabled: false,
            useInMemoryDatabase: true);
        using var scope = host.App.Services.CreateScope();
        var profile = scope.ServiceProvider
            .GetRequiredService<IDatabaseProfileRuntimeAccessor>()
            .ResolveCurrentProfile();
        var generation = scope.ServiceProvider
            .GetRequiredService<IAgentExecutionProfileGenerationSource>()
            .GetGeneration();
        var agentId = Guid.NewGuid();
        var chatSessionId = Guid.NewGuid();
        var operationId = AgentExecutionOperationId.New();
        var streamId = new AgentExecutionActivityStreamId(
            profile.Profile.Id,
            WorkspaceScopeDescriptor.Organization(profile.Profile.Id.ToString("N")),
            generation,
            operationId);
        var coordinator = scope.ServiceProvider
            .GetRequiredService<IAgentExecutionActivityCoordinator>();
        var admission = Assert.IsType<AgentExecutionActivityAdmitted>(
            coordinator.AdmitOperation(
                streamId,
                agentId,
                chatSessionId,
                "Test operation admitted."));
        using var operation = admission.Operation;

        using var response = await host.Client.PostAsJsonAsync(
            "/api/agents/execution-runs/stream",
            new
            {
                agentId,
                chatSessionId,
                prompt = "This command must not start.",
                activityOperationId = operationId.Value
            });

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal(
            operationId.Value.ToString("D"),
            Assert.Single(response.Headers.GetValues(
                AgentApiHeaderNames.ActivityOperationId)));
        var payload = await response.Content.ReadFromJsonAsync<ApiErrorResponse>();
        Assert.NotNull(payload);
        Assert.Equal(
            AgentActivityApiResults.DuplicateOperationCode,
            Assert.Single(payload.Errors).Code);
        Assert.False(string.IsNullOrWhiteSpace(payload.CorrelationId));
        Assert.Equal(agentId, payload.AgentId);
        Assert.Null(payload.ExecutionRunId);
        Assert.Equal(chatSessionId, payload.ChatSessionId);
        Assert.Null(payload.ProviderFailureCategory);
    }

    [Fact]
    public async Task Exhausted_activity_capacity_returns_correlated_service_unavailable_from_http_command()
    {
        await using var host = await ApiTestHost.CreateAsync(
            jwtEnabled: false,
            configureServices: services => services.AddSingleton(
                new PartitionedSequencedStream<
                    AgentExecutionActivityStreamId,
                    AgentExecutionActivity>(
                    new PartitionedSequencedStreamPolicy(
                        maxPartitions: 1,
                        maxEventsPerPartition: 16,
                        maxTerminalPartitions: 1,
                        terminalRetention: TimeSpan.FromMinutes(5),
                        maxTombstones: 1,
                        tombstoneRetention: TimeSpan.FromMinutes(5)),
                    TimeProvider.System)),
            useInMemoryDatabase: true);
        using var scope = host.App.Services.CreateScope();
        var profile = scope.ServiceProvider
            .GetRequiredService<IDatabaseProfileRuntimeAccessor>()
            .ResolveCurrentProfile();
        var generation = scope.ServiceProvider
            .GetRequiredService<IAgentExecutionProfileGenerationSource>()
            .GetGeneration();
        var coordinator = scope.ServiceProvider
            .GetRequiredService<IAgentExecutionActivityCoordinator>();
        var occupiedStreamId = new AgentExecutionActivityStreamId(
            profile.Profile.Id,
            WorkspaceScopeDescriptor.Organization(profile.Profile.Id.ToString("N")),
            generation,
            AgentExecutionOperationId.New());
        var occupiedAdmission = Assert.IsType<AgentExecutionActivityAdmitted>(
            coordinator.AdmitOperation(
                occupiedStreamId,
                Guid.NewGuid(),
                chatSessionId: null,
                "Keep the only activity partition occupied."));
        using var occupiedOperation = occupiedAdmission.Operation;
        var agentId = Guid.NewGuid();
        var chatSessionId = Guid.NewGuid();
        var rejectedOperationId = AgentExecutionOperationId.New();

        using var response = await host.Client.PostAsJsonAsync(
            "/api/agents/execution-runs",
            new
            {
                agentId,
                chatSessionId,
                prompt = "This command must be rejected at admission.",
                activityOperationId = rejectedOperationId.Value
            });

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        Assert.Equal(
            rejectedOperationId.Value.ToString("D"),
            Assert.Single(response.Headers.GetValues(
                AgentApiHeaderNames.ActivityOperationId)));
        var payload = await response.Content.ReadFromJsonAsync<ApiErrorResponse>();
        Assert.NotNull(payload);
        Assert.Equal(
            AgentActivityApiResults.CapacityExhaustedCode,
            Assert.Single(payload.Errors).Code);
        Assert.False(string.IsNullOrWhiteSpace(payload.CorrelationId));
        Assert.Equal(agentId, payload.AgentId);
        Assert.Null(payload.ExecutionRunId);
        Assert.Equal(chatSessionId, payload.ChatSessionId);
        Assert.Null(payload.ProviderFailureCategory);
    }

    [Fact]
    public async Task Invalid_agent_stream_command_returns_structured_bad_request_before_admission()
    {
        await using var host = await ApiTestHost.CreateAsync(
            jwtEnabled: false,
            useInMemoryDatabase: true);

        using var response = await host.Client.PostAsJsonAsync(
            "/api/agents/execution-runs/stream",
            new
            {
                agentId = Guid.Empty,
                prompt = " "
            });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("application/json", response.Content.Headers.ContentType?.MediaType);
        Assert.False(response.Headers.Contains(AgentApiHeaderNames.ActivityOperationId));
        using var payload = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal(
            "agents.request-invalid",
            payload.RootElement.GetProperty("errors")[0].GetProperty("code").GetString());
    }

    [Fact]
    public async Task Same_request_agent_stream_emits_ordered_canonical_activity_and_idless_completion()
    {
        await using var host = await CreateProcessMockHostAsync();
        var agentId = await ResolveProcessMockAgentIdAsync(
            host,
            ProcessMockAgentRoleKeys.ProductOwner);
        var operationId = Guid.NewGuid();

        using var response = await host.Client.PostAsJsonAsync(
            $"/api/agents/{agentId:D}/execution-runs/stream",
            new
            {
                prompt = "Prepare a deterministic API streaming test scope.",
                activityOperationId = operationId
            });
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(
            ServerSentEventResponseWriter.ContentType,
            response.Content.Headers.ContentType?.MediaType);
        Assert.Equal(
            operationId.ToString("D"),
            Assert.Single(response.Headers.GetValues(
                AgentApiHeaderNames.ActivityOperationId)));
        Assert.Contains(
            $"event: {AgentServerEventNames.ActivityCompleted}",
            body,
            StringComparison.Ordinal);
        var frames = SplitFrames(body);
        var canonicalIds = frames
            .Where(frame => frame.Contains("id: ", StringComparison.Ordinal))
            .Select(ReadFrameId)
            .ToArray();
        Assert.NotEmpty(canonicalIds);
        Assert.Equal(
            canonicalIds.Order().ToArray(),
            canonicalIds);
        var completed = Assert.Single(
            frames,
            frame => frame.Contains(
                $"event: {AgentServerEventNames.CommandCompleted}",
                StringComparison.Ordinal));
        Assert.DoesNotContain("id: ", completed, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Same_request_agent_stream_reports_runtime_failure_in_band_without_advancing_cursor()
    {
        await using var host = await CreateProcessMockHostAsync();
        var agentId = await ResolveProcessMockAgentIdAsync(
            host,
            ProcessMockAgentRoleKeys.ProductOwner);
        await using (var scope = host.App.Services.CreateAsyncScope())
        {
            var workspaceFactory = scope.ServiceProvider
                .GetRequiredService<ICanDoItAllAgentWorkspaceFactory>();
            var workspaceService = workspaceFactory.GetOrganizationWorkspaceService();
            var editor = await workspaceService.GetAgentEditorAsync(agentId);
            editor.Tags = [ProcessMockAgentCatalog.AgentTag];
            await workspaceService.SaveAgentAsync(editor);
        }

        using var response = await host.Client.PostAsJsonAsync(
            $"/api/agents/{agentId:D}/execution-runs/stream",
            new
            {
                prompt = "Trigger the deterministic invalid-role failure."
            });
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains(
            $"event: {AgentServerEventNames.ActivityFailed}",
            body,
            StringComparison.Ordinal);
        var failed = Assert.Single(
            SplitFrames(body),
            frame => frame.Contains(
                $"event: {AgentServerEventNames.CommandFailed}",
                StringComparison.Ordinal));
        Assert.DoesNotContain("id: ", failed, StringComparison.Ordinal);
        Assert.Contains(
            $"\"code\":\"{ApiEndpointResults.RunFailedCode}\"",
            failed,
            StringComparison.Ordinal);
    }

    private static void AssertSsePost(JsonElement paths, string path)
    {
        var operation = paths
            .GetProperty(path)
            .GetProperty("post");
        var content = operation
            .GetProperty("responses")
            .GetProperty("200")
            .GetProperty("content");
        Assert.True(content.TryGetProperty("text/event-stream", out _));
    }

    private static Task<ApiTestHost> CreateProcessMockHostAsync()
    {
        return ApiTestHost.CreateAsync(
            jwtEnabled: false,
            services => services.PostConfigure<ProcessMockAgentOptions>(
                options => options.Enabled = true),
            useInMemoryDatabase: true);
    }

    private static async Task<Guid> ResolveProcessMockAgentIdAsync(
        ApiTestHost host,
        string roleKey)
    {
        await using var scope = host.App.Services.CreateAsyncScope();
        var catalog = await scope.ServiceProvider
            .GetRequiredService<ProcessMockAgentCatalogService>()
            .EnsureCatalogAsync();
        Assert.NotNull(catalog);
        return catalog.AgentIdsByRoleKey[roleKey];
    }

    private static string[] SplitFrames(string body)
    {
        return body.Split(
            "\n\n",
            StringSplitOptions.RemoveEmptyEntries |
            StringSplitOptions.TrimEntries);
    }

    private static long ReadFrameId(string frame)
    {
        var idLine = frame
            .Split('\n')
            .Single(line => line.StartsWith("id: ", StringComparison.Ordinal));
        return long.Parse(
            idLine.AsSpan("id: ".Length),
            System.Globalization.CultureInfo.InvariantCulture);
    }

    private static ProviderProfile CreateProvider()
    {
        return new ProviderProfile(
            Guid.NewGuid(),
            "API stream test provider",
            ProviderKind.OpenAi,
            "https://example.invalid",
            "API_STREAM_TEST_KEY",
            "test-model",
            ProviderTransportKind.Responses,
            true,
            true,
            true,
            true,
            false,
            "{}",
            string.Empty,
            "Healthy",
            DateTimeOffset.UnixEpoch,
            ["test-model"]);
    }

    private sealed class StaticProviderSource(ProviderProfile provider) :
        IProviderRuntimeProfileSource
    {
        public Task<IReadOnlyList<ProviderProfile>> ListProvidersAsync(
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult<IReadOnlyList<ProviderProfile>>([provider]);
        }

        public Task<ProviderProfile?> GetProviderAsync(
            Guid providerId,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult<ProviderProfile?>(
                provider.Id == providerId
                    ? provider
                    : null);
        }
    }

    private class ProviderAdministrationProxy : DispatchProxy, IDisposable
    {
        private CancellationTokenRegistration cancellationRegistration;

        public TaskCompletionSource<ProviderTestChatResult> Completion { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public TaskCompletionSource Cancelled { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        protected override object? Invoke(
            MethodInfo? targetMethod,
            object?[]? args)
        {
            if (targetMethod?.Name !=
                nameof(IProviderRuntimeAdministrationService.RunProviderTestChatAsync))
            {
                throw new NotSupportedException(
                    $"Unexpected provider-administration call '{targetMethod?.Name}'.");
            }

            var cancellationToken = (CancellationToken)args![2]!;
            cancellationRegistration = cancellationToken.Register(() =>
            {
                Cancelled.TrySetResult();
                Completion.TrySetCanceled(cancellationToken);
            });
            return Completion.Task;
        }

        public void Dispose()
        {
            cancellationRegistration.Dispose();
        }
    }

    private sealed class ThrowOnRunningFrameStream : MemoryStream
    {
        public override ValueTask WriteAsync(
            ReadOnlyMemory<byte> buffer,
            CancellationToken cancellationToken = default)
        {
            return System.Text.Encoding.UTF8
                .GetString(buffer.Span)
                .Contains(
                    $"event: {AgentServerEventNames.ProviderRunning}",
                    StringComparison.Ordinal)
                ? ValueTask.FromException(
                    new IOException("The test client disconnected while the running frame was flushed."))
                : base.WriteAsync(buffer, cancellationToken);
        }
    }
}
