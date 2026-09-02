using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.Workflows.Abstractions;
using CanDoItAll.Composition;
using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.Modules.AgentFramework;
using CanDoItAll.Tests.Support;
using Microsoft.EntityFrameworkCore;

namespace CanDoItAll.Tests.Integration.AgentFramework;

public sealed class WorkflowLaunchIdempotencyPersistenceIntegrationTests
{
    private static readonly DateTimeOffset ClaimedAtUtc = new(2026, 7, 12, 22, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task PostgreSql_ConcurrentClaimsAndReleaseReclaim_KeepOneAtomicScope()
    {
        var fixture = await CreateFixtureAsync("workflowlaunchidempotencyclaims");
        await using var database = fixture.Database;
        var scope = CreateScope();
        var fingerprint = new WorkflowLaunchRequestFingerprint(new string('A', 64));
        var attempts = Enumerable.Range(0, 8)
            .Select(_ => new ClaimAttempt(
                WorkflowLaunchIdempotencyClaimToken.New(),
                WorkflowRunId.New()))
            .ToArray();

        var results = await Task.WhenAll(attempts.Select(async attempt => new
        {
            Attempt = attempt,
            Result = await fixture.Store.TryClaimAsync(
                scope,
                fingerprint,
                attempt.Token,
                attempt.ProposedRunId,
                ClaimedAtUtc,
                ClaimedAtUtc.AddMinutes(5))
        }));

        var owner = Assert.Single(results, item =>
            item.Result.Outcome == WorkflowLaunchIdempotencyClaimOutcome.Acquired);
        Assert.All(results, item => Assert.Equal(owner.Result.ReservedRunId, item.Result.ReservedRunId));
        Assert.True(await fixture.Store.TryReleaseClaimAsync(scope, owner.Attempt.Token));

        for (var iteration = 0; iteration < 12; iteration++)
        {
            var currentOwner = WorkflowLaunchIdempotencyClaimToken.New();
            var current = await fixture.Store.TryClaimAsync(
                scope,
                fingerprint,
                currentOwner,
                WorkflowRunId.New(),
                ClaimedAtUtc.AddMinutes(iteration + 1),
                ClaimedAtUtc.AddMinutes(iteration + 6));
            Assert.Equal(WorkflowLaunchIdempotencyClaimOutcome.Acquired, current.Outcome);

            var contenders = Enumerable.Range(0, 4)
                .Select(_ => new ClaimAttempt(
                    WorkflowLaunchIdempotencyClaimToken.New(),
                    WorkflowRunId.New()))
                .ToArray();
            var release = fixture.Store.TryReleaseClaimAsync(scope, currentOwner);
            var claimTasks = contenders.Select(async attempt => new
                {
                    Attempt = attempt,
                    Result = await fixture.Store.TryClaimAsync(
                        scope,
                        fingerprint,
                        attempt.Token,
                        attempt.ProposedRunId,
                        ClaimedAtUtc.AddMinutes(iteration + 1),
                        ClaimedAtUtc.AddMinutes(iteration + 6))
                })
                .ToArray();
            await Task.WhenAll([release, .. claimTasks]);
            Assert.True(await release);
            var contenderResults = await Task.WhenAll(claimTasks);
            var contenderOwner = contenderResults.SingleOrDefault(item =>
                item.Result.Outcome == WorkflowLaunchIdempotencyClaimOutcome.Acquired);
            if (contenderOwner is not null)
            {
                Assert.True(await fixture.Store.TryReleaseClaimAsync(
                    scope,
                    contenderOwner.Attempt.Token));
                continue;
            }

            var cleanupOwner = WorkflowLaunchIdempotencyClaimToken.New();
            var cleanup = await fixture.Store.TryClaimAsync(
                scope,
                fingerprint,
                cleanupOwner,
                WorkflowRunId.New(),
                ClaimedAtUtc.AddMinutes(iteration + 7),
                ClaimedAtUtc.AddMinutes(iteration + 12));
            Assert.Equal(WorkflowLaunchIdempotencyClaimOutcome.Acquired, cleanup.Outcome);
            Assert.True(await fixture.Store.TryReleaseClaimAsync(scope, cleanupOwner));
        }

        var finalToken = WorkflowLaunchIdempotencyClaimToken.New();
        var finalClaim = await fixture.Store.TryClaimAsync(
            scope,
            fingerprint,
            finalToken,
            WorkflowRunId.New(),
            ClaimedAtUtc.AddHours(1),
            ClaimedAtUtc.AddHours(2));
        Assert.Equal(WorkflowLaunchIdempotencyClaimOutcome.Acquired, finalClaim.Outcome);
        await Assert.ThrowsAsync<WorkflowLaunchIdempotencyConflictException>(() =>
            fixture.Store.TryClaimAsync(
                scope,
                new WorkflowLaunchRequestFingerprint(new string('B', 64)),
                WorkflowLaunchIdempotencyClaimToken.New(),
                WorkflowRunId.New(),
                ClaimedAtUtc.AddHours(1),
                ClaimedAtUtc.AddHours(2)));
    }

    [Fact]
    public async Task PostgreSql_ExpiredClaimAfterAcceptedRun_ReusesReservedIdentityWithoutBackendInvocation()
    {
        var fixture = await CreateFixtureAsync("workflowlaunchidempotencycrash");
        await using var database = fixture.Database;
        var scope = CreateScope();
        var fingerprint = new WorkflowLaunchRequestFingerprint(new string('C', 64));
        var firstToken = WorkflowLaunchIdempotencyClaimToken.New();
        var proposedRunId = WorkflowRunId.New();
        var first = await fixture.Store.TryClaimAsync(
            scope,
            fingerprint,
            firstToken,
            proposedRunId,
            ClaimedAtUtc,
            ClaimedAtUtc.AddSeconds(1));
        var reservedRunId = Assert.IsType<WorkflowRunId>(first.ReservedRunId);
        var definition = CreateDefinition(scope.WorkflowId, scope.RequestedVersionId!.Value);
        var origin = CreateOrigin();
        var running = new WorkflowRunSnapshot(
            reservedRunId,
            definition.Id,
            definition.VersionId,
            WorkflowRunState.Running,
            WorkflowRuntimeBackendKind.InProcess,
            reservedRunId.ToString(),
            "Accepted before process interruption.",
            ClaimedAtUtc,
            ClaimedAtUtc)
        {
            Origin = origin
        };
        var runStore = new PersistentWorkflowRunStore(fixture.Factory);
        await runStore.CreateRunWithStartedEventAsync(
            running,
            new WorkflowEventRecord(
                Guid.NewGuid(),
                reservedRunId,
                WorkflowEventKind.Started,
                NodeId: null,
                "Started",
                "{}",
                ClaimedAtUtc));

        var takeoverToken = WorkflowLaunchIdempotencyClaimToken.New();
        var takeover = await fixture.Store.TryClaimAsync(
            scope,
            fingerprint,
            takeoverToken,
            WorkflowRunId.New(),
            ClaimedAtUtc.AddSeconds(2),
            ClaimedAtUtc.AddMinutes(5));
        Assert.Equal(WorkflowLaunchIdempotencyClaimOutcome.Acquired, takeover.Outcome);
        Assert.Equal(reservedRunId, takeover.ReservedRunId);

        var backend = new RejectingWorkflowBackend();
        var runtime = WorkflowRuntimeManager.CreateInMemory([backend], runStore);
        var recovered = await runtime.StartAsync(
            definition,
            new WorkflowRunStartRequest(
                definition.Id,
                definition.VersionId,
                "{}",
                WorkflowRuntimeBackendKind.InProcess,
                SourceProcessRunId: null,
                SourceProcessAssignmentId: null)
            {
                Origin = origin,
                Idempotency = new WorkflowLaunchIdempotency.CallerSupplied(scope.CallerKey),
                RequestedRunId = reservedRunId
            });

        Assert.Equal(reservedRunId, recovered.RunId);
        Assert.Equal(0, backend.InvocationCount);
        var backendDescriptor = new WorkflowRuntimeBackendCatalog([WorkflowRuntimeBackendKind.InProcess])
            .GetRequiredBackend(WorkflowRuntimeBackendKind.InProcess);
        var resolvedRequest = new WorkflowResolvedRuntimeRequest(
            definition,
            "{}",
            backendDescriptor,
            WorkflowPreviewSimulationPlan.Empty,
            WorkflowLaunchMode.Production,
            origin,
            WorkflowLaunchCompletionPolicy.WaitForStopped,
            new WorkflowLaunchIdempotency.CallerSupplied(scope.CallerKey),
            ClaimedAtUtc)
        {
            RequestedRunId = reservedRunId
        };
        Assert.True(await fixture.Store.TryCompleteClaimAsync(
            scope,
            takeoverToken,
            new WorkflowLaunchIdempotencyCompletion(recovered, resolvedRequest, ClaimedAtUtc.AddSeconds(2))));

        var replay = await fixture.Store.TryClaimAsync(
            scope,
            fingerprint,
            WorkflowLaunchIdempotencyClaimToken.New(),
            WorkflowRunId.New(),
            ClaimedAtUtc.AddSeconds(3),
            ClaimedAtUtc.AddMinutes(5));
        Assert.Equal(WorkflowLaunchIdempotencyClaimOutcome.Completed, replay.Outcome);
        Assert.Equal(reservedRunId, replay.Completion?.Run.RunId);
    }

    private static async Task<PersistenceFixture> CreateFixtureAsync(string databaseName)
    {
        AppDbContextModelRegistry.ConfigureAssemblies(ModuleAssemblies.All);
        var database = PostgresTestDatabaseLease.Create(databaseName);
        var factory = new WorkflowUsagePostgresDbContextFactory(database.CreateAppDbContextOptions());
        await using (var dbContext = factory.CreateDbContext())
        {
            await dbContext.Database.EnsureCreatedAsync();
        }

        return new PersistenceFixture(
            database,
            factory,
            new PersistentWorkflowLaunchIdempotencyStore(factory));
    }

    private static WorkflowLaunchIdempotencyScope CreateScope()
    {
        var workflowId = WorkflowId.New();
        return new WorkflowLaunchIdempotencyScope(
            new WorkflowLaunchIdempotencyKey("postgres-concurrent-launch"),
            workflowId,
            WorkflowDefinitionSelectionKind.ExactSavedVersion,
            WorkflowVersionId.New(),
            WorkflowLaunchMode.Production,
            WorkflowLaunchOriginKind.Api,
            new WorkflowLaunchOriginScopeKey(new string('D', 64)));
    }

    private static WorkflowLaunchOrigin CreateOrigin() => new WorkflowLaunchOrigin.Api(
        new WorkflowLaunchActor(WorkflowLaunchActorKind.Service, "postgres-idempotency-test"),
        new WorkflowLaunchCorrelationId("postgres-idempotency-correlation"));

    private static WorkflowDefinition CreateDefinition(
        WorkflowId workflowId,
        WorkflowVersionId versionId)
    {
        var startNodeId = new WorkflowNodeId("start");
        return new WorkflowDefinition(
            workflowId,
            versionId,
            "PostgreSQL idempotency",
            "Crash-safe workflow launch identity.",
            WorkflowLifecycleStatus.Active,
            new WorkflowGraph(
                startNodeId,
                [
                    new WorkflowNode(
                        startNodeId,
                        WorkflowNodeKind.Start,
                        "Start",
                        [],
                        new WorkflowNodeSettings(
                            ComponentId: null,
                            AgentId: null,
                            SubworkflowId: null,
                            ExternalRequestKind: null,
                            Instructions: string.Empty,
                            InputShape: new WorkflowValueShape(WorkflowValueShapeKind.Json, "{}", "Input"),
                            ResultShape: new WorkflowValueShape(WorkflowValueShapeKind.Json, "{}", "Output")))
                ],
                []),
            new WorkflowRuntimePolicy(
                WorkflowRuntimeBackendKind.InProcess,
                AllowInProcessPreviewRuns: true,
                RequireDurableProductionRuns: false,
                ExposeAzureFunctionsStatusEndpoint: false,
                ExposeAzureFunctionsMcpTool: false),
            ClaimedAtUtc,
            ClaimedAtUtc);
    }

    private sealed record PersistenceFixture(
        PostgresTestDatabaseLease Database,
        WorkflowUsagePostgresDbContextFactory Factory,
        PersistentWorkflowLaunchIdempotencyStore Store);

    private sealed record ClaimAttempt(
        WorkflowLaunchIdempotencyClaimToken Token,
        WorkflowRunId ProposedRunId);

    private sealed class RejectingWorkflowBackend : IWorkflowExecutionBackend
    {
        public int InvocationCount { get; private set; }

        public WorkflowRuntimeBackendDescriptor Descriptor { get; } =
            new WorkflowRuntimeBackendCatalog([WorkflowRuntimeBackendKind.InProcess])
                .GetRequiredBackend(WorkflowRuntimeBackendKind.InProcess);

        public Task<WorkflowBackendStartResult> StartAsync(
            WorkflowDefinition definition,
            WorkflowRunStartRequest request,
            WorkflowRunId runId,
            CancellationToken cancellationToken = default)
        {
            InvocationCount++;
            throw new InvalidOperationException("Recovered runs must not invoke the backend again.");
        }
    }
}
