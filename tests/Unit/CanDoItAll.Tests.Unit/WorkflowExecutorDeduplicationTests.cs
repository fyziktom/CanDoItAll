using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.Workflows.Abstractions;
using Microsoft.Extensions.DependencyInjection;

namespace CanDoItAll.Tests.Unit.AgentFramework;

public sealed class WorkflowExecutorDeduplicationTests
{
    private static readonly DateTimeOffset Now = new(2026, 8, 20, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task CompletedInvocationReplaysResultAndPropagatesStableParticipantKey()
    {
        var executor = new RecordingParticipatingExecutor();
        var store = new InMemoryDeduplicationStore();
        var fixture = CreateFixture(executor, store);

        var first = await fixture.ExecuteAsync("{\"value\":1}");
        var second = await fixture.ExecuteAsync("{\"value\":1}");

        Assert.Equal(first, second);
        Assert.Equal(1, executor.InvocationCount);
        Assert.Single(executor.IdempotencyKeys);
        Assert.NotNull(executor.IdempotencyKeys[0]);
        Assert.Equal(store.SingleRecord.Identity.IdempotencyKey, executor.IdempotencyKeys[0]);
        Assert.Equal(WorkflowExecutorInvocationState.Completed, store.SingleRecord.State);
    }

    [Fact]
    public async Task CompletedApprovalReplayAfterAuthorizationExpiryFailsClosed()
    {
        var clock = new ManualTimeProvider(Now);
        var executor = new RecordingParticipatingExecutor(requiresApproval: true);
        var store = new InMemoryDeduplicationStore();
        var fixture = CreateFixture(executor, store, clock);

        await fixture.ExecuteAsync("{\"value\":1}");
        clock.Advance(TimeSpan.FromSeconds(
            WorkflowExternalResponseAuthorizationPolicy.ResponseLifetimeSeconds + 1));

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => fixture.ExecuteAsync("{\"value\":1}").AsTask());

        Assert.Equal(1, executor.InvocationCount);
        Assert.Equal(1, store.ClaimCount);
        Assert.Equal(WorkflowExecutorInvocationState.Completed, store.SingleRecord.State);
    }

    [Fact]
    public async Task ActiveLeaseBlocksParallelDuplicateWithoutSecondEffect()
    {
        var executor = new RecordingParticipatingExecutor(block: true);
        var store = new InMemoryDeduplicationStore();
        var fixture = CreateFixture(executor, store);

        var first = fixture.ExecuteAsync("{\"value\":1}").AsTask();
        await executor.WaitUntilEnteredAsync();

        var exception = await Assert.ThrowsAsync<WorkflowExecutorInvocationDeduplicationException>(
            () => fixture.ExecuteAsync("{\"value\":1}").AsTask());

        Assert.Equal(WorkflowExecutorInvocationClaimOutcome.ActiveLease, exception.ClaimOutcome);
        Assert.Equal(1, executor.InvocationCount);
        executor.Release();
        await first;
    }

    [Fact]
    public async Task LongExecutionRenewsLeaseAndBlocksTakeoverPastOriginalExpiry()
    {
        var clock = new ManualTimeProvider(Now);
        var executor = new RecordingParticipatingExecutor(block: true);
        var store = new InMemoryDeduplicationStore();
        var fixture = CreateFixture(executor, store, clock);

        var first = fixture.ExecuteAsync("{\"value\":1}").AsTask();
        await executor.WaitUntilEnteredAsync();
        var originalExpiry = store.SingleRecord.Lease!.ExpiresAtUtc;
        var leaseDuration = originalExpiry - Now;

        clock.Advance(WorkflowExecutorInvocationDeduplicationPolicy.ResolveLeaseRenewalInterval(
            leaseDuration));
        await store.WaitUntilRenewedAsync();
        clock.Advance(originalExpiry - clock.GetUtcNow() + TimeSpan.FromSeconds(1));

        var exception = await Assert.ThrowsAsync<WorkflowExecutorInvocationDeduplicationException>(
            () => fixture.ExecuteAsync("{\"value\":1}").AsTask());

        Assert.True(clock.GetUtcNow() > originalExpiry);
        Assert.Equal(WorkflowExecutorInvocationClaimOutcome.ActiveLease, exception.ClaimOutcome);
        Assert.True(store.RenewalCount >= 1);
        Assert.Equal(1, executor.InvocationCount);
        executor.Release();
        await first;
    }

    [Fact]
    public async Task SameScopeWithChangedInputFailsClosed()
    {
        var executor = new RecordingParticipatingExecutor();
        var store = new InMemoryDeduplicationStore();
        var fixture = CreateFixture(executor, store);
        await fixture.ExecuteAsync("{\"value\":1}");

        var exception = await Assert.ThrowsAsync<WorkflowExecutorInvocationDeduplicationException>(
            () => fixture.ExecuteAsync("{\"value\":2}").AsTask());

        Assert.Equal(WorkflowExecutorInvocationClaimOutcome.InputMismatch, exception.ClaimOutcome);
        Assert.Equal(1, executor.InvocationCount);
    }

    [Fact]
    public async Task RetryableFailureReusesParticipantKeyAndStopsAtBoundedAttemptLimit()
    {
        var executor = new RecordingParticipatingExecutor(fail: true);
        var store = new InMemoryDeduplicationStore();
        var fixture = CreateFixture(executor, store);

        for (var attempt = 0; attempt < WorkflowExecutorInvocationDeduplicationPolicy.MaximumAttempts; attempt++)
        {
            await Assert.ThrowsAsync<WorkflowExecutorInvocationDeduplicationException>(
                () => fixture.ExecuteAsync("{\"value\":1}").AsTask());
        }

        var terminal = await Assert.ThrowsAsync<WorkflowExecutorInvocationDeduplicationException>(
            () => fixture.ExecuteAsync("{\"value\":1}").AsTask());

        Assert.Equal(WorkflowExecutorInvocationClaimOutcome.AttemptLimitReached, terminal.ClaimOutcome);
        Assert.Equal(WorkflowExecutorInvocationDeduplicationPolicy.MaximumAttempts, executor.InvocationCount);
        Assert.Single(executor.IdempotencyKeys.Distinct());
        Assert.Equal(WorkflowExecutorInvocationState.FailedTerminal, store.SingleRecord.State);
        Assert.Equal(
            WorkflowExecutorInvocationFailureCode.AttemptLimitReached,
            store.SingleRecord.FailureCode);
        Assert.Null(store.SingleRecord.Lease);
    }

    [Fact]
    public async Task UnsafeOversizedResultBecomesTerminalAndIsNeverReinvoked()
    {
        var executor = new RecordingParticipatingExecutor(
            payload: new string('x', WorkflowExecutorInvocationDeduplicationPolicy.MaximumStoredResultCharacters + 1));
        var store = new InMemoryDeduplicationStore();
        var fixture = CreateFixture(executor, store);

        await Assert.ThrowsAsync<WorkflowExecutorInvocationDeduplicationException>(
            () => fixture.ExecuteAsync("{\"value\":1}").AsTask());
        var replay = await Assert.ThrowsAsync<WorkflowExecutorInvocationDeduplicationException>(
            () => fixture.ExecuteAsync("{\"value\":1}").AsTask());

        Assert.Equal(WorkflowExecutorInvocationClaimOutcome.FailedTerminal, replay.ClaimOutcome);
        Assert.Equal(1, executor.InvocationCount);
        Assert.Equal(WorkflowExecutorInvocationState.FailedTerminal, store.SingleRecord.State);
    }

    [Fact]
    public async Task SecretUsingParticipantWithSafeReceiptExecutesOnceAndReplays()
    {
        var executor = new RecordingParticipatingExecutor(usesSecrets: true);
        var store = new InMemoryDeduplicationStore();
        var fixture = CreateFixture(executor, store);

        var first = await fixture.ExecuteAsync("{\"value\":1}");
        var replay = await fixture.ExecuteAsync("{\"value\":1}");

        Assert.Equal(first, replay);
        Assert.Equal(1, executor.InvocationCount);
        Assert.Equal(2, store.ClaimCount);
        Assert.Equal(WorkflowExecutorInvocationState.Completed, store.SingleRecord.State);
    }

    [Fact]
    public async Task SecretBearingReceiptBecomesTerminalAndIsNeverPersistedOrReinvoked()
    {
        const string secret = "receipt-secret-value";
        var executor = new RecordingParticipatingExecutor(
            usesSecrets: true,
            payload: $"{{\"secret\":\"{secret}\"}}");
        var store = new InMemoryDeduplicationStore();
        var fixture = CreateFixture(executor, store);

        var first = await Assert.ThrowsAsync<WorkflowExecutorInvocationDeduplicationException>(
            () => fixture.ExecuteAsync("{\"value\":1}").AsTask());
        var replay = await Assert.ThrowsAsync<WorkflowExecutorInvocationDeduplicationException>(
            () => fixture.ExecuteAsync("{\"value\":1}").AsTask());

        Assert.Equal(WorkflowExecutorInvocationClaimOutcome.FailedTerminal, replay.ClaimOutcome);
        Assert.Equal(1, executor.InvocationCount);
        Assert.Equal(WorkflowExecutorInvocationState.FailedTerminal, store.SingleRecord.State);
        Assert.Equal(
            WorkflowExecutorInvocationFailureCode.UnsafeResultNotPersisted,
            store.SingleRecord.FailureCode);
        Assert.Null(store.SingleRecord.StoredResult);
        Assert.DoesNotContain(secret, first.Message, StringComparison.Ordinal);
        Assert.DoesNotContain(secret, replay.Message, StringComparison.Ordinal);
        Assert.DoesNotContain(secret, store.SingleRecord.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task NonParticipatingExecutorBypassesStoreAndReceivesNoSyntheticKey()
    {
        var executor = new RecordingParticipatingExecutor(participates: false);
        var store = new InMemoryDeduplicationStore();
        var fixture = CreateFixture(executor, store);

        await fixture.ExecuteAsync("{\"value\":1}");
        await fixture.ExecuteAsync("{\"value\":1}");

        Assert.Equal(2, executor.InvocationCount);
        Assert.Equal(0, store.ClaimCount);
        Assert.All(executor.IdempotencyKeys, Assert.Null);
    }

    [Fact]
    public void DeduplicationRegistrationIsIdempotentAndDoesNotResolveRecursively()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IWorkflowExecutorInvocationDeduplicationStore>(
            new InMemoryDeduplicationStore());
        services.AddWorkflowExecutorContribution<DiParticipatingExecutor>(
            DiParticipatingExecutor.TestDescriptor,
            ServiceLifetime.Scoped);
        services.AddWorkflowExecutorInvocationDeduplication();
        services.AddWorkflowExecutorInvocationDeduplication();

        Assert.Single(
            services,
            service => service.ServiceType == typeof(IWorkflowExecutorInvoker));
        using var provider = services.BuildServiceProvider(new ServiceProviderOptions
        {
            ValidateScopes = true,
            ValidateOnBuild = true
        });
        using var scope = provider.CreateScope();

        Assert.IsType<DeduplicatingWorkflowExecutorInvoker>(
            scope.ServiceProvider.GetRequiredService<IWorkflowExecutorInvoker>());
        Assert.IsType<WorkflowExecutorInvoker>(
            scope.ServiceProvider.GetRequiredService<WorkflowExecutorInvoker>());
    }

    private static DedupFixture CreateFixture(
        RecordingParticipatingExecutor executor,
        InMemoryDeduplicationStore store,
        TimeProvider? timeProvider = null)
    {
        var catalog = new WorkflowExecutorCatalog([executor]);
        var clock = timeProvider ?? new FixedTimeProvider(Now);
        var inner = new WorkflowExecutorInvoker(catalog, [executor], timeProvider: clock);
        var decorator = new DeduplicatingWorkflowExecutorInvoker(
            inner,
            catalog,
            store,
            clock);
        var node = new WorkflowNode(
            new WorkflowNodeId("governed-effect"),
            WorkflowNodeKind.Executor,
            "Governed effect",
            [],
            new WorkflowNodeSettings(
                ComponentId: null,
                AgentId: null,
                SubworkflowId: null,
                ExternalRequestKind: null,
                Instructions: string.Empty,
                InputShape: WorkflowValueShape.Text,
                ResultShape: WorkflowValueShape.Text)
            {
                ExecutorId = executor.Descriptor.Id,
                ExecutorSettingsJson = "{}",
                ExecutionPolicy = WorkflowExecutorExecutionPolicy.Default
            });
        var definition = new WorkflowDefinition(
            WorkflowId.New(),
            WorkflowVersionId.New(),
            "Deduplication test",
            "Deduplication test",
            WorkflowLifecycleStatus.Draft,
            new WorkflowGraph(node.Id, [node], []),
            new WorkflowRuntimePolicy(
                WorkflowRuntimeBackendKind.InProcess,
                AllowInProcessPreviewRuns: true,
                RequireDurableProductionRuns: false,
                ExposeAzureFunctionsStatusEndpoint: false,
                ExposeAzureFunctionsMcpTool: false),
            Now,
            Now);
        var runId = WorkflowRunId.New();
        var requestId = WorkflowExternalRequestId.New();
        var requestVersion = new WorkflowExternalRequestVersion(7);
        var operationId = WorkflowExternalResponseOperationId.New();
        var invocationContext = new WorkflowExecutorInvocationContext
        {
            CausationRequestId = requestId,
            CausationRequestVersion = requestVersion,
            CausationOperationId = operationId,
            InvocationGeneration = new WorkflowExecutorInvocationGeneration(requestVersion.Value)
        };
        if (executor.Descriptor.PermissionPolicy.RequiresApproval)
        {
            var token = WorkflowExecutorApprovalToken.New();
            var actor = new WorkflowLaunchActor(WorkflowLaunchActorKind.User, "deduplication-approver");
            var responseAuthorization = new WorkflowExternalResponseAuthorization(
                operationId,
                requestId,
                requestVersion,
                runId,
                definition.Id,
                definition.VersionId,
                WorkflowExternalRequestKind.Approval,
                WorkflowExternalResponseAction.Approve,
                actor,
                WorkspaceScopeDescriptor.Organization("deduplication-test"),
                actor,
                WorkflowExternalResponseAuthorizationPolicy.CurrentFingerprint,
                clock.GetUtcNow(),
                clock.GetUtcNow().AddSeconds(
                    WorkflowExternalResponseAuthorizationPolicy.ResponseLifetimeSeconds));
            invocationContext = invocationContext with
            {
                ExternalResponseAuthorization = responseAuthorization,
                ApprovalAuthorization = new WorkflowExecutorApprovalAuthorization(
                    WorkflowExecutorApprovalRequestId.New(),
                    token,
                    token,
                    runId,
                    definition.Id,
                    definition.VersionId,
                    node.Id,
                    executor.Descriptor.Id,
                    executor.Descriptor.PermissionPolicy.RequiredCapabilities,
                    executor.Descriptor.PermissionPolicy.ApprovalRequirement,
                    WorkflowExecutorInputHash.Compute(new WorkflowNodeInput("{\"value\":1}")),
                    responseAuthorization,
                    Approved: true,
                    "Approved for replay expiry validation.")
            };
        }

        return new DedupFixture(
            decorator,
            definition,
            node,
            runId,
            invocationContext);
    }

    private sealed record DedupFixture(
        IWorkflowExecutorInvoker Invoker,
        WorkflowDefinition Definition,
        WorkflowNode Node,
        WorkflowRunId RunId,
        WorkflowExecutorInvocationContext Context)
    {
        public async ValueTask<WorkflowNodeExecutionResult> ExecuteAsync(string payloadJson)
        {
            using var scope = WorkflowExecutorExecutionAuditScope.Push(RunId);
            return await Invoker.ExecuteAsync(
                Definition,
                Node,
                new WorkflowNodeInput(payloadJson),
                Context);
        }
    }

    private sealed class RecordingParticipatingExecutor : IWorkflowExecutor
    {
        private readonly TaskCompletionSource entered = new(
            TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly TaskCompletionSource release = new(
            TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly bool block;
        private readonly bool fail;
        private readonly string payload;

        public RecordingParticipatingExecutor(
            bool block = false,
            bool fail = false,
            bool participates = true,
            bool usesSecrets = false,
            bool requiresApproval = false,
            string payload = "{\"executed\":true}")
        {
            this.block = block;
            this.fail = fail;
            this.payload = payload;
            Descriptor = CreateDescriptor(participates, usesSecrets, requiresApproval);
        }

        public WorkflowExecutorDescriptor Descriptor { get; }

        public int InvocationCount { get; private set; }

        public List<WorkflowExecutorInvocationIdempotencyKey?> IdempotencyKeys { get; } = [];

        public Task WaitUntilEnteredAsync() => entered.Task;

        public void Release() => release.TrySetResult();

        public async ValueTask<WorkflowNodeExecutionResult> ExecuteAsync(
            WorkflowExecutorExecutionContext context,
            WorkflowNodeInput input,
            CancellationToken cancellationToken = default)
        {
            InvocationCount++;
            IdempotencyKeys.Add(context.IdempotencyKey);
            entered.TrySetResult();
            if (block)
            {
                await release.Task.WaitAsync(cancellationToken);
            }

            if (fail)
            {
                throw new InvalidOperationException("deterministic executor failure");
            }

            return new WorkflowNodeExecutionResult(
                context.Node.Id,
                payload,
                context.Descriptor.ResultShape);
        }

        private static WorkflowExecutorDescriptor CreateDescriptor(
            bool participates,
            bool usesSecrets,
            bool requiresApproval)
        {
            var capabilities = WorkflowExecutorCapabilityFlags.WritesExternalData;
            if (participates)
            {
                capabilities |= WorkflowExecutorCapabilityFlags.IdempotentExternalMarker;
            }

            if (usesSecrets)
            {
                capabilities |= WorkflowExecutorCapabilityFlags.UsesSecrets;
            }

            return BuiltInWorkflowExecutorDescriptors.JsonTransform with
            {
                Id = new WorkflowExecutorId("test.governed-participant"),
                Name = "Governed participant",
                PermissionPolicy = new WorkflowExecutorPermissionPolicy(
                    capabilities,
                    requiresApproval
                        ? WorkflowExecutorApprovalRequirement.AlwaysRequired
                        : WorkflowExecutorApprovalRequirement.NotRequired),
                SideEffects = participates
                    ? WorkflowExecutorSideEffectDescriptor.IdempotentProcessedMarker(
                        "$.idempotencyKey",
                        "{\"type\":\"object\"}")
                    : WorkflowExecutorSideEffectDescriptor.ExternalWrite(
                        WorkflowExecutorExternalMutationKind.None,
                        requiresCommitIdempotencyKey: false,
                        allowsIdempotentRetry: false,
                        idempotencyKeyJsonPath: string.Empty,
                        receiptSchema: string.Empty)
            };
        }
    }

    private sealed class DiParticipatingExecutor : IWorkflowExecutor
    {
        public static WorkflowExecutorDescriptor TestDescriptor { get; } =
            BuiltInWorkflowExecutorDescriptors.JsonTransform with
            {
                Id = new WorkflowExecutorId("test.di-governed-participant"),
                Name = "DI governed participant",
                PermissionPolicy = new WorkflowExecutorPermissionPolicy(
                    WorkflowExecutorCapabilityFlags.WritesExternalData |
                    WorkflowExecutorCapabilityFlags.IdempotentExternalMarker,
                    WorkflowExecutorApprovalRequirement.NotRequired),
                SideEffects = WorkflowExecutorSideEffectDescriptor.IdempotentProcessedMarker(
                    "$.idempotencyKey",
                    "{\"type\":\"object\"}")
            };

        public WorkflowExecutorDescriptor Descriptor => TestDescriptor;

        public ValueTask<WorkflowNodeExecutionResult> ExecuteAsync(
            WorkflowExecutorExecutionContext context,
            WorkflowNodeInput input,
            CancellationToken cancellationToken = default)
            => ValueTask.FromResult(new WorkflowNodeExecutionResult(
                context.Node.Id,
                input.PayloadJson,
                context.Descriptor.ResultShape));
    }

    private sealed class FixedTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => utcNow;
    }

    private sealed class ManualTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        private readonly List<ManualTimer> timers = [];
        private DateTimeOffset current = utcNow;
        private long timestamp;

        public override long TimestampFrequency => TimeSpan.TicksPerSecond;

        public override DateTimeOffset GetUtcNow() => current;

        public override long GetTimestamp() => timestamp;

        public override ITimer CreateTimer(
            TimerCallback callback,
            object? state,
            TimeSpan dueTime,
            TimeSpan period)
        {
            var timer = new ManualTimer(this, callback, state, dueTime, period);
            timers.Add(timer);
            return timer;
        }

        public void Advance(TimeSpan duration)
        {
            if (duration < TimeSpan.Zero)
            {
                throw new ArgumentOutOfRangeException(nameof(duration));
            }

            current += duration;
            timestamp += duration.Ticks;
            foreach (var timer in timers.ToArray())
            {
                timer.FireIfDue(timestamp);
            }

            timers.RemoveAll(timer => timer.IsDisposed);
        }

        private sealed class ManualTimer : ITimer
        {
            private readonly ManualTimeProvider owner;
            private readonly TimerCallback callback;
            private readonly object? state;
            private long dueTimestamp;
            private TimeSpan period;

            public ManualTimer(
                ManualTimeProvider owner,
                TimerCallback callback,
                object? state,
                TimeSpan dueTime,
                TimeSpan period)
            {
                this.owner = owner;
                this.callback = callback;
                this.state = state;
                Change(dueTime, period);
            }

            public bool IsDisposed { get; private set; }

            public bool Change(TimeSpan dueTime, TimeSpan period)
            {
                if (IsDisposed)
                {
                    return false;
                }

                this.period = period;
                dueTimestamp = dueTime == Timeout.InfiniteTimeSpan
                    ? long.MaxValue
                    : owner.timestamp + dueTime.Ticks;
                return true;
            }

            public void Dispose()
            {
                IsDisposed = true;
            }

            public ValueTask DisposeAsync()
            {
                Dispose();
                return ValueTask.CompletedTask;
            }

            public void FireIfDue(long currentTimestamp)
            {
                if (IsDisposed || currentTimestamp < dueTimestamp)
                {
                    return;
                }

                dueTimestamp = period == Timeout.InfiniteTimeSpan
                    ? long.MaxValue
                    : currentTimestamp + period.Ticks;
                callback(state);
            }
        }
    }

    private sealed class InMemoryDeduplicationStore : IWorkflowExecutorInvocationDeduplicationStore
    {
        private readonly object gate = new();
        private readonly TaskCompletionSource renewed = new(
            TaskCreationOptions.RunContinuationsAsynchronously);
        private WorkflowExecutorInvocationRecord? record;

        public int ClaimCount { get; private set; }

        public int RenewalCount { get; private set; }

        public WorkflowExecutorInvocationRecord SingleRecord => record ?? throw new InvalidOperationException();

        public Task WaitUntilRenewedAsync() => renewed.Task;

        public Task<WorkflowExecutorInvocationClaimResult> TryClaimAsync(
            WorkflowExecutorInvocationClaimRequest request,
            CancellationToken cancellationToken = default)
        {
            lock (gate)
            {
                cancellationToken.ThrowIfCancellationRequested();
                ClaimCount++;
                if (record is null)
                {
                    var lease = new WorkflowExecutorInvocationLease(
                        request.LeaseOwnerId,
                        new WorkflowExecutorInvocationLeaseEpoch(1),
                        request.ClaimedAtUtc,
                        request.LeaseExpiresAtUtc);
                    record = new WorkflowExecutorInvocationRecord(
                        request.Identity,
                        WorkflowExecutorInvocationState.Claimed,
                        Attempt: 1,
                        WorkflowExecutorInvocationConcurrencyVersion.Initial,
                        request.ClaimedAtUtc,
                        request.ClaimedAtUtc)
                    {
                        Lease = lease
                    };
                    return Task.FromResult(Claimed(record));
                }

                if (record.Identity.ScopeKey != request.Identity.ScopeKey ||
                    record.Identity.Key != request.Identity.Key ||
                    record.Identity.InputHash != request.Identity.InputHash)
                {
                    return Task.FromResult(new WorkflowExecutorInvocationClaimResult(
                        WorkflowExecutorInvocationClaimOutcome.InputMismatch,
                        record,
                        Claim: null));
                }

                if (record.State == WorkflowExecutorInvocationState.Completed)
                {
                    return Task.FromResult(new WorkflowExecutorInvocationClaimResult(
                        WorkflowExecutorInvocationClaimOutcome.ReplayedCompleted,
                        record,
                        Claim: null));
                }

                if (record.State == WorkflowExecutorInvocationState.FailedTerminal)
                {
                    return Task.FromResult(new WorkflowExecutorInvocationClaimResult(
                        WorkflowExecutorInvocationClaimOutcome.FailedTerminal,
                        record,
                        Claim: null));
                }

                if (record.State == WorkflowExecutorInvocationState.Claimed &&
                    record.Lease!.ExpiresAtUtc > request.ClaimedAtUtc)
                {
                    return Task.FromResult(new WorkflowExecutorInvocationClaimResult(
                        WorkflowExecutorInvocationClaimOutcome.ActiveLease,
                        record,
                        Claim: null));
                }

                if (record.Attempt >= request.MaximumAttempts)
                {
                    record = record with
                    {
                        State = WorkflowExecutorInvocationState.FailedTerminal,
                        ConcurrencyVersion = record.ConcurrencyVersion.Next(),
                        UpdatedAtUtc = request.ClaimedAtUtc,
                        Lease = null,
                        FailureCode = WorkflowExecutorInvocationFailureCode.AttemptLimitReached,
                        SafeMessage = "The governed executor invocation exhausted its bounded recovery attempts."
                    };
                    return Task.FromResult(new WorkflowExecutorInvocationClaimResult(
                        WorkflowExecutorInvocationClaimOutcome.AttemptLimitReached,
                        record,
                        Claim: null));
                }

                var leaseEpoch = record.Lease?.Epoch.Next() ?? new WorkflowExecutorInvocationLeaseEpoch(1);
                var takeoverLease = new WorkflowExecutorInvocationLease(
                    request.LeaseOwnerId,
                    leaseEpoch,
                    request.ClaimedAtUtc,
                    request.LeaseExpiresAtUtc);
                record = record with
                {
                    State = WorkflowExecutorInvocationState.Claimed,
                    Attempt = record.Attempt + 1,
                    ConcurrencyVersion = record.ConcurrencyVersion.Next(),
                    UpdatedAtUtc = request.ClaimedAtUtc,
                    Lease = takeoverLease
                };
                return Task.FromResult(Claimed(record));
            }
        }

        public Task<WorkflowExecutorInvocationRecord?> GetAsync(
            WorkflowExecutorInvocationKey key,
            CancellationToken cancellationToken = default)
        {
            lock (gate)
            {
                cancellationToken.ThrowIfCancellationRequested();
                return Task.FromResult(record?.Identity.Key == key ? record : null);
            }
        }

        public Task<WorkflowExecutorInvocationMutationResult> TryRenewLeaseAsync(
            WorkflowExecutorInvocationLeaseRenewalRequest request,
            CancellationToken cancellationToken = default)
        {
            lock (gate)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (!OwnsClaim(request.Key, request.ExpectedVersion, request.LeaseOwnerId, request.LeaseEpoch))
                {
                    return Task.FromResult(new WorkflowExecutorInvocationMutationResult(
                        WorkflowExecutorInvocationMutationOutcome.ConcurrencyConflict,
                        record));
                }

                if (record!.Lease!.ExpiresAtUtc < request.RenewedAtUtc)
                {
                    return Task.FromResult(new WorkflowExecutorInvocationMutationResult(
                        WorkflowExecutorInvocationMutationOutcome.LeaseExpired,
                        record));
                }

                record = record with
                {
                    ConcurrencyVersion = record.ConcurrencyVersion.Next(),
                    UpdatedAtUtc = request.RenewedAtUtc,
                    Lease = record.Lease with { ExpiresAtUtc = request.LeaseExpiresAtUtc }
                };
                RenewalCount++;
                renewed.TrySetResult();
                return Task.FromResult(new WorkflowExecutorInvocationMutationResult(
                    WorkflowExecutorInvocationMutationOutcome.Updated,
                    record));
            }
        }

        public Task<WorkflowExecutorInvocationMutationResult> TryCompleteAsync(
            WorkflowExecutorInvocationCompletionRequest request,
            CancellationToken cancellationToken = default)
        {
            lock (gate)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (!OwnsClaim(request.Key, request.ExpectedVersion, request.LeaseOwnerId, request.LeaseEpoch))
                {
                    return Task.FromResult(new WorkflowExecutorInvocationMutationResult(
                        WorkflowExecutorInvocationMutationOutcome.ConcurrencyConflict,
                        record));
                }

                record = record! with
                {
                    State = WorkflowExecutorInvocationState.Completed,
                    ConcurrencyVersion = record.ConcurrencyVersion.Next(),
                    UpdatedAtUtc = request.StoredResult.CompletedAtUtc,
                    Lease = null,
                    StoredResult = request.StoredResult
                };
                return Task.FromResult(new WorkflowExecutorInvocationMutationResult(
                    WorkflowExecutorInvocationMutationOutcome.Updated,
                    record));
            }
        }

        public Task<WorkflowExecutorInvocationMutationResult> TryFailAsync(
            WorkflowExecutorInvocationFailureRequest request,
            CancellationToken cancellationToken = default)
        {
            lock (gate)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (!OwnsClaim(request.Key, request.ExpectedVersion, request.LeaseOwnerId, request.LeaseEpoch))
                {
                    return Task.FromResult(new WorkflowExecutorInvocationMutationResult(
                        WorkflowExecutorInvocationMutationOutcome.ConcurrencyConflict,
                        record));
                }

                record = record! with
                {
                    State = request.FailureState,
                    ConcurrencyVersion = record.ConcurrencyVersion.Next(),
                    UpdatedAtUtc = request.FailedAtUtc,
                    Lease = null,
                    FailureCode = request.FailureCode,
                    SafeMessage = request.SafeMessage
                };
                return Task.FromResult(new WorkflowExecutorInvocationMutationResult(
                    WorkflowExecutorInvocationMutationOutcome.Updated,
                    record));
            }
        }

        private bool OwnsClaim(
            WorkflowExecutorInvocationKey key,
            WorkflowExecutorInvocationConcurrencyVersion version,
            WorkflowExecutorInvocationLeaseOwnerId ownerId,
            WorkflowExecutorInvocationLeaseEpoch epoch)
            => record is
            {
                State: WorkflowExecutorInvocationState.Claimed,
                Lease: { } lease
            } &&
            record.Identity.Key == key &&
            record.ConcurrencyVersion == version &&
            lease.OwnerId == ownerId &&
            lease.Epoch == epoch;

        private static WorkflowExecutorInvocationClaimResult Claimed(
            WorkflowExecutorInvocationRecord claimedRecord)
            => new(
                WorkflowExecutorInvocationClaimOutcome.Claimed,
                claimedRecord,
                new WorkflowExecutorInvocationClaim(
                    claimedRecord.Identity,
                    claimedRecord.Lease!,
                    claimedRecord.Attempt,
                    claimedRecord.ConcurrencyVersion));
    }
}
