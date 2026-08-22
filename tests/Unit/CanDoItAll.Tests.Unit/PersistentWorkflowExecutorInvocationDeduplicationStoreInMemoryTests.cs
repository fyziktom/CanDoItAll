using System.Security.Cryptography;
using System.Text;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.Modules.AgentFramework;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;

namespace CanDoItAll.Tests.Unit.AgentFramework;

[Collection(AppDbContextModelRegistryTestCollectionNames.Name)]
public sealed class PersistentWorkflowExecutorInvocationDeduplicationStoreInMemoryTests
{
    private static readonly DateTimeOffset TestTime =
        new(2026, 8, 21, 21, 0, 0, TimeSpan.Zero);

    private static readonly WorkflowValueShape JsonShape = new(
        WorkflowValueShapeKind.Json,
        "{}",
        "JSON");

    [Fact]
    public async Task TryClaimAsync_ConcurrentSameScope_ClaimsOnceAndReportsActiveLease()
    {
        var store = CreateStore();
        var identity = CreateIdentity("concurrent-claim");
        var owners = Enumerable.Range(1, 8)
            .Select(index => new WorkflowExecutorInvocationLeaseOwnerId($"owner-{index}"))
            .ToArray();

        var results = await Task.WhenAll(owners.Select(owner => store.TryClaimAsync(
            CreateClaimRequest(identity, owner, TestTime, maximumAttempts: 3))));

        var claimed = Assert.Single(
            results,
            result => result.Outcome == WorkflowExecutorInvocationClaimOutcome.Claimed);
        Assert.NotNull(claimed.Claim);
        Assert.Equal(1, claimed.Record?.Attempt);
        Assert.All(
            results.Where(result => result != claimed),
            result =>
            {
                Assert.Equal(WorkflowExecutorInvocationClaimOutcome.ActiveLease, result.Outcome);
                Assert.Null(result.Claim);
                Assert.Equal(claimed.Record, result.Record);
            });
    }

    [Fact]
    public async Task TryClaimAndRenewAsync_ExpiredLease_TakesOverAndRenewsNewLease()
    {
        var store = CreateStore();
        var identity = CreateIdentity("expired-takeover");
        var first = await store.TryClaimAsync(CreateClaimRequest(
            identity,
            new WorkflowExecutorInvocationLeaseOwnerId("first-owner"),
            TestTime,
            maximumAttempts: 3,
            leaseDuration: TimeSpan.FromSeconds(10)));
        var firstClaim = Assert.IsType<WorkflowExecutorInvocationClaim>(first.Claim);
        var takeoverAtUtc = TestTime.AddSeconds(11);

        var takeover = await store.TryClaimAsync(CreateClaimRequest(
            identity,
            new WorkflowExecutorInvocationLeaseOwnerId("takeover-owner"),
            takeoverAtUtc,
            maximumAttempts: 3,
            leaseDuration: TimeSpan.FromSeconds(10)));

        Assert.Equal(WorkflowExecutorInvocationClaimOutcome.Claimed, takeover.Outcome);
        var takeoverClaim = Assert.IsType<WorkflowExecutorInvocationClaim>(takeover.Claim);
        Assert.Equal(2, takeoverClaim.Attempt);
        Assert.Equal(firstClaim.Lease.Epoch.Next(), takeoverClaim.Lease.Epoch);
        Assert.Equal("takeover-owner", takeoverClaim.Lease.OwnerId.Value);
        var renewedAtUtc = takeoverAtUtc.AddSeconds(2);
        var renewedUntilUtc = takeoverAtUtc.AddMinutes(2);

        var renewed = await store.TryRenewLeaseAsync(
            new WorkflowExecutorInvocationLeaseRenewalRequest(
                identity.Key,
                takeoverClaim.ConcurrencyVersion,
                takeoverClaim.Lease.OwnerId,
                takeoverClaim.Lease.Epoch,
                renewedAtUtc,
                renewedUntilUtc));

        Assert.Equal(WorkflowExecutorInvocationMutationOutcome.Updated, renewed.Outcome);
        Assert.Equal(takeoverClaim.ConcurrencyVersion.Next(), renewed.Record?.ConcurrencyVersion);
        Assert.Equal(renewedUntilUtc, renewed.Record?.Lease?.ExpiresAtUtc);
        Assert.Equal(takeoverClaim.Lease.Epoch, renewed.Record?.Lease?.Epoch);
    }

    [Fact]
    public async Task TryCompleteAndClaimAsync_CompletedInvocation_ReplaysStoredResult()
    {
        var store = CreateStore();
        var identity = CreateIdentity("complete-replay");
        var claimed = await store.TryClaimAsync(CreateClaimRequest(
            identity,
            new WorkflowExecutorInvocationLeaseOwnerId("completion-owner"),
            TestTime,
            maximumAttempts: 3));
        var claim = Assert.IsType<WorkflowExecutorInvocationClaim>(claimed.Claim);
        var storedResult = new WorkflowExecutorInvocationStoredResult(
            new WorkflowNodeExecutionResult(
                identity.NodeId,
                "{\"receipt\":\"durable-result\"}",
                JsonShape),
            TestTime.AddSeconds(5));

        var completed = await store.TryCompleteAsync(
            new WorkflowExecutorInvocationCompletionRequest(
                identity.Key,
                identity.InputHash,
                claim.ConcurrencyVersion,
                claim.Lease.OwnerId,
                claim.Lease.Epoch,
                storedResult));
        var replay = await store.TryClaimAsync(CreateClaimRequest(
            identity,
            new WorkflowExecutorInvocationLeaseOwnerId("replay-owner"),
            TestTime.AddMinutes(2),
            maximumAttempts: 3));

        Assert.Equal(WorkflowExecutorInvocationMutationOutcome.Updated, completed.Outcome);
        Assert.Equal(WorkflowExecutorInvocationState.Completed, completed.Record?.State);
        Assert.Null(completed.Record?.Lease);
        Assert.Equal(WorkflowExecutorInvocationClaimOutcome.ReplayedCompleted, replay.Outcome);
        Assert.Null(replay.Claim);
        Assert.Equal(WorkflowExecutorInvocationState.Completed, replay.Record?.State);
        Assert.Equal(completed.Record?.Attempt, replay.Record?.Attempt);
        Assert.Equal(completed.Record?.ConcurrencyVersion, replay.Record?.ConcurrencyVersion);
        Assert.Equal(storedResult.CompletedAtUtc, replay.Record?.StoredResult?.CompletedAtUtc);
        Assert.Equal(storedResult.Result.NodeId, replay.Record?.StoredResult?.Result.NodeId);
        Assert.Equal(storedResult.Result.PayloadJson, replay.Record?.StoredResult?.Result.PayloadJson);
        Assert.Equal(storedResult.Result.ResultShape, replay.Record?.StoredResult?.Result.ResultShape);
    }

    [Fact]
    public async Task TryFailAndClaimAsync_RetryableFailures_ReclaimsThenTerminalizesAttemptExhaustion()
    {
        const int maximumAttempts = 2;
        var store = CreateStore();
        var identity = CreateIdentity("retry-exhaustion");
        var first = await store.TryClaimAsync(CreateClaimRequest(
            identity,
            new WorkflowExecutorInvocationLeaseOwnerId("attempt-one-owner"),
            TestTime,
            maximumAttempts));
        var firstClaim = Assert.IsType<WorkflowExecutorInvocationClaim>(first.Claim);

        var firstFailure = await store.TryFailAsync(CreateRetryableFailure(
            identity,
            firstClaim,
            TestTime.AddSeconds(5)));
        Assert.Equal(WorkflowExecutorInvocationMutationOutcome.Updated, firstFailure.Outcome);
        Assert.Equal(WorkflowExecutorInvocationState.FailedRetryable, firstFailure.Record?.State);
        Assert.Null(firstFailure.Record?.Lease);

        var second = await store.TryClaimAsync(CreateClaimRequest(
            identity,
            new WorkflowExecutorInvocationLeaseOwnerId("attempt-two-owner"),
            TestTime.AddSeconds(10),
            maximumAttempts));
        var secondClaim = Assert.IsType<WorkflowExecutorInvocationClaim>(second.Claim);
        Assert.Equal(2, secondClaim.Attempt);
        Assert.Equal(firstClaim.Lease.Epoch.Next(), secondClaim.Lease.Epoch);

        var secondFailure = await store.TryFailAsync(CreateRetryableFailure(
            identity,
            secondClaim,
            TestTime.AddSeconds(15)));
        Assert.Equal(WorkflowExecutorInvocationMutationOutcome.Updated, secondFailure.Outcome);
        Assert.Equal(WorkflowExecutorInvocationState.FailedRetryable, secondFailure.Record?.State);

        var exhausted = await store.TryClaimAsync(CreateClaimRequest(
            identity,
            new WorkflowExecutorInvocationLeaseOwnerId("exhausted-owner"),
            TestTime.AddSeconds(20),
            maximumAttempts));
        var terminalReplay = await store.TryClaimAsync(CreateClaimRequest(
            identity,
            new WorkflowExecutorInvocationLeaseOwnerId("terminal-replay-owner"),
            TestTime.AddSeconds(25),
            maximumAttempts));

        Assert.Equal(WorkflowExecutorInvocationClaimOutcome.AttemptLimitReached, exhausted.Outcome);
        Assert.Equal(WorkflowExecutorInvocationState.FailedTerminal, exhausted.Record?.State);
        Assert.Equal(maximumAttempts, exhausted.Record?.Attempt);
        Assert.Equal(
            WorkflowExecutorInvocationFailureCode.AttemptLimitReached,
            exhausted.Record?.FailureCode);
        Assert.Null(exhausted.Record?.Lease);
        Assert.Equal(WorkflowExecutorInvocationClaimOutcome.FailedTerminal, terminalReplay.Outcome);
        Assert.Equal(exhausted.Record, terminalReplay.Record);
    }

    private static PersistentWorkflowExecutorInvocationDeduplicationStore CreateStore()
    {
        AppDbContextModelRegistry.ConfigureAssemblies([
            typeof(AgentFrameworkModuleAssemblyMarker).Assembly
        ]);
        var options = AppDbContextTestOptionsBuilder.Create()
            .UseInMemoryDatabase($"workflow-executor-dedup-{Guid.NewGuid():N}")
            .Options;
        return new PersistentWorkflowExecutorInvocationDeduplicationStore(
            new TestDbContextFactory(options),
            new EphemeralDataProtectionProvider());
    }

    private static WorkflowExecutorInvocationClaimRequest CreateClaimRequest(
        WorkflowExecutorInvocationIdentity identity,
        WorkflowExecutorInvocationLeaseOwnerId ownerId,
        DateTimeOffset claimedAtUtc,
        int maximumAttempts,
        TimeSpan? leaseDuration = null)
        => new(
            identity,
            ownerId,
            claimedAtUtc,
            claimedAtUtc.Add(leaseDuration ?? TimeSpan.FromMinutes(1)),
            maximumAttempts);

    private static WorkflowExecutorInvocationFailureRequest CreateRetryableFailure(
        WorkflowExecutorInvocationIdentity identity,
        WorkflowExecutorInvocationClaim claim,
        DateTimeOffset failedAtUtc)
        => new(
            identity.Key,
            identity.InputHash,
            claim.ConcurrencyVersion,
            claim.Lease.OwnerId,
            claim.Lease.Epoch,
            WorkflowExecutorInvocationState.FailedRetryable,
            WorkflowExecutorInvocationFailureCode.ExecutionFailed,
            "The simulated executor attempt failed and may be retried.",
            failedAtUtc);

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
            new WorkflowExecutorId("test.in-memory-participant"),
            new WorkflowExecutorContractVersion("test/1"),
            WorkflowExternalRequestId.New(),
            new WorkflowExternalRequestVersion(1),
            WorkflowExternalResponseOperationId.New(),
            WorkflowExecutorInvocationGeneration.Initial,
            WorkflowExecutorInputHash.Compute(input));
    }

    private static string Hash(string value)
        => Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(value)));

    private sealed class TestDbContextFactory(DbContextOptions<AppDbContext> options) :
        IDbContextFactory<AppDbContext>
    {
        public AppDbContext CreateDbContext() => new(options);

        public Task<AppDbContext> CreateDbContextAsync(CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(CreateDbContext());
        }
    }
}
