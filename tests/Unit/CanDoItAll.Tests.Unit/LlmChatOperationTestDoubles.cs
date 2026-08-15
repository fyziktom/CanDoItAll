using CanDoItAll.AgentFramework.Llm.Abstractions;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.Providers;
using CanDoItAll.Modules.LlmChats.Application;
using CanDoItAll.Modules.LlmChats.Common;
using CanDoItAll.Modules.LlmChats.Conversations;
using CanDoItAll.Modules.LlmChats.Definitions;
using CanDoItAll.Modules.LlmChats.Operations;
using CanDoItAll.Modules.LlmChats.Persistence;
using CanDoItAll.Modules.LlmChats.Ports;
using CanDoItAll.SharedKernel;
using Microsoft.Extensions.Logging.Abstractions;

namespace CanDoItAll.Tests.Unit;

internal sealed class SequenceStreamingInvocationPort(IReadOnlyList<LlmStreamingUpdate> updates)
    : ILlmStreamingInvocationPort
{
    public async IAsyncEnumerable<LlmStreamingUpdate> StreamAsync(
        LlmInvocationRequest request,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        await Task.Yield();
        foreach (var update in updates)
        {
            cancellationToken.ThrowIfCancellationRequested();
            yield return update;
        }
    }
}

internal sealed class DelegatingStreamingInvocationPort(
    Func<LlmInvocationRequest, CancellationToken, IAsyncEnumerable<LlmStreamingUpdate>> stream)
    : ILlmStreamingInvocationPort
{
    public IAsyncEnumerable<LlmStreamingUpdate> StreamAsync(
        LlmInvocationRequest request,
        CancellationToken cancellationToken = default)
        => stream(request, cancellationToken);
}

internal sealed class InMemoryLlmChatOperationRepository : ILlmChatOperationRepository
{
    private readonly object gate = new();
    private readonly Dictionary<LlmChatOperationId, LlmChatOperation> operations = [];

    public Task<LlmChatOperation?> TryGetAsync(
        LlmChatOperationId id,
        CancellationToken cancellationToken = default)
    {
        lock (gate)
        {
            return Task.FromResult(operations.GetValueOrDefault(id));
        }
    }

    public Task<LlmChatOperation?> TryGetForUpdateAsync(
        LlmChatOperationId id,
        CancellationToken cancellationToken = default)
        => TryGetAsync(id, cancellationToken);

    public Task<IReadOnlyList<LlmChatOperationId>> ListDispatchCandidatesAsync(
        DateTimeOffset observedAtUtc,
        int take,
        CancellationToken cancellationToken = default)
    {
        lock (gate)
        {
            IReadOnlyList<LlmChatOperationId> result = operations.Values
                .Where(operation =>
                    operation.Status is LlmChatOperationStatus.Pending or
                        LlmChatOperationStatus.Running or
                        LlmChatOperationStatus.CancellationRequested &&
                    !operation.HasLiveExecutionLease(observedAtUtc))
                .OrderBy(operation => operation.StartedAtUtc)
                .ThenBy(operation => operation.Id.Value)
                .Take(take)
                .Select(operation => operation.Id)
                .ToArray();
            return Task.FromResult(result);
        }
    }

    public Task<LlmChatOperationAdmission> AdmitAsync(
        LlmChatOperation operation,
        CancellationToken cancellationToken = default)
    {
        lock (gate)
        {
            if (operations.TryGetValue(operation.Id, out var existing))
            {
                return Task.FromResult(new LlmChatOperationAdmission(existing, false));
            }

            operations.Add(operation.Id, operation);
            return Task.FromResult(new LlmChatOperationAdmission(operation, true));
        }
    }

    public Task<bool> TryReplaceAsync(
        LlmChatOperation operation,
        long expectedConcurrencyToken,
        CancellationToken cancellationToken = default)
    {
        lock (gate)
        {
            if (!operations.TryGetValue(operation.Id, out var current) ||
                current.ConcurrencyToken != expectedConcurrencyToken)
            {
                return Task.FromResult(false);
            }

            operations[operation.Id] = operation;
            return Task.FromResult(true);
        }
    }

    public Task<bool> TryReplaceOwnedAsync(
        LlmChatOperation operation,
        long expectedConcurrencyToken,
        LlmChatExecutionLeaseIdentity executionLease,
        DateTimeOffset observedAtUtc,
        CancellationToken cancellationToken = default)
    {
        lock (gate)
        {
            if (!operations.TryGetValue(operation.Id, out var current) ||
                current.ConcurrencyToken != expectedConcurrencyToken ||
                current.ExecutionOwnerId != executionLease.OwnerId ||
                current.ExecutionEpoch != executionLease.Epoch ||
                current.LeaseExpiresAtUtc <= observedAtUtc)
            {
                return Task.FromResult(false);
            }

            operations[operation.Id] = operation;
            return Task.FromResult(true);
        }
    }

    public void Seed(LlmChatOperation operation)
    {
        lock (gate)
        {
            operations[operation.Id] = operation;
        }
    }
}

internal sealed class InMemoryLlmChatOperationReadStore(
    InMemoryLlmChatOperationRepository operations,
    InMemoryLlmChatInvocationRecordRepository invocations) : ILlmChatOperationReadStore
{
    public async Task<LlmChatOperationReadModel?> TryGetAsync(
        LlmChatOperationId id,
        CancellationToken cancellationToken = default)
    {
        var operation = await operations.TryGetAsync(id, cancellationToken);
        return operation is null
            ? null
            : new LlmChatOperationReadModel(
                operation,
                await invocations.ListAsync(id, cancellationToken));
    }

    public Task<IReadOnlyList<LlmChatOperationId>> ListDispatchCandidatesAsync(
        DateTimeOffset observedAtUtc,
        int take,
        CancellationToken cancellationToken = default)
        => operations.ListDispatchCandidatesAsync(observedAtUtc, take, cancellationToken);
}

internal sealed class InMemoryLlmChatInvocationRecordRepository : ILlmChatInvocationRecordRepository
{
    private readonly object gate = new();
    private readonly List<LlmChatInvocationRecord> records = [];

    public Task AppendAsync(LlmChatInvocationRecord record, CancellationToken cancellationToken = default)
    {
        lock (gate)
        {
            if (records.Any(item => item.OperationId == record.OperationId && item.Ordinal == record.Ordinal))
            {
                throw new InvalidOperationException("The invocation record already exists.");
            }

            records.Add(record);
            return Task.CompletedTask;
        }
    }

    public Task<IReadOnlyList<LlmChatInvocationRecord>> ListAsync(
        LlmChatOperationId operationId,
        CancellationToken cancellationToken = default)
    {
        lock (gate)
        {
            return Task.FromResult<IReadOnlyList<LlmChatInvocationRecord>>(
                records.Where(item => item.OperationId == operationId).OrderBy(item => item.Ordinal).ToArray());
        }
    }
}

internal sealed class InMemoryLlmChatExecutionLeaseHeartbeatStore(
    InMemoryLlmChatOperationRepository operations) : ILlmChatExecutionLeaseHeartbeatStore
{
    public async Task<LlmChatExecutionLeaseObservation> RenewAndObserveAsync(
        LlmChatExecutionLeaseIdentity lease,
        LlmChatRuntimeIdentity runtimeIdentity,
        DateTimeOffset observedAtUtc,
        DateTimeOffset leaseExpiresAtUtc,
        CancellationToken cancellationToken = default)
    {
        var current = await operations.TryGetAsync(lease.OperationId, cancellationToken);
        if (!IsCurrent(current, lease, observedAtUtc))
        {
            return new LlmChatExecutionLeaseObservation(false, false);
        }

        var renewed = current! with
        {
            HeartbeatAtUtc = observedAtUtc,
            LeaseExpiresAtUtc = leaseExpiresAtUtc,
            ConcurrencyToken = current.ConcurrencyToken + 1
        };
        if (!await operations.TryReplaceOwnedAsync(
                renewed,
                current.ConcurrencyToken,
                lease,
                observedAtUtc,
                cancellationToken))
        {
            return new LlmChatExecutionLeaseObservation(false, false);
        }

        return Observe(renewed);
    }

    public async Task<LlmChatExecutionLeaseObservation> ObserveAsync(
        LlmChatExecutionLeaseIdentity lease,
        LlmChatRuntimeIdentity runtimeIdentity,
        DateTimeOffset observedAtUtc,
        CancellationToken cancellationToken = default)
    {
        var current = await operations.TryGetAsync(lease.OperationId, cancellationToken);
        return IsCurrent(current, lease, observedAtUtc)
            ? Observe(current!)
            : new LlmChatExecutionLeaseObservation(false, false);
    }

    private static bool IsCurrent(
        LlmChatOperation? operation,
        LlmChatExecutionLeaseIdentity lease,
        DateTimeOffset observedAtUtc)
        => operation is not null &&
           operation.ExecutionOwnerId == lease.OwnerId &&
           operation.ExecutionEpoch == lease.Epoch &&
           operation.LeaseExpiresAtUtc > observedAtUtc &&
           operation.Status is LlmChatOperationStatus.Running or LlmChatOperationStatus.CancellationRequested;

    private static LlmChatExecutionLeaseObservation Observe(LlmChatOperation operation)
        => new(
            true,
            operation.CancellationGeneration > 0 ||
            operation.Status == LlmChatOperationStatus.CancellationRequested);
}

internal sealed class EvidenceAwareLlmChatConversationEngine(
    ILlmChatOperationEvidenceSink evidence,
    TimeProvider timeProvider,
    bool blockDispatch,
    bool blockUntilCancelled,
    bool failAfterPartial) : ILlmChatConversationEngine
{
    private readonly Dictionary<LlmChatConversationId, LlmChatConversationEngineState> states = [];
    private readonly Dictionary<LlmChatOperationId, LlmChatConversationTurnEvidence> turnEvidence = [];
    private readonly Dictionary<LlmChatOperationId, LlmConversationTurnAdmission> admissions = [];
    private readonly TaskCompletionSource<bool> releaseDispatch =
        new(TaskCreationOptions.RunContinuationsAsynchronously);

    public TaskCompletionSource<bool> DispatchStarted { get; } =
        new(TaskCreationOptions.RunContinuationsAsynchronously);

    public int SendCount { get; private set; }

    public int AbandonCount { get; private set; }

    public bool FailCompensation { get; set; }

    public Task<LlmChatConversationEngineState> CreateAsync(
        LlmChatConversationId conversationId,
        LlmChatDefinitionRevision definitionRevision,
        string title,
        CancellationToken cancellationToken = default)
    {
        var now = timeProvider.GetUtcNow();
        var state = new LlmChatConversationEngineState(conversationId, 1, false, now, now);
        states[conversationId] = state;
        return Task.FromResult(state);
    }

    public Task<LlmChatConversationEngineState?> TryGetAsync(
        LlmChatConversationId conversationId,
        CancellationToken cancellationToken = default)
        => Task.FromResult(states.GetValueOrDefault(conversationId));

    public Task<LlmChatTranscriptPage?> TryGetTranscriptPageAsync(
        LlmChatConversationId conversationId,
        int take,
        LlmChatTranscriptCursor? cursor,
        CancellationToken cancellationToken = default)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(take, 1);
        var state = states.GetValueOrDefault(conversationId);
        return Task.FromResult(state is null
            ? null
            : new LlmChatTranscriptPage(state, [], null));
    }

    public Task<LlmChatConversationEngineState> RenameAsync(
        LlmChatConversationId conversationId,
        string title,
        long expectedTranscriptRevision,
        CancellationToken cancellationToken = default)
        => throw new NotSupportedException();

    public Task<LlmConversationTurnAdmission> AdmitTurnAsync(
        LlmChatConversationId conversationId,
        LlmChatOperationId operationId,
        LlmChatDefinition definition,
        LlmChatDefinitionRevision definitionRevision,
        string userText,
        long expectedTranscriptRevision,
        CancellationToken cancellationToken = default)
    {
        var now = timeProvider.GetUtcNow();
        var provider = ProviderRuntimeTestData.CreateProvider();
        var userEntry = new LlmConversationTranscriptEntry(
            Guid.NewGuid(),
            operationId.Value,
            LlmMessageRole.User,
            userText,
            now);
        var document = new LlmConversationDocument(
            conversationId.Value,
            "Conversation",
            LlmConversationProviderSnapshot.FromProfile(provider, definitionRevision.Model),
            now,
            now,
            expectedTranscriptRevision + 1,
            [userEntry],
            new LlmConversationActiveTurn(
                operationId.Value,
                userEntry.EntryId,
                now,
                expectedTranscriptRevision + 1));
        var state = new LlmChatConversationEngineState(
            conversationId,
            document.TranscriptRevision,
            true,
            now,
            now);
        states[conversationId] = state;
        turnEvidence[operationId] = new LlmChatConversationTurnEvidence(state, operationId, true, null);
        var admission = new LlmConversationTurnAdmission(
            document,
            userEntry,
            new LlmInvocationRequest(
                provider,
                definitionRevision.Model,
                [new LlmMessage(LlmMessageRole.User, userText)],
                settings: definitionRevision.Settings,
                correlationId: operationId.ToString()));
        admissions[operationId] = admission;
        return Task.FromResult(admission);
    }

    public Task<LlmConversationTurnAdmission> ResumeAdmittedTurnAsync(
        LlmChatConversationId conversationId,
        LlmChatOperationId operationId,
        LlmChatDefinition definition,
        LlmChatDefinitionRevision definitionRevision,
        CancellationToken cancellationToken = default)
        => Task.FromResult(admissions.TryGetValue(operationId, out var admission)
            ? admission
            : throw new InvalidOperationException("The admitted turn does not exist."));

    public async IAsyncEnumerable<LlmStreamingUpdate> StreamTurnAsync(
        LlmConversationTurnAdmission admission,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var operationId = new LlmChatOperationId(admission.UserEntry.TurnId);
        SendCount++;
        var now = timeProvider.GetUtcNow();
        var started = new LlmStreamingAttemptStarted(
            1,
            admission.InvocationRequest.Provider.Id,
            admission.InvocationRequest.Provider.Kind,
            admission.InvocationRequest.Model,
            LlmStreamingDeliveryMode.Incremental,
            now);
        await evidence.MarkProviderDispatchStartedAsync(operationId, started, cancellationToken);
        yield return started;
        DispatchStarted.TrySetResult(true);
        try
        {
            if (blockUntilCancelled)
            {
                await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
            }

            if (blockDispatch)
            {
                await releaseDispatch.Task;
            }
        }
        catch (OperationCanceledException)
        {
            await evidence.RecordInvocationAsync(new LlmChatInvocationRecord(
                operationId,
                admission.InvocationRequest.Provider.Id,
                admission.InvocationRequest.Provider.Kind,
                admission.InvocationRequest.Provider.Name,
                admission.InvocationRequest.Model,
                admission.InvocationRequest.Settings?.ThinkingEffort,
                admission.InvocationRequest.Settings?.ThinkingEffort ?? AgentReasoningEffortLevel.Low,
                1,
                LlmUsage.Zero,
                LlmChatInvocationOutcome.Cancelled,
                LlmChatErrorCodes.Cancelled,
                now,
                timeProvider.GetUtcNow(),
                operationId.ToString()), CancellationToken.None);
            throw;
        }

        if (failAfterPartial)
        {
            yield return new LlmStreamingTextDelta(1, "partial answer", 1);
            await evidence.RecordInvocationAsync(new LlmChatInvocationRecord(
                operationId,
                admission.InvocationRequest.Provider.Id,
                admission.InvocationRequest.Provider.Kind,
                admission.InvocationRequest.Provider.Name,
                admission.InvocationRequest.Model,
                admission.InvocationRequest.Settings?.ThinkingEffort,
                admission.InvocationRequest.Settings?.ThinkingEffort ?? AgentReasoningEffortLevel.Low,
                1,
                LlmUsage.Zero,
                LlmChatInvocationOutcome.Failed,
                LlmChatErrorCodes.ProviderUnavailable,
                now,
                timeProvider.GetUtcNow(),
                operationId.ToString()), cancellationToken);
            yield return new LlmStreamingFailed(
                1,
                LlmInvocationFailureKind.ProviderFailure,
                LlmUsage.Zero,
                false,
                timeProvider.GetUtcNow());
            yield break;
        }

        var usage = new LlmUsage(2, 1);
        await evidence.RecordInvocationAsync(new LlmChatInvocationRecord(
            operationId,
            admission.InvocationRequest.Provider.Id,
            admission.InvocationRequest.Provider.Kind,
            admission.InvocationRequest.Provider.Name,
            admission.InvocationRequest.Model,
            admission.InvocationRequest.Settings?.ThinkingEffort,
            admission.InvocationRequest.Settings?.ThinkingEffort ?? AgentReasoningEffortLevel.Low,
            1,
            usage,
            LlmChatInvocationOutcome.Succeeded,
            string.Empty,
            now,
            now,
            operationId.ToString()), cancellationToken);
        yield return new LlmStreamingTextDelta(1, "answer", 1);
        yield return new LlmStreamingCompleted(
            1,
            admission.InvocationRequest.Model,
            "stop",
            usage,
            LlmStreamingDeliveryMode.Incremental,
            timeProvider.GetUtcNow());
    }

    public Task<LlmChatConversationEngineTurnResult> CompleteTurnAsync(
        LlmConversationTurnAdmission admission,
        LlmInvocationResult invocationResult,
        CancellationToken cancellationToken = default)
    {
        var operationId = new LlmChatOperationId(admission.UserEntry.TurnId);
        var conversationId = new LlmChatConversationId(admission.Conversation.ConversationId);
        var now = timeProvider.GetUtcNow();
        var current = states[conversationId];
        var updated = current with
        {
            TranscriptRevision = admission.Conversation.TranscriptRevision + 1,
            HasActiveTurn = false,
            UpdatedAtUtc = now
        };
        states[conversationId] = updated;
        var assistantEntryId = Guid.NewGuid();
        turnEvidence[operationId] = new LlmChatConversationTurnEvidence(
            updated,
            operationId,
            false,
            new LlmChatAssistantTurnEvidence(
                assistantEntryId,
                invocationResult.ResponseText,
                invocationResult.Model,
                invocationResult.Usage,
                now));
        return Task.FromResult(new LlmChatConversationEngineTurnResult(
            updated,
            operationId,
            assistantEntryId,
            invocationResult.ResponseText,
            invocationResult.Model,
            invocationResult.Usage));
    }

    public Task<LlmChatConversationEngineState> CompensateTurnAsync(
        LlmChatConversationId conversationId,
        LlmChatOperationId operationId,
        CancellationToken cancellationToken = default)
    {
        if (FailCompensation)
        {
            throw new InvalidOperationException("Injected compensation failure.");
        }

        var evidenceResult = turnEvidence.GetValueOrDefault(operationId);
        if (evidenceResult?.HasExactActiveTurn != true)
        {
            throw new InvalidOperationException("The exact operation turn is not active.");
        }

        AbandonCount++;
        var updated = evidenceResult.State with
        {
            TranscriptRevision = evidenceResult.State.TranscriptRevision + 1,
            HasActiveTurn = false,
            UpdatedAtUtc = timeProvider.GetUtcNow()
        };
        states[conversationId] = updated;
        turnEvidence[operationId] = evidenceResult with
        {
            State = updated,
            HasExactActiveTurn = false
        };
        return Task.FromResult(updated);
    }

    public Task<LlmChatConversationTurnEvidence?> InspectTurnAsync(
        LlmChatConversationId conversationId,
        LlmChatOperationId operationId,
        CancellationToken cancellationToken = default)
    {
        if (turnEvidence.TryGetValue(operationId, out var evidenceResult))
        {
            return Task.FromResult<LlmChatConversationTurnEvidence?>(evidenceResult);
        }

        var state = states.GetValueOrDefault(conversationId);
        return Task.FromResult(state is null
            ? null
            : new LlmChatConversationTurnEvidence(state, operationId, false, null));
    }

    public Task<LlmChatConversationEngineState> AbandonActiveTurnAsync(
        LlmChatConversationId conversationId,
        LlmChatOperationId operationId,
        CancellationToken cancellationToken = default)
        => CompensateTurnAsync(conversationId, operationId, cancellationToken);

    public void SeedConversation(LlmChatConversationId conversationId)
    {
        var now = timeProvider.GetUtcNow();
        states[conversationId] = new LlmChatConversationEngineState(conversationId, 1, false, now, now);
    }

    public void SeedAssistantEvidence(LlmChatOperationId operationId, string text)
    {
        var state = states.Values.Single() with
        {
            TranscriptRevision = 3,
            HasActiveTurn = false
        };
        states[state.ConversationId] = state;
        turnEvidence[operationId] = new LlmChatConversationTurnEvidence(
            state,
            operationId,
            false,
            new LlmChatAssistantTurnEvidence(
                Guid.NewGuid(),
                text,
                "model-fast",
                new LlmUsage(2, 1),
                timeProvider.GetUtcNow()));
    }

    public void SeedActiveTurn(LlmChatOperationId operationId)
    {
        var state = states.Values.Single() with { HasActiveTurn = true };
        states[state.ConversationId] = state;
        turnEvidence[operationId] = new LlmChatConversationTurnEvidence(state, operationId, true, null);
    }

    public void ReleaseDispatch()
        => releaseDispatch.TrySetResult(true);
}

internal sealed class LlmChatOperationHarness
{
    private readonly LlmChatExecutionLeaseService leaseService;
    private readonly LlmChatOperationExecutor executor;
    private readonly ILlmChatOperationScopeAccessor operationScope;
    private readonly LlmChatRuntimeIdentity runtimeIdentity;
    private readonly LlmChatExecutionOwnerId ownerId = LlmChatExecutionOwnerId.New();

    private LlmChatOperationHarness(
        DateTimeOffset now,
        LlmChatConversationId conversationId,
        LlmChatDefinitionRevision revision,
        InMemoryLlmChatConversationRepository conversations,
        InMemoryLlmChatOperationRepository operations,
        InMemoryLlmChatInvocationRecordRepository invocations,
        EvidenceAwareLlmChatConversationEngine engine,
        LlmChatOperationCancellationRegistry cancellations,
        LlmChatOperationApplicationService service,
        LlmChatExecutionLeaseService leaseService,
        LlmChatOperationExecutor executor,
        ILlmChatOperationScopeAccessor operationScope,
        LlmChatRuntimeIdentity runtimeIdentity,
        IDisposable executorRegistration)
    {
        Now = now;
        ConversationId = conversationId;
        Revision = revision;
        Conversations = conversations;
        Operations = operations;
        Invocations = invocations;
        Engine = engine;
        Cancellations = cancellations;
        Service = service;
        this.leaseService = leaseService;
        this.executor = executor;
        this.operationScope = operationScope;
        this.runtimeIdentity = runtimeIdentity;
        ExecutorRegistration = executorRegistration;
    }

    public DateTimeOffset Now { get; }

    public LlmChatConversationId ConversationId { get; }

    public LlmChatDefinitionRevision Revision { get; }

    public InMemoryLlmChatConversationRepository Conversations { get; }

    public InMemoryLlmChatOperationRepository Operations { get; }

    public InMemoryLlmChatInvocationRecordRepository Invocations { get; }

    public EvidenceAwareLlmChatConversationEngine Engine { get; }

    public LlmChatOperationCancellationRegistry Cancellations { get; }

    public LlmChatOperationApplicationService Service { get; }

    public IDisposable ExecutorRegistration { get; }

    public static async Task<LlmChatOperationHarness> CreateAsync(
        bool blockDispatch = false,
        bool blockUntilCancelled = false,
        bool executorAvailable = true,
        bool failAfterPartial = false)
    {
        var now = new DateTimeOffset(2026, 8, 14, 12, 0, 0, TimeSpan.Zero);
        var timeProvider = new FixedTimeProvider(now);
        var provider = ProviderRuntimeTestData.CreateProvider();
        var definitionId = new LlmChatDefinitionId(Guid.NewGuid());
        var revision = ProviderRuntimeTestData.CreateRevision(definitionId, 1, provider, null);
        var definition = ProviderRuntimeTestData.CreateDefinition(
            definitionId,
            1,
            LlmChatDefinitionStatus.Active);
        var definitions = new InMemoryLlmChatDefinitionRepository();
        await definitions.CreateAsync(definition, revision);
        var conversationId = LlmChatConversationId.New();
        var conversations = new InMemoryLlmChatConversationRepository();
        await conversations.CreateAsync(new LlmChatConversation(
            conversationId,
            definitionId,
            revision.Revision,
            "Conversation",
            LlmChatConversationStatus.Active,
            LlmChatConversationOrigin.Api,
            now,
            now,
            0));
        var operations = new InMemoryLlmChatOperationRepository();
        var invocations = new InMemoryLlmChatInvocationRecordRepository();
        var unitOfWork = new InlineLlmChatUnitOfWork();
        var operationScope = new LlmChatOperationScopeAccessor();
        var streamingOptions = new LlmChatStreamingOptions();
        var eventJournal = new LlmChatOperationEventJournal(
            operations,
            new InMemoryLlmChatOperationEventRepository(operations),
            unitOfWork,
            new NoopLlmChatOperationEventSignal(),
            operationScope,
            streamingOptions,
            timeProvider);
        var evidence = new LlmChatOperationEvidenceService(
            operations,
            invocations,
            unitOfWork,
            operationScope,
            timeProvider,
            eventJournal);
        var engine = new EvidenceAwareLlmChatConversationEngine(
            evidence,
            timeProvider,
            blockDispatch,
            blockUntilCancelled,
            failAfterPartial);
        engine.SeedConversation(conversationId);
        var cancellations = new LlmChatOperationCancellationRegistry();
        var details = new LlmChatOperationDetailsReader(
            operations,
            new InMemoryLlmChatOperationReadStore(operations, invocations),
            engine,
            eventJournal);
        var admission = new LlmChatOperationAdmissionService(
            definitions,
            conversations,
            operations,
            new StubLlmChatTurnStateRepository(),
            unitOfWork,
            engine,
            evidence,
            timeProvider,
            eventJournal);
        var stateMachine = new LlmChatOperationStateMachine(
            operations,
            invocations,
            unitOfWork,
            engine,
            evidence,
            details,
            timeProvider,
            NullLogger<LlmChatOperationStateMachine>.Instance);
        var dispatchSignal = new LlmChatOperationDispatchSignal();
        IDisposable executorRegistration = executorAvailable
            ? dispatchSignal.RegisterExecutor()
            : new CancellationTokenSource();
        var service = new LlmChatOperationApplicationService(
            admission,
            stateMachine,
            details,
            cancellations,
            dispatchSignal,
            NullLogger<LlmChatOperationApplicationService>.Instance);
        var options = new LlmChatExecutionLeaseOptions
        {
            PollInterval = TimeSpan.FromMilliseconds(100),
            HeartbeatInterval = TimeSpan.FromMilliseconds(100),
            LeaseDuration = TimeSpan.FromSeconds(1)
        };
        var leaseService = new LlmChatExecutionLeaseService(
            operations,
            unitOfWork,
            options,
            timeProvider,
            eventJournal);
        var streamingPipeline = new LlmChatStreamingPipeline(
            eventJournal,
            streamingOptions,
            timeProvider);
        var executor = new LlmChatOperationExecutor(
            definitions,
            conversations,
            engine,
            new InMemoryLlmChatExecutionLeaseHeartbeatStore(operations),
            cancellations,
            operationScope,
            streamingPipeline,
            stateMachine,
            options,
            timeProvider,
            NullLogger<LlmChatOperationExecutor>.Instance);
        var runtimeIdentity = new LlmChatRuntimeIdentity(
            ProviderRuntimeTestData.RuntimeIdentity.ActiveProfileId!.Value,
            ProviderRuntimeTestData.RuntimeIdentity.ActiveFingerprint!,
            ProviderRuntimeTestData.RuntimeIdentity.Generation);
        return new LlmChatOperationHarness(
            now,
            conversationId,
            revision,
            conversations,
            operations,
            invocations,
            engine,
            cancellations,
            service,
            leaseService,
            executor,
            operationScope,
            runtimeIdentity,
            executorRegistration);
    }

    public async Task<Result<LlmChatOperationDetails>> SendAndDispatchAsync(SendLlmChatTurnCommand command)
    {
        var admitted = await Service.SendAsync(command);
        if (admitted.IsFailure || admitted.Value!.Operation.IsTerminal ||
            admitted.Value.Operation.Status == LlmChatOperationStatus.RecoveryRequired)
        {
            return admitted;
        }

        await DispatchAsync(command.OperationId);
        return await Service.GetAsync(command.OperationId);
    }

    public async Task DispatchAsync(LlmChatOperationId operationId)
    {
        using var scope = operationScope.Push(new LlmChatOperationExecutionContext(operationId, runtimeIdentity));
        var claim = await leaseService.TryClaimAsync(operationId, ownerId);
        if (claim.Claimed)
        {
            await executor.ExecuteAsync(claim);
        }
    }

    public SendLlmChatTurnCommand CreateSendCommand(LlmChatOperationId operationId, string message)
        => new(operationId, ConversationId, 1, message);

    public LlmChatOperation CreateOperation(
        LlmChatOperationId operationId,
        LlmChatOperationStatus status)
        => new(
            operationId,
            ConversationId,
            LlmChatOperationKind.SendTurn,
            LlmChatFingerprints.CreateRequest(
                ConversationId,
                1,
                "hello",
                Revision.SettingsFingerprint),
            1,
            status,
            Now,
            0);
}

internal sealed class LlmChatInvocationAuditHarness
{
    private readonly ILlmInvocationPort port;
    private readonly ILlmChatOperationScopeAccessor scope;
    private readonly LlmInvocationRequest request;

    private LlmChatInvocationAuditHarness(
        LlmChatOperationId operationId,
        InMemoryLlmChatInvocationRecordRepository invocations,
        ILlmInvocationPort port,
        ILlmChatOperationScopeAccessor scope,
        LlmInvocationRequest request)
    {
        OperationId = operationId;
        Invocations = invocations;
        this.port = port;
        this.scope = scope;
        this.request = request;
    }

    public LlmChatOperationId OperationId { get; }

    public InMemoryLlmChatInvocationRecordRepository Invocations { get; }

    public static LlmChatInvocationAuditHarness Create(
        AgentReasoningEffortLevel? requestedEffort,
        Func<LlmInvocationRequest, LlmInvocationResult> invoke)
    {
        var now = new DateTimeOffset(2026, 8, 14, 12, 0, 0, TimeSpan.Zero);
        var provider = ProviderRuntimeTestData.CreateProvider();
        var operationId = LlmChatOperationId.New();
        var operations = new InMemoryLlmChatOperationRepository();
        operations.Seed(new LlmChatOperation(
            operationId,
            LlmChatConversationId.New(),
            LlmChatOperationKind.SendTurn,
            new LlmChatRequestFingerprint(new string('a', 64)),
            1,
            LlmChatOperationStatus.Running,
            now,
            0)
        {
            TurnAdmittedAtUtc = now
        });
        var invocations = new InMemoryLlmChatInvocationRecordRepository();
        var scope = new LlmChatOperationScopeAccessor();
        var unitOfWork = new InlineLlmChatUnitOfWork();
        var timeProvider = new FixedTimeProvider(now);
        var evidence = new LlmChatOperationEvidenceService(
            operations,
            invocations,
            unitOfWork,
            scope,
            timeProvider,
            LlmChatOperationEventTestFactory.Create(operations, unitOfWork, scope, timeProvider));
        var inner = new DelegatingInvocationPort((request, cancellationToken) =>
            Task.FromResult(invoke(request)));
        var port = new AuditedLlmChatInvocationPort(
            inner,
            evidence,
            new ProviderModelCapabilityResolver(),
            scope,
            new FixedTimeProvider(now));
        var settings = new LlmModelSettings(0.2, "{}") { ThinkingEffort = requestedEffort };
        var request = new LlmInvocationRequest(
            provider,
            "model-fast",
            [new LlmMessage(LlmMessageRole.User, "hello")],
            settings: settings,
            correlationId: operationId.ToString());
        return new LlmChatInvocationAuditHarness(operationId, invocations, port, scope, request);
    }

    public async Task<LlmInvocationResult> InvokeAsync()
    {
        using var operation = scope.Push(new LlmChatOperationExecutionContext(
            OperationId,
            new LlmChatRuntimeIdentity(
                ProviderRuntimeTestData.RuntimeIdentity.ActiveProfileId!.Value,
                ProviderRuntimeTestData.RuntimeIdentity.ActiveFingerprint!,
                ProviderRuntimeTestData.RuntimeIdentity.Generation)));
        return await port.InvokeAsync(request);
    }
}
