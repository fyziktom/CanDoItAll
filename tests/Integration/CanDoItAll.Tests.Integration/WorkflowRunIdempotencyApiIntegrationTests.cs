using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.Workflows.Abstractions;
using CanDoItAll.Modules.Security;
using CanDoItAll.Web.Api;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;

namespace CanDoItAll.Tests.Integration.AgentFramework;

public sealed class WorkflowRunIdempotencyApiIntegrationTests
{
    [Fact]
    public async Task Public_start_routes_replay_canonical_requests_and_reject_changed_fingerprints()
    {
        await using var host = await CreateHostAsync(jwtEnabled: false);
        var workflows = await SeedWorkflowsAsync(host);
        var key = $"workflow-start-{Guid.NewGuid():N}";
        const string firstInput =
            """{"customer":{"region":"EU","tier":"enterprise"},"items":[{"sku":"A","quantity":2}]}""";
        const string reorderedInput =
            """{"items":[{"quantity":2,"sku":"A"}],"customer":{"tier":"enterprise","region":"EU"}}""";

        using var createResponse = await StartAsync(
            host.Client,
            $"/api/workflows/definitions/{workflows.PrimaryV1.Id.Value:D}/runs/start",
            key,
            workflowId: null,
            versionId: workflows.PrimaryV1.VersionId.Value,
            requestedBackend: WorkflowRuntimeBackendKind.InProcess,
            inputJson: firstInput);
        var created = await ReadStartResponseAsync(createResponse);

        using var replayResponse = await StartAsync(
            host.Client,
            "/api/workflows/runs/start",
            key,
            workflows.PrimaryV1.Id.Value,
            workflows.PrimaryV1.VersionId.Value,
            WorkflowRuntimeBackendKind.InProcess,
            reorderedInput);
        var replayed = await ReadStartResponseAsync(replayResponse);

        Assert.Equal(HttpStatusCode.OK, createResponse.StatusCode);
        Assert.Equal(HttpStatusCode.OK, replayResponse.StatusCode);
        Assert.Equal(created.Run.RunId, replayed.Run.RunId);
        Assert.Equal(WorkflowRunState.Completed, created.Run.State);
        Assert.Equal(WorkflowLaunchIdempotencyDisposition.EnforcedNewRun, created.IdempotencyDisposition);
        Assert.Equal(WorkflowLaunchIdempotencyDisposition.ReplayedExistingRun, replayed.IdempotencyDisposition);
        Assert.True(created.Created);
        Assert.False(created.Replayed);
        Assert.False(replayed.Created);
        Assert.True(replayed.Replayed);
        var expectedKeyHash = WorkflowLaunchIdempotencyRequestFactory.CreateKeyHash(
            new WorkflowLaunchIdempotencyKey(key));
        Assert.Equal(expectedKeyHash, created.IdempotencyKeyHash);
        Assert.Equal(expectedKeyHash, replayed.IdempotencyKeyHash);

        var reverseRouteKey = $"workflow-start-reverse-{Guid.NewGuid():N}";
        using var genericCreateResponse = await StartAsync(
            host.Client,
            "/api/workflows/runs/start",
            reverseRouteKey,
            workflows.PrimaryV1.Id.Value,
            workflows.PrimaryV1.VersionId.Value,
            WorkflowRuntimeBackendKind.InProcess,
            firstInput);
        var genericCreated = await ReadStartResponseAsync(genericCreateResponse);
        using var definitionReplayResponse = await StartAsync(
            host.Client,
            $"/api/workflows/definitions/{workflows.PrimaryV1.Id.Value:D}/runs/start",
            reverseRouteKey,
            workflowId: null,
            versionId: workflows.PrimaryV1.VersionId.Value,
            requestedBackend: WorkflowRuntimeBackendKind.InProcess,
            inputJson: reorderedInput);
        var definitionReplayed = await ReadStartResponseAsync(definitionReplayResponse);

        Assert.Equal(HttpStatusCode.OK, genericCreateResponse.StatusCode);
        Assert.Equal(HttpStatusCode.OK, definitionReplayResponse.StatusCode);
        Assert.Equal(genericCreated.Run.RunId, definitionReplayed.Run.RunId);
        Assert.True(genericCreated.Created);
        Assert.True(definitionReplayed.Replayed);

        using var changedWorkflow = await StartAsync(
            host.Client,
            "/api/workflows/runs/start",
            key,
            workflows.Other.Id.Value,
            workflows.Other.VersionId.Value,
            WorkflowRuntimeBackendKind.InProcess,
            firstInput);
        using var changedVersion = await StartAsync(
            host.Client,
            "/api/workflows/runs/start",
            key,
            workflows.PrimaryV2.Id.Value,
            workflows.PrimaryV2.VersionId.Value,
            WorkflowRuntimeBackendKind.InProcess,
            firstInput);
        using var changedBackend = await StartAsync(
            host.Client,
            $"/api/workflows/definitions/{workflows.PrimaryV1.Id.Value:D}/runs/start",
            key,
            workflowId: null,
            versionId: workflows.PrimaryV1.VersionId.Value,
            requestedBackend: WorkflowRuntimeBackendKind.DurableTask,
            inputJson: firstInput);
        using var changedCanonicalInput = await StartAsync(
            host.Client,
            $"/api/workflows/definitions/{workflows.PrimaryV1.Id.Value:D}/runs/start",
            key,
            workflowId: null,
            versionId: workflows.PrimaryV1.VersionId.Value,
            requestedBackend: WorkflowRuntimeBackendKind.InProcess,
            inputJson: """{"customer":{"region":"EU","tier":"standard"},"items":[{"sku":"A","quantity":2}]}""");

        await AssertConflictAsync(changedWorkflow);
        await AssertConflictAsync(changedVersion);
        await AssertConflictAsync(changedBackend);
        await AssertConflictAsync(changedCanonicalInput);

        var concurrentKey = $"workflow-start-concurrent-{Guid.NewGuid():N}";
        var concurrentTasks = Enumerable.Range(0, 8)
            .Select(_ => StartAsync(
                host.Client,
                "/api/workflows/runs/start",
                concurrentKey,
                workflows.Other.Id.Value,
                workflows.Other.VersionId.Value,
                WorkflowRuntimeBackendKind.InProcess,
                firstInput))
            .ToArray();
        var concurrentResponses = await Task.WhenAll(concurrentTasks);
        try
        {
            var concurrentStarts = new List<WorkflowRunStartApiResponse>();
            foreach (var response in concurrentResponses)
            {
                Assert.Equal(HttpStatusCode.OK, response.StatusCode);
                concurrentStarts.Add(await ReadStartResponseAsync(response));
            }

            var concurrentRunId = Assert.Single(
                concurrentStarts.Select(item => item.Run.RunId).Distinct());
            Assert.Single(concurrentStarts, item => item.Created && !item.Replayed);
            Assert.Equal(7, concurrentStarts.Count(item => !item.Created && item.Replayed));

            using var runListResponse = await host.Client.GetAsync(
                $"/api/workflows/runs?workflowId={workflows.Other.Id.Value:D}");
            var runListBody = await runListResponse.Content.ReadAsStringAsync();
            Assert.Equal(HttpStatusCode.OK, runListResponse.StatusCode);
            var persistedRuns = JsonSerializer.Deserialize<IReadOnlyList<WorkflowRunSnapshot>>(
                runListBody,
                JsonOptions)!;
            var persistedRun = Assert.Single(persistedRuns);
            Assert.Equal(concurrentRunId, persistedRun.RunId);
        }
        finally
        {
            foreach (var response in concurrentResponses)
            {
                response.Dispose();
            }
        }

        using var lookupResponse = await host.Client.GetAsync(
            $"/api/workflows/runs/by-idempotency-key/{Uri.EscapeDataString(key)}");
        var lookupBody = await lookupResponse.Content.ReadAsStringAsync();
        var evidence = JsonSerializer.Deserialize<WorkflowLaunchIdempotencyEvidence>(
            lookupBody,
            JsonOptions)!;
        var expectedFingerprint = WorkflowLaunchIdempotencyRequestFactory.CreateFingerprint(
            CreateFingerprintIntent(workflows.PrimaryV1, key),
            firstInput);

        Assert.Equal(HttpStatusCode.OK, lookupResponse.StatusCode);
        Assert.DoesNotContain(key, lookupBody, StringComparison.Ordinal);
        Assert.Equal(expectedKeyHash, evidence.IdempotencyKeyHash);
        Assert.Equal(expectedFingerprint.Value, evidence.RequestFingerprint);
        Assert.Equal(expectedFingerprint.CanonicalInputHash, evidence.CanonicalInputHash);
        Assert.Equal(workflows.PrimaryV1.Id, evidence.WorkflowId);
        Assert.Equal(WorkflowDefinitionSelectionKind.ExactSavedVersion, evidence.SelectionKind);
        Assert.Equal(workflows.PrimaryV1.VersionId, evidence.RequestedVersionId);
        Assert.Equal(workflows.PrimaryV1.VersionId, evidence.ResolvedVersionId);
        Assert.Equal(WorkflowRuntimeBackendKind.InProcess, evidence.ResolvedBackend);
        Assert.Equal(created.Run.RunId, evidence.OriginalRunId);
        Assert.Equal(WorkflowLaunchIdempotencyRecordState.Completed, evidence.ClaimState);
        Assert.Equal(WorkflowRunState.Completed, evidence.RunState);
        Assert.True(evidence.IsTerminal);
        Assert.True(evidence.WasReplayed);
        Assert.Equal(1, evidence.ReplayCount);
        Assert.NotNull(evidence.CompletedAtUtc);
        Assert.NotNull(evidence.LastReplayedAtUtc);

        using var missingResponse = await host.Client.GetAsync(
            $"/api/workflows/runs/by-idempotency-key/missing-{Guid.NewGuid():N}");
        var missingBody = await missingResponse.Content.ReadAsStringAsync();
        Assert.Equal(HttpStatusCode.NotFound, missingResponse.StatusCode);
        Assert.Contains("workflows.idempotency-key-not-found", missingBody, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Idempotency_lookup_inherits_api_authorization()
    {
        await using var host = await CreateHostAsync(jwtEnabled: true);

        using var response = await host.Client.GetAsync(
            "/api/workflows/runs/by-idempotency-key/protected-key");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Openapi_exposes_idempotency_headers_and_typed_success_not_found_and_conflict_responses()
    {
        await using var host = await CreateHostAsync(jwtEnabled: false);

        using var document = JsonDocument.Parse(await host.Client.GetStringAsync("/openapi/v1.json"));
        var paths = document.RootElement.GetProperty("paths");
        var definitionStart = paths
            .GetProperty("/api/workflows/definitions/{workflowId}/runs/start")
            .GetProperty("post");
        var genericStart = paths
            .GetProperty("/api/workflows/runs/start")
            .GetProperty("post");
        var lookup = paths
            .GetProperty("/api/workflows/runs/by-idempotency-key/{key}")
            .GetProperty("get");

        AssertHeaderParameter(definitionStart, "Idempotency-Key");
        AssertHeaderParameter(genericStart, "Idempotency-Key");
        AssertResponseSchema(definitionStart, "200", "WorkflowRunStartApiResponse");
        AssertResponseSchema(definitionStart, "404", "ApiErrorResponse");
        AssertResponseSchema(definitionStart, "409", "ApiErrorResponse");
        AssertResponseSchema(genericStart, "200", "WorkflowRunStartApiResponse");
        AssertResponseSchema(genericStart, "404", "ApiErrorResponse");
        AssertResponseSchema(genericStart, "409", "ApiErrorResponse");
        AssertResponseSchema(lookup, "200", "WorkflowLaunchIdempotencyEvidence");
        AssertResponseSchema(lookup, "404", "ApiErrorResponse");
    }

    private static async Task<ApiTestHost> CreateHostAsync(bool jwtEnabled)
        => await ApiTestHost.CreateAsync(
            jwtEnabled,
            services =>
            {
                services.RemoveAll<ILoggerProvider>();
                services.RemoveAll<ISecretVault>();
                services.AddSingleton<ISecretVault, InMemorySecretVault>();
            });

    private static async Task<SeededWorkflows> SeedWorkflowsAsync(ApiTestHost host)
    {
        await using var scope = host.App.Services.CreateAsyncScope();
        var catalog = scope.ServiceProvider.GetRequiredService<IWorkflowCatalogService>();
        var primaryV1 = await catalog.SaveDefinitionAsync(CreateSaveRequest(
            workflowId: null,
            expectedVersionId: null,
            name: "Idempotency primary v1"));
        var primaryV2 = await catalog.SaveDefinitionAsync(CreateSaveRequest(
            primaryV1.Id,
            primaryV1.VersionId,
            "Idempotency primary v2"));
        var other = await catalog.SaveDefinitionAsync(CreateSaveRequest(
            workflowId: null,
            expectedVersionId: null,
            name: "Idempotency other"));
        return new SeededWorkflows(primaryV1, primaryV2, other);
    }

    private static WorkflowDefinitionSaveRequest CreateSaveRequest(
        WorkflowId? workflowId,
        WorkflowVersionId? expectedVersionId,
        string name)
        => new(
            workflowId,
            expectedVersionId,
            name,
            "Public workflow launch idempotency integration fixture.",
            WorkflowLifecycleStatus.Active,
            CreatePassthroughGraph(),
            new WorkflowRuntimePolicy(
                WorkflowRuntimeBackendKind.InProcess,
                AllowInProcessPreviewRuns: true,
                RequireDurableProductionRuns: false,
                ExposeAzureFunctionsStatusEndpoint: false,
                ExposeAzureFunctionsMcpTool: false));

    private static WorkflowGraph CreatePassthroughGraph()
        => new(
            new WorkflowNodeId("start"),
            [
                CreateNode("start", WorkflowNodeKind.Start),
                CreateNode("end", WorkflowNodeKind.End)
            ],
            [
                new WorkflowEdge(
                    new WorkflowEdgeId("start-to-end"),
                    new WorkflowNodeId("start"),
                    SourcePortId: null,
                    new WorkflowNodeId("end"),
                    TargetPortId: null,
                    WorkflowEdgeKind.Direct,
                    ConditionExpression: string.Empty)
            ]);

    private static WorkflowNode CreateNode(string id, WorkflowNodeKind kind)
        => new(
            new WorkflowNodeId(id),
            kind,
            id,
            [],
            new WorkflowNodeSettings(
                ComponentId: null,
                AgentId: null,
                SubworkflowId: null,
                ExternalRequestKind: null,
                Instructions: string.Empty,
                InputShape: WorkflowValueShape.Text,
                ResultShape: WorkflowValueShape.Text));

    private static async Task<HttpResponseMessage> StartAsync(
        HttpClient client,
        string route,
        string idempotencyKey,
        Guid? workflowId,
        Guid? versionId,
        WorkflowRuntimeBackendKind? requestedBackend,
        string inputJson)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, route);
        request.Headers.Add("Idempotency-Key", idempotencyKey);
        request.Content = JsonContent.Create(
            new WorkflowRunStartApiRequest
            {
                WorkflowId = workflowId,
                VersionId = versionId,
                InputJson = inputJson,
                RequestedBackend = requestedBackend
            },
            options: JsonOptions);
        return await client.SendAsync(request);
    }

    private static async Task<WorkflowRunStartApiResponse> ReadStartResponseAsync(
        HttpResponseMessage response)
    {
        var body = await response.Content.ReadAsStringAsync();
        Assert.True(response.IsSuccessStatusCode, body);
        return JsonSerializer.Deserialize<WorkflowRunStartApiResponse>(body, JsonOptions)
               ?? throw new InvalidOperationException("Workflow start response did not deserialize.");
    }

    private static async Task AssertConflictAsync(HttpResponseMessage response)
    {
        var body = await response.Content.ReadAsStringAsync();
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Contains("workflows.idempotency-key-conflict", body, StringComparison.Ordinal);
    }

    private static WorkflowLaunchIntent CreateFingerprintIntent(
        WorkflowDefinition definition,
        string key)
        => new(
            new WorkflowDefinitionSelection.ExactSavedVersion(definition.Id, definition.VersionId),
            WorkflowLaunchMode.Production,
            new WorkflowLaunchOrigin.Api(
                new WorkflowLaunchActor(WorkflowLaunchActorKind.Service, "candoitall-api"),
                new WorkflowLaunchCorrelationId("fingerprint-verification")),
            "{}",
            WorkflowLaunchCompletionPolicy.WaitForStopped,
            new WorkflowLaunchIdempotency.CallerSupplied(new WorkflowLaunchIdempotencyKey(key)))
        {
            RequestedBackend = WorkflowRuntimeBackendKind.InProcess
        };

    private static void AssertHeaderParameter(JsonElement operation, string name)
    {
        var parameter = operation
            .GetProperty("parameters")
            .EnumerateArray()
            .Single(item =>
                string.Equals(item.GetProperty("name").GetString(), name, StringComparison.OrdinalIgnoreCase));
        Assert.Equal("header", parameter.GetProperty("in").GetString());
    }

    private static void AssertResponseSchema(
        JsonElement operation,
        string statusCode,
        string expectedSchema)
    {
        var schema = operation
            .GetProperty("responses")
            .GetProperty(statusCode)
            .GetProperty("content")
            .GetProperty("application/json")
            .GetProperty("schema");
        var reference = schema.GetProperty("$ref").GetString();
        Assert.EndsWith($"/{expectedSchema}", reference, StringComparison.Ordinal);
    }

    private static JsonSerializerOptions JsonOptions { get; } = new(JsonSerializerDefaults.Web);

    private sealed record SeededWorkflows(
        WorkflowDefinition PrimaryV1,
        WorkflowDefinition PrimaryV2,
        WorkflowDefinition Other);
}
