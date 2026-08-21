using System.Security.Cryptography;
using System.Text;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Composition;
using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.Modules.AgentFramework;
using CanDoItAll.Tests.Support;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;

namespace CanDoItAll.Tests.Integration.AgentFramework;

public sealed class WorkflowHitlRecoveryExecutorDedupPersistenceIntegrationTests
{
    private static readonly DateTimeOffset TestTime =
        new(2026, 8, 20, 23, 0, 0, TimeSpan.Zero);

    private static readonly WorkflowValueShape JsonShape = new(
        WorkflowValueShapeKind.Json,
        "{}",
        "JSON");

    [Fact]
    public async Task PostgreSql_ExecutorDedup_EnforcesCasLeaseReplayAndProtectedResult()
    {
        await using var fixture = await CreateFixtureAsync("workflowhitlexecutordedup");
        var store = fixture.CreateStore();
        var identity = CreateIdentity("cas-replay");
        var owners = Enumerable.Range(0, 8)
            .Select(index => new WorkflowExecutorInvocationLeaseOwnerId($"executor-host-{index}"))
            .ToArray();
        var claims = await Task.WhenAll(owners.Select(owner => store.TryClaimAsync(
            new WorkflowExecutorInvocationClaimRequest(
                identity,
                owner,
                TestTime,
                TestTime.AddMinutes(1),
                MaximumAttempts: 3))));
        var winner = Assert.Single(
            claims,
            claim => claim.Outcome == WorkflowExecutorInvocationClaimOutcome.Claimed);
        Assert.All(
            claims.Where(claim => claim != winner),
            claim => Assert.Equal(WorkflowExecutorInvocationClaimOutcome.ActiveLease, claim.Outcome));

        var initialClaim = winner.Claim!;
        var renewed = await store.TryRenewLeaseAsync(
            new WorkflowExecutorInvocationLeaseRenewalRequest(
                identity.Key,
                initialClaim.ConcurrencyVersion,
                initialClaim.Lease.OwnerId,
                initialClaim.Lease.Epoch,
                TestTime.AddSeconds(5),
                TestTime.AddMinutes(2)));
        Assert.Equal(WorkflowExecutorInvocationMutationOutcome.Updated, renewed.Outcome);
        var claim = new WorkflowExecutorInvocationClaim(
            identity,
            renewed.Record!.Lease!,
            renewed.Record.Attempt,
            renewed.Record.ConcurrencyVersion);
        var storedResult = new WorkflowExecutorInvocationStoredResult(
            new WorkflowNodeExecutionResult(
                identity.NodeId,
                "{\"receipt\":\"executor-result-secret\"}",
                JsonShape),
            TestTime.AddSeconds(10));
        var staleOwner = await store.TryCompleteAsync(
            new WorkflowExecutorInvocationCompletionRequest(
                identity.Key,
                identity.InputHash,
                claim.ConcurrencyVersion,
                new WorkflowExecutorInvocationLeaseOwnerId("stale-owner"),
                claim.Lease.Epoch,
                storedResult));
        Assert.Equal(WorkflowExecutorInvocationMutationOutcome.LeaseConflict, staleOwner.Outcome);
        var staleVersion = await store.TryCompleteAsync(
            new WorkflowExecutorInvocationCompletionRequest(
                identity.Key,
                identity.InputHash,
                claim.ConcurrencyVersion.Next(),
                claim.Lease.OwnerId,
                claim.Lease.Epoch,
                storedResult));
        Assert.Equal(WorkflowExecutorInvocationMutationOutcome.ConcurrencyConflict, staleVersion.Outcome);
        var staleEpoch = await store.TryCompleteAsync(
            new WorkflowExecutorInvocationCompletionRequest(
                identity.Key,
                identity.InputHash,
                claim.ConcurrencyVersion,
                claim.Lease.OwnerId,
                claim.Lease.Epoch.Next(),
                storedResult));
        Assert.Equal(WorkflowExecutorInvocationMutationOutcome.LeaseConflict, staleEpoch.Outcome);

        var completed = await store.TryCompleteAsync(
            new WorkflowExecutorInvocationCompletionRequest(
                identity.Key,
                identity.InputHash,
                claim.ConcurrencyVersion,
                claim.Lease.OwnerId,
                claim.Lease.Epoch,
                storedResult));
        Assert.Equal(WorkflowExecutorInvocationMutationOutcome.Updated, completed.Outcome);
        Assert.Equal(WorkflowExecutorInvocationState.Completed, completed.Record!.State);
        Assert.Null(completed.Record.Lease);

        await using (var dbContext = fixture.Factory.CreateDbContext())
        {
            var entity = await dbContext.Set<WorkflowExecutorInvocationRecordEntity>()
                .AsNoTracking()
                .SingleAsync(item => item.InvocationKey == identity.Key.Value);
            Assert.DoesNotContain("executor-result-secret", entity.ProtectedStoredResult, StringComparison.Ordinal);
            Assert.Equal(64, entity.StoredResultHash.Length);
            Assert.Null(entity.LeaseOwnerId);
            Assert.Null(entity.LeaseAcquiredAtUtc);
            Assert.Null(entity.LeaseExpiresAtUtc);
        }

        var reconstructed = fixture.CreateStore(reconstructDataProtectionProvider: true);
        var replay = await reconstructed.TryClaimAsync(
            new WorkflowExecutorInvocationClaimRequest(
                identity,
                new WorkflowExecutorInvocationLeaseOwnerId("replay-host"),
                TestTime.AddMinutes(2),
                TestTime.AddMinutes(3),
                MaximumAttempts: 3));
        Assert.Equal(WorkflowExecutorInvocationClaimOutcome.ReplayedCompleted, replay.Outcome);
        Assert.Equal(storedResult.CompletedAtUtc, replay.Record!.StoredResult!.CompletedAtUtc);
        Assert.Equal(storedResult.Result.NodeId, replay.Record.StoredResult.Result.NodeId);
        Assert.Equal(storedResult.Result.PayloadJson, replay.Record.StoredResult.Result.PayloadJson);
        Assert.Equal(storedResult.Result.ResultShape, replay.Record.StoredResult.Result.ResultShape);

        string originalHash;
        string originalProtectedResult;
        await using (var dbContext = fixture.Factory.CreateDbContext())
        {
            var entity = await dbContext.Set<WorkflowExecutorInvocationRecordEntity>()
                .SingleAsync(item => item.InvocationKey == identity.Key.Value);
            originalHash = entity.StoredResultHash;
            originalProtectedResult = entity.ProtectedStoredResult;
            entity.StoredResultHash = new string('0', 64);
            await dbContext.SaveChangesAsync();
        }

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => reconstructed.GetAsync(identity.Key));
        await using (var dbContext = fixture.Factory.CreateDbContext())
        {
            var entity = await dbContext.Set<WorkflowExecutorInvocationRecordEntity>()
                .SingleAsync(item => item.InvocationKey == identity.Key.Value);
            entity.StoredResultHash = originalHash;
            entity.ProtectedStoredResult = "tampered-ciphertext";
            await dbContext.SaveChangesAsync();
        }

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => reconstructed.GetAsync(identity.Key));
        Assert.DoesNotContain("executor-result-secret", originalProtectedResult, StringComparison.Ordinal);
    }

    [Fact]
    public async Task PostgreSql_ExecutorDedup_CanonicalizesSubMicrosecondCompletionBeforeProtectedReplay()
    {
        await using var fixture = await CreateFixtureAsync("workflowhitlexecutortimestamp");
        var store = fixture.CreateStore();
        var identity = CreateIdentity("submicrosecond-completion");
        var claimed = await store.TryClaimAsync(
            new WorkflowExecutorInvocationClaimRequest(
                identity,
                new WorkflowExecutorInvocationLeaseOwnerId("timestamp-host"),
                TestTime,
                TestTime.AddMinutes(1),
                MaximumAttempts: 3));
        var claim = Assert.IsType<WorkflowExecutorInvocationClaim>(claimed.Claim);
        var suppliedCompletedAtUtc = TestTime.AddSeconds(1).AddTicks(7);
        var expectedCompletedAtUtc = suppliedCompletedAtUtc.AddTicks(
            -(suppliedCompletedAtUtc.Ticks % TimeSpan.TicksPerMicrosecond));

        var completed = await store.TryCompleteAsync(
            new WorkflowExecutorInvocationCompletionRequest(
                identity.Key,
                identity.InputHash,
                claim.ConcurrencyVersion,
                claim.Lease.OwnerId,
                claim.Lease.Epoch,
                new WorkflowExecutorInvocationStoredResult(
                    new WorkflowNodeExecutionResult(
                        identity.NodeId,
                        "{\"receipt\":\"timestamp-safe\"}",
                        JsonShape),
                    suppliedCompletedAtUtc)));

        Assert.Equal(WorkflowExecutorInvocationMutationOutcome.Updated, completed.Outcome);
        Assert.Equal(expectedCompletedAtUtc, completed.Record!.StoredResult!.CompletedAtUtc);

        var reconstructed = fixture.CreateStore(reconstructDataProtectionProvider: true);
        var replay = await reconstructed.GetAsync(identity.Key);
        Assert.Equal(expectedCompletedAtUtc, replay!.StoredResult!.CompletedAtUtc);
        await using var dbContext = fixture.Factory.CreateDbContext();
        var entity = await dbContext.Set<WorkflowExecutorInvocationRecordEntity>()
            .AsNoTracking()
            .SingleAsync(item => item.InvocationKey == identity.Key.Value);
        Assert.Equal(expectedCompletedAtUtc, entity.CompletedAtUtc);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    public async Task PostgreSql_ExecutorDedup_RejectsLeaseOwnerMutationAtExactOrSubMicrosecondExpiry(
        int observationTicksAfterExpiry)
    {
        await using var fixture = await CreateFixtureAsync(
            $"workflowhitlexecutorleaseboundary{observationTicksAfterExpiry}");
        var store = fixture.CreateStore();
        var leaseOwnerId = new WorkflowExecutorInvocationLeaseOwnerId(
            $"lease-boundary-owner-{observationTicksAfterExpiry}");
        var leaseExpiresAtUtc = TestTime.AddSeconds(10);
        var observedAtUtc = leaseExpiresAtUtc.AddTicks(observationTicksAfterExpiry);

        async Task<(WorkflowExecutorInvocationIdentity Identity, WorkflowExecutorInvocationClaim Claim)>
            ClaimAsync(string mutation)
        {
            var identity = CreateIdentity(
                $"lease-boundary-{observationTicksAfterExpiry}-{mutation}");
            var claimed = await store.TryClaimAsync(
                new WorkflowExecutorInvocationClaimRequest(
                    identity,
                    leaseOwnerId,
                    TestTime,
                    leaseExpiresAtUtc,
                    MaximumAttempts: 3));
            Assert.Equal(WorkflowExecutorInvocationClaimOutcome.Claimed, claimed.Outcome);
            return (identity, Assert.IsType<WorkflowExecutorInvocationClaim>(claimed.Claim));
        }

        var renewal = await ClaimAsync("renew");
        var completion = await ClaimAsync("complete");
        var failure = await ClaimAsync("fail");

        var renewed = await store.TryRenewLeaseAsync(
            new WorkflowExecutorInvocationLeaseRenewalRequest(
                renewal.Identity.Key,
                renewal.Claim.ConcurrencyVersion,
                renewal.Claim.Lease.OwnerId,
                renewal.Claim.Lease.Epoch,
                observedAtUtc,
                observedAtUtc.AddMinutes(1)));
        var completed = await store.TryCompleteAsync(
            new WorkflowExecutorInvocationCompletionRequest(
                completion.Identity.Key,
                completion.Identity.InputHash,
                completion.Claim.ConcurrencyVersion,
                completion.Claim.Lease.OwnerId,
                completion.Claim.Lease.Epoch,
                new WorkflowExecutorInvocationStoredResult(
                    new WorkflowNodeExecutionResult(
                        completion.Identity.NodeId,
                        "{\"stale\":true}",
                        JsonShape),
                    observedAtUtc)));
        var failed = await store.TryFailAsync(
            new WorkflowExecutorInvocationFailureRequest(
                failure.Identity.Key,
                failure.Identity.InputHash,
                failure.Claim.ConcurrencyVersion,
                failure.Claim.Lease.OwnerId,
                failure.Claim.Lease.Epoch,
                WorkflowExecutorInvocationState.FailedRetryable,
                WorkflowExecutorInvocationFailureCode.ExecutionFailed,
                "The stale lease owner must not mutate the invocation.",
                observedAtUtc));

        Assert.All(
            new[] { renewed, completed, failed },
            result =>
            {
                Assert.Equal(WorkflowExecutorInvocationMutationOutcome.LeaseExpired, result.Outcome);
                Assert.Equal(WorkflowExecutorInvocationState.Claimed, result.Record!.State);
                Assert.Equal(leaseExpiresAtUtc, result.Record.Lease!.ExpiresAtUtc);
                Assert.Equal(TestTime, result.Record.UpdatedAtUtc);
            });

        var keys = new[]
        {
            renewal.Identity.Key.Value,
            completion.Identity.Key.Value,
            failure.Identity.Key.Value
        };
        await using var dbContext = fixture.Factory.CreateDbContext();
        var entities = await dbContext.Set<WorkflowExecutorInvocationRecordEntity>()
            .AsNoTracking()
            .Where(item => keys.Contains(item.InvocationKey))
            .ToListAsync();
        Assert.Equal(3, entities.Count);
        Assert.All(
            entities,
            entity =>
            {
                Assert.Equal(WorkflowExecutorInvocationState.Claimed, entity.State);
                Assert.Equal(WorkflowExecutorInvocationConcurrencyVersion.Initial.Value, entity.ConcurrencyVersion);
                Assert.Equal(leaseOwnerId.Value, entity.LeaseOwnerId);
                Assert.Equal(TestTime, entity.LeaseAcquiredAtUtc);
                Assert.Equal(leaseExpiresAtUtc, entity.LeaseExpiresAtUtc);
                Assert.Equal(TestTime, entity.UpdatedAtUtc);
                Assert.Null(entity.CompletedAtUtc);
                Assert.Empty(entity.ProtectedStoredResult);
                Assert.Empty(entity.StoredResultHash);
                Assert.Empty(entity.FailureCode);
                Assert.Empty(entity.SafeMessage);
            });
    }

    [Fact]
    public async Task PostgreSql_ExecutorDedup_ExpiresTakesOverAndTerminalizesAttemptAndUnsafeFailures()
    {
        await using var fixture = await CreateFixtureAsync("workflowhitlexecutortakeover");
        var store = fixture.CreateStore();
        var identity = CreateIdentity("takeover");
        var first = await store.TryClaimAsync(
            new WorkflowExecutorInvocationClaimRequest(
                identity,
                new WorkflowExecutorInvocationLeaseOwnerId("first-owner"),
                TestTime,
                TestTime.AddSeconds(10),
                MaximumAttempts: 2));
        var takeover = await store.TryClaimAsync(
            new WorkflowExecutorInvocationClaimRequest(
                identity,
                new WorkflowExecutorInvocationLeaseOwnerId("takeover-owner"),
                TestTime.AddSeconds(11),
                TestTime.AddSeconds(21),
                MaximumAttempts: 2));
        Assert.Equal(WorkflowExecutorInvocationClaimOutcome.Claimed, takeover.Outcome);
        Assert.Equal(2, takeover.Record!.Attempt);
        Assert.Equal(first.Claim!.Lease.Epoch.Next(), takeover.Claim!.Lease.Epoch);

        var staleCompletion = await store.TryCompleteAsync(
            new WorkflowExecutorInvocationCompletionRequest(
                identity.Key,
                identity.InputHash,
                first.Claim.ConcurrencyVersion,
                first.Claim.Lease.OwnerId,
                first.Claim.Lease.Epoch,
                new WorkflowExecutorInvocationStoredResult(
                    new WorkflowNodeExecutionResult(
                        identity.NodeId,
                        "{\"stale\":true}",
                        JsonShape),
                    TestTime.AddSeconds(12))));
        Assert.NotEqual(WorkflowExecutorInvocationMutationOutcome.Updated, staleCompletion.Outcome);

        var exhausted = await store.TryClaimAsync(
            new WorkflowExecutorInvocationClaimRequest(
                identity,
                new WorkflowExecutorInvocationLeaseOwnerId("exhausted-owner"),
                TestTime.AddSeconds(22),
                TestTime.AddSeconds(32),
                MaximumAttempts: 2));
        Assert.Equal(WorkflowExecutorInvocationClaimOutcome.AttemptLimitReached, exhausted.Outcome);
        Assert.Equal(WorkflowExecutorInvocationState.FailedTerminal, exhausted.Record!.State);
        Assert.Equal(WorkflowExecutorInvocationFailureCode.AttemptLimitReached, exhausted.Record.FailureCode);
        Assert.Null(exhausted.Record.Lease);

        var unsafeIdentity = CreateIdentity("unsafe-terminal");
        var unsafeClaim = await store.TryClaimAsync(
            new WorkflowExecutorInvocationClaimRequest(
                unsafeIdentity,
                new WorkflowExecutorInvocationLeaseOwnerId("unsafe-owner"),
                TestTime,
                TestTime.AddMinutes(1),
                MaximumAttempts: 3));
        var unsafeFailure = await store.TryFailAsync(
            new WorkflowExecutorInvocationFailureRequest(
                unsafeIdentity.Key,
                unsafeIdentity.InputHash,
                unsafeClaim.Claim!.ConcurrencyVersion,
                unsafeClaim.Claim.Lease.OwnerId,
                unsafeClaim.Claim.Lease.Epoch,
                WorkflowExecutorInvocationState.FailedTerminal,
                WorkflowExecutorInvocationFailureCode.UnsafeResultNotPersisted,
                "The executor result is unsafe for durable replay.",
                TestTime.AddSeconds(1)));
        Assert.Equal(WorkflowExecutorInvocationMutationOutcome.Updated, unsafeFailure.Outcome);
        Assert.Null(unsafeFailure.Record!.Lease);
        var terminalReplay = await store.TryClaimAsync(
            new WorkflowExecutorInvocationClaimRequest(
                unsafeIdentity,
                new WorkflowExecutorInvocationLeaseOwnerId("unsafe-replay-owner"),
                TestTime.AddMinutes(2),
                TestTime.AddMinutes(3),
                MaximumAttempts: 3));
        Assert.Equal(WorkflowExecutorInvocationClaimOutcome.FailedTerminal, terminalReplay.Outcome);
        Assert.Null(terminalReplay.Record!.StoredResult);
    }

    [Fact]
    public async Task PostgreSql_ParticipatingIdempotency_DeduplicatesEffectAcrossPreFinalizeCrash()
    {
        await using var fixture = await CreateFixtureAsync("workflowhitlexecutorcrash");
        await CreateEffectReceiverAsync(fixture.Factory);
        var clock = new MutableTimeProvider(TestTime);
        var firstPersistentStore = fixture.CreateStore();
        var failingStore = new ThrowBeforeFirstCompletionStore(firstPersistentStore);
        var firstExecutor = new PostgreSqlParticipatingEffectExecutor(fixture.Factory);
        var invocation = CreateInvocation(firstExecutor, failingStore, clock);

        await Assert.ThrowsAsync<WorkflowExecutorInvocationDeduplicationException>(
            () => invocation.ExecuteAsync().AsTask());
        var stranded = await firstPersistentStore.GetAsync(invocation.Identity.Key);
        Assert.Equal(WorkflowExecutorInvocationState.Claimed, stranded!.State);
        Assert.Equal(1, await CountEffectsAsync(fixture.Factory));

        clock.SetUtcNow(stranded.Lease!.ExpiresAtUtc.AddSeconds(1));
        var reconstructedStore = fixture.CreateStore(reconstructDataProtectionProvider: true);
        var recoveredExecutor = new PostgreSqlParticipatingEffectExecutor(fixture.Factory);
        var recoveredInvocation = CreateInvocation(
            recoveredExecutor,
            reconstructedStore,
            clock,
            invocation);
        var recovered = await recoveredInvocation.ExecuteAsync();
        var replayed = await recoveredInvocation.ExecuteAsync();

        Assert.Equivalent(recovered, replayed, strict: true);
        Assert.Equal(1, recoveredExecutor.InvocationCount);
        Assert.Equal(
            Assert.Single(firstExecutor.IdempotencyKeys),
            Assert.Single(recoveredExecutor.IdempotencyKeys));
        Assert.Equal(1, await CountEffectsAsync(fixture.Factory));
        var durable = await reconstructedStore.GetAsync(invocation.Identity.Key);
        Assert.Equal(WorkflowExecutorInvocationState.Completed, durable!.State);
        Assert.NotNull(durable.StoredResult);
        Assert.Equal(2, durable.Attempt);
    }

    private static WorkflowExecutorInvocationIdentity CreateIdentity(string seed)
    {
        var input = new WorkflowNodeInput($"{{\"seed\":\"{seed}\"}}");
        return new WorkflowExecutorInvocationIdentity(
            new WorkflowExecutorInvocationScopeKey(Hash($"scope:{seed}")),
            new WorkflowExecutorInvocationKey(Hash($"key:{seed}")),
            new WorkflowExecutorInvocationIdempotencyKey(Hash($"participant:{seed}")),
            WorkflowRunId.New(),
            WorkflowVersionId.New(),
            new WorkflowNodeId($"node-{seed}"),
            new WorkflowExecutorId("test.postgresql-participant"),
            new WorkflowExecutorContractVersion("test/1"),
            WorkflowExternalRequestId.New(),
            new WorkflowExternalRequestVersion(1),
            WorkflowExternalResponseOperationId.New(),
            WorkflowExecutorInvocationGeneration.Initial,
            WorkflowExecutorInputHash.Compute(input));
    }

    private static string Hash(string value)
        => Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(value)));

    private static async Task<ExecutorDedupFixture> CreateFixtureAsync(string databaseName)
    {
        AppDbContextModelRegistry.ConfigureAssemblies(ModuleAssemblies.All);
        var database = PostgresTestDatabaseLease.Create(databaseName);
        var keyDirectory = new DataProtectionKeyDirectory();
        var factory = new ExecutorDedupDbContextFactory(database.CreateAppDbContextOptions());
        await using (var dbContext = factory.CreateDbContext())
        {
            await dbContext.Database.MigrateAsync();
        }

        return new ExecutorDedupFixture(database, keyDirectory, factory);
    }

    private static async Task CreateEffectReceiverAsync(ExecutorDedupDbContextFactory factory)
    {
        await using var dbContext = factory.CreateDbContext();
        await dbContext.Database.ExecuteSqlRawAsync(
            """
            CREATE TABLE "Test_WorkflowExecutorEffects" (
                "IdempotencyKey" TEXT PRIMARY KEY,
                "PayloadJson" TEXT NOT NULL
            )
            """);
    }

    private static async Task<int> CountEffectsAsync(ExecutorDedupDbContextFactory factory)
    {
        await using var dbContext = factory.CreateDbContext();
        return await dbContext.Database
            .SqlQueryRaw<int>("SELECT COUNT(*)::int AS \"Value\" FROM \"Test_WorkflowExecutorEffects\"")
            .SingleAsync();
    }

    private static DurableInvocationFixture CreateInvocation(
        PostgreSqlParticipatingEffectExecutor executor,
        IWorkflowExecutorInvocationDeduplicationStore store,
        TimeProvider clock,
        DurableInvocationFixture? identitySource = null)
    {
        var catalog = new WorkflowExecutorCatalog([executor]);
        var inner = new WorkflowExecutorInvoker(catalog, [executor]);
        var invoker = new DeduplicatingWorkflowExecutorInvoker(inner, catalog, store, clock);
        var node = identitySource?.Node ?? new WorkflowNode(
            new WorkflowNodeId("postgresql-participating-effect"),
            WorkflowNodeKind.Executor,
            "PostgreSQL participating effect",
            [],
            new WorkflowNodeSettings(
                ComponentId: null,
                AgentId: null,
                SubworkflowId: null,
                ExternalRequestKind: null,
                Instructions: string.Empty,
                InputShape: JsonShape,
                ResultShape: JsonShape)
            {
                ExecutorId = executor.Descriptor.Id,
                ExecutorSettingsJson = "{}",
                ExecutionPolicy = WorkflowExecutorExecutionPolicy.Default
            });
        var definition = identitySource?.Definition ?? new WorkflowDefinition(
            WorkflowId.New(),
            WorkflowVersionId.New(),
            "PostgreSQL crash-window proof",
            "PostgreSQL crash-window proof",
            WorkflowLifecycleStatus.Draft,
            new WorkflowGraph(node.Id, [node], []),
            new WorkflowRuntimePolicy(
                WorkflowRuntimeBackendKind.InProcess,
                AllowInProcessPreviewRuns: true,
                RequireDurableProductionRuns: false,
                ExposeAzureFunctionsStatusEndpoint: false,
                ExposeAzureFunctionsMcpTool: false),
            TestTime,
            TestTime);
        var runId = identitySource?.RunId ?? WorkflowRunId.New();
        var context = identitySource?.Context ?? new WorkflowExecutorInvocationContext
        {
            CausationRequestId = WorkflowExternalRequestId.New(),
            CausationRequestVersion = new WorkflowExternalRequestVersion(1),
            CausationOperationId = WorkflowExternalResponseOperationId.New(),
            InvocationGeneration = WorkflowExecutorInvocationGeneration.Initial
        };
        var input = identitySource?.Input ?? new WorkflowNodeInput("{\"effect\":\"write-once\"}");
        var identity = WorkflowExecutorInvocationKeyFactory.Create(
            runId,
            definition.VersionId,
            node.Id,
            executor.Descriptor.Id,
            WorkflowExecutorInvocationDeduplicationPolicy.ResolveContractVersion(executor.Descriptor),
            context.CausationRequestId!.Value,
            context.CausationRequestVersion!.Value,
            context.CausationOperationId!.Value,
            context.InvocationGeneration,
            input);
        return new DurableInvocationFixture(
            invoker,
            definition,
            node,
            runId,
            context,
            input,
            identity);
    }

    private sealed record DurableInvocationFixture(
        IWorkflowExecutorInvoker Invoker,
        WorkflowDefinition Definition,
        WorkflowNode Node,
        WorkflowRunId RunId,
        WorkflowExecutorInvocationContext Context,
        WorkflowNodeInput Input,
        WorkflowExecutorInvocationIdentity Identity)
    {
        public async ValueTask<WorkflowNodeExecutionResult> ExecuteAsync()
        {
            using var scope = WorkflowExecutorExecutionAuditScope.Push(RunId);
            return await Invoker.ExecuteAsync(Definition, Node, Input, Context);
        }
    }

    private sealed class PostgreSqlParticipatingEffectExecutor(
        ExecutorDedupDbContextFactory factory) : IWorkflowExecutor
    {
        public WorkflowExecutorDescriptor Descriptor { get; } =
            BuiltInWorkflowExecutorDescriptors.JsonTransform with
            {
                Id = new WorkflowExecutorId("test.postgresql-participating-effect"),
                Name = "PostgreSQL participating effect",
                PermissionPolicy = new WorkflowExecutorPermissionPolicy(
                    WorkflowExecutorCapabilityFlags.WritesExternalData |
                    WorkflowExecutorCapabilityFlags.IdempotentExternalMarker,
                    WorkflowExecutorApprovalRequirement.NotRequired),
                SideEffects = WorkflowExecutorSideEffectDescriptor.IdempotentProcessedMarker(
                    "$.idempotencyKey",
                    "{\"type\":\"object\"}")
            };

        public int InvocationCount { get; private set; }

        public List<WorkflowExecutorInvocationIdempotencyKey> IdempotencyKeys { get; } = [];

        public async ValueTask<WorkflowNodeExecutionResult> ExecuteAsync(
            WorkflowExecutorExecutionContext context,
            WorkflowNodeInput input,
            CancellationToken cancellationToken = default)
        {
            var idempotencyKey = context.IdempotencyKey ?? throw new InvalidOperationException(
                "A participating effect requires its propagated idempotency key.");
            InvocationCount++;
            IdempotencyKeys.Add(idempotencyKey);
            await using var dbContext = factory.CreateDbContext();
            await dbContext.Database.ExecuteSqlInterpolatedAsync(
                $"""
                INSERT INTO "Test_WorkflowExecutorEffects" ("IdempotencyKey", "PayloadJson")
                VALUES ({idempotencyKey.Value}, {input.PayloadJson})
                ON CONFLICT ("IdempotencyKey") DO NOTHING
                """,
                cancellationToken);
            return new WorkflowNodeExecutionResult(
                context.Node.Id,
                "{\"receipt\":\"participating-effect-committed\"}",
                context.Descriptor.ResultShape);
        }
    }

    private sealed class ThrowBeforeFirstCompletionStore(
        IWorkflowExecutorInvocationDeduplicationStore inner) :
        IWorkflowExecutorInvocationDeduplicationStore
    {
        private int completionsToFail = 1;

        public Task<WorkflowExecutorInvocationClaimResult> TryClaimAsync(
            WorkflowExecutorInvocationClaimRequest request,
            CancellationToken cancellationToken = default)
            => inner.TryClaimAsync(request, cancellationToken);

        public Task<WorkflowExecutorInvocationRecord?> GetAsync(
            WorkflowExecutorInvocationKey key,
            CancellationToken cancellationToken = default)
            => inner.GetAsync(key, cancellationToken);

        public Task<WorkflowExecutorInvocationMutationResult> TryRenewLeaseAsync(
            WorkflowExecutorInvocationLeaseRenewalRequest request,
            CancellationToken cancellationToken = default)
            => inner.TryRenewLeaseAsync(request, cancellationToken);

        public Task<WorkflowExecutorInvocationMutationResult> TryCompleteAsync(
            WorkflowExecutorInvocationCompletionRequest request,
            CancellationToken cancellationToken = default)
        {
            if (Interlocked.Exchange(ref completionsToFail, 0) == 1)
            {
                throw new WorkflowExecutorInvocationDeduplicationException(
                    request.Key,
                    "Simulated process loss before durable invocation completion.");
            }

            return inner.TryCompleteAsync(request, cancellationToken);
        }

        public Task<WorkflowExecutorInvocationMutationResult> TryFailAsync(
            WorkflowExecutorInvocationFailureRequest request,
            CancellationToken cancellationToken = default)
            => inner.TryFailAsync(request, cancellationToken);
    }

    private sealed class MutableTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        private DateTimeOffset current = utcNow;

        public override DateTimeOffset GetUtcNow() => current;

        public void SetUtcNow(DateTimeOffset value) => current = value;
    }

    private sealed class ExecutorDedupFixture(
        PostgresTestDatabaseLease database,
        DataProtectionKeyDirectory keyDirectory,
        ExecutorDedupDbContextFactory factory) : IAsyncDisposable
    {
        public ExecutorDedupDbContextFactory Factory { get; } = factory;

        public PersistentWorkflowExecutorInvocationDeduplicationStore CreateStore(
            bool reconstructDataProtectionProvider = false)
            => new(Factory, keyDirectory.CreateProvider());

        public async ValueTask DisposeAsync()
        {
            await database.DisposeAsync();
            keyDirectory.Dispose();
        }
    }

    private sealed class ExecutorDedupDbContextFactory(DbContextOptions<AppDbContext> options) :
        IDbContextFactory<AppDbContext>
    {
        public AppDbContext CreateDbContext() => new(options);

        public Task<AppDbContext> CreateDbContextAsync(
            CancellationToken cancellationToken = default)
            => Task.FromResult(CreateDbContext());
    }

    private sealed class DataProtectionKeyDirectory : IDisposable
    {
        private readonly string path = Path.Combine(
            Path.GetTempPath(),
            "CanDoItAll.Tests.WorkflowExecutorDedup",
            Guid.NewGuid().ToString("N"));

        public DataProtectionKeyDirectory()
        {
            Directory.CreateDirectory(path);
        }

        public IDataProtectionProvider CreateProvider()
            => DataProtectionProvider.Create(
                new DirectoryInfo(path),
                builder => builder.SetApplicationName("CanDoItAll.WorkflowExecutorDedupTests"));

        public void Dispose()
        {
            if (Directory.Exists(path))
            {
                Directory.Delete(path, recursive: true);
            }
        }
    }
}
