using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.Modules.AgentFramework;
using Microsoft.EntityFrameworkCore;

namespace CanDoItAll.Tests.Unit.AgentFramework;

public sealed class WorkflowLaunchIdempotencyInMemoryStoreTests
{
    private static readonly DateTimeOffset ClaimedAtUtc =
        new(2026, 8, 21, 19, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task StateMachineSupportsClaimRenewCompleteReplayReleaseAndExpiredTakeover()
    {
        var store = CreateStore();
        var releaseScope = CreateScope("in-memory-release");
        var releaseFingerprint = CreateFingerprint("release");
        var releaseToken = WorkflowLaunchIdempotencyClaimToken.New();
        var firstRunId = WorkflowRunId.New();

        var firstClaim = await store.TryClaimAsync(
            releaseScope,
            releaseFingerprint,
            releaseToken,
            firstRunId,
            ClaimedAtUtc,
            ClaimedAtUtc.AddMinutes(5));
        var competingClaim = await store.TryClaimAsync(
            releaseScope,
            releaseFingerprint,
            WorkflowLaunchIdempotencyClaimToken.New(),
            WorkflowRunId.New(),
            ClaimedAtUtc.AddSeconds(1),
            ClaimedAtUtc.AddMinutes(5));

        Assert.Equal(WorkflowLaunchIdempotencyClaimOutcome.Acquired, firstClaim.Outcome);
        Assert.Equal(WorkflowLaunchIdempotencyClaimOutcome.InProgress, competingClaim.Outcome);
        Assert.True(await store.TryRenewClaimAsync(
            releaseScope,
            releaseToken,
            ClaimedAtUtc.AddMinutes(6)));
        Assert.True(await store.TryReleaseClaimAsync(releaseScope, releaseToken));
        Assert.Null(await store.FindApiKeyAsync(releaseScope.CallerKey));

        var takeoverScope = CreateScope("in-memory-takeover");
        var takeoverFingerprint = CreateFingerprint("takeover");
        var expiredToken = WorkflowLaunchIdempotencyClaimToken.New();
        var reservedRunId = WorkflowRunId.New();
        await store.TryClaimAsync(
            takeoverScope,
            takeoverFingerprint,
            expiredToken,
            reservedRunId,
            ClaimedAtUtc,
            ClaimedAtUtc.AddSeconds(1));
        var takeoverToken = WorkflowLaunchIdempotencyClaimToken.New();
        var takeover = await store.TryClaimAsync(
            takeoverScope,
            takeoverFingerprint,
            takeoverToken,
            WorkflowRunId.New(),
            ClaimedAtUtc.AddSeconds(2),
            ClaimedAtUtc.AddMinutes(5));

        Assert.Equal(WorkflowLaunchIdempotencyClaimOutcome.Acquired, takeover.Outcome);
        Assert.Equal(reservedRunId, takeover.ReservedRunId);
        Assert.False(await store.TryReleaseClaimAsync(takeoverScope, expiredToken));

        var completion = CreateCompletion(takeoverScope, reservedRunId);
        Assert.True(await store.TryCompleteClaimAsync(takeoverScope, takeoverToken, completion));
        var replay = await store.TryClaimAsync(
            takeoverScope,
            takeoverFingerprint,
            WorkflowLaunchIdempotencyClaimToken.New(),
            WorkflowRunId.New(),
            ClaimedAtUtc.AddSeconds(3),
            ClaimedAtUtc.AddMinutes(5));
        var record = await store.FindApiKeyAsync(takeoverScope.CallerKey);

        Assert.Equal(WorkflowLaunchIdempotencyClaimOutcome.Completed, replay.Outcome);
        Assert.Equal(reservedRunId, replay.ReservedRunId);
        Assert.Equal(reservedRunId, replay.Completion?.Run.RunId);
        Assert.Equal(WorkflowLaunchIdempotencyRecordState.Completed, record?.State);
        Assert.Equal(1, record?.ReplayCount);
    }

    private static PersistentWorkflowLaunchIdempotencyStore CreateStore()
    {
        AppDbContextModelRegistry.ConfigureAssemblies([
            typeof(PersistentWorkflowLaunchIdempotencyStore).Assembly
        ]);
        var options = AppDbContextTestOptionsBuilder.Create()
            .UseInMemoryDatabase($"workflow-launch-idempotency-{Guid.NewGuid():N}")
            .Options;
        return new PersistentWorkflowLaunchIdempotencyStore(
            new WorkflowUsageTestDbContextFactory(options));
    }

    private static WorkflowLaunchIdempotencyScope CreateScope(string key)
        => new(
            new WorkflowLaunchIdempotencyKey(key),
            WorkflowId.New(),
            WorkflowDefinitionSelectionKind.ExactSavedVersion,
            WorkflowVersionId.New(),
            WorkflowLaunchMode.Production,
            WorkflowLaunchOriginKind.Api,
            new WorkflowLaunchOriginScopeKey(new string('A', 64)));

    private static WorkflowLaunchRequestFingerprint CreateFingerprint(string value)
        => new(new string(value[0], 64), new string(value[^1], 64));

    private static WorkflowLaunchIdempotencyCompletion CreateCompletion(
        WorkflowLaunchIdempotencyScope scope,
        WorkflowRunId runId)
    {
        var versionId = scope.RequestedVersionId
            ?? throw new InvalidOperationException("The test scope requires an exact version.");
        var startNodeId = new WorkflowNodeId("start");
        var definition = new WorkflowDefinition(
            scope.WorkflowId,
            versionId,
            "In-memory launch",
            "In-memory launch idempotency test.",
            WorkflowLifecycleStatus.Active,
            new WorkflowGraph(startNodeId, [], []),
            new WorkflowRuntimePolicy(
                WorkflowRuntimeBackendKind.InProcess,
                AllowInProcessPreviewRuns: true,
                RequireDurableProductionRuns: false,
                ExposeAzureFunctionsStatusEndpoint: false,
                ExposeAzureFunctionsMcpTool: false),
            ClaimedAtUtc,
            ClaimedAtUtc);
        var backend = new WorkflowRuntimeBackendDescriptor(
            WorkflowRuntimeBackendKind.InProcess,
            "In-process",
            IsDurable: false,
            SupportsStreaming: true,
            SupportsExternalRequests: true,
            SupportsDashboardObservability: true,
            OperationalNotes: string.Empty);
        var origin = new WorkflowLaunchOrigin.Api(
            new WorkflowLaunchActor(WorkflowLaunchActorKind.Service, "in-memory-test"),
            new WorkflowLaunchCorrelationId("in-memory-test"));
        var idempotency = new WorkflowLaunchIdempotency.CallerSupplied(scope.CallerKey);
        var resolved = new WorkflowResolvedRuntimeRequest(
            definition,
            "{}",
            backend,
            WorkflowPreviewSimulationPlan.Empty,
            WorkflowLaunchMode.Production,
            origin,
            WorkflowLaunchCompletionPolicy.WaitForStopped,
            idempotency,
            ClaimedAtUtc)
        {
            RequestedRunId = runId
        };
        var run = new WorkflowRunSnapshot(
            runId,
            scope.WorkflowId,
            versionId,
            WorkflowRunState.Running,
            WorkflowRuntimeBackendKind.InProcess,
            runId.ToString(),
            "Running",
            ClaimedAtUtc,
            ClaimedAtUtc)
        {
            Origin = origin
        };
        return new WorkflowLaunchIdempotencyCompletion(run, resolved, ClaimedAtUtc.AddSeconds(2));
    }
}
