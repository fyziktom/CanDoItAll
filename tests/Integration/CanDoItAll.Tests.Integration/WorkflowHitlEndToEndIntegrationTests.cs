using System.Collections.Concurrent;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.Workflows.Abstractions;
using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.Modules.AgentFramework;
using CanDoItAll.Modules.Workspace.ApiAccess;
using CanDoItAll.SharedKernel;
using CanDoItAll.Tests.Support;
using CanDoItAll.Web.Api;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;

namespace CanDoItAll.Tests.Integration;

public sealed class WorkflowHitlEndToEndIntegrationTests
{
    private static readonly WorkflowValueShape JsonObjectShape = new(
        WorkflowValueShapeKind.Json,
        """{"type":"object"}""",
        "JSON object");

    [Fact]
    public async Task HumanInput_AfterHostReconstruction_ResumesConsecutiveWaitsWithoutRerunningPrefix()
    {
        await using var fixture = RestartableHitlFixture.Create("human-input");
        WorkflowRunStartApiResponse started;

        await using (var firstHost = await fixture.CreateHostAsync())
        {
            Authorize(firstHost, "restart-human-input");
            started = await StartAsync(
                firstHost,
                CreateConsecutiveHumanInputGraph(),
                "human-input");
        }

        Assert.Equal(WorkflowRunState.WaitingForInput, started.Run.State);
        Assert.Equal(1, fixture.Probe.PrefixInvocationCount);
        var firstRequest = Assert.Single(started.PendingExternalRequests);
        var firstCheckpoint = Assert.Single(started.Checkpoints);
        Assert.Equal(firstRequest.Id, firstCheckpoint.ExternalRequestId);
        Assert.Equal(WorkflowCheckpointTrustBoundary.TrustedRuntimeState, firstCheckpoint.TrustBoundary);
        Assert.Equal(WorkflowResumeAvailability.Available, firstCheckpoint.ResumeAvailability);

        WorkflowExternalResponseApiResponse firstResume;
        await using (var secondHost = await fixture.CreateHostAsync())
        {
            Authorize(secondHost, "restart-human-input");
            using var response = await SubmitAsync(
                secondHost.Client,
                firstRequest.Id,
                firstRequest.Version,
                "human-input-first",
                """{"answer":"first"}""");
            firstResume = await ReadSuccessAsync<WorkflowExternalResponseApiResponse>(response);
        }

        Assert.Equal(WorkflowExternalResponseServiceOutcome.WaitingAgain, firstResume.Outcome);
        Assert.Equal(WorkflowRunState.WaitingForInput, firstResume.RunState);
        Assert.Equal(1, fixture.Probe.PrefixInvocationCount);
        var secondRequest = Assert.IsType<WorkflowPendingExternalRequestApiResponse>(
            firstResume.NextPendingRequest);
        Assert.NotEqual(firstRequest.Id, secondRequest.Id);

        await using (var thirdHost = await fixture.CreateHostAsync())
        {
            Authorize(thirdHost, "restart-human-input");
            using var response = await SubmitAsync(
                thirdHost.Client,
                secondRequest.Id,
                secondRequest.Version,
                "human-input-second",
                """{"answer":"second"}""");
            var completed = await ReadSuccessAsync<WorkflowExternalResponseApiResponse>(response);

            Assert.Equal(WorkflowExternalResponseServiceOutcome.Completed, completed.Outcome);
            Assert.Equal(WorkflowRunState.Completed, completed.RunState);
            Assert.Null(completed.NextPendingRequest);

            using var detailResponse = await thirdHost.Client.GetAsync(
                $"/api/workflows/runs/{started.Run.RunId:D}/detail");
            var detail = await ReadSuccessAsync<WorkflowRunDetailApiResponse>(detailResponse);
            Assert.Equal(WorkflowRunState.Completed, detail.Run.State);
            Assert.Empty(detail.PendingExternalRequests);
            Assert.Contains(detail.Checkpoints, checkpoint => checkpoint.Id == firstCheckpoint.Id);
            var externalBoundaryCheckpoints = detail.Checkpoints
                .Where(checkpoint => checkpoint.ExternalRequestId.HasValue)
                .ToArray();
            Assert.Equal(2, externalBoundaryCheckpoints.Length);
            Assert.Equal(
                2,
                externalBoundaryCheckpoints
                    .Select(checkpoint => checkpoint.ExternalRequestId)
                    .Distinct()
                    .Count());
            Assert.Equal(
                2,
                externalBoundaryCheckpoints
                    .Select(checkpoint => checkpoint.Id)
                    .Distinct()
                    .Count());
        }

        Assert.Equal(1, fixture.Probe.PrefixInvocationCount);
    }

    [Theory]
    [InlineData(true, WorkflowExternalResponseServiceOutcome.Completed, 1)]
    [InlineData(false, WorkflowExternalResponseServiceOutcome.Denied, 0)]
    public async Task Approval_AfterHostReconstruction_EnforcesDecision(
        bool approved,
        WorkflowExternalResponseServiceOutcome expectedOutcome,
        int expectedEffectCount)
    {
        string decision = approved ? "approve" : "deny";
        await using var fixture = RestartableHitlFixture.Create($"approval-{decision}");
        WorkflowRunStartApiResponse started;

        await using (var firstHost = await fixture.CreateHostAsync())
        {
            Authorize(firstHost, $"restart-{decision}-launcher");
            started = await StartAsync(firstHost, CreateApprovalGraph(), $"approval-{decision}");
        }

        Assert.Equal(WorkflowRunState.WaitingForInput, started.Run.State);
        Assert.Equal(0, fixture.Probe.ApprovalExecutorInvocationCount);
        Assert.Equal(0, fixture.Probe.AppliedEffectCount);
        var pending = Assert.Single(started.PendingExternalRequests);
        string idempotencyKey = $"approval-{decision}-response";
        string responseJson = JsonSerializer.Serialize(new
        {
            approved,
            message = $"Decision: {decision}."
        });

        await using var secondHost = await fixture.CreateHostAsync();
        Authorize(secondHost, $"restart-{decision}-approver");
        using var response = await SubmitAsync(
            secondHost.Client,
            pending.Id,
            pending.Version,
            idempotencyKey,
            responseJson);
        var result = await ReadSuccessAsync<WorkflowExternalResponseApiResponse>(response);

        Assert.Equal(expectedOutcome, result.Outcome);
        Assert.Equal(WorkflowRunState.Completed, result.RunState);
        Assert.Equal(expectedEffectCount, fixture.Probe.ApprovalExecutorInvocationCount);
        Assert.Equal(expectedEffectCount, fixture.Probe.AppliedEffectCount);

        using var replayResponse = await SubmitAsync(
            secondHost.Client,
            pending.Id,
            pending.Version,
            idempotencyKey,
            responseJson);
        var replay = await ReadSuccessAsync<WorkflowExternalResponseApiResponse>(replayResponse);
        Assert.Equal(expectedOutcome, replay.Outcome);
        Assert.Equal(result.OperationId, replay.OperationId);
        Assert.True(replay.Replayed);
        Assert.Equal(expectedEffectCount, fixture.Probe.ApprovalExecutorInvocationCount);
        Assert.Equal(expectedEffectCount, fixture.Probe.AppliedEffectCount);
    }

    [Fact]
    public async Task ConcurrentApprovalResponses_CreateOneActiveContinuationAndOneEffect()
    {
        await using var fixture = RestartableHitlFixture.Create("approval-concurrency");
        WorkflowRunStartApiResponse started;

        await using (var firstHost = await fixture.CreateHostAsync())
        {
            Authorize(firstHost, "restart-concurrency-launcher");
            started = await StartAsync(
                firstHost,
                CreateApprovalGraph(),
                "approval-concurrency");
        }

        var pending = Assert.Single(started.PendingExternalRequests);
        var blockingHook = new BlockingRecoveryHook(
            WorkflowExternalResponseRecoveryPoint.ClaimedBeforeResponseDelivery);
        await using var secondHost = await fixture.CreateHostAsync(blockingHook.InvokeAsync);
        Authorize(secondHost, "restart-concurrency-approver");
        Task<HttpResponseMessage> winningRequest = SubmitAsync(
            secondHost.Client,
            pending.Id,
            pending.Version,
            "approval-concurrency-winner",
            """{"approved":true,"message":"winner"}""");

        try
        {
            await blockingHook.WaitUntilEnteredAsync();
            using var conflictingResponse = await SubmitAsync(
                secondHost.Client,
                pending.Id,
                pending.Version,
                "approval-concurrency-conflict",
                """{"approved":true,"message":"conflict"}""");
            Assert.Equal(HttpStatusCode.Conflict, conflictingResponse.StatusCode);
            var conflict = await ReadAsync<WorkflowExternalResponseApiResponse>(
                conflictingResponse);
            Assert.Equal(
                WorkflowExternalResponseServiceOutcome.ActiveOperationConflict,
                conflict.Outcome);
        }
        finally
        {
            blockingHook.Release();
        }

        using var winningResponse = await winningRequest.WaitAsync(TimeSpan.FromSeconds(30));
        var completed = await ReadSuccessAsync<WorkflowExternalResponseApiResponse>(winningResponse);
        Assert.Equal(WorkflowExternalResponseServiceOutcome.Completed, completed.Outcome);
        Assert.Equal(WorkflowRunState.Completed, completed.RunState);
        Assert.Equal(1, fixture.Probe.ApprovalExecutorInvocationCount);
        Assert.Equal(1, fixture.Probe.AppliedEffectCount);

        var operations = await LoadOperationsAsync(secondHost, pending.Id);
        var operation = Assert.Single(operations);
        Assert.Equal(completed.OperationId!.Value, operation.Id);
        Assert.Equal((int)WorkflowExternalResponseOperationState.Completed, operation.State);
        Assert.Equal((int)WorkflowExternalResponseOperationOutcomeCode.Completed, operation.OutcomeCode);
    }

    [Theory]
    [InlineData(WorkflowExternalResponseRecoveryPoint.ClaimedBeforeResponseDelivery, 0)]
    [InlineData(WorkflowExternalResponseRecoveryPoint.ResponseDeliveredBeforeCommit, 1)]
    public async Task ApprovalCrashWindow_AfterHostReconstruction_RecoversWithoutDuplicateEffect(
        WorkflowExternalResponseRecoveryPoint recoveryPoint,
        int expectedEffectCountBeforeRecovery)
    {
        var clock = new ManualTimeProvider(DateTimeOffset.UtcNow);
        await using var fixture = RestartableHitlFixture.Create(
            $"approval-crash-{recoveryPoint}",
            clock);
        WorkflowRunStartApiResponse started;

        await using (var firstHost = await fixture.CreateHostAsync())
        {
            clock.SetUtcNow(
                firstHost.App.Services.GetRequiredService<IClock>().GetUtcNow().AddSeconds(1));
            Authorize(firstHost, $"restart-crash-{recoveryPoint}-launcher");
            started = await StartAsync(
                firstHost,
                CreateApprovalGraph(),
                $"approval-crash-{recoveryPoint}");
        }

        var pending = Assert.Single(started.PendingExternalRequests);
        var crashHook = new CrashOnceRecoveryHook(recoveryPoint);
        WorkflowExternalResponseOperationEntity interruptedOperation;
        await using (var crashHost = await fixture.CreateHostAsync(crashHook.InvokeAsync))
        {
            var tokenClock = crashHost.App.Services.GetRequiredService<IClock>();
            clock.SetUtcNow(tokenClock.GetUtcNow().AddSeconds(1));
            Authorize(crashHost, $"restart-crash-{recoveryPoint}-approver");
            using var interruptedResponse = await SubmitAsync(
                crashHost.Client,
                pending.Id,
                pending.Version,
                $"approval-crash-{recoveryPoint}-response",
                """{"approved":true,"message":"recover"}""");
            Assert.True(
                interruptedResponse.StatusCode == HttpStatusCode.InternalServerError,
                $"Expected the injected crash to return 500, but received " +
                $"{interruptedResponse.StatusCode}. " +
                $"Workflow clock: {clock.GetUtcNow():O}; token clock: {tokenClock.GetUtcNow():O}.");

            interruptedOperation = Assert.Single(await LoadOperationsAsync(crashHost, pending.Id));
            Assert.Equal((int)WorkflowExternalResponseOperationState.Resuming, interruptedOperation.State);
            Assert.Equal(1, interruptedOperation.Attempt);
            Assert.NotNull(interruptedOperation.LeaseExpiresAtUtc);
            Assert.Equal(
                expectedEffectCountBeforeRecovery,
                fixture.Probe.ApprovalExecutorInvocationCount);
            Assert.Equal(expectedEffectCountBeforeRecovery, fixture.Probe.AppliedEffectCount);
        }

        clock.Advance(TimeSpan.FromMinutes(3));
        await using var recoveryHost = await fixture.CreateHostAsync();
        await using var scope = recoveryHost.App.Services.CreateAsyncScope();
        var coordinator = scope.ServiceProvider
            .GetRequiredService<WorkflowExternalResponseRecoveryCoordinator>();
        var recovery = Assert.Single(await coordinator.RecoverAsync());

        Assert.Equal(WorkflowExternalResponseContinuationOutcome.Completed, recovery.Outcome);
        Assert.NotNull(recovery.Operation);
        Assert.Equal(interruptedOperation.Id, recovery.Operation.Id.Value);
        Assert.Equal(WorkflowExternalResponseOperationState.Completed, recovery.Operation.State);
        Assert.Equal(WorkflowExternalResponseOperationOutcomeCode.Completed, recovery.Operation.OutcomeCode);
        Assert.Equal(2, recovery.Operation.Attempt);
        Assert.Equal(1, fixture.Probe.ApprovalExecutorInvocationCount);
        Assert.Equal(1, fixture.Probe.AppliedEffectCount);

        var persisted = Assert.Single(await LoadOperationsAsync(recoveryHost, pending.Id));
        Assert.Equal((int)WorkflowExternalResponseOperationState.Completed, persisted.State);
        Assert.Equal((int)WorkflowExternalResponseOperationOutcomeCode.Completed, persisted.OutcomeCode);
        Assert.Equal(2, persisted.Attempt);
    }

    [Theory]
    [InlineData(
        RecoveryCorruption.MissingCheckpoint,
        WorkflowExternalResponseServiceOutcome.CheckpointMissing,
        WorkflowExternalResponseOperationOutcomeCode.CheckpointMissing)]
    [InlineData(
        RecoveryCorruption.CorruptCheckpoint,
        WorkflowExternalResponseServiceOutcome.CheckpointCorrupt,
        WorkflowExternalResponseOperationOutcomeCode.CheckpointCorrupt)]
    [InlineData(
        RecoveryCorruption.TopologyMismatch,
        WorkflowExternalResponseServiceOutcome.TopologyMismatch,
        WorkflowExternalResponseOperationOutcomeCode.TopologyMismatch)]
    [InlineData(
        RecoveryCorruption.WorkflowVersionMismatch,
        WorkflowExternalResponseServiceOutcome.WorkflowVersionMismatch,
        WorkflowExternalResponseOperationOutcomeCode.WorkflowVersionMismatch)]
    public async Task CorruptRecoveryState_AfterHostReconstruction_FailsClosed(
        RecoveryCorruption corruption,
        WorkflowExternalResponseServiceOutcome expectedServiceOutcome,
        WorkflowExternalResponseOperationOutcomeCode expectedOperationOutcome)
    {
        await using var fixture = RestartableHitlFixture.Create($"corruption-{corruption}");
        WorkflowRunStartApiResponse started;

        await using (var firstHost = await fixture.CreateHostAsync())
        {
            Authorize(firstHost, $"restart-corruption-{corruption}-launcher");
            started = await StartAsync(
                firstHost,
                CreatePrefixedApprovalGraph(),
                $"corruption-{corruption}");
            await MutateRecoveryStateAsync(
                firstHost,
                Assert.Single(started.PendingExternalRequests).Id,
                corruption);
        }

        Assert.Equal(WorkflowRunState.WaitingForInput, started.Run.State);
        Assert.Equal(1, fixture.Probe.PrefixInvocationCount);
        Assert.Equal(0, fixture.Probe.ApprovalExecutorInvocationCount);
        Assert.Equal(0, fixture.Probe.AppliedEffectCount);
        var pending = Assert.Single(started.PendingExternalRequests);

        await using var secondHost = await fixture.CreateHostAsync();
        Authorize(secondHost, $"restart-corruption-{corruption}-approver");
        using var response = await SubmitAsync(
            secondHost.Client,
            pending.Id,
            pending.Version,
            $"corruption-{corruption}-response",
            """{"approved":true,"message":"must fail closed"}""");
        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        var result = await ReadAsync<WorkflowExternalResponseApiResponse>(response);

        Assert.Equal(expectedServiceOutcome, result.Outcome);
        Assert.Equal(WorkflowExternalResponseOperationState.FailedTerminal, result.OperationState);
        Assert.Equal(expectedOperationOutcome, result.OperationOutcome);
        Assert.NotNull(result.RunState);
        Assert.NotEqual(WorkflowRunState.Completed, result.RunState.Value);
        Assert.NotNull(result.OperationId);

        var operation = Assert.Single(await LoadOperationsAsync(secondHost, pending.Id));
        Assert.Equal(result.OperationId!.Value, operation.Id);
        Assert.Equal((int)WorkflowExternalResponseOperationState.FailedTerminal, operation.State);
        Assert.Equal((int)expectedOperationOutcome, operation.OutcomeCode);

        using var detailResponse = await secondHost.Client.GetAsync(
            $"/api/workflows/runs/{started.Run.RunId:D}/detail");
        var detail = await ReadSuccessAsync<WorkflowRunDetailApiResponse>(detailResponse);
        Assert.Equal(result.RunState.Value, detail.Run.State);
        Assert.NotEqual(WorkflowRunState.Completed, detail.Run.State);
        Assert.Equal(1, fixture.Probe.PrefixInvocationCount);
        Assert.Equal(0, fixture.Probe.ApprovalExecutorInvocationCount);
        Assert.Equal(0, fixture.Probe.AppliedEffectCount);
    }

    [Fact]
    public async Task LegacyWaitingRun_AfterHostReconstruction_RemainsInspectableAndRejectsResponse()
    {
        await using var fixture = RestartableHitlFixture.Create("legacy-waiting");
        WorkflowRunStartApiResponse started;

        await using (var firstHost = await fixture.CreateHostAsync())
        {
            Authorize(firstHost, "restart-legacy-launcher");
            started = await StartAsync(
                firstHost,
                CreateConsecutiveHumanInputGraph(),
                "legacy-waiting");
            await DeleteBoundaryAsync(
                firstHost,
                Assert.Single(started.PendingExternalRequests).Id);
        }

        Assert.Equal(1, fixture.Probe.PrefixInvocationCount);
        var pending = Assert.Single(started.PendingExternalRequests);
        await using var secondHost = await fixture.CreateHostAsync();
        Authorize(secondHost, "restart-legacy-responder");

        using var detailResponse = await secondHost.Client.GetAsync(
            $"/api/workflows/runs/{started.Run.RunId:D}/detail");
        var detail = await ReadSuccessAsync<WorkflowRunDetailApiResponse>(detailResponse);
        Assert.Equal(WorkflowRunState.WaitingForInput, detail.Run.State);
        var legacyRequest = Assert.Single(detail.PendingExternalRequests);
        Assert.Equal(pending.Id, legacyRequest.Id);
        Assert.Equal(WorkflowExternalRequestState.LegacyNonResumable, legacyRequest.State);

        using var response = await SubmitAsync(
            secondHost.Client,
            pending.Id,
            pending.Version,
            "legacy-waiting-response",
            """{"answer":"cannot resume"}""");
        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        var result = await ReadAsync<WorkflowExternalResponseApiResponse>(response);
        Assert.Equal(WorkflowExternalResponseServiceOutcome.LegacyNonResumable, result.Outcome);
        Assert.Null(result.OperationId);
        Assert.Empty(await LoadOperationsAsync(secondHost, pending.Id));
        Assert.Equal(1, fixture.Probe.PrefixInvocationCount);
    }

    [Fact]
    public async Task RestartableResponsesAndLogs_DoNotExposeCheckpointOrSecrets()
    {
        const string inputSentinel = "input-secret-hitl-f47aa920";
        const string responseSentinel = "response-secret-hitl-9fb8b2c1";
        await using var fixture = RestartableHitlFixture.Create(
            "safe-projections",
            captureLogs: true);
        var responseBodies = new List<string>();
        var issuedTokens = new List<string>();
        WorkflowRunStartApiResponse started;
        WorkflowPendingExternalRequestApiResponse pending;
        string databaseConnectionString;
        var nativeCheckpointGuards = new List<ProjectionSecretGuard>();
        var explicitlyPublicIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        await using (var firstHost = await fixture.CreateHostAsync())
        {
            databaseConnectionString = firstHost.ActiveProfile.ConnectionString;
            issuedTokens.Add(Authorize(firstHost, "restart-safe-launcher"));
            started = await StartAsync(
                firstHost,
                CreateConsecutiveHumanInputGraph(),
                "safe-projections",
                responseBodies,
                JsonSerializer.Serialize(new { input = inputSentinel }));
            pending = Assert.Single(started.PendingExternalRequests);
            explicitlyPublicIds.Add(started.Run.RunId.ToString("D"));
            explicitlyPublicIds.Add(started.Run.WorkflowId.ToString("D"));
            explicitlyPublicIds.Add(started.Run.VersionId.ToString("D"));
            explicitlyPublicIds.Add(pending.Id.ToString("D"));
            nativeCheckpointGuards.AddRange(await LoadNativeCheckpointSecretsAsync(
                firstHost,
                pending.Id,
                explicitlyPublicIds));
            await AssertNativeCheckpointContainsSentinelAsync(
                firstHost,
                pending.Id,
                inputSentinel);
        }

        await using (var secondHost = await fixture.CreateHostAsync())
        {
            issuedTokens.Add(Authorize(secondHost, "restart-safe-responder"));
            using var response = await SubmitAsync(
                secondHost.Client,
                pending.Id,
                pending.Version,
                "safe-projections-response",
                JsonSerializer.Serialize(new { answer = responseSentinel }));
            var waitingAgain = await ReadSuccessAsync<WorkflowExternalResponseApiResponse>(
                response,
                responseBodies);
            Assert.Equal(WorkflowExternalResponseServiceOutcome.WaitingAgain, waitingAgain.Outcome);
            var secondPending = Assert.IsType<WorkflowPendingExternalRequestApiResponse>(
                waitingAgain.NextPendingRequest);
            explicitlyPublicIds.Add(secondPending.Id.ToString("D"));
            nativeCheckpointGuards.AddRange(await LoadNativeCheckpointSecretsAsync(
                secondHost,
                secondPending.Id,
                explicitlyPublicIds));
            await AssertNativeCheckpointContainsSentinelAsync(
                secondHost,
                secondPending.Id,
                responseSentinel);

            using var completionResponse = await SubmitAsync(
                secondHost.Client,
                secondPending.Id,
                secondPending.Version,
                "safe-projections-completion",
                """{"answer":"safe completion"}""");
            var completed = await ReadSuccessAsync<WorkflowExternalResponseApiResponse>(
                completionResponse,
                responseBodies);
            Assert.Equal(WorkflowExternalResponseServiceOutcome.Completed, completed.Outcome);
            Assert.Equal(WorkflowRunState.Completed, completed.RunState);
            Assert.NotNull(completed.OperationId);

            using var detailResponse = await secondHost.Client.GetAsync(
                $"/api/workflows/runs/{started.Run.RunId:D}/detail");
            var detail = await ReadSuccessAsync<WorkflowRunDetailApiResponse>(
                detailResponse,
                responseBodies);
            Assert.Equal(WorkflowRunState.Completed, detail.Run.State);
            Assert.NotEmpty(detail.Events);
            Assert.NotEmpty(detail.Checkpoints);

            using var eventsResponse = await secondHost.Client.GetAsync(
                $"/api/workflows/runs/{started.Run.RunId:D}/events");
            _ = await ReadSuccessAsync<JsonElement>(eventsResponse, responseBodies);
            using var checkpointsResponse = await secondHost.Client.GetAsync(
                $"/api/workflows/runs/{started.Run.RunId:D}/checkpoints");
            _ = await ReadSuccessAsync<JsonElement>(checkpointsResponse, responseBodies);
            using var statusResponse = await secondHost.Client.GetAsync(
                $"/api/workflows/external-response-operations/{completed.OperationId:D}");
            var status = await ReadSuccessAsync<WorkflowExternalResponseApiResponse>(
                statusResponse,
                responseBodies);
            Assert.Equal(WorkflowExternalResponseServiceOutcome.Completed, status.Outcome);
            Assert.Equal(WorkflowExternalResponseOperationState.Completed, status.OperationState);
            Assert.Equal(completed.OperationId, status.OperationId);
        }

        Assert.True(responseBodies.Count >= 8, $"Captured only {responseBodies.Count} response bodies.");
        var logEntries = Assert.IsType<RecordingLoggerProvider>(fixture.LogProvider).Entries;
        Assert.NotEmpty(logEntries);
        string httpProjectionMaterial = string.Join('\n', responseBodies);
        string logProjectionMaterial = string.Join('\n', logEntries);
        List<ProjectionSecretGuard> forbiddenValues =
        [
            ProjectionSecretGuard.Everywhere(
                ProjectionSecretCategory.InputPayloadSentinel,
                inputSentinel),
            ProjectionSecretGuard.Everywhere(
                ProjectionSecretCategory.ResponsePayloadSentinel,
                responseSentinel),
            ProjectionSecretGuard.Everywhere(
                ProjectionSecretCategory.DatabaseConnectionString,
                databaseConnectionString),
            ProjectionSecretGuard.Everywhere(
                ProjectionSecretCategory.NativeCheckpointUri,
                "maf-checkpoint://"),
            ProjectionSecretGuard.HttpOnly(
                ProjectionSecretCategory.ProtectedPayloadField,
                "ProtectedPayload"),
            ProjectionSecretGuard.HttpOnly(
                ProjectionSecretCategory.ProtectedResponsePayloadField,
                "ProtectedResponsePayload"),
            ProjectionSecretGuard.HttpOnly(
                ProjectionSecretCategory.ContinuationField,
                "ContinuationJson")
        ];
        forbiddenValues.AddRange(issuedTokens.Select(token =>
            ProjectionSecretGuard.Everywhere(ProjectionSecretCategory.BearerToken, token)));
        forbiddenValues.AddRange(nativeCheckpointGuards);
        foreach (var guard in forbiddenValues.Where(guard => !string.IsNullOrWhiteSpace(guard.Value)))
        {
            if (guard.Classification == ProjectionValueClassification.PublicAlias)
            {
                continue;
            }

            if (guard.Surfaces.HasFlag(ProjectionInspectionSurface.HttpResponse))
            {
                AssertProjectionDoesNotExpose(
                    httpProjectionMaterial,
                    guard,
                    ProjectionInspectionSurface.HttpResponse);
            }

            if (guard.Surfaces.HasFlag(ProjectionInspectionSurface.ApplicationLog))
            {
                AssertProjectionDoesNotExpose(
                    logProjectionMaterial,
                    guard,
                    ProjectionInspectionSurface.ApplicationLog);
            }
        }
    }

    private static void AssertProjectionDoesNotExpose(
        string projectionMaterial,
        ProjectionSecretGuard guard,
        ProjectionInspectionSurface surface)
    {
        bool exposed = projectionMaterial.Contains(
            guard.Value,
            StringComparison.OrdinalIgnoreCase);
        Assert.False(
            exposed,
            $"Safe projection exposed forbidden category " +
            $"{guard.Category}/{guard.Classification} on {surface}.");
    }

    private static string Authorize(ApiTestHost host, string subject)
    {
        var token = host.App.Services.GetRequiredService<IApiTokenService>().IssueToken(
            new ApiTokenIssueRequest
            {
                Subject = subject,
                DisplayName = subject,
                Scopes = [ApiAccessScopeNames.RespondWorkflows]
            });
        host.Client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue(token.TokenType, token.Token);
        return token.Token;
    }

    private static async Task<WorkflowRunStartApiResponse> StartAsync(
        ApiTestHost host,
        WorkflowGraph graph,
        string scenario,
        ICollection<string>? responseBodies = null,
        string inputJson = """{"input":"restartable"}""")
    {
        using var saveResponse = await host.Client.PostAsJsonAsync(
            "/api/workflows/definitions",
            new WorkflowDefinitionSaveRequest(
                Id: null,
                ExpectedVersionId: null,
                $"Restartable HITL E2E: {scenario}",
                "Exercises Web, authorization, PostgreSQL, the response service, and native MAF reconstruction.",
                WorkflowLifecycleStatus.Draft,
                graph,
                new WorkflowRuntimePolicy(
                    WorkflowRuntimeBackendKind.InProcess,
                    AllowInProcessPreviewRuns: true,
                    RequireDurableProductionRuns: false,
                    ExposeAzureFunctionsStatusEndpoint: false,
                    ExposeAzureFunctionsMcpTool: false)));
        var saved = await ReadSuccessAsync<WorkflowDefinition>(saveResponse, responseBodies);

        using var publishResponse = await host.Client.PostAsync(
            $"/api/workflows/definitions/{saved.Id.Value:D}/publish?expectedVersionId={saved.VersionId.Value:D}",
            content: null);
        var published = await ReadSuccessAsync<WorkflowDefinition>(
            publishResponse,
            responseBodies);

        using var startRequest = new HttpRequestMessage(
            HttpMethod.Post,
            $"/api/workflows/definitions/{published.Id.Value:D}/runs/start");
        startRequest.Headers.Add("Idempotency-Key", $"restartable-hitl-{scenario}");
        startRequest.Content = JsonContent.Create(
            new WorkflowRunStartApiRequest
            {
                VersionId = published.VersionId.Value,
                InputJson = inputJson,
                RequestedBackend = WorkflowRuntimeBackendKind.InProcess
            });
        using var startResponse = await host.Client.SendAsync(startRequest);
        return await ReadSuccessAsync<WorkflowRunStartApiResponse>(
            startResponse,
            responseBodies);
    }

    private static async Task<HttpResponseMessage> SubmitAsync(
        HttpClient client,
        Guid requestId,
        long expectedVersion,
        string idempotencyKey,
        string responseJson)
    {
        using var request = new HttpRequestMessage(
            HttpMethod.Post,
            $"/api/workflows/external-requests/{requestId:D}/response")
        {
            Content = new StringContent(
                $"{{\"expectedRequestVersion\":{expectedVersion},\"response\":{responseJson}}}",
                Encoding.UTF8,
                "application/json")
        };
        request.Headers.Add("Idempotency-Key", idempotencyKey);
        return await client.SendAsync(request);
    }

    private static async Task<T> ReadSuccessAsync<T>(
        HttpResponseMessage response,
        ICollection<string>? responseBodies = null)
    {
        string body = await response.Content.ReadAsStringAsync();
        responseBodies?.Add(body);
        Assert.True(
            response.IsSuccessStatusCode,
            $"Expected a successful {typeof(T).Name} response; received {response.StatusCode}.");
        return JsonSerializer.Deserialize<T>(body, JsonOptions())
            ?? throw new InvalidOperationException($"The API response did not contain {typeof(T).Name}.");
    }

    private static async Task<T> ReadAsync<T>(
        HttpResponseMessage response,
        ICollection<string>? responseBodies = null)
    {
        string body = await response.Content.ReadAsStringAsync();
        responseBodies?.Add(body);
        return JsonSerializer.Deserialize<T>(body, JsonOptions())
            ?? throw new InvalidOperationException($"The API response did not contain {typeof(T).Name}.");
    }

    private static async Task<IReadOnlyList<WorkflowExternalResponseOperationEntity>> LoadOperationsAsync(
        ApiTestHost host,
        Guid requestId)
    {
        var factory = host.App.Services.GetRequiredService<IDbContextFactory<AppDbContext>>();
        await using var dbContext = await factory.CreateDbContextAsync();
        return await dbContext.Set<WorkflowExternalResponseOperationEntity>()
            .AsNoTracking()
            .Where(operation => operation.RequestId == requestId)
            .OrderBy(operation => operation.AcceptedAtUtc)
            .ToArrayAsync();
    }

    private static async Task MutateRecoveryStateAsync(
        ApiTestHost host,
        Guid requestId,
        RecoveryCorruption corruption)
    {
        var factory = host.App.Services.GetRequiredService<IDbContextFactory<AppDbContext>>();
        await using var dbContext = await factory.CreateDbContextAsync();
        var boundaryEntity = await dbContext.Set<WorkflowExternalRequestBoundaryEntity>()
            .SingleAsync(boundary => boundary.RequestId == requestId);
        var boundary = PersistentWorkflowExternalRequestBoundaryStore.ToRecord(boundaryEntity);

        switch (corruption)
        {
            case RecoveryCorruption.MissingCheckpoint:
                PersistentWorkflowExternalRequestBoundaryStore.Apply(
                    boundaryEntity,
                    boundary with
                    {
                        Continuation = boundary.Continuation with
                        {
                            Checkpoint = boundary.Continuation.Checkpoint with
                            {
                                CheckpointId = WorkflowBackendCheckpointId.New()
                            }
                        }
                    });
                break;

            case RecoveryCorruption.CorruptCheckpoint:
                var checkpoint = await dbContext.Set<WorkflowBackendCheckpointPayloadEntity>()
                    .SingleAsync(payload =>
                        payload.Id == boundary.Continuation.Checkpoint.CheckpointId.Value);
                checkpoint.ProtectedPayload = $"tampered-checkpoint-{Guid.NewGuid():N}";
                break;

            case RecoveryCorruption.TopologyMismatch:
                PersistentWorkflowExternalRequestBoundaryStore.Apply(
                    boundaryEntity,
                    boundary with
                    {
                        Continuation = boundary.Continuation with
                        {
                            TopologyFingerprint = WorkflowTopologyFingerprint.Create(
                                $"mutated-topology-{Guid.NewGuid():N}")
                        }
                    });
                break;

            case RecoveryCorruption.WorkflowVersionMismatch:
                var session = await dbContext.Set<WorkflowBackendCheckpointSessionEntity>()
                    .SingleAsync(candidate =>
                        candidate.Id == boundary.Continuation.Checkpoint.SessionId.Value);
                session.WorkflowVersionId = WorkflowVersionId.New().Value;
                break;

            default:
                throw new ArgumentOutOfRangeException(nameof(corruption), corruption, null);
        }

        await dbContext.SaveChangesAsync();
    }

    private static async Task DeleteBoundaryAsync(ApiTestHost host, Guid requestId)
    {
        var factory = host.App.Services.GetRequiredService<IDbContextFactory<AppDbContext>>();
        await using var dbContext = await factory.CreateDbContextAsync();
        var boundary = await dbContext.Set<WorkflowExternalRequestBoundaryEntity>()
            .SingleAsync(candidate => candidate.RequestId == requestId);
        dbContext.Remove(boundary);
        await dbContext.SaveChangesAsync();
    }

    private static async Task AssertNativeCheckpointContainsSentinelAsync(
        ApiTestHost host,
        Guid requestId,
        string sentinel)
    {
        var factory = host.App.Services.GetRequiredService<IDbContextFactory<AppDbContext>>();
        WorkflowBackendCheckpointLink checkpointLink;
        await using (var dbContext = await factory.CreateDbContextAsync())
        {
            var boundaryEntity = await dbContext.Set<WorkflowExternalRequestBoundaryEntity>()
                .AsNoTracking()
                .SingleAsync(candidate => candidate.RequestId == requestId);
            checkpointLink = PersistentWorkflowExternalRequestBoundaryStore
                .ToRecord(boundaryEntity)
                .Continuation
                .Checkpoint;
            var checkpointEntity = await dbContext.Set<WorkflowBackendCheckpointPayloadEntity>()
                .AsNoTracking()
                .SingleAsync(candidate => candidate.Id == checkpointLink.CheckpointId.Value);
            bool plaintextInProtectedStorage = checkpointEntity.ProtectedPayload.Contains(
                sentinel,
                StringComparison.Ordinal);
            Assert.False(
                plaintextInProtectedStorage,
                "Protected native checkpoint storage exposed the plaintext sentinel category.");
        }

        await using var scope = host.App.Services.CreateAsyncScope();
        var checkpointStore = scope.ServiceProvider
            .GetRequiredService<IWorkflowBackendCheckpointPayloadStore>();
        var read = await checkpointStore.ReadAsync(checkpointLink);
        Assert.Equal(WorkflowBackendCheckpointReadOutcome.Found, read.Outcome);
        Assert.NotNull(read.Checkpoint);
        bool sentinelPresent = read.Checkpoint.Payload.Json.Contains(
            sentinel,
            StringComparison.Ordinal);
        Assert.True(
            sentinelPresent,
            "Decrypted native checkpoint state did not contain the expected sentinel category.");
    }

    private static async Task<IReadOnlyList<ProjectionSecretGuard>> LoadNativeCheckpointSecretsAsync(
        ApiTestHost host,
        Guid requestId,
        IReadOnlySet<string> explicitlyPublicIds)
    {
        var factory = host.App.Services.GetRequiredService<IDbContextFactory<AppDbContext>>();
        await using var dbContext = await factory.CreateDbContextAsync();
        var boundaryEntity = await dbContext.Set<WorkflowExternalRequestBoundaryEntity>()
            .AsNoTracking()
            .SingleAsync(candidate => candidate.RequestId == requestId);
        var boundary = PersistentWorkflowExternalRequestBoundaryStore.ToRecord(boundaryEntity);
        var checkpoint = await dbContext.Set<WorkflowBackendCheckpointPayloadEntity>()
            .AsNoTracking()
            .SingleAsync(candidate =>
                candidate.Id == boundary.Continuation.Checkpoint.CheckpointId.Value);

        return
        [
            ProjectionSecretGuard.Everywhere(
                ProjectionSecretCategory.ContinuationValue,
                boundaryEntity.ContinuationJson,
                ProjectionValueClassification.Internal),
            NativeGuard(
                ProjectionSecretCategory.BackendCheckpointId,
                boundary.Continuation.Checkpoint.CheckpointId.Value,
                explicitlyPublicIds),
            NativeGuard(
                ProjectionSecretCategory.BackendRequestId,
                boundary.Continuation.Request.BackendRequestId.Value,
                explicitlyPublicIds),
            NativeGuard(
                ProjectionSecretCategory.BackendPortId,
                boundary.Continuation.Request.BackendRequestPortId.Value,
                explicitlyPublicIds),
            NativeGuard(
                ProjectionSecretCategory.CheckpointPayloadHash,
                boundary.Continuation.CheckpointPayloadHash.Value,
                explicitlyPublicIds),
            ProjectionSecretGuard.Everywhere(
                ProjectionSecretCategory.ProtectedCheckpointCiphertext,
                checkpoint.ProtectedPayload,
                ProjectionValueClassification.Internal),
            NativeGuard(
                ProjectionSecretCategory.StoredPayloadHash,
                checkpoint.PayloadHash,
                explicitlyPublicIds),
            NativeGuard(
                ProjectionSecretCategory.SessionId,
                boundary.Continuation.Checkpoint.SessionId.Value,
                explicitlyPublicIds)
        ];
    }

    private static ProjectionSecretGuard NativeGuard(
        ProjectionSecretCategory category,
        string value,
        IReadOnlySet<string> explicitlyPublicIds)
        => ProjectionSecretGuard.Everywhere(
            category,
            value,
            IsExplicitlyPublicIdentifier(value, explicitlyPublicIds)
                ? ProjectionValueClassification.PublicAlias
                : ProjectionValueClassification.Internal);

    private static bool IsExplicitlyPublicIdentifier(
        string candidate,
        IReadOnlySet<string> explicitlyPublicIds)
        => explicitlyPublicIds.Contains(candidate);

    private static WorkflowGraph CreateConsecutiveHumanInputGraph()
        => new(
            new WorkflowNodeId("start"),
            [
                CreateNode("start", WorkflowNodeKind.Start, resultShape: JsonObjectShape),
                CreateExecutorNode("prefix", PrefixMarkerExecutor.TestDescriptor),
                CreateNode(
                    "human-one",
                    WorkflowNodeKind.HumanInput,
                    inputShape: JsonObjectShape,
                    resultShape: JsonObjectShape),
                CreateNode(
                    "human-two",
                    WorkflowNodeKind.HumanInput,
                    inputShape: JsonObjectShape,
                    resultShape: JsonObjectShape),
                CreateNode("end", WorkflowNodeKind.End, inputShape: JsonObjectShape)
            ],
            [
                CreateEdge("start-prefix", "start", "prefix"),
                CreateEdge("prefix-human-one", "prefix", "human-one"),
                CreateEdge("human-one-human-two", "human-one", "human-two"),
                CreateEdge("human-two-end", "human-two", "end")
            ]);

    private static WorkflowGraph CreateApprovalGraph()
        => new(
            new WorkflowNodeId("start"),
            [
                CreateNode("start", WorkflowNodeKind.Start, resultShape: JsonObjectShape),
                CreateExecutorNode("approval-effect", ApprovalEffectExecutor.TestDescriptor),
                CreateNode(
                    "end",
                    WorkflowNodeKind.End,
                    inputShape: ApprovalEffectExecutor.TestDescriptor.ResultShape)
            ],
            [
                CreateEdge("start-approval", "start", "approval-effect"),
                CreateEdge("approval-end", "approval-effect", "end")
            ]);

    private static WorkflowGraph CreatePrefixedApprovalGraph()
        => new(
            new WorkflowNodeId("start"),
            [
                CreateNode("start", WorkflowNodeKind.Start, resultShape: JsonObjectShape),
                CreateExecutorNode("prefix", PrefixMarkerExecutor.TestDescriptor),
                CreateExecutorNode("approval-effect", ApprovalEffectExecutor.TestDescriptor),
                CreateNode(
                    "end",
                    WorkflowNodeKind.End,
                    inputShape: ApprovalEffectExecutor.TestDescriptor.ResultShape)
            ],
            [
                CreateEdge("start-prefix", "start", "prefix"),
                CreateEdge("prefix-approval", "prefix", "approval-effect"),
                CreateEdge("approval-end", "approval-effect", "end")
            ]);

    private static WorkflowNode CreateExecutorNode(
        string id,
        WorkflowExecutorDescriptor descriptor)
        => new(
            new WorkflowNodeId(id),
            WorkflowNodeKind.Executor,
            id,
            [],
            new WorkflowNodeSettings(
                ComponentId: null,
                AgentId: null,
                SubworkflowId: null,
                ExternalRequestKind: null,
                Instructions: string.Empty,
                InputShape: descriptor.InputShape,
                ResultShape: descriptor.ResultShape)
            {
                ExecutorId = descriptor.Id,
                ExecutorSettingsJson = "{}",
                ExecutionPolicy = WorkflowExecutorExecutionPolicy.Default
            });

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
                ExternalRequestKind: kind == WorkflowNodeKind.HumanInput
                    ? WorkflowExternalRequestKind.HumanInput
                    : null,
                Instructions: kind == WorkflowNodeKind.HumanInput
                    ? "Provide the reviewed JSON payload."
                    : string.Empty,
                InputShape: inputShape ?? WorkflowValueShape.Text,
                ResultShape: resultShape ?? WorkflowValueShape.Text));

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

    private static JsonSerializerOptions JsonOptions()
        => new(JsonSerializerDefaults.Web)
        {
            PropertyNameCaseInsensitive = true
        };

    private sealed class RestartableHitlFixture : IAsyncDisposable
    {
        private readonly CanDoItAllTestEnvironment testEnvironment;
        private readonly TestDatabaseProfile activeProfile;
        private readonly ManualTimeProvider? clock;

        private RestartableHitlFixture(
            string scenario,
            ManualTimeProvider? clock,
            bool captureLogs)
        {
            testEnvironment = CanDoItAllTestEnvironment.Create($"workflow-hitl-e2e-{scenario}");
            activeProfile = testEnvironment.CreatePostgreSqlProfile("api-host");
            this.clock = clock;
            LogProvider = captureLogs ? new RecordingLoggerProvider() : null;
        }

        public WorkflowHitlProbe Probe { get; } = new();

        public RecordingLoggerProvider? LogProvider { get; }

        public static RestartableHitlFixture Create(
            string scenario,
            ManualTimeProvider? clock = null,
            bool captureLogs = false)
            => new(scenario, clock, captureLogs);

        public Task<ApiTestHost> CreateHostAsync(
            WorkflowExternalResponseRecoveryHook? recoveryHook = null)
            => ApiTestHost.CreateAsync(
                jwtEnabled: true,
                configureServices: services =>
                {
                    services.RemoveAll<WorkflowExternalResponseRecoveryHook>();
                    if (recoveryHook is not null)
                    {
                        services.AddSingleton(recoveryHook);
                    }

                    if (clock is not null)
                    {
                        services.RemoveAll<TimeProvider>();
                        services.AddSingleton<TimeProvider>(clock);
                    }

                    if (LogProvider is not null)
                    {
                        services.RemoveAll<ILoggerProvider>();
                        services.AddSingleton<ILoggerProvider>(LogProvider);
                    }

                    services.AddSingleton(Probe);
                    services.AddWorkflowExecutorContribution<PrefixMarkerExecutor>(
                        PrefixMarkerExecutor.TestDescriptor,
                        ServiceLifetime.Singleton);
                    services.AddWorkflowExecutorContribution<ApprovalEffectExecutor>(
                        ApprovalEffectExecutor.TestDescriptor,
                        ServiceLifetime.Singleton);
                },
                useInMemoryDatabase: false,
                sharedTestEnvironment: testEnvironment,
                sharedActiveProfile: activeProfile);

        public ValueTask DisposeAsync() => testEnvironment.DisposeAsync();
    }

    public enum RecoveryCorruption
    {
        MissingCheckpoint,
        CorruptCheckpoint,
        TopologyMismatch,
        WorkflowVersionMismatch
    }

    private enum ProjectionSecretCategory
    {
        InputPayloadSentinel,
        ResponsePayloadSentinel,
        BearerToken,
        DatabaseConnectionString,
        ContinuationValue,
        BackendCheckpointId,
        BackendRequestId,
        BackendPortId,
        CheckpointPayloadHash,
        ProtectedCheckpointCiphertext,
        StoredPayloadHash,
        SessionId,
        NativeCheckpointUri,
        ProtectedPayloadField,
        ProtectedResponsePayloadField,
        ContinuationField
    }

    [Flags]
    private enum ProjectionInspectionSurface
    {
        HttpResponse = 1,
        ApplicationLog = 2,
        Everywhere = HttpResponse | ApplicationLog
    }

    private enum ProjectionValueClassification
    {
        Sensitive,
        Internal,
        PublicAlias,
        StructuralName
    }

    private readonly record struct ProjectionSecretGuard(
        ProjectionSecretCategory Category,
        string Value,
        ProjectionInspectionSurface Surfaces,
        ProjectionValueClassification Classification)
    {
        public static ProjectionSecretGuard Everywhere(
            ProjectionSecretCategory category,
            string value,
            ProjectionValueClassification classification =
                ProjectionValueClassification.Sensitive)
            => new(
                category,
                value,
                ProjectionInspectionSurface.Everywhere,
                classification);

        public static ProjectionSecretGuard HttpOnly(
            ProjectionSecretCategory category,
            string value)
            => new(
                category,
                value,
                ProjectionInspectionSurface.HttpResponse,
                ProjectionValueClassification.StructuralName);
    }

    private sealed class BlockingRecoveryHook(WorkflowExternalResponseRecoveryPoint target)
    {
        private readonly TaskCompletionSource entered = new(
            TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly TaskCompletionSource released = new(
            TaskCreationOptions.RunContinuationsAsynchronously);
        private int pendingBlock = 1;

        public async ValueTask InvokeAsync(
            WorkflowExternalResponseRecoveryPoint point,
            WorkflowExternalResponseOperationId operationId,
            CancellationToken cancellationToken)
        {
            _ = operationId;
            if (point != target || Interlocked.Exchange(ref pendingBlock, 0) == 0)
            {
                return;
            }

            entered.TrySetResult();
            await released.Task.WaitAsync(cancellationToken);
        }

        public Task WaitUntilEnteredAsync()
            => entered.Task.WaitAsync(TimeSpan.FromSeconds(15));

        public void Release() => released.TrySetResult();
    }

    private sealed class CrashOnceRecoveryHook(WorkflowExternalResponseRecoveryPoint target)
    {
        private int armed = 1;

        public ValueTask InvokeAsync(
            WorkflowExternalResponseRecoveryPoint point,
            WorkflowExternalResponseOperationId operationId,
            CancellationToken cancellationToken)
        {
            _ = operationId;
            cancellationToken.ThrowIfCancellationRequested();
            if (point == target && Interlocked.Exchange(ref armed, 0) == 1)
            {
                throw new InvalidOperationException($"Injected workflow HITL crash at {point}.");
            }

            return ValueTask.CompletedTask;
        }
    }

    private sealed class ManualTimeProvider : TimeProvider
    {
        private readonly object gate = new();
        private DateTimeOffset utcNow;

        public ManualTimeProvider(DateTimeOffset utcNow)
        {
            this.utcNow = utcNow.ToUniversalTime();
        }

        public override DateTimeOffset GetUtcNow()
        {
            lock (gate)
            {
                return utcNow;
            }
        }

        public void SetUtcNow(DateTimeOffset value)
        {
            value = value.ToUniversalTime();
            lock (gate)
            {
                if (value < utcNow)
                {
                    throw new ArgumentOutOfRangeException(
                        nameof(value),
                        "The manual recovery clock cannot move backwards.");
                }

                utcNow = value;
            }
        }

        public void Advance(TimeSpan elapsed)
        {
            if (elapsed <= TimeSpan.Zero)
            {
                throw new ArgumentOutOfRangeException(nameof(elapsed));
            }

            lock (gate)
            {
                utcNow = utcNow.Add(elapsed);
            }
        }
    }

    private sealed class RecordingLoggerProvider : ILoggerProvider
    {
        private readonly ConcurrentQueue<string> entries = new();

        public IReadOnlyList<string> Entries => entries.ToArray();

        public ILogger CreateLogger(string categoryName)
            => new RecordingLogger(categoryName, entries);

        public void Dispose()
        {
        }

        private sealed class RecordingLogger(
            string categoryName,
            ConcurrentQueue<string> entries) : ILogger
        {
            public IDisposable? BeginScope<TState>(TState state)
                where TState : notnull
                => null;

            public bool IsEnabled(LogLevel logLevel) => true;

            public void Log<TState>(
                LogLevel logLevel,
                EventId eventId,
                TState state,
                Exception? exception,
                Func<TState, Exception?, string> formatter)
            {
                _ = eventId;
                entries.Enqueue(
                    $"{categoryName}|{logLevel}|{formatter(state, exception)}|{exception}");
            }
        }
    }

    private sealed class WorkflowHitlProbe
    {
        private readonly ConcurrentDictionary<string, byte> appliedEffectKeys = new(StringComparer.Ordinal);
        private int prefixInvocationCount;
        private int approvalExecutorInvocationCount;
        private int appliedEffectCount;

        public int PrefixInvocationCount => Volatile.Read(ref prefixInvocationCount);

        public int ApprovalExecutorInvocationCount =>
            Volatile.Read(ref approvalExecutorInvocationCount);

        public int AppliedEffectCount => Volatile.Read(ref appliedEffectCount);

        public void RecordPrefixInvocation() => Interlocked.Increment(ref prefixInvocationCount);

        public void RecordApprovalInvocation(WorkflowExecutorInvocationIdempotencyKey key)
        {
            Interlocked.Increment(ref approvalExecutorInvocationCount);
            if (appliedEffectKeys.TryAdd(key.Value, 0))
            {
                Interlocked.Increment(ref appliedEffectCount);
            }
        }
    }

    private sealed class PrefixMarkerExecutor(WorkflowHitlProbe probe) : IWorkflowExecutor
    {
        public static WorkflowExecutorDescriptor TestDescriptor { get; } =
            BuiltInWorkflowExecutorDescriptors.JsonTransform with
            {
                Id = new WorkflowExecutorId("test.hitl-e2e-prefix"),
                Name = "HITL E2E prefix marker",
                InputShape = JsonObjectShape,
                ResultShape = JsonObjectShape,
                PermissionPolicy = new WorkflowExecutorPermissionPolicy(
                    WorkflowExecutorCapabilityFlags.SupportsDeterministicTestMode,
                    WorkflowExecutorApprovalRequirement.NotRequired)
            };

        public WorkflowExecutorDescriptor Descriptor => TestDescriptor;

        public ValueTask<WorkflowNodeExecutionResult> ExecuteAsync(
            WorkflowExecutorExecutionContext context,
            WorkflowNodeInput input,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            probe.RecordPrefixInvocation();
            return ValueTask.FromResult(new WorkflowNodeExecutionResult(
                context.Node.Id,
                input.PayloadJson,
                context.Descriptor.ResultShape));
        }
    }

    private sealed class ApprovalEffectExecutor(WorkflowHitlProbe probe) : IWorkflowExecutor
    {
        public static WorkflowExecutorDescriptor TestDescriptor { get; } =
            BuiltInWorkflowExecutorDescriptors.JsonTransform with
            {
                Id = new WorkflowExecutorId("test.hitl-e2e-approval-effect"),
                Name = "HITL E2E approval effect",
                InputShape = JsonObjectShape,
                ResultShape = JsonObjectShape,
                PermissionPolicy = new WorkflowExecutorPermissionPolicy(
                    WorkflowExecutorCapabilityFlags.WritesExternalData |
                    WorkflowExecutorCapabilityFlags.IdempotentExternalMarker,
                    WorkflowExecutorApprovalRequirement.AlwaysRequired),
                SideEffects = WorkflowExecutorSideEffectDescriptor.IdempotentProcessedMarker(
                    "$.idempotencyKey",
                    """{"type":"object"}""")
            };

        public WorkflowExecutorDescriptor Descriptor => TestDescriptor;

        public ValueTask<WorkflowNodeExecutionResult> ExecuteAsync(
            WorkflowExecutorExecutionContext context,
            WorkflowNodeInput input,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var idempotencyKey = context.IdempotencyKey ?? throw new InvalidOperationException(
                "The governed E2E effect requires a propagated idempotency key.");
            probe.RecordApprovalInvocation(idempotencyKey);
            return ValueTask.FromResult(new WorkflowNodeExecutionResult(
                context.Node.Id,
                """{"effect":"applied"}""",
                context.Descriptor.ResultShape));
        }
    }
}
