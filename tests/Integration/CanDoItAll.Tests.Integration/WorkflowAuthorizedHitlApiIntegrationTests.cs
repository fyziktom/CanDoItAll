using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.Workflows.Abstractions;
using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.Modules.AgentFramework;
using CanDoItAll.Modules.Workspace.ApiAccess;
using CanDoItAll.Web.Api;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace CanDoItAll.Tests.Integration;

public sealed class WorkflowAuthorizedHitlApiIntegrationTests
{
    private static readonly DateTimeOffset FixedUtcNow =
        new(2026, 8, 21, 12, 0, 0, TimeSpan.Zero);

    [Theory]
    [MemberData(nameof(OutcomeScenarios))]
    public async Task ResponseEndpoint_MapsFrozenOutcomeWithoutBadGateway(
        string caseName,
        WorkflowExternalResponseServiceOutcome outcome,
        int statusCode,
        bool replayed)
    {
        var service = new RecordingExternalResponseService
        {
            NextResult = Result(outcome, replayed)
        };
        await using var host = await CreateHostAsync(service, jwtEnabled: false);
        using var response = await SubmitAsync(
            host.Client,
            WorkflowExternalRequestId.New().Value,
            $"matrix-{caseName}");

        Assert.Equal((HttpStatusCode)statusCode, response.StatusCode);
        Assert.NotEqual(HttpStatusCode.BadGateway, response.StatusCode);
        var json = await response.Content.ReadAsStringAsync();
        using var document = JsonDocument.Parse(json);
        Assert.Equal(
            (int)outcome,
            document.RootElement.GetProperty("outcome").GetInt32());
        Assert.Equal(replayed, document.RootElement.GetProperty("replayed").GetBoolean());
    }

    [Fact]
    public async Task ResponseEndpoint_UsesTypedJsonAndTrustedAuthenticatedActor()
    {
        var service = new RecordingExternalResponseService
        {
            NextResult = Result(WorkflowExternalResponseServiceOutcome.Completed)
        };
        await using var host = await CreateHostAsync(service, jwtEnabled: true);
        SetToken(host, ApiAccessScopeNames.RespondWorkflows, "workflow-human");

        using var response = await SubmitAsync(
            host.Client,
            WorkflowExternalRequestId.New().Value,
            "typed-command",
            expectedVersion: 3,
            responseJson: "{\"approved\":true,\"message\":\"Reviewed.\"}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var command = Assert.Single(service.Commands);
        Assert.Equal(3, command.ExpectedRequestVersion.Value);
        Assert.True(command.Response.GetProperty("approved").GetBoolean());
        Assert.Equal("typed-command", command.IdempotencyKey.Value);
        var actor = Assert.IsType<WorkflowExternalResponseActorContext.Authenticated>(
            command.ActorContext);
        Assert.Equal(WorkflowLaunchActorKind.User, actor.Actor.Kind);
        Assert.Equal("workflow-human", actor.Actor.SubjectId);
        Assert.Equal(WorkflowExternalResponseTrustedChannel.Api, actor.Channel);
        Assert.True(actor.Access.Capabilities.HasFlag(
            WorkflowExternalResponseCallerCapabilities.SubmitApprovalDecision));
    }

    [Fact]
    public async Task ResponseEndpoint_RequiresTheExactWorkflowResponseScope()
    {
        var service = new RecordingExternalResponseService
        {
            NextResult = Result(WorkflowExternalResponseServiceOutcome.Completed)
        };
        await using var host = await CreateHostAsync(service, jwtEnabled: true);
        SetToken(host, ApiAccessScopeNames.Api, "broad-api-client");

        using var forbidden = await SubmitAsync(
            host.Client,
            WorkflowExternalRequestId.New().Value,
            "wrong-scope");

        Assert.Equal(HttpStatusCode.Forbidden, forbidden.StatusCode);
        Assert.Empty(service.Commands);

        SetToken(host, ApiAccessScopeNames.RespondWorkflows, "workflow-responder");
        using var accepted = await SubmitAsync(
            host.Client,
            WorkflowExternalRequestId.New().Value,
            "exact-scope");

        Assert.Equal(HttpStatusCode.OK, accepted.StatusCode);
        Assert.Single(service.Commands);
    }

    [Fact]
    public async Task ResponseEndpoint_DoesNotManufactureActorWhenGlobalAuthorizationIsDisabled()
    {
        var service = new RecordingExternalResponseService
        {
            HonorUnauthenticatedActor = true,
            NextResult = Result(WorkflowExternalResponseServiceOutcome.Completed)
        };
        await using var host = await CreateHostAsync(service, jwtEnabled: false);

        using var response = await SubmitAsync(
            host.Client,
            WorkflowExternalRequestId.New().Value,
            "anonymous");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.IsType<WorkflowExternalResponseActorContext.Unauthenticated>(
            Assert.Single(service.Commands).ActorContext);
    }

    [Fact]
    public async Task ResponseEndpoint_RejectsTransportAmbiguityBeforeCallingService()
    {
        var service = new RecordingExternalResponseService();
        await using var host = await CreateHostAsync(service, jwtEnabled: false);
        var requestId = WorkflowExternalRequestId.New().Value;
        var invalidRequests = new[]
        {
            CreateRequest(requestId, "{}", "missing-key", includeKey: false),
            CreateRequest(requestId, "{}", "missing-contract"),
            CreateRequest(requestId, "{\"expectedRequestVersion\":1,\"response\":{},\"actor\":\"spoof\"}", "unknown-member"),
            CreateRequest(requestId, "{\"expectedRequestVersion\":1,\"response\":{},\"response\":{}}", "duplicate"),
            CreateRequest(requestId, "{\"expectedRequestVersion\":1,\"response\":", "malformed"),
            CreateRequest(requestId, "{\"expectedRequestVersion\":1,\"response\":{}}", "wrong-content", contentType: "text/plain")
        };

        foreach (var request in invalidRequests)
        {
            using (request)
            using (var response = await host.Client.SendAsync(request))
            {
                Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            }
        }

        Assert.Empty(service.Commands);
    }

    [Fact]
    public async Task OperationStatusEndpoint_UsesTheSameActorBoundaryAndReturnsOnlySafeFields()
    {
        var runId = WorkflowRunId.New();
        var requestId = WorkflowExternalRequestId.New();
        var operation = CreateOperation(runId, requestId);
        var service = new RecordingExternalResponseService
        {
            NextStatusResult = new WorkflowExternalResponseServiceResult(
                WorkflowExternalResponseServiceOutcome.Completed,
                operation,
                CreateRun(runId),
                Request: null,
                NextRequest: null,
                Replayed: true,
                "Completed. Authorization: Bearer super-secret-status")
        };
        await using var host = await CreateHostAsync(service, jwtEnabled: true);
        SetToken(host, ApiAccessScopeNames.RespondWorkflows, "status-reader");

        using var response = await host.Client.GetAsync(
            $"/api/workflows/external-response-operations/{operation.Id.Value:D}");
        var json = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var query = Assert.Single(service.StatusQueries);
        var actor = Assert.IsType<WorkflowExternalResponseActorContext.Authenticated>(
            query.ActorContext);
        Assert.Equal("status-reader", actor.Actor.SubjectId);
        Assert.Contains(operation.Id.Value.ToString("D"), json, StringComparison.OrdinalIgnoreCase);
        using var statusDocument = JsonDocument.Parse(json);
        Assert.False(statusDocument.RootElement.GetProperty("replayed").GetBoolean());
        Assert.Contains("[REDACTED]", json, StringComparison.Ordinal);
        Assert.DoesNotContain("super-secret", json, StringComparison.Ordinal);
        Assert.DoesNotContain(operation.ResponsePayload.Json, json, StringComparison.Ordinal);
        Assert.DoesNotContain(operation.IdempotencyKeyHash.Value, json, StringComparison.Ordinal);
        Assert.DoesNotContain(operation.ResponsePayloadHash.Value, json, StringComparison.Ordinal);
        Assert.DoesNotContain(operation.Actor.SubjectId, json, StringComparison.Ordinal);
        Assert.DoesNotContain("lease", json, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task OperationStatusEndpoint_MapsMissingOperationToNotFound()
    {
        var service = new RecordingExternalResponseService
        {
            NextStatusResult = Result(WorkflowExternalResponseServiceOutcome.OperationNotFound)
        };
        await using var host = await CreateHostAsync(service, jwtEnabled: true);
        SetToken(host, ApiAccessScopeNames.RespondWorkflows, "status-reader");

        using var response = await host.Client.GetAsync(
            $"/api/workflows/external-response-operations/{Guid.NewGuid():D}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task ResponseEndpoints_OpenApiPublishesTheTypedContractAndFrozenStatusMap()
    {
        await using var host = await CreateHostAsync(
            new RecordingExternalResponseService(),
            jwtEnabled: true);
        SetToken(host, ApiAccessScopeNames.RespondWorkflows, "openapi-reader");

        using var response = await host.Client.GetAsync("/openapi/v1.json");
        var body = await response.Content.ReadAsStringAsync();
        Assert.True(response.IsSuccessStatusCode, body);
        using var document = JsonDocument.Parse(body);
        var paths = document.RootElement.GetProperty("paths");
        var submit = paths
            .GetProperty("/api/workflows/external-requests/{requestId}/response")
            .GetProperty("post");
        var requestBody = submit.GetProperty("requestBody");
        Assert.True(requestBody.GetProperty("required").GetBoolean());
        var content = requestBody.GetProperty("content");
        Assert.True(content.TryGetProperty("application/json", out var jsonContent));
        Assert.True(content.TryGetProperty("application/*+json", out _));
        var requestSchema = ResolveOpenApiSchema(
            document.RootElement,
            jsonContent.GetProperty("schema"));
        var requestProperties = requestSchema.GetProperty("properties");
        Assert.Equal(
            ["expectedRequestVersion", "response"],
            requestProperties.EnumerateObject().Select(property => property.Name).Order());
        Assert.Equal(
            ["expectedRequestVersion", "response"],
            requestSchema.GetProperty("required").EnumerateArray()
                .Select(item => item.GetString())
                .Order());
        var idempotency = submit
            .GetProperty("parameters")
            .EnumerateArray()
            .Single(parameter => string.Equals(
                parameter.GetProperty("name").GetString(),
                "Idempotency-Key",
                StringComparison.OrdinalIgnoreCase));
        Assert.Equal("header", idempotency.GetProperty("in").GetString());
        Assert.True(idempotency.GetProperty("required").GetBoolean());

        var status = paths
            .GetProperty("/api/workflows/external-response-operations/{operationId}")
            .GetProperty("get");
        foreach (var operation in new[] { submit, status })
        {
            var responses = operation.GetProperty("responses");
            foreach (var statusCode in new[]
                     {
                         "200", "202", "400", "401", "403", "404", "409", "410", "422", "500", "503"
                     })
            {
                Assert.True(responses.TryGetProperty(statusCode, out _), statusCode);
            }

            Assert.False(responses.TryGetProperty("502", out _));
        }
    }

    [Fact]
    public async Task AuthenticatedResponse_TraversesWebServicePostgreSqlAndMaf()
    {
        await using var host = await ApiTestHost.CreateAsync(
            jwtEnabled: true,
            useInMemoryDatabase: false);
        SetToken(host, ApiAccessScopeNames.RespondWorkflows, "real-hitl-human");
        var started = await StartRealHitlRunAsync(host, "authorized-response");
        Assert.Equal(WorkflowRunState.WaitingForInput, started.Run.State);
        var pending = Assert.Single(started.PendingExternalRequests);
        var responseKey = $"real-hitl-response-{Guid.NewGuid():N}";
        const string responseJson = "{\"answer\":\"approved by integration\"}";

        SetToken(host, ApiAccessScopeNames.Api, "real-hitl-human");
        using var forbiddenResponse = await SubmitAsync(
            host.Client,
            pending.Id,
            responseKey,
            pending.Version,
            responseJson);
        Assert.Equal(HttpStatusCode.Forbidden, forbiddenResponse.StatusCode);
        Assert.Empty(await LoadOperationsAsync(host, pending.Id));

        SetToken(host, ApiAccessScopeNames.RespondWorkflows, "real-hitl-human");
        using var response = await SubmitAsync(
            host.Client,
            pending.Id,
            responseKey,
            pending.Version,
            responseJson);
        var responseBody = await response.Content.ReadAsStringAsync();
        Assert.True(response.StatusCode == HttpStatusCode.OK, responseBody);
        var result = JsonSerializer.Deserialize<WorkflowExternalResponseApiResponse>(
            responseBody,
            JsonOptions())!;
        Assert.Equal(WorkflowExternalResponseServiceOutcome.Completed, result.Outcome);
        Assert.NotNull(result.OperationId);
        Assert.Equal(WorkflowRunState.Completed, result.RunState);

        using var beforeReplayEventsResponse = await host.Client.GetAsync(
            $"/api/workflows/runs/{started.Run.RunId:D}/events");
        var beforeReplayEventsBody = await beforeReplayEventsResponse.Content.ReadAsStringAsync();
        Assert.True(beforeReplayEventsResponse.IsSuccessStatusCode, beforeReplayEventsBody);
        var beforeReplayEvents = JsonSerializer.Deserialize<IReadOnlyList<WorkflowEventApiResponse>>(
            beforeReplayEventsBody,
            JsonOptions())!;

        using var replayResponse = await SubmitAsync(
            host.Client,
            pending.Id,
            responseKey,
            pending.Version,
            responseJson);
        var replayBody = await replayResponse.Content.ReadAsStringAsync();
        Assert.True(replayResponse.StatusCode == HttpStatusCode.OK, replayBody);
        var replay = JsonSerializer.Deserialize<WorkflowExternalResponseApiResponse>(
            replayBody,
            JsonOptions())!;
        Assert.Equal(WorkflowExternalResponseServiceOutcome.Completed, replay.Outcome);
        Assert.Equal(result.OperationId, replay.OperationId);
        Assert.True(replay.Replayed);

        using var afterReplayEventsResponse = await host.Client.GetAsync(
            $"/api/workflows/runs/{started.Run.RunId:D}/events");
        var afterReplayEventsBody = await afterReplayEventsResponse.Content.ReadAsStringAsync();
        Assert.True(afterReplayEventsResponse.IsSuccessStatusCode, afterReplayEventsBody);
        var afterReplayEvents = JsonSerializer.Deserialize<IReadOnlyList<WorkflowEventApiResponse>>(
            afterReplayEventsBody,
            JsonOptions())!;
        Assert.Equal(
            beforeReplayEvents.Select(item => item.Id).Order(),
            afterReplayEvents.Select(item => item.Id).Order());

        using var conflictResponse = await SubmitAsync(
            host.Client,
            pending.Id,
            responseKey,
            pending.Version,
            "{\"answer\":\"changed after completion\"}");
        var conflictBody = await conflictResponse.Content.ReadAsStringAsync();
        Assert.Equal(HttpStatusCode.Conflict, conflictResponse.StatusCode);
        var conflict = JsonSerializer.Deserialize<WorkflowExternalResponseApiResponse>(
            conflictBody,
            JsonOptions())!;
        Assert.Equal(
            WorkflowExternalResponseServiceOutcome.IdempotencyConflict,
            conflict.Outcome);
        Assert.Equal(result.OperationId, conflict.OperationId);

        var operation = Assert.Single(await LoadOperationsAsync(host, pending.Id));
        Assert.Equal(result.OperationId, operation.Id);

        using var afterConflictEventsResponse = await host.Client.GetAsync(
            $"/api/workflows/runs/{started.Run.RunId:D}/events");
        var afterConflictEventsBody = await afterConflictEventsResponse.Content.ReadAsStringAsync();
        Assert.True(afterConflictEventsResponse.IsSuccessStatusCode, afterConflictEventsBody);
        var afterConflictEvents = JsonSerializer.Deserialize<IReadOnlyList<WorkflowEventApiResponse>>(
            afterConflictEventsBody,
            JsonOptions())!;
        Assert.Equal(
            afterReplayEvents.Select(item => item.Id).Order(),
            afterConflictEvents.Select(item => item.Id).Order());

        using var statusResponse = await host.Client.GetAsync(
            $"/api/workflows/external-response-operations/{result.OperationId:D}");
        var statusBody = await statusResponse.Content.ReadAsStringAsync();
        Assert.Equal(HttpStatusCode.OK, statusResponse.StatusCode);
        var status = JsonSerializer.Deserialize<WorkflowExternalResponseApiResponse>(
            statusBody,
            JsonOptions())!;
        Assert.Equal(result.OperationId, status.OperationId);
        Assert.Equal(WorkflowExternalResponseServiceOutcome.Completed, status.Outcome);
        Assert.False(status.Replayed);
    }

    [Fact]
    public async Task AuthenticatedResponse_AfterRealCancellation_ReturnsGoneWithoutCreatingOperation()
    {
        await using var host = await ApiTestHost.CreateAsync(
            jwtEnabled: true,
            useInMemoryDatabase: false);
        SetToken(host, ApiAccessScopeNames.RespondWorkflows, "real-hitl-cancel-human");
        var started = await StartRealHitlRunAsync(host, "late-response-after-cancel");
        Assert.Equal(WorkflowRunState.WaitingForInput, started.Run.State);
        var pending = Assert.Single(started.PendingExternalRequests);

        using var cancelResponse = await host.Client.PostAsync(
            $"/api/workflows/runs/{started.Run.RunId:D}/cancel",
            content: null);
        var cancelBody = await cancelResponse.Content.ReadAsStringAsync();
        Assert.True(cancelResponse.IsSuccessStatusCode, cancelBody);

        using var beforeLateResponseEventsResponse = await host.Client.GetAsync(
            $"/api/workflows/runs/{started.Run.RunId:D}/events");
        var beforeLateResponseEventsBody =
            await beforeLateResponseEventsResponse.Content.ReadAsStringAsync();
        Assert.True(
            beforeLateResponseEventsResponse.IsSuccessStatusCode,
            beforeLateResponseEventsBody);
        var beforeLateResponseEvents = JsonSerializer.Deserialize<IReadOnlyList<WorkflowEventApiResponse>>(
            beforeLateResponseEventsBody,
            JsonOptions())!;

        using var response = await SubmitAsync(
            host.Client,
            pending.Id,
            $"real-hitl-late-response-{Guid.NewGuid():N}",
            pending.Version,
            "{\"answer\":\"too late\"}");
        var responseBody = await response.Content.ReadAsStringAsync();
        Assert.Equal(HttpStatusCode.Gone, response.StatusCode);
        var result = JsonSerializer.Deserialize<WorkflowExternalResponseApiResponse>(
            responseBody,
            JsonOptions())!;
        Assert.Equal(WorkflowExternalResponseServiceOutcome.Cancelled, result.Outcome);
        Assert.Null(result.OperationId);
        Assert.Empty(await LoadOperationsAsync(host, pending.Id));

        using var afterLateResponseEventsResponse = await host.Client.GetAsync(
            $"/api/workflows/runs/{started.Run.RunId:D}/events");
        var afterLateResponseEventsBody =
            await afterLateResponseEventsResponse.Content.ReadAsStringAsync();
        Assert.True(
            afterLateResponseEventsResponse.IsSuccessStatusCode,
            afterLateResponseEventsBody);
        var afterLateResponseEvents = JsonSerializer.Deserialize<IReadOnlyList<WorkflowEventApiResponse>>(
            afterLateResponseEventsBody,
            JsonOptions())!;
        Assert.Equal(
            beforeLateResponseEvents.Select(item => item.Id).Order(),
            afterLateResponseEvents.Select(item => item.Id).Order());
    }

    public static TheoryData<string, WorkflowExternalResponseServiceOutcome, int, bool> OutcomeScenarios { get; } = new()
    {
        { "approve-completed", WorkflowExternalResponseServiceOutcome.Completed, 200, false },
        { "stable-replay-completed", WorkflowExternalResponseServiceOutcome.Completed, 200, true },
        { "consecutive-human-input-wait", WorkflowExternalResponseServiceOutcome.WaitingAgain, 200, false },
        { "deny", WorkflowExternalResponseServiceOutcome.Denied, 200, false },
        { "active-operation-resuming", WorkflowExternalResponseServiceOutcome.Resuming, 202, false },
        { "invalid-schema", WorkflowExternalResponseServiceOutcome.InvalidResponse, 400, false },
        { "unauthenticated", WorkflowExternalResponseServiceOutcome.Unauthenticated, 401, false },
        { "wrong-scope-or-profile", WorkflowExternalResponseServiceOutcome.Forbidden, 403, false },
        { "autonomous-self-approval", WorkflowExternalResponseServiceOutcome.Forbidden, 403, false },
        { "missing-authorization-policy", WorkflowExternalResponseServiceOutcome.AuthorizationContextUnavailable, 403, false },
        { "missing-request", WorkflowExternalResponseServiceOutcome.RequestNotFound, 404, false },
        { "missing-operation", WorkflowExternalResponseServiceOutcome.OperationNotFound, 404, false },
        { "stale-version", WorkflowExternalResponseServiceOutcome.RequestVersionMismatch, 409, false },
        { "changed-payload-conflict", WorkflowExternalResponseServiceOutcome.IdempotencyConflict, 409, false },
        { "competing-active-operation", WorkflowExternalResponseServiceOutcome.ActiveOperationConflict, 409, false },
        { "already-answered", WorkflowExternalResponseServiceOutcome.RequestNotPending, 409, false },
        { "run-not-waiting", WorkflowExternalResponseServiceOutcome.RunNotWaiting, 409, false },
        { "cancelled", WorkflowExternalResponseServiceOutcome.Cancelled, 410, false },
        { "superseded", WorkflowExternalResponseServiceOutcome.Superseded, 410, false },
        { "legacy", WorkflowExternalResponseServiceOutcome.LegacyNonResumable, 422, false },
        { "missing-checkpoint", WorkflowExternalResponseServiceOutcome.CheckpointMissing, 422, false },
        { "corrupt-checkpoint", WorkflowExternalResponseServiceOutcome.CheckpointCorrupt, 422, false },
        { "incompatible-checkpoint", WorkflowExternalResponseServiceOutcome.CheckpointIncompatible, 422, false },
        { "topology-mismatch", WorkflowExternalResponseServiceOutcome.TopologyMismatch, 422, false },
        { "workflow-version-mismatch", WorkflowExternalResponseServiceOutcome.WorkflowVersionMismatch, 422, false },
        { "request-mismatch", WorkflowExternalResponseServiceOutcome.RequestMismatch, 422, false },
        { "backend-unavailable", WorkflowExternalResponseServiceOutcome.BackendUnavailable, 503, false },
        { "retryable-store-or-lease", WorkflowExternalResponseServiceOutcome.RetryableFailure, 503, false },
        { "terminal-recovery", WorkflowExternalResponseServiceOutcome.TerminalFailure, 500, false }
    };

    private static WorkflowExternalResponseServiceResult Result(
        WorkflowExternalResponseServiceOutcome outcome,
        bool replayed = false)
        => new(
            outcome,
            Operation: null,
            Run: null,
            Request: null,
            NextRequest: null,
            Replayed: replayed,
            $"Safe {outcome} status.");

    private static Task<ApiTestHost> CreateHostAsync(
        RecordingExternalResponseService service,
        bool jwtEnabled)
        => ApiTestHost.CreateAsync(
            jwtEnabled,
            services =>
            {
                services.RemoveAll<IWorkflowExternalResponseService>();
                services.AddSingleton<IWorkflowExternalResponseService>(service);
            },
            useInMemoryDatabase: true);

    private static void SetToken(
        ApiTestHost host,
        string scope,
        string subject)
    {
        var token = host.App.Services.GetRequiredService<IApiTokenService>().IssueToken(
            new ApiTokenIssueRequest
            {
                Subject = subject,
                DisplayName = subject,
                Scopes = [scope]
            });
        host.Client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue(token.TokenType, token.Token);
    }

    private static async Task<HttpResponseMessage> SubmitAsync(
        HttpClient client,
        Guid requestId,
        string idempotencyKey,
        long expectedVersion = 1,
        string responseJson = "{\"answer\":\"yes\"}")
    {
        using var request = CreateRequest(
            requestId,
            $"{{\"expectedRequestVersion\":{expectedVersion},\"response\":{responseJson}}}",
            idempotencyKey);
        return await client.SendAsync(request);
    }

    private static HttpRequestMessage CreateRequest(
        Guid requestId,
        string json,
        string idempotencyKey,
        bool includeKey = true,
        string contentType = "application/json")
    {
        var request = new HttpRequestMessage(
            HttpMethod.Post,
            $"/api/workflows/external-requests/{requestId:D}/response")
        {
            Content = new StringContent(json, Encoding.UTF8, contentType)
        };
        if (includeKey)
        {
            request.Headers.TryAddWithoutValidation("Idempotency-Key", idempotencyKey);
        }

        return request;
    }

    private static WorkflowExternalResponseOperationRecord CreateOperation(
        WorkflowRunId runId,
        WorkflowExternalRequestId requestId)
        => new(
            WorkflowExternalResponseOperationId.New(),
            requestId,
            runId,
            WorkflowExternalRequestVersion.Initial,
            new WorkflowExternalResponseIdempotencyKeyHash(new string('a', 64)),
            new WorkflowExternalResponsePayloadHash(new string('b', 64)),
            new WorkflowExternalResponseActorScopeFingerprint(new string('c', 64)),
            new WorkflowExternalResponsePayload("{\"secret\":\"protected-response\"}"),
            new WorkflowLaunchActor(WorkflowLaunchActorKind.User, "protected-actor"),
            new WorkflowLaunchCorrelationId("protected-correlation"),
            WorkflowExternalResponseOperationState.Completed,
            Attempt: 1,
            WorkflowExternalResponseOperationConcurrencyVersion.Initial,
            FixedUtcNow)
        {
            CompletedAtUtc = FixedUtcNow,
            OutcomeCode = WorkflowExternalResponseOperationOutcomeCode.Completed,
            SafeMessage = "Completed safely."
        };

    private static WorkflowRunSnapshot CreateRun(WorkflowRunId runId)
        => new(
            runId,
            WorkflowId.New(),
            WorkflowVersionId.New(),
            WorkflowRunState.Completed,
            WorkflowRuntimeBackendKind.InProcess,
            "protected-backend-id",
            "Completed safely.",
            FixedUtcNow,
            FixedUtcNow);

    private static async Task<WorkflowRunStartApiResponse> StartRealHitlRunAsync(
        ApiTestHost host,
        string scenario)
    {
        using var saveResponse = await host.Client.PostAsJsonAsync(
            "/api/workflows/definitions",
            new WorkflowDefinitionSaveRequest(
                Id: null,
                ExpectedVersionId: null,
                $"Authorized HITL API integration: {scenario}",
                "Exercises the authenticated HTTP, service, PostgreSQL, and MAF continuation path.",
                WorkflowLifecycleStatus.Draft,
                CreateHumanInputGraph(),
                new WorkflowRuntimePolicy(
                    WorkflowRuntimeBackendKind.InProcess,
                    AllowInProcessPreviewRuns: true,
                    RequireDurableProductionRuns: false,
                    ExposeAzureFunctionsStatusEndpoint: false,
                    ExposeAzureFunctionsMcpTool: false)));
        var saveBody = await saveResponse.Content.ReadAsStringAsync();
        Assert.True(saveResponse.IsSuccessStatusCode, saveBody);
        var saved = JsonSerializer.Deserialize<WorkflowDefinition>(saveBody, JsonOptions())!;

        using var publishResponse = await host.Client.PostAsync(
            $"/api/workflows/definitions/{saved.Id.Value:D}/publish?expectedVersionId={saved.VersionId.Value:D}",
            content: null);
        var publishBody = await publishResponse.Content.ReadAsStringAsync();
        Assert.True(publishResponse.IsSuccessStatusCode, publishBody);
        var published = JsonSerializer.Deserialize<WorkflowDefinition>(publishBody, JsonOptions())!;

        using var startRequest = new HttpRequestMessage(
            HttpMethod.Post,
            $"/api/workflows/definitions/{published.Id.Value:D}/runs/start");
        startRequest.Headers.Add("Idempotency-Key", $"real-hitl-start-{Guid.NewGuid():N}");
        startRequest.Content = JsonContent.Create(
            new WorkflowRunStartApiRequest
            {
                VersionId = published.VersionId.Value,
                InputJson = "{\"route\":\"manual\"}",
                RequestedBackend = WorkflowRuntimeBackendKind.InProcess
            });
        using var startResponse = await host.Client.SendAsync(startRequest);
        var startBody = await startResponse.Content.ReadAsStringAsync();
        Assert.True(startResponse.IsSuccessStatusCode, startBody);
        return JsonSerializer.Deserialize<WorkflowRunStartApiResponse>(
            startBody,
            JsonOptions())!;
    }

    private static WorkflowGraph CreateHumanInputGraph()
        => new(
            new WorkflowNodeId("start"),
            [
                CreateNode("start", WorkflowNodeKind.Start, resultShape: JsonShape()),
                CreateNode("human", WorkflowNodeKind.HumanInput, inputShape: JsonShape(), resultShape: JsonShape()),
                CreateNode("end", WorkflowNodeKind.End, inputShape: JsonShape())
            ],
            [
                CreateEdge("start-to-human", "start", "human"),
                CreateEdge("human-to-end", "human", "end")
            ]);

    private static WorkflowEdge CreateEdge(string id, string source, string target)
        => new(
            new WorkflowEdgeId(id),
            new WorkflowNodeId(source),
            SourcePortId: null,
            new WorkflowNodeId(target),
            TargetPortId: null,
            WorkflowEdgeKind.Direct,
            ConditionExpression: string.Empty)
        {
            Routing = WorkflowEdgeRouting.Always
        };

    private static WorkflowNode CreateNode(
        string id,
        WorkflowNodeKind kind,
        WorkflowValueShape? inputShape = null,
        WorkflowValueShape? resultShape = null)
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
                InputShape: inputShape ?? WorkflowValueShape.Text,
                ResultShape: resultShape ?? WorkflowValueShape.Text));

    private static WorkflowValueShape JsonShape()
        => new(WorkflowValueShapeKind.Json, "{}", "JSON payload");

    private static JsonSerializerOptions JsonOptions()
        => new(JsonSerializerDefaults.Web)
        {
            PropertyNameCaseInsensitive = true
        };

    private static async Task<IReadOnlyList<WorkflowExternalResponseOperationEntity>> LoadOperationsAsync(
        ApiTestHost host,
        Guid requestId)
    {
        var dbContextFactory = host.App.Services
            .GetRequiredService<IDbContextFactory<AppDbContext>>();
        await using var dbContext = await dbContextFactory.CreateDbContextAsync();
        return await dbContext.Set<WorkflowExternalResponseOperationEntity>()
            .AsNoTracking()
            .Where(operation => operation.RequestId == requestId)
            .OrderBy(operation => operation.Id)
            .ToArrayAsync();
    }

    private static JsonElement ResolveOpenApiSchema(
        JsonElement document,
        JsonElement schema)
    {
        if (!schema.TryGetProperty("$ref", out var reference))
        {
            return schema;
        }

        var current = document;
        foreach (var segment in reference.GetString()![2..].Split('/'))
        {
            current = current.GetProperty(segment);
        }

        return current;
    }

    private sealed class RecordingExternalResponseService : IWorkflowExternalResponseService
    {
        public List<WorkflowExternalResponseCommand> Commands { get; } = [];

        public List<WorkflowExternalResponseStatusQuery> StatusQueries { get; } = [];

        public WorkflowExternalResponseServiceResult NextResult { get; set; } =
            Result(WorkflowExternalResponseServiceOutcome.Completed);

        public WorkflowExternalResponseServiceResult NextStatusResult { get; set; } =
            Result(WorkflowExternalResponseServiceOutcome.Completed);

        public bool HonorUnauthenticatedActor { get; init; }

        public Task<WorkflowExternalResponseServiceResult> SubmitAsync(
            WorkflowExternalResponseCommand command,
            CancellationToken cancellationToken = default)
        {
            Commands.Add(command);
            return Task.FromResult(
                HonorUnauthenticatedActor &&
                command.ActorContext is WorkflowExternalResponseActorContext.Unauthenticated
                    ? Result(WorkflowExternalResponseServiceOutcome.Unauthenticated)
                    : NextResult);
        }

        public Task<WorkflowExternalResponseServiceResult> GetStatusAsync(
            WorkflowExternalResponseStatusQuery query,
            CancellationToken cancellationToken = default)
        {
            StatusQueries.Add(query);
            return Task.FromResult(NextStatusResult);
        }
    }
}
