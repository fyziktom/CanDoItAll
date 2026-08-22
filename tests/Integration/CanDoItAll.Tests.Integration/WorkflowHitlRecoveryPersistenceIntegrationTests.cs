using System.Data.Common;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.Workflows.Abstractions;
using CanDoItAll.Composition;
using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.Modules.AgentFramework;
using CanDoItAll.Tests.Support;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Npgsql;

namespace CanDoItAll.Tests.Integration.AgentFramework;

public sealed class WorkflowHitlRecoveryPersistenceIntegrationTests
{
    private const string RecoveryMigrationId = "20260821021747_AddWorkflowHitlRecovery";
    private const string NativeRequestUniquenessMigrationId =
        "20260822013043_AddWorkflowNativeCheckpointRequestUniqueness";
    private static readonly DateTimeOffset TestTime =
        new(2026, 8, 20, 22, 0, 0, TimeSpan.Zero);
    private static readonly WorkspaceScopeDescriptor TestAuthorizationScope =
        WorkspaceScopeDescriptor.Organization("workflow-hitl-persistence-tests");

    [Fact]
    public async Task PostgreSql_MigrationStartup_AppliesWorkflowHitlRecoverySchema()
    {
        await using var fixture = await CreateFixtureAsync("workflowhitlmigration");
        await using var dbContext = fixture.Factory.CreateDbContext();
        var appliedMigrations = await dbContext.Database.GetAppliedMigrationsAsync();
        Assert.Contains(RecoveryMigrationId, appliedMigrations);
        Assert.Contains(NativeRequestUniquenessMigrationId, appliedMigrations);
        Assert.False(await dbContext.Set<WorkflowBackendCheckpointSessionEntity>().AnyAsync());
        Assert.False(await dbContext.Set<WorkflowBackendCheckpointPayloadEntity>().AnyAsync());
        Assert.False(await dbContext.Set<WorkflowExternalRequestBoundaryEntity>().AnyAsync());
        Assert.False(await dbContext.Set<WorkflowExternalResponseOperationEntity>().AnyAsync());
        Assert.False(await dbContext.Set<WorkflowExecutorInvocationRecordEntity>().AnyAsync());
    }

    [Fact]
    public async Task PostgreSql_Checkpoints_AreProtectedOrderedAndReadableAfterStoreReconstruction()
    {
        await using var fixture = await CreateFixtureAsync("workflowhitlcheckpoints");
        var run = await SeedRunAsync(fixture.Factory);
        var session = CreateCheckpointSession(run);
        var store = fixture.CreateCheckpointStore();
        var payloads = Enumerable.Range(0, 8)
            .Select(index => $"{{\"ordinal\":{index},\"secret\":\"checkpoint-secret-{index}\"}}")
            .ToArray();

        var creates = await Task.WhenAll(payloads.Select(payload => store.CreateAsync(
            new WorkflowBackendCheckpointCreateRequest(
                session,
                Parent: null,
                WorkflowBackendCheckpointPayload.Create(payload)))));

        Assert.All(creates, result =>
            Assert.Equal(WorkflowBackendCheckpointCreateOutcome.Created, result.Outcome));
        var index = await store.ListIndexAsync(session.Id);
        Assert.Equal(WorkflowBackendCheckpointListOutcome.Found, index.Outcome);
        Assert.Equal(Enumerable.Range(0, 8).Select(value => (long)value),
            index.Checkpoints.Select(checkpoint => checkpoint.CommitOrdinal.Value));

        await using (var dbContext = fixture.Factory.CreateDbContext())
        {
            var persisted = await dbContext.Set<WorkflowBackendCheckpointPayloadEntity>()
                .AsNoTracking()
                .OrderBy(checkpoint => checkpoint.CommitOrdinal)
                .ToArrayAsync();
            Assert.Equal(payloads.Length, persisted.Length);
            Assert.All(persisted, checkpoint =>
            {
                Assert.DoesNotContain("checkpoint-secret", checkpoint.ProtectedPayload, StringComparison.Ordinal);
                Assert.Equal(64, checkpoint.PayloadHash.Length);
            });
        }

        var reconstructed = fixture.CreateCheckpointStore(reconstructDataProtectionProvider: true);
        foreach (var created in creates)
        {
            var read = await reconstructed.ReadAsync(created.Checkpoint!.Index.Link);
            Assert.Equal(WorkflowBackendCheckpointReadOutcome.Found, read.Outcome);
            Assert.True(payloads.Contains(read.Checkpoint!.Payload.Json, StringComparer.Ordinal));
            Assert.True(read.Checkpoint.Payload.HasValidHash);
        }

        var mismatched = await reconstructed.CreateAsync(
            new WorkflowBackendCheckpointCreateRequest(
                session with { WorkflowVersionId = WorkflowVersionId.New() },
                Parent: null,
                WorkflowBackendCheckpointPayload.Create("{\"changed\":true}")));
        Assert.Equal(WorkflowBackendCheckpointCreateOutcome.SessionMetadataMismatch, mismatched.Outcome);

        var fakeParent = new WorkflowBackendCheckpointLink(
            session.Id,
            WorkflowBackendCheckpointId.New());
        var missingParent = await reconstructed.CreateAsync(
            new WorkflowBackendCheckpointCreateRequest(
                session,
                fakeParent,
                WorkflowBackendCheckpointPayload.Create("{\"child\":true}")));
        Assert.Equal(WorkflowBackendCheckpointCreateOutcome.ParentNotFound, missingParent.Outcome);
    }

    [Fact]
    public async Task PostgreSql_CheckpointPayloadTampering_ReturnsTypedCorruptOutcome()
    {
        await using var fixture = await CreateFixtureAsync("workflowhitlcheckpointcorrupt");
        var run = await SeedRunAsync(fixture.Factory);
        var store = fixture.CreateCheckpointStore();
        var created = await store.CreateAsync(
            new WorkflowBackendCheckpointCreateRequest(
                CreateCheckpointSession(run),
                Parent: null,
                WorkflowBackendCheckpointPayload.Create("{\"trusted\":true}")));

        await using (var dbContext = fixture.Factory.CreateDbContext())
        {
            var checkpoint = await dbContext.Set<WorkflowBackendCheckpointPayloadEntity>()
                .SingleAsync(item => item.Id == created.Checkpoint!.Index.Link.CheckpointId.Value);
            checkpoint.ProtectedPayload = "tampered";
            await dbContext.SaveChangesAsync();
        }

        var read = await store.ReadAsync(created.Checkpoint!.Index.Link);
        Assert.Equal(WorkflowBackendCheckpointReadOutcome.PayloadCorrupt, read.Outcome);
        Assert.Null(read.Checkpoint);
    }

    [Fact]
    public async Task PostgreSql_ExternalRequestBoundary_LeavesLegacyRowsNonResumable()
    {
        await using var fixture = await CreateFixtureAsync("workflowhitllegacyboundary");
        var seeded = await SeedWaitingRequestAsync(fixture, createBoundary: false);
        var store = new PersistentWorkflowExternalRequestBoundaryStore(fixture.Factory);

        var legacy = await store.ReadAsync(seeded.Request.Id);
        Assert.Equal(WorkflowExternalRequestBoundaryReadOutcome.LegacyNonResumable, legacy.Outcome);
        Assert.Null(legacy.Boundary);
    }

    [Fact]
    public async Task PostgreSql_ExternalRequestBoundary_AtomicallyLinksPreexistingNativeCheckpoint()
    {
        await using var fixture = await CreateFixtureAsync("workflowhitlinitiallink");
        var seeded = await SeedWaitingRequestAsync(fixture, createBoundary: true);

        var read = await new PersistentWorkflowExternalRequestBoundaryStore(fixture.Factory)
            .ReadAsync(seeded.Request.Id);
        Assert.Equal(WorkflowExternalRequestBoundaryReadOutcome.Found, read.Outcome);
        Assert.Equal(CreateBoundary(seeded.Request), read.Boundary);
        var continuation = seeded.Request.Continuation!;

        await using var dbContext = fixture.Factory.CreateDbContext();
        var checkpoint = await dbContext.Set<WorkflowBackendCheckpointPayloadEntity>()
            .AsNoTracking()
            .SingleAsync(item => item.Id == continuation.Checkpoint.CheckpointId.Value);
        Assert.Equal(seeded.Request.Id.Value, checkpoint.ExternalRequestId);
        Assert.Equal(
            continuation.Request.BackendRequestId.Value,
            checkpoint.BackendRequestId);
        Assert.Equal(
            continuation.Request.BackendRequestPortId.Value,
            checkpoint.BackendRequestPortId);
    }

    [Fact]
    public async Task PostgreSql_NativeCheckpointLink_AllowsCrossSessionRequestReuseAndRejectsSameSessionDuplicate()
    {
        await using var fixture = await CreateFixtureAsync("workflowhitllinkscope");
        var first = await SeedWaitingRequestAsync(fixture, createBoundary: true);
        var reusedBackendRequestId = first.Request.Continuation!.Request.BackendRequestId;
        var reusedPortId = first.Request.Continuation.Request.BackendRequestPortId;

        var duplicateCheckpoint = await fixture.CreateCheckpointStore().CreateAsync(
            new WorkflowBackendCheckpointCreateRequest(
                CreateCheckpointSession(first.Run),
                Parent: null,
                WorkflowBackendCheckpointPayload.Create("{\"native\":\"same-session-duplicate\"}")));
        Assert.True(duplicateCheckpoint.Succeeded);
        var duplicateCheckpointValue = duplicateCheckpoint.Checkpoint!;
        var duplicateLink = new WorkflowBackendExternalRequestLink(
            WorkflowExternalRequestId.New(),
            reusedBackendRequestId,
            reusedPortId);
        var duplicateRequest = CreateExternalRequest(
            first.Run,
            duplicateLink.ExternalRequestId,
            duplicateLink,
            duplicateCheckpointValue);
        await PersistRequestAndCheckpointAsync(
            fixture.Factory,
            first.Run,
            duplicateRequest,
            duplicateCheckpointValue);

        var boundaryStore = new PersistentWorkflowExternalRequestBoundaryStore(fixture.Factory);
        var rejected = await boundaryStore.UpsertAsync(CreateBoundary(duplicateRequest));
        Assert.Equal(WorkflowExternalRequestBoundarySaveOutcome.VersionConflict, rejected.Outcome);
        await AssertNativeCheckpointRequestLinkAsync(
            fixture.Factory,
            duplicateCheckpointValue.Index.Link.CheckpointId,
            expectedLink: null);

        var secondRun = await SeedRunAsync(fixture.Factory);
        var secondSession = CreateCheckpointSession(
            secondRun,
            new WorkflowBackendSessionId($"maf-session-{secondRun.RunId.Value:N}"));
        var crossSessionCheckpoint = await fixture.CreateCheckpointStore().CreateAsync(
            new WorkflowBackendCheckpointCreateRequest(
                secondSession,
                Parent: null,
                WorkflowBackendCheckpointPayload.Create("{\"native\":\"cross-session-reuse\"}")));
        Assert.True(crossSessionCheckpoint.Succeeded);
        var crossSessionCheckpointValue = crossSessionCheckpoint.Checkpoint!;
        var crossSessionLink = new WorkflowBackendExternalRequestLink(
            WorkflowExternalRequestId.New(),
            reusedBackendRequestId,
            reusedPortId);
        var crossSessionRequest = CreateExternalRequest(
            secondRun,
            crossSessionLink.ExternalRequestId,
            crossSessionLink,
            crossSessionCheckpointValue);
        await PersistRequestAndCheckpointAsync(
            fixture.Factory,
            secondRun,
            crossSessionRequest,
            crossSessionCheckpointValue);

        var saved = await boundaryStore.UpsertAsync(CreateBoundary(crossSessionRequest));
        Assert.Equal(WorkflowExternalRequestBoundarySaveOutcome.Created, saved.Outcome);
        await AssertNativeCheckpointRequestLinkAsync(
            fixture.Factory,
            crossSessionCheckpointValue.Index.Link.CheckpointId,
            crossSessionLink);
    }

    [Fact]
    public async Task PostgreSql_NativeCheckpointLink_ConcurrentSameSessionTupleHasSingleWinner()
    {
        await using var fixture = await CreateFixtureAsync("workflowhitllinkrace");
        var run = await SeedRunAsync(fixture.Factory);
        var session = CreateCheckpointSession(run);
        var checkpointStore = fixture.CreateCheckpointStore();
        var firstCheckpoint = await checkpointStore.CreateAsync(
            new WorkflowBackendCheckpointCreateRequest(
                session,
                Parent: null,
                WorkflowBackendCheckpointPayload.Create("{\"native\":\"first-racer\"}")));
        var secondCheckpoint = await checkpointStore.CreateAsync(
            new WorkflowBackendCheckpointCreateRequest(
                session,
                Parent: null,
                WorkflowBackendCheckpointPayload.Create("{\"native\":\"second-racer\"}")));
        Assert.True(firstCheckpoint.Succeeded);
        Assert.True(secondCheckpoint.Succeeded);

        var backendRequestId = new WorkflowBackendRequestId("native-request-race");
        var backendRequestPortId = new WorkflowBackendRequestPortId("native-port-race");
        var firstLink = new WorkflowBackendExternalRequestLink(
            WorkflowExternalRequestId.New(),
            backendRequestId,
            backendRequestPortId);
        var secondLink = new WorkflowBackendExternalRequestLink(
            WorkflowExternalRequestId.New(),
            backendRequestId,
            backendRequestPortId);
        var firstRequest = CreateExternalRequest(
            run,
            firstLink.ExternalRequestId,
            firstLink,
            firstCheckpoint.Checkpoint!);
        var secondRequest = CreateExternalRequest(
            run,
            secondLink.ExternalRequestId,
            secondLink,
            secondCheckpoint.Checkpoint!);
        await PersistRequestAndCheckpointAsync(
            fixture.Factory,
            run,
            firstRequest,
            firstCheckpoint.Checkpoint!);
        await PersistRequestAndCheckpointAsync(
            fixture.Factory,
            run,
            secondRequest,
            secondCheckpoint.Checkpoint!);

        var interceptor = new NativeRequestPrecheckBarrierInterceptor();
        var boundaryStore = new PersistentWorkflowExternalRequestBoundaryStore(
            fixture.Factory.WithInterceptor(interceptor));
        var results = await Task.WhenAll(
            boundaryStore.UpsertAsync(CreateBoundary(firstRequest)),
            boundaryStore.UpsertAsync(CreateBoundary(secondRequest)));

        Assert.Single(
            results,
            result => result.Outcome == WorkflowExternalRequestBoundarySaveOutcome.Created);
        Assert.Single(
            results,
            result => result.Outcome == WorkflowExternalRequestBoundarySaveOutcome.VersionConflict);
        var checkpointIds = new[]
        {
            firstCheckpoint.Checkpoint!.Index.Link.CheckpointId.Value,
            secondCheckpoint.Checkpoint!.Index.Link.CheckpointId.Value
        };
        var requestIds = new[] { firstRequest.Id.Value, secondRequest.Id.Value };
        await using var verificationContext = fixture.Factory.CreateDbContext();
        var persistedCheckpoints = await verificationContext
            .Set<WorkflowBackendCheckpointPayloadEntity>()
            .AsNoTracking()
            .Where(checkpoint => checkpointIds.Contains(checkpoint.Id))
            .ToArrayAsync();
        var linkedCheckpoint = Assert.Single(
            persistedCheckpoints,
            checkpoint => checkpoint.ExternalRequestId.HasValue);
        var unlinkedCheckpoint = Assert.Single(
            persistedCheckpoints,
            checkpoint => checkpoint.ExternalRequestId is null);
        Assert.Equal(backendRequestId.Value, linkedCheckpoint.BackendRequestId);
        Assert.Equal(backendRequestPortId.Value, linkedCheckpoint.BackendRequestPortId);
        Assert.Contains(linkedCheckpoint.ExternalRequestId!.Value, requestIds);
        Assert.Null(unlinkedCheckpoint.BackendRequestId);
        Assert.Null(unlinkedCheckpoint.BackendRequestPortId);
        var persistedBoundaryRequestId = await verificationContext
            .Set<WorkflowExternalRequestBoundaryEntity>()
            .Where(boundary => requestIds.Contains(boundary.RequestId))
            .Select(boundary => boundary.RequestId)
            .SingleAsync();
        Assert.Equal(linkedCheckpoint.ExternalRequestId.Value, persistedBoundaryRequestId);
    }

    [Fact]
    public async Task PostgreSql_OperationLedger_EnforcesReplayConflictLeaseAndExpiredTakeover()
    {
        await using var fixture = await CreateFixtureAsync("workflowhitloperationledger");
        var seeded = await SeedWaitingRequestAsync(fixture, createBoundary: true);
        var store = fixture.CreateOperationStore();
        var actor = new WorkflowLaunchActor(WorkflowLaunchActorKind.User, "operator-42");
        var idempotencyKey = new WorkflowExternalResponseIdempotencyKey("response-key-42");
        var fingerprint = WorkflowExternalResponseFingerprintFactory.Create(
            seeded.Request.Id,
            seeded.Request.Version,
            actor,
            TestAuthorizationScope,
            WorkflowExternalResponseAuthorizationPolicy.CurrentFingerprint,
            idempotencyKey,
            "{\"answer\":42}");
        var createRequest = new WorkflowExternalResponseOperationCreateRequest(
            WorkflowExternalResponseOperationId.New(),
            seeded.Request.Id,
            seeded.Run.RunId,
            seeded.Request.Version,
            fingerprint,
            actor,
            new WorkflowLaunchCorrelationId("operation-ledger-test"),
            TestTime);

        var created = await store.CreateOrReplayAsync(createRequest);
        Assert.Equal(WorkflowExternalResponseOperationCreateOutcome.Created, created.Outcome);
        var replayed = await store.CreateOrReplayAsync(createRequest with
        {
            OperationId = WorkflowExternalResponseOperationId.New()
        });
        Assert.Equal(WorkflowExternalResponseOperationCreateOutcome.Replayed, replayed.Outcome);
        Assert.Equal(created.Operation!.Id, replayed.Operation!.Id);

        var changedPayload = WorkflowExternalResponseFingerprintFactory.Create(
            seeded.Request.Id,
            seeded.Request.Version,
            actor,
            TestAuthorizationScope,
            WorkflowExternalResponseAuthorizationPolicy.CurrentFingerprint,
            idempotencyKey,
            "{\"answer\":43}");
        var conflict = await store.CreateOrReplayAsync(createRequest with
        {
            OperationId = WorkflowExternalResponseOperationId.New(),
            Fingerprint = changedPayload
        });
        Assert.Equal(WorkflowExternalResponseOperationCreateOutcome.IdempotencyConflict, conflict.Outcome);

        var owners = Enumerable.Range(0, 8)
            .Select(index => new WorkflowExternalResponseLeaseOwnerId($"host-{index}"))
            .ToArray();
        var claims = await Task.WhenAll(owners.Select(owner => store.TryClaimAsync(
            new WorkflowExternalResponseOperationClaimRequest(
                created.Operation.Id,
                created.Operation.ConcurrencyVersion,
                owner,
                TestTime.AddSeconds(1),
                TestTime.AddSeconds(11),
                MaximumAttempts: 4))));
        var firstClaim = Assert.Single(claims, claim => claim.Outcome == WorkflowExternalResponseOperationClaimOutcome.Claimed);
        var firstOwner = firstClaim.Claim!.Lease.OwnerId;
        Assert.All(claims.Where(claim => claim != firstClaim), claim =>
            Assert.Equal(WorkflowExternalResponseOperationClaimOutcome.ConcurrencyConflict, claim.Outcome));

        var activeConflict = await store.TryClaimAsync(
            new WorkflowExternalResponseOperationClaimRequest(
                created.Operation.Id,
                firstClaim.Operation!.ConcurrencyVersion,
                new WorkflowExternalResponseLeaseOwnerId("active-contender"),
                TestTime.AddSeconds(2),
                TestTime.AddSeconds(12),
                MaximumAttempts: 4));
        Assert.Equal(WorkflowExternalResponseOperationClaimOutcome.ActiveLease, activeConflict.Outcome);

        var resuming = await store.TryMarkResumingAsync(
            new WorkflowExternalResponseOperationMarkResumingRequest(
                created.Operation.Id,
                firstClaim.Operation.ConcurrencyVersion,
                firstOwner,
                firstClaim.Claim.Lease.Epoch,
                TestTime.AddSeconds(3)));
        Assert.Equal(WorkflowExternalResponseOperationMutationOutcome.Updated, resuming.Outcome);

        var takeoverOwner = new WorkflowExternalResponseLeaseOwnerId("takeover-host");
        var takeover = await store.TryClaimAsync(
            new WorkflowExternalResponseOperationClaimRequest(
                created.Operation.Id,
                resuming.Operation!.ConcurrencyVersion,
                takeoverOwner,
                TestTime.AddSeconds(12),
                TestTime.AddSeconds(22),
                MaximumAttempts: 4));
        Assert.Equal(WorkflowExternalResponseOperationClaimOutcome.Claimed, takeover.Outcome);
        Assert.Equal(2, takeover.Operation!.Attempt);
        Assert.Equal(2, takeover.Claim!.Lease.Epoch.Value);
        Assert.Equal(WorkflowExternalResponseOperationState.Resuming, takeover.Claim.Recovery!.PriorState);
        Assert.Equal(
            [
                WorkflowExternalResponseOperationState.FailedRetryable,
                WorkflowExternalResponseOperationState.Claimed
            ],
            takeover.Claim.Recovery.TransitionPath);

        var staleOwner = await store.TryRenewLeaseAsync(
            new WorkflowExternalResponseOperationLeaseRenewalRequest(
                takeover.Operation.Id,
                takeover.Operation.ConcurrencyVersion,
                firstOwner,
                firstClaim.Claim.Lease.Epoch,
                TestTime.AddSeconds(13),
                TestTime.AddSeconds(23)));
        Assert.Equal(WorkflowExternalResponseOperationMutationOutcome.LeaseConflict, staleOwner.Outcome);

        var thirdClaim = await store.TryClaimAsync(
            new WorkflowExternalResponseOperationClaimRequest(
                takeover.Operation.Id,
                takeover.Operation.ConcurrencyVersion,
                takeoverOwner,
                TestTime.AddSeconds(23),
                TestTime.AddSeconds(33),
                MaximumAttempts: 4));
        Assert.Equal(WorkflowExternalResponseOperationClaimOutcome.Claimed, thirdClaim.Outcome);
        var fourthClaim = await store.TryClaimAsync(
            new WorkflowExternalResponseOperationClaimRequest(
                thirdClaim.Operation!.Id,
                thirdClaim.Operation.ConcurrencyVersion,
                takeoverOwner,
                TestTime.AddSeconds(34),
                TestTime.AddSeconds(44),
                MaximumAttempts: 4));
        Assert.Equal(WorkflowExternalResponseOperationClaimOutcome.Claimed, fourthClaim.Outcome);
        var exhausted = await store.TryClaimAsync(
            new WorkflowExternalResponseOperationClaimRequest(
                fourthClaim.Operation!.Id,
                fourthClaim.Operation.ConcurrencyVersion,
                takeoverOwner,
                TestTime.AddSeconds(45),
                TestTime.AddSeconds(55),
                MaximumAttempts: 4));
        Assert.Equal(WorkflowExternalResponseOperationClaimOutcome.AttemptLimitReached, exhausted.Outcome);
        Assert.Equal(WorkflowExternalResponseOperationState.FailedTerminal, exhausted.Operation!.State);
        Assert.Equal(WorkflowExternalResponseOperationOutcomeCode.AttemptLimitReached, exhausted.Operation.OutcomeCode);
        Assert.Equal(TestTime.AddSeconds(45), exhausted.Operation.CompletedAtUtc);
        Assert.Null(exhausted.Operation.Lease);
        Assert.Equal(fourthClaim.Operation.ConcurrencyVersion.Next(), exhausted.Operation.ConcurrencyVersion);
        Assert.DoesNotContain(
            await store.ListRecoverableAsync(TestTime.AddHours(1), maximumCount: 20),
            operation => operation.Id == exhausted.Operation.Id);

        var reconstructed = fixture.CreateOperationStore(reconstructDataProtectionProvider: true);
        var recovered = await reconstructed.GetAsync(exhausted.Operation.Id);
        Assert.Equal(exhausted.Operation.State, recovered!.State);
        Assert.Equal(exhausted.Operation.Lease, recovered.Lease);
        Assert.Equal("{\"answer\":42}", recovered.ResponsePayload.Json);
    }

    [Fact]
    public async Task PostgreSql_OperationReplay_WaitsForConcurrentLeaseMutationAndReturnsCurrentState()
    {
        await using var fixture = await CreateFixtureAsync("workflowhitloperationreplayrace");
        var seeded = await SeedWaitingRequestAsync(fixture, createBoundary: true);
        var store = fixture.CreateOperationStore();
        var actor = new WorkflowLaunchActor(WorkflowLaunchActorKind.User, "replay-race-operator");
        var fingerprint = WorkflowExternalResponseFingerprintFactory.Create(
            seeded.Request.Id,
            seeded.Request.Version,
            actor,
            TestAuthorizationScope,
            WorkflowExternalResponseAuthorizationPolicy.CurrentFingerprint,
            new WorkflowExternalResponseIdempotencyKey("replay-race-key"),
            "{\"answer\":42}");
        var createRequest = new WorkflowExternalResponseOperationCreateRequest(
            WorkflowExternalResponseOperationId.New(),
            seeded.Request.Id,
            seeded.Run.RunId,
            seeded.Request.Version,
            fingerprint,
            actor,
            new WorkflowLaunchCorrelationId("replay-race"),
            TestTime);
        var created = await store.CreateOrReplayAsync(createRequest);
        var owner = new WorkflowExternalResponseLeaseOwnerId("replay-race-host");
        var claimed = await store.TryClaimAsync(
            new WorkflowExternalResponseOperationClaimRequest(
                created.Operation!.Id,
                created.Operation.ConcurrencyVersion,
                owner,
                TestTime.AddSeconds(1),
                TestTime.AddMinutes(1),
                MaximumAttempts: 3));
        Assert.Equal(WorkflowExternalResponseOperationClaimOutcome.Claimed, claimed.Outcome);

        await using var lockConnection = new NpgsqlConnection(fixture.ConnectionString);
        await lockConnection.OpenAsync();
        await using var lockTransaction = await lockConnection.BeginTransactionAsync();
        await using (var lockCommand = lockConnection.CreateCommand())
        {
            lockCommand.Transaction = lockTransaction;
            lockCommand.CommandText =
                """
                SELECT "Id"
                FROM "AgentFramework_WorkflowExternalResponseOperations"
                WHERE "Id" = @operationId
                FOR UPDATE
                """;
            lockCommand.Parameters.AddWithValue("operationId", created.Operation.Id.Value);
            Assert.Equal(created.Operation.Id.Value, await lockCommand.ExecuteScalarAsync());
        }

        var interceptor = new OperationReplayCommandInterceptor(seeded.Request.Id.Value);
        var racingStore = fixture.CreateOperationStore(interceptor);
        var renewedLeaseExpiry = TestTime.AddMinutes(2);
        var renewalTask = racingStore.TryRenewLeaseAsync(
            new WorkflowExternalResponseOperationLeaseRenewalRequest(
                claimed.Operation!.Id,
                claimed.Operation.ConcurrencyVersion,
                owner,
                claimed.Claim!.Lease.Epoch,
                TestTime.AddSeconds(2),
                renewedLeaseExpiry));
        Task<WorkflowExternalResponseOperationCreateResult>? replayTask = null;
        string replayReadCommand;
        try
        {
            await WaitForBlockedDatabaseCommandAsync(fixture.ConnectionString);
            replayTask = racingStore.CreateOrReplayAsync(createRequest with
            {
                OperationId = WorkflowExternalResponseOperationId.New(),
                AcceptedAtUtc = TestTime.AddSeconds(3)
            });
            replayReadCommand = await interceptor.OperationReadStarted.Task.WaitAsync(TimeSpan.FromSeconds(10));
        }
        finally
        {
            await lockTransaction.RollbackAsync();
        }

        var renewed = await renewalTask;
        var replayed = await replayTask;

        Assert.Contains("FOR UPDATE", replayReadCommand, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(WorkflowExternalResponseOperationMutationOutcome.Updated, renewed.Outcome);
        Assert.Equal(WorkflowExternalResponseOperationCreateOutcome.Replayed, replayed.Outcome);
        Assert.Equal(renewedLeaseExpiry, replayed.Operation!.Lease!.ExpiresAtUtc);
        Assert.Equal(renewed.Operation!.ConcurrencyVersion, replayed.Operation.ConcurrencyVersion);
        await using var verificationContext = fixture.Factory.CreateDbContext();
        var persisted = await verificationContext.Set<WorkflowExternalResponseOperationEntity>()
            .AsNoTracking()
            .SingleAsync(operation => operation.Id == created.Operation.Id.Value);
        Assert.Equal(1, persisted.ReplayCount);
        Assert.Equal(renewedLeaseExpiry, persisted.LeaseExpiresAtUtc);
    }

    [Fact]
    public async Task PostgreSql_OperationLedger_AllowsOnlyOneActiveClaimPerRun()
    {
        await using var fixture = await CreateFixtureAsync("workflowhitlrunclaimrace");
        var seeded = await SeedWaitingRequestAsync(fixture, createBoundary: true);
        var secondRequestId = WorkflowExternalRequestId.New();
        var secondRequest = seeded.Request with
        {
            Id = secondRequestId,
            Continuation = seeded.Request.Continuation! with
            {
                Request = seeded.Request.Continuation.Request with
                {
                    ExternalRequestId = secondRequestId
                }
            }
        };
        await using (var dbContext = fixture.Factory.CreateDbContext())
        {
            dbContext.Set<WorkflowExternalRequestRecordEntity>().Add(
                WorkflowExternalRequestRecordEntity.FromRequest(secondRequest));
            var boundaryEntity = new WorkflowExternalRequestBoundaryEntity
            {
                RequestId = secondRequest.Id.Value
            };
            PersistentWorkflowExternalRequestBoundaryStore.Apply(
                boundaryEntity,
                CreateBoundary(secondRequest));
            dbContext.Set<WorkflowExternalRequestBoundaryEntity>().Add(boundaryEntity);
            await dbContext.SaveChangesAsync();
        }

        var store = fixture.CreateOperationStore();
        var actor = new WorkflowLaunchActor(WorkflowLaunchActorKind.User, "run-race-operator");
        async Task<WorkflowExternalResponseOperationRecord> CreateOperationAsync(
            WorkflowExternalRequestRecord request,
            string idempotencyKey)
        {
            var fingerprint = WorkflowExternalResponseFingerprintFactory.Create(
                request.Id,
                request.Version,
                actor,
                TestAuthorizationScope,
                WorkflowExternalResponseAuthorizationPolicy.CurrentFingerprint,
                new WorkflowExternalResponseIdempotencyKey(idempotencyKey),
                "{\"answer\":true}");
            var created = await store.CreateOrReplayAsync(
                new WorkflowExternalResponseOperationCreateRequest(
                    WorkflowExternalResponseOperationId.New(),
                    request.Id,
                    request.RunId,
                    request.Version,
                    fingerprint,
                    actor,
                    new WorkflowLaunchCorrelationId(idempotencyKey),
                    TestTime));
            Assert.Equal(WorkflowExternalResponseOperationCreateOutcome.Created, created.Outcome);
            return created.Operation!;
        }

        var operations = new[]
        {
            await CreateOperationAsync(seeded.Request, "run-race-1"),
            await CreateOperationAsync(secondRequest, "run-race-2")
        };
        var claims = await Task.WhenAll(operations.Select((operation, index) =>
            store.TryClaimAsync(
                new WorkflowExternalResponseOperationClaimRequest(
                    operation.Id,
                    operation.ConcurrencyVersion,
                    new WorkflowExternalResponseLeaseOwnerId($"run-race-host-{index}"),
                    TestTime.AddSeconds(1),
                    TestTime.AddMinutes(1),
                    MaximumAttempts: 3))));

        Assert.Single(claims, claim => claim.Outcome == WorkflowExternalResponseOperationClaimOutcome.Claimed);
        Assert.Single(claims, claim => claim.Outcome == WorkflowExternalResponseOperationClaimOutcome.ActiveLease);
    }

    [Fact]
    public async Task PostgreSql_OperationPayloadHashMismatch_ThrowsTypedCorruptState()
    {
        await using var fixture = await CreateFixtureAsync("workflowhitlresponsecorrupt");
        var seeded = await SeedWaitingRequestAsync(fixture, createBoundary: true);
        var store = fixture.CreateOperationStore();
        var actor = new WorkflowLaunchActor(WorkflowLaunchActorKind.User, "hash-auditor");
        var fingerprint = WorkflowExternalResponseFingerprintFactory.Create(
            seeded.Request.Id,
            seeded.Request.Version,
            actor,
            TestAuthorizationScope,
            WorkflowExternalResponseAuthorizationPolicy.CurrentFingerprint,
            new WorkflowExternalResponseIdempotencyKey("response-hash-mismatch"),
            "{\"secret\":\"response-secret\"}");
        var created = await store.CreateOrReplayAsync(
            new WorkflowExternalResponseOperationCreateRequest(
                WorkflowExternalResponseOperationId.New(),
                seeded.Request.Id,
                seeded.Run.RunId,
                seeded.Request.Version,
                fingerprint,
                actor,
                new WorkflowLaunchCorrelationId("response-hash-mismatch"),
                TestTime));

        await using (var dbContext = fixture.Factory.CreateDbContext())
        {
            var operation = await dbContext.Set<WorkflowExternalResponseOperationEntity>()
                .SingleAsync(item => item.Id == created.Operation!.Id.Value);
            Assert.DoesNotContain("response-secret", operation.ProtectedResponsePayload, StringComparison.Ordinal);
            operation.ResponsePayloadHash = new string('0', 64);
            await dbContext.SaveChangesAsync();
        }

        await Assert.ThrowsAsync<WorkflowExternalResponsePayloadCorruptException>(
            () => store.GetAsync(created.Operation!.Id));
        var resumeStore = new PersistentWorkflowResumeBoundaryStore(
            fixture.Factory,
            fixture.CreateDataProtectionProvider());
        await Assert.ThrowsAsync<WorkflowExternalResponsePayloadCorruptException>(
            () => resumeStore.LoadAsync(new WorkflowResumeBoundaryLoadRequest(created.Operation!.Id)));
    }

    [Fact]
    public async Task PostgreSql_ResumeBoundaryLoad_ClassifiesCheckpointRecoveryFailures()
    {
        await using var fixture = await CreateFixtureAsync("workflowhitlloadfailures");
        var seeded = await SeedWaitingRequestAsync(fixture, createBoundary: true);
        var operationStore = fixture.CreateOperationStore();
        var actor = new WorkflowLaunchActor(WorkflowLaunchActorKind.User, "load-failure-auditor");
        var fingerprint = WorkflowExternalResponseFingerprintFactory.Create(
            seeded.Request.Id,
            seeded.Request.Version,
            actor,
            TestAuthorizationScope,
            WorkflowExternalResponseAuthorizationPolicy.CurrentFingerprint,
            new WorkflowExternalResponseIdempotencyKey("load-failure-response"),
            "{\"answer\":\"inspect\"}");
        var created = await operationStore.CreateOrReplayAsync(
            new WorkflowExternalResponseOperationCreateRequest(
                WorkflowExternalResponseOperationId.New(),
                seeded.Request.Id,
                seeded.Run.RunId,
                seeded.Request.Version,
                fingerprint,
                actor,
                new WorkflowLaunchCorrelationId("load-failure-classification"),
                TestTime));
        var resumeStore = new PersistentWorkflowResumeBoundaryStore(
            fixture.Factory,
            fixture.CreateDataProtectionProvider());
        var loadRequest = new WorkflowResumeBoundaryLoadRequest(created.Operation!.Id);
        var loaded = await resumeStore.LoadAsync(loadRequest);
        Assert.Equal(WorkflowResumeBoundaryLoadOutcome.Found, loaded.Outcome);
        Assert.NotNull(loaded.Context);
        Assert.Equal(WorkflowExternalRequestState.Pending, loaded.Context.Request.EffectiveState);
        Assert.Equal(WorkflowExternalRequestState.ResponseClaimed, loaded.Context.Boundary.State);

        async Task AssertBoundaryMutationAsync(
            Func<WorkflowExternalRequestBoundaryRecord, WorkflowExternalRequestBoundaryRecord> mutate,
            WorkflowResumeBoundaryLoadOutcome expected)
        {
            await using (var dbContext = fixture.Factory.CreateDbContext())
            {
                var boundaryEntity = await dbContext.Set<WorkflowExternalRequestBoundaryEntity>()
                    .SingleAsync(item => item.RequestId == seeded.Request.Id.Value);
                var original = PersistentWorkflowExternalRequestBoundaryStore.ToRecord(boundaryEntity);
                PersistentWorkflowExternalRequestBoundaryStore.Apply(boundaryEntity, mutate(original));
                await dbContext.SaveChangesAsync();
            }

            Assert.Equal(expected, (await resumeStore.LoadAsync(loadRequest)).Outcome);

            await using var restoreContext = fixture.Factory.CreateDbContext();
            var restoreEntity = await restoreContext.Set<WorkflowExternalRequestBoundaryEntity>()
                .SingleAsync(item => item.RequestId == seeded.Request.Id.Value);
            PersistentWorkflowExternalRequestBoundaryStore.Apply(restoreEntity, CreateBoundary(seeded.Request) with
            {
                State = WorkflowExternalRequestState.ResponseClaimed
            });
            await restoreContext.SaveChangesAsync();
        }

        await AssertBoundaryMutationAsync(
            boundary => boundary with
            {
                Continuation = boundary.Continuation with
                {
                    Checkpoint = boundary.Continuation.Checkpoint with
                    {
                        CheckpointId = WorkflowBackendCheckpointId.New()
                    }
                }
            },
            WorkflowResumeBoundaryLoadOutcome.CheckpointMissing);
        await AssertBoundaryMutationAsync(
            boundary => boundary with
            {
                Continuation = boundary.Continuation with
                {
                    Checkpoint = boundary.Continuation.Checkpoint with
                    {
                        SessionId = new WorkflowBackendSessionId(Guid.NewGuid().ToString("N"))
                    }
                }
            },
            WorkflowResumeBoundaryLoadOutcome.CheckpointIncompatible);
        await AssertBoundaryMutationAsync(
            boundary => boundary with
            {
                Continuation = boundary.Continuation with
                {
                    CompilerContractVersion = new WorkflowCompilerContractVersion(99)
                }
            },
            WorkflowResumeBoundaryLoadOutcome.CheckpointIncompatible);
        await AssertBoundaryMutationAsync(
            boundary => boundary with
            {
                Continuation = boundary.Continuation with
                {
                    CheckpointPayloadHash = new WorkflowBackendCheckpointPayloadHash(new string('0', 64))
                }
            },
            WorkflowResumeBoundaryLoadOutcome.CheckpointCorrupt);
        await AssertBoundaryMutationAsync(
            boundary => boundary with
            {
                Continuation = boundary.Continuation with
                {
                    TopologyFingerprint = WorkflowTopologyFingerprint.Create("mutated-topology")
                }
            },
            WorkflowResumeBoundaryLoadOutcome.TopologyMismatch);

        string protectedPayload;
        await using (var dbContext = fixture.Factory.CreateDbContext())
        {
            var checkpoint = await dbContext.Set<WorkflowBackendCheckpointPayloadEntity>()
                .SingleAsync(item => item.Id == seeded.Request.Continuation!.Checkpoint.CheckpointId.Value);
            protectedPayload = checkpoint.ProtectedPayload;
            checkpoint.ProtectedPayload = "tampered-checkpoint-ciphertext";
            await dbContext.SaveChangesAsync();
        }

        Assert.Equal(
            WorkflowResumeBoundaryLoadOutcome.CheckpointCorrupt,
            (await resumeStore.LoadAsync(loadRequest)).Outcome);
        await using (var dbContext = fixture.Factory.CreateDbContext())
        {
            var checkpoint = await dbContext.Set<WorkflowBackendCheckpointPayloadEntity>()
                .SingleAsync(item => item.Id == seeded.Request.Continuation!.Checkpoint.CheckpointId.Value);
            checkpoint.ProtectedPayload = protectedPayload;
            await dbContext.SaveChangesAsync();
        }

        Guid workflowVersionId;
        await using (var dbContext = fixture.Factory.CreateDbContext())
        {
            var session = await dbContext.Set<WorkflowBackendCheckpointSessionEntity>()
                .SingleAsync(item => item.Id == seeded.Request.Continuation!.Checkpoint.SessionId.Value);
            workflowVersionId = session.WorkflowVersionId;
            session.WorkflowVersionId = WorkflowVersionId.New().Value;
            await dbContext.SaveChangesAsync();
        }

        Assert.Equal(
            WorkflowResumeBoundaryLoadOutcome.WorkflowVersionMismatch,
            (await resumeStore.LoadAsync(loadRequest)).Outcome);
        await using (var dbContext = fixture.Factory.CreateDbContext())
        {
            var session = await dbContext.Set<WorkflowBackendCheckpointSessionEntity>()
                .SingleAsync(item => item.Id == seeded.Request.Continuation!.Checkpoint.SessionId.Value);
            session.WorkflowVersionId = workflowVersionId;
            await dbContext.SaveChangesAsync();
        }
    }

    [Fact]
    public async Task PostgreSql_TerminalResumeFailure_CanStillCancelWaitingRun()
    {
        await using var fixture = await CreateFixtureAsync("workflowhitlterminalcancel");
        var seeded = await SeedWaitingRequestAsync(fixture, createBoundary: true);
        var operationStore = fixture.CreateOperationStore();
        var actor = new WorkflowLaunchActor(WorkflowLaunchActorKind.User, "cancellation-operator");
        var fingerprint = WorkflowExternalResponseFingerprintFactory.Create(
            seeded.Request.Id,
            seeded.Request.Version,
            actor,
            TestAuthorizationScope,
            WorkflowExternalResponseAuthorizationPolicy.CurrentFingerprint,
            new WorkflowExternalResponseIdempotencyKey("terminal-cancel-response"),
            "{\"answer\":\"cannot-resume\"}");
        var created = await operationStore.CreateOrReplayAsync(
            new WorkflowExternalResponseOperationCreateRequest(
                WorkflowExternalResponseOperationId.New(),
                seeded.Request.Id,
                seeded.Run.RunId,
                seeded.Request.Version,
                fingerprint,
                actor,
                new WorkflowLaunchCorrelationId("terminal-cancel"),
                TestTime));
        var owner = new WorkflowExternalResponseLeaseOwnerId("terminal-cancel-owner");
        var claimed = await operationStore.TryClaimAsync(
            new WorkflowExternalResponseOperationClaimRequest(
                created.Operation!.Id,
                created.Operation.ConcurrencyVersion,
                owner,
                TestTime.AddSeconds(1),
                TestTime.AddMinutes(1),
                MaximumAttempts: 3));
        var resuming = await operationStore.TryMarkResumingAsync(
            new WorkflowExternalResponseOperationMarkResumingRequest(
                claimed.Operation!.Id,
                claimed.Operation.ConcurrencyVersion,
                owner,
                claimed.Claim!.Lease.Epoch,
                TestTime.AddSeconds(2)));
        var terminal = await operationStore.TryFailAsync(
            new WorkflowExternalResponseOperationFailureRequest(
                resuming.Operation!.Id,
                resuming.Operation.ConcurrencyVersion,
                owner,
                claimed.Claim.Lease.Epoch,
                WorkflowExternalResponseOperationState.FailedTerminal,
                WorkflowExternalResponseOperationOutcomeCode.CheckpointCorrupt,
                "The native checkpoint is corrupt.",
                TestTime.AddSeconds(3)));
        Assert.Equal(WorkflowExternalResponseOperationMutationOutcome.Updated, terminal.Outcome);

        var resumeStore = new PersistentWorkflowResumeBoundaryStore(
            fixture.Factory,
            fixture.CreateDataProtectionProvider());
        var cancelled = await resumeStore.TryCancelAsync(
            new WorkflowResumeBoundaryCancellationRequest(
                seeded.Run.RunId,
                seeded.Request.Id,
                seeded.Request.Version,
                TestTime.AddSeconds(4),
                "Cancelled after terminal recovery failure."));

        Assert.Equal(WorkflowResumeBoundaryCancellationOutcome.Cancelled, cancelled.Outcome);
        Assert.Equal(WorkflowRunState.Cancelled, cancelled.Run!.State);
        Assert.Equal(WorkflowExternalRequestState.Cancelled, cancelled.Request!.EffectiveState);
        Assert.Equal(WorkflowExternalResponseOperationState.FailedTerminal, cancelled.Operation!.State);
        Assert.Equal(terminal.Operation!.ConcurrencyVersion, cancelled.Operation.ConcurrencyVersion);
        Assert.Equal(WorkflowExternalResponseOperationOutcomeCode.CheckpointCorrupt, cancelled.Operation.OutcomeCode);
    }

    [Fact]
    public async Task PostgreSql_ResumeBoundary_CommitsEntireConsecutiveWaitOrNothing()
    {
        await using var fixture = await CreateFixtureAsync("workflowhitlatomicresume");
        var seeded = await SeedWaitingRequestAsync(fixture, createBoundary: true);
        var operationStore = fixture.CreateOperationStore();
        var actor = new WorkflowLaunchActor(WorkflowLaunchActorKind.User, "atomic-operator");
        var fingerprint = WorkflowExternalResponseFingerprintFactory.Create(
            seeded.Request.Id,
            seeded.Request.Version,
            actor,
            TestAuthorizationScope,
            WorkflowExternalResponseAuthorizationPolicy.CurrentFingerprint,
            new WorkflowExternalResponseIdempotencyKey("atomic-response-key"),
            "{\"answer\":\"continue\"}");
        var created = await operationStore.CreateOrReplayAsync(
            new WorkflowExternalResponseOperationCreateRequest(
                WorkflowExternalResponseOperationId.New(),
                seeded.Request.Id,
                seeded.Run.RunId,
                seeded.Request.Version,
                fingerprint,
                actor,
                new WorkflowLaunchCorrelationId("atomic-resume"),
                TestTime));
        var owner = new WorkflowExternalResponseLeaseOwnerId("atomic-host");
        var claimed = await operationStore.TryClaimAsync(
            new WorkflowExternalResponseOperationClaimRequest(
                created.Operation!.Id,
                created.Operation.ConcurrencyVersion,
                owner,
                TestTime.AddSeconds(1),
                TestTime.AddMinutes(1),
                MaximumAttempts: 3));
        var resuming = await operationStore.TryMarkResumingAsync(
            new WorkflowExternalResponseOperationMarkResumingRequest(
                created.Operation.Id,
                claimed.Operation!.ConcurrencyVersion,
                owner,
                claimed.Claim!.Lease.Epoch,
                TestTime.AddSeconds(2)));

        var nextRequestId = WorkflowExternalRequestId.New();
        var nextBackendLink = new WorkflowBackendExternalRequestLink(
            nextRequestId,
            new WorkflowBackendRequestId("native-request-next"),
            new WorkflowBackendRequestPortId("native-port-next"));
        var nativeCheckpoint = await fixture.CreateCheckpointStore().CreateAsync(
            new WorkflowBackendCheckpointCreateRequest(
                CreateCheckpointSession(seeded.Run),
                Parent: null,
                WorkflowBackendCheckpointPayload.Create("{\"native\":\"next-boundary\"}")));
        Assert.True(nativeCheckpoint.Succeeded);
        var nativeCheckpointValue = nativeCheckpoint.Checkpoint!;
        var nextRequest = CreateExternalRequest(
            seeded.Run,
            nextRequestId,
            nextBackendLink,
            nativeCheckpointValue);
        var checkpointMetadata = new WorkflowCheckpointRecord(
            WorkflowCheckpointId.New(),
            seeded.Run.RunId,
            seeded.Run.WorkflowId,
            seeded.Run.VersionId,
            seeded.Run.Backend,
            WorkflowCheckpointKind.WaitingForInput,
            WorkflowCheckpointTrustBoundary.TrustedRuntimeState,
            WorkflowResumeAvailability.Available,
            nextRequest.NodeId,
            nextRequest.Id,
            nativeCheckpointValue.Index.Link.CheckpointId.Value,
            $"maf-checkpoint://{nativeCheckpointValue.Index.Link.SessionId.Value}/{nativeCheckpointValue.Index.Link.CheckpointId.Value}",
            nativeCheckpointValue.Payload.Sha256.Value,
            "Waiting again.",
            ResumeUnavailableReason: string.Empty,
            TestTime.AddSeconds(3),
            ResumedAtUtc: null);
        var workflowEvent = new WorkflowEventRecord(
            Guid.NewGuid(),
            seeded.Run.RunId,
            WorkflowEventKind.WaitingForInput,
            nextRequest.NodeId,
            "Waiting again.",
            "{}",
            TestTime.AddSeconds(3));
        var artifact = new WorkflowArtifactRecord(
            WorkflowArtifactId.New(),
            seeded.Run.RunId,
            WorkflowArtifactKind.Json,
            nextRequest.NodeId,
            "next-request.json",
            "application/json",
            "artifacts/next-request.json",
            "Persisted request artifact.",
            TestTime.AddSeconds(3));
        var usageObservation = new WorkflowUsageObservation(
            WorkflowUsageObservationId.New(),
            seeded.Run.RunId,
            seeded.Run.WorkflowId,
            seeded.Run.VersionId,
            nextRequest.NodeId,
            new WorkflowExecutorId("hitl.resume"),
            ComponentId: null,
            WorkflowUsageProducerKind.Executor,
            Guid.NewGuid(),
            Attempt: 1,
            ProviderProfileId: null,
            "persistent-test-provider",
            ProviderKind.OpenAi,
            ProviderTransportKind.Responses,
            "persistent-test-model",
            ProviderUsageSourcePhases.AgentRuntimeContinuation,
            WorkflowUsageStatus.Observed,
            WorkflowPricingStatus.Known,
            WorkflowUsagePricingProvenance.ProviderReported,
            InputTokens: 12,
            CachedInputTokens: 2,
            OutputTokens: 5,
            ReasoningTokens: 1,
            TotalTokens: 17,
            ToolCallCount: 0,
            CostUsd: 0.001m,
            PricingProfileHash: "persistent-test-pricing",
            PricingVersion: "v1",
            ProviderRequestId: "provider-request-resume",
            ProviderResponseId: "provider-response-resume",
            TestTime.AddSeconds(2),
            TestTime.AddSeconds(3),
            TestTime.AddSeconds(3),
            Origin: null);
        var backendRun = seeded.Run with
        {
            Summary = "Waiting again.",
            UpdatedAtUtc = TestTime.AddSeconds(3)
        };
        var backendResult = new WorkflowBackendStartResult(
            backendRun,
            [workflowEvent],
            [nextRequest],
            [artifact])
        {
            Checkpoints = [checkpointMetadata],
            UsageObservations = [usageObservation]
        };
        var finalResult = new WorkflowExternalResponseOperationFinalResult(
            WorkflowExternalResponseOperationState.WaitingAgain,
            WorkflowExternalResponseOperationOutcomeCode.WaitingAgain,
            "The response was accepted and another request is waiting.",
            WorkflowRunState.WaitingForInput)
        {
            ResultCheckpointId = checkpointMetadata.Id,
            NextExternalRequestId = nextRequest.Id
        };
        var boundaryStore = new PersistentWorkflowResumeBoundaryStore(
            fixture.Factory,
            fixture.CreateDataProtectionProvider());
        var invalid = await boundaryStore.TryCommitAsync(
            new WorkflowResumeBoundaryCommitRequest(
                created.Operation.Id,
                resuming.Operation!.ConcurrencyVersion,
                owner,
                claimed.Claim.Lease.Epoch,
                seeded.Request.Version,
                backendResult with
                {
                    Checkpoints =
                    [
                        checkpointMetadata with
                        {
                            PayloadHash = new string('0', 64)
                        }
                    ]
                },
                finalResult,
                TestTime.AddSeconds(4)));
        Assert.Equal(WorkflowResumeBoundaryCommitOutcome.InvalidResultBoundary, invalid.Outcome);
        await AssertResumeBoundaryUnchangedAsync(fixture.Factory, seeded, created.Operation.Id, nextRequest.Id);
        await AssertNativeCheckpointRequestLinkAsync(
            fixture.Factory,
            nativeCheckpoint.Checkpoint!.Index.Link.CheckpointId,
            expectedLink: null);

        var committed = await boundaryStore.TryCommitAsync(
            new WorkflowResumeBoundaryCommitRequest(
                created.Operation.Id,
                resuming.Operation.ConcurrencyVersion,
                owner,
                claimed.Claim.Lease.Epoch,
                seeded.Request.Version,
                backendResult,
                finalResult,
                TestTime.AddSeconds(5)));
        Assert.Equal(WorkflowResumeBoundaryCommitOutcome.Committed, committed.Outcome);
        Assert.Equal(WorkflowExternalResponseOperationState.WaitingAgain, committed.Operation!.State);
        Assert.Equal(nextRequest.Id, committed.NextRequest!.Id);
        Assert.Equal("{\"answer\":\"continue\"}", committed.Operation.ResponsePayload.Json);

        await using var dbContext = fixture.Factory.CreateDbContext();
        var sourceRequest = await dbContext.Set<WorkflowExternalRequestRecordEntity>()
            .SingleAsync(item => item.Id == seeded.Request.Id.Value);
        var sourceBoundary = await dbContext.Set<WorkflowExternalRequestBoundaryEntity>()
            .SingleAsync(item => item.RequestId == seeded.Request.Id.Value);
        var persistedRun = await dbContext.Set<WorkflowRunRecordEntity>()
            .SingleAsync(item => item.RunId == seeded.Run.RunId.Value);
        var persistedOperation = await dbContext.Set<WorkflowExternalResponseOperationEntity>()
            .SingleAsync(item => item.Id == created.Operation.Id.Value);
        var persistedNativeCheckpoint = await dbContext.Set<WorkflowBackendCheckpointPayloadEntity>()
            .SingleAsync(item => item.Id == nativeCheckpoint.Checkpoint!.Index.Link.CheckpointId.Value);
        Assert.Equal(string.Empty, sourceRequest.ResponseJson);
        Assert.DoesNotContain("continue", persistedOperation.ProtectedResponsePayload, StringComparison.Ordinal);
        Assert.DoesNotContain("next-boundary", persistedNativeCheckpoint.ProtectedPayload, StringComparison.Ordinal);
        Assert.Equal(TestTime.AddSeconds(5), sourceRequest.RespondedAtUtc);
        Assert.Equal((int)WorkflowExternalRequestState.Responded, sourceBoundary.State);
        Assert.Equal(WorkflowRunState.WaitingForInput, persistedRun.State);
        Assert.True(await dbContext.Set<WorkflowEventRecordEntity>().AnyAsync(item => item.Id == workflowEvent.Id));
        Assert.True(await dbContext.Set<WorkflowArtifactRecordEntity>().AnyAsync(item => item.Id == artifact.Id.Value));
        Assert.True(await dbContext.Set<WorkflowCheckpointRecordEntity>().AnyAsync(item => item.Id == checkpointMetadata.Id.Value));
        Assert.True(await dbContext.Set<WorkflowUsageObservationRecordEntity>().AnyAsync(item => item.Id == usageObservation.Id.Value));
        Assert.True(await dbContext.Set<WorkflowExternalRequestBoundaryEntity>().AnyAsync(item => item.RequestId == nextRequest.Id.Value));
        Assert.Equal(nextRequest.Id.Value, persistedNativeCheckpoint.ExternalRequestId);
        Assert.Equal(nextBackendLink.BackendRequestId.Value, persistedNativeCheckpoint.BackendRequestId);
        Assert.Equal(nextBackendLink.BackendRequestPortId.Value, persistedNativeCheckpoint.BackendRequestPortId);
    }

    private static async Task<WorkflowHitlPersistenceFixture> CreateFixtureAsync(string databaseName)
    {
        AppDbContextModelRegistry.ConfigureAssemblies(ModuleAssemblies.All);
        var database = PostgresTestDatabaseLease.Create(databaseName);
        var keyDirectory = new DataProtectionKeyDirectory();
        var factory = new WorkflowHitlDbContextFactory(database.CreateAppDbContextOptions());
        await using (var dbContext = factory.CreateDbContext())
        {
            await dbContext.Database.MigrateAsync();
        }

        return new WorkflowHitlPersistenceFixture(database, keyDirectory, factory);
    }

    private static async Task<WorkflowRunSnapshot> SeedRunAsync(
        WorkflowHitlDbContextFactory factory)
    {
        var run = new WorkflowRunSnapshot(
            WorkflowRunId.New(),
            WorkflowId.New(),
            WorkflowVersionId.New(),
            WorkflowRunState.WaitingForInput,
            WorkflowRuntimeBackendKind.InProcess,
            "maf-session",
            "Waiting for input.",
            TestTime,
            TestTime);
        await using var dbContext = factory.CreateDbContext();
        dbContext.Set<WorkflowRunRecordEntity>().Add(WorkflowRunRecordEntity.FromSnapshot(run));
        await dbContext.SaveChangesAsync();
        return run;
    }

    private static async Task<SeededExternalRequest> SeedWaitingRequestAsync(
        WorkflowHitlPersistenceFixture fixture,
        bool createBoundary)
    {
        var run = await SeedRunAsync(fixture.Factory);
        WorkflowExternalRequestRecord request;
        WorkflowCheckpointRecord? checkpointMetadata = null;
        if (createBoundary)
        {
            var requestId = WorkflowExternalRequestId.New();
            var backendRequest = new WorkflowBackendExternalRequestLink(
                requestId,
                new WorkflowBackendRequestId("native-request-initial"),
                new WorkflowBackendRequestPortId("native-port-initial"));
            var checkpoint = await fixture.CreateCheckpointStore().CreateAsync(
                new WorkflowBackendCheckpointCreateRequest(
                    CreateCheckpointSession(run),
                    Parent: null,
                    WorkflowBackendCheckpointPayload.Create("{\"native\":\"initial-boundary\"}")));
            Assert.True(checkpoint.Succeeded);
            request = CreateExternalRequest(run, requestId, backendRequest, checkpoint.Checkpoint!);
            checkpointMetadata = CreateCheckpointMetadata(run, request, checkpoint.Checkpoint!, TestTime);
        }
        else
        {
            request = CreateExternalRequest(run);
        }

        await using (var dbContext = fixture.Factory.CreateDbContext())
        {
            dbContext.Set<WorkflowExternalRequestRecordEntity>().Add(
                WorkflowExternalRequestRecordEntity.FromRequest(request));
            if (checkpointMetadata is not null)
            {
                dbContext.Set<WorkflowCheckpointRecordEntity>().Add(
                    WorkflowCheckpointRecordEntity.FromCheckpoint(checkpointMetadata));
            }

            await dbContext.SaveChangesAsync();
        }

        if (createBoundary)
        {
            var boundaryStore = new PersistentWorkflowExternalRequestBoundaryStore(fixture.Factory);
            var saved = await boundaryStore.UpsertAsync(CreateBoundary(request));
            Assert.True(saved.Succeeded);
        }

        return new SeededExternalRequest(run, request);
    }

    private static WorkflowExternalRequestRecord CreateExternalRequest(WorkflowRunSnapshot run)
    {
        var requestId = WorkflowExternalRequestId.New();
        var checkpointPayloadHash = WorkflowBackendCheckpointPayloadHash.Compute("{\"checkpoint\":true}");
        return new WorkflowExternalRequestRecord(
            requestId,
            run.RunId,
            WorkflowExternalRequestKind.HumanInput,
            new WorkflowNodeId("collect-answer"),
            "Collect answer",
            "{\"question\":\"What is the answer?\"}",
            ResponseJson: string.Empty,
            TestTime,
            RespondedAtUtc: null)
        {
            Version = WorkflowExternalRequestVersion.Initial,
            State = WorkflowExternalRequestState.Pending,
            ResponseContract = new WorkflowExternalResponseContract(
                WorkflowExternalRequestKind.HumanInput,
                "human-input-response",
                schemaVersion: 1,
                "{\"type\":\"object\"}",
                maximumPayloadBytes: 4096),
            Continuation = new WorkflowExternalRequestContinuation(
                new WorkflowBackendExternalRequestLink(
                    requestId,
                    new WorkflowBackendRequestId("native-request"),
                    new WorkflowBackendRequestPortId("native-port")),
                new WorkflowBackendCheckpointLink(
                    new WorkflowBackendSessionId("maf-session"),
                    new WorkflowBackendCheckpointId("native-checkpoint")),
                new WorkflowCompilerContractVersion(1),
                WorkflowTopologyFingerprint.Create("test-topology"),
                checkpointPayloadHash)
        };
    }

    private static WorkflowExternalRequestRecord CreateExternalRequest(
        WorkflowRunSnapshot run,
        WorkflowExternalRequestId requestId,
        WorkflowBackendExternalRequestLink backendRequest,
        WorkflowBackendCheckpointPayloadRecord checkpoint)
        => new(
            requestId,
            run.RunId,
            WorkflowExternalRequestKind.HumanInput,
            new WorkflowNodeId("collect-follow-up"),
            "Collect follow-up",
            "{\"question\":\"One more answer?\"}",
            ResponseJson: string.Empty,
            TestTime.AddSeconds(3),
            RespondedAtUtc: null)
        {
            Version = WorkflowExternalRequestVersion.Initial,
            State = WorkflowExternalRequestState.Pending,
            ResponseContract = new WorkflowExternalResponseContract(
                WorkflowExternalRequestKind.HumanInput,
                "human-input-response",
                schemaVersion: 1,
                "{\"type\":\"object\"}",
                maximumPayloadBytes: 4096),
            Continuation = new WorkflowExternalRequestContinuation(
                backendRequest,
                checkpoint.Index.Link,
                checkpoint.Session.CompilerContractVersion,
                checkpoint.Session.TopologyFingerprint,
                checkpoint.Payload.Sha256)
        };

    private static WorkflowCheckpointRecord CreateCheckpointMetadata(
        WorkflowRunSnapshot run,
        WorkflowExternalRequestRecord request,
        WorkflowBackendCheckpointPayloadRecord checkpoint,
        DateTimeOffset createdAtUtc)
        => new(
            WorkflowCheckpointId.New(),
            run.RunId,
            run.WorkflowId,
            run.VersionId,
            run.Backend,
            WorkflowCheckpointKind.WaitingForInput,
            WorkflowCheckpointTrustBoundary.TrustedRuntimeState,
            WorkflowResumeAvailability.Available,
            request.NodeId,
            request.Id,
            checkpoint.Index.Link.CheckpointId.Value,
            $"maf-checkpoint://{checkpoint.Index.Link.SessionId.Value}/{checkpoint.Index.Link.CheckpointId.Value}",
            checkpoint.Payload.Sha256.Value,
            "Waiting for input.",
            ResumeUnavailableReason: string.Empty,
            createdAtUtc,
            ResumedAtUtc: null);

    private static async Task PersistRequestAndCheckpointAsync(
        WorkflowHitlDbContextFactory factory,
        WorkflowRunSnapshot run,
        WorkflowExternalRequestRecord request,
        WorkflowBackendCheckpointPayloadRecord checkpoint)
    {
        await using var dbContext = factory.CreateDbContext();
        dbContext.Set<WorkflowExternalRequestRecordEntity>().Add(
            WorkflowExternalRequestRecordEntity.FromRequest(request));
        dbContext.Set<WorkflowCheckpointRecordEntity>().Add(
            WorkflowCheckpointRecordEntity.FromCheckpoint(
                CreateCheckpointMetadata(
                    run,
                    request,
                    checkpoint,
                    request.CreatedAtUtc)));
        await dbContext.SaveChangesAsync();
    }

    private static async Task AssertResumeBoundaryUnchangedAsync(
        WorkflowHitlDbContextFactory factory,
        SeededExternalRequest seeded,
        WorkflowExternalResponseOperationId operationId,
        WorkflowExternalRequestId nextRequestId)
    {
        await using var dbContext = factory.CreateDbContext();
        var sourceRequest = await dbContext.Set<WorkflowExternalRequestRecordEntity>()
            .AsNoTracking()
            .SingleAsync(item => item.Id == seeded.Request.Id.Value);
        var operation = await dbContext.Set<WorkflowExternalResponseOperationEntity>()
            .AsNoTracking()
            .SingleAsync(item => item.Id == operationId.Value);
        var run = await dbContext.Set<WorkflowRunRecordEntity>()
            .AsNoTracking()
            .SingleAsync(item => item.RunId == seeded.Run.RunId.Value);
        Assert.Null(sourceRequest.RespondedAtUtc);
        Assert.Equal(string.Empty, sourceRequest.ResponseJson);
        Assert.Equal((int)WorkflowExternalResponseOperationState.Resuming, operation.State);
        Assert.Equal(WorkflowRunState.WaitingForInput, run.State);
        Assert.False(await dbContext.Set<WorkflowExternalRequestRecordEntity>()
            .AnyAsync(item => item.Id == nextRequestId.Value));
    }

    private static async Task AssertNativeCheckpointRequestLinkAsync(
        WorkflowHitlDbContextFactory factory,
        WorkflowBackendCheckpointId checkpointId,
        WorkflowBackendExternalRequestLink? expectedLink)
    {
        await using var dbContext = factory.CreateDbContext();
        var checkpoint = await dbContext.Set<WorkflowBackendCheckpointPayloadEntity>()
            .AsNoTracking()
            .SingleAsync(item => item.Id == checkpointId.Value);
        Assert.Equal(expectedLink?.ExternalRequestId.Value, checkpoint.ExternalRequestId);
        Assert.Equal(expectedLink?.BackendRequestId.Value, checkpoint.BackendRequestId);
        Assert.Equal(expectedLink?.BackendRequestPortId.Value, checkpoint.BackendRequestPortId);
    }

    private static async Task WaitForBlockedDatabaseCommandAsync(string connectionString)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText =
            """
            SELECT EXISTS (
                SELECT 1
                FROM pg_stat_activity
                WHERE datname = current_database()
                  AND pid <> pg_backend_pid()
                  AND wait_event_type = 'Lock')
            """;
        for (var attempt = 0; attempt < 100; attempt++)
        {
            if (await command.ExecuteScalarAsync() is true)
            {
                return;
            }

            await Task.Delay(TimeSpan.FromMilliseconds(25));
        }

        throw new TimeoutException("The PostgreSQL mutation did not reach the operation-row lock barrier.");
    }

    private static WorkflowExternalRequestBoundaryRecord CreateBoundary(
        WorkflowExternalRequestRecord request)
    {
        Assert.True(WorkflowExternalRequestBoundaryRecord.TryCreate(request, out var boundary));
        return boundary!;
    }

    private static WorkflowBackendCheckpointSession CreateCheckpointSession(
        WorkflowRunSnapshot run,
        WorkflowBackendSessionId? sessionId = null)
        => new(
            sessionId ?? new WorkflowBackendSessionId("maf-session"),
            run.RunId,
            run.WorkflowId,
            run.VersionId,
            run.Backend,
            new WorkflowBackendCheckpointFormat("maf-json"),
            new WorkflowBackendCheckpointFormatVersion(1),
            new WorkflowCompilerContractVersion(1),
            WorkflowTopologyFingerprint.Create("test-topology"));

    private sealed record SeededExternalRequest(
        WorkflowRunSnapshot Run,
        WorkflowExternalRequestRecord Request);

    private sealed class WorkflowHitlPersistenceFixture(
        PostgresTestDatabaseLease database,
        DataProtectionKeyDirectory keyDirectory,
        WorkflowHitlDbContextFactory factory) : IAsyncDisposable
    {
        public WorkflowHitlDbContextFactory Factory { get; } = factory;

        public PersistentWorkflowBackendCheckpointPayloadStore CreateCheckpointStore(
            bool reconstructDataProtectionProvider = false)
            => new(
                Factory,
                keyDirectory.CreateProvider(),
                TimeProvider.System);

        public PersistentWorkflowExternalResponseOperationStore CreateOperationStore(
            bool reconstructDataProtectionProvider = false)
            => new(Factory, keyDirectory.CreateProvider());

        public PersistentWorkflowExternalResponseOperationStore CreateOperationStore(
            DbCommandInterceptor interceptor)
            => new(Factory.WithInterceptor(interceptor), keyDirectory.CreateProvider());

        public string ConnectionString => database.ConnectionString;

        public IDataProtectionProvider CreateDataProtectionProvider()
            => keyDirectory.CreateProvider();

        public async ValueTask DisposeAsync()
        {
            await database.DisposeAsync();
            keyDirectory.Dispose();
        }
    }

    private sealed class WorkflowHitlDbContextFactory(DbContextOptions<AppDbContext> options) :
        IDbContextFactory<AppDbContext>
    {
        public AppDbContext CreateDbContext() => new(options);

        public WorkflowHitlDbContextFactory WithInterceptor(DbCommandInterceptor interceptor)
            => new(new DbContextOptionsBuilder<AppDbContext>(options)
                .AddInterceptors(interceptor)
                .Options);

        public Task<AppDbContext> CreateDbContextAsync(
            CancellationToken cancellationToken = default)
            => Task.FromResult(CreateDbContext());
    }

    private sealed class OperationReplayCommandInterceptor(Guid requestId) : DbCommandInterceptor
    {
        public TaskCompletionSource<string> OperationReadStarted { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(
            DbCommand command,
            CommandEventData eventData,
            InterceptionResult<DbDataReader> result,
            CancellationToken cancellationToken = default)
        {
            if (command.CommandText.Contains(
                    "AgentFramework_WorkflowExternalResponseOperations",
                    StringComparison.Ordinal) &&
                command.Parameters.Cast<DbParameter>().Any(parameter =>
                    parameter.Value is Guid value && value == requestId))
            {
                OperationReadStarted.TrySetResult(command.CommandText);
            }

            return ValueTask.FromResult(result);
        }
    }

    private sealed class NativeRequestPrecheckBarrierInterceptor : DbCommandInterceptor
    {
        private readonly TaskCompletionSource bothPrechecksCompleted =
            new(TaskCreationOptions.RunContinuationsAsynchronously);
        private int completedPrechecks;

        public override async ValueTask<DbDataReader> ReaderExecutedAsync(
            DbCommand command,
            CommandExecutedEventData eventData,
            DbDataReader result,
            CancellationToken cancellationToken = default)
        {
            if (!command.CommandText.Contains("SELECT EXISTS", StringComparison.OrdinalIgnoreCase) ||
                !command.CommandText.Contains(
                    "AgentFramework_WorkflowBackendCheckpointPayloads",
                    StringComparison.Ordinal) ||
                !command.CommandText.Contains("BackendRequestId", StringComparison.Ordinal) ||
                !command.CommandText.Contains("BackendRequestPortId", StringComparison.Ordinal))
            {
                return result;
            }

            if (Interlocked.Increment(ref completedPrechecks) == 2)
            {
                bothPrechecksCompleted.TrySetResult();
            }

            await bothPrechecksCompleted.Task.WaitAsync(
                TimeSpan.FromSeconds(10),
                cancellationToken);
            return result;
        }
    }

    private sealed class DataProtectionKeyDirectory : IDisposable
    {
        private readonly string path = Path.Combine(
            Path.GetTempPath(),
            "CanDoItAll.Tests.WorkflowHitl",
            Guid.NewGuid().ToString("N"));

        public DataProtectionKeyDirectory()
        {
            Directory.CreateDirectory(path);
        }

        public IDataProtectionProvider CreateProvider()
            => DataProtectionProvider.Create(
                new DirectoryInfo(path),
                builder => builder.SetApplicationName("CanDoItAll.WorkflowHitlRecoveryTests"));

        public void Dispose()
        {
            if (Directory.Exists(path))
            {
                Directory.Delete(path, recursive: true);
            }
        }
    }
}
