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
using Microsoft.Extensions.Logging.Abstractions;

namespace CanDoItAll.Tests.Unit;

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

    public Task<LlmChatOperation?> TryClaimDispatchAsync(
        LlmChatOperationId id,
        LlmChatRequestFingerprint requestFingerprint,
        CancellationToken cancellationToken = default)
    {
        lock (gate)
        {
            if (!operations.TryGetValue(id, out var operation) ||
                operation.Status != LlmChatOperationStatus.Pending ||
                operation.RequestFingerprint != requestFingerprint)
            {
                return Task.FromResult<LlmChatOperation?>(null);
            }

            var claimed = operation with
            {
                Status = LlmChatOperationStatus.Running,
                ConcurrencyToken = operation.ConcurrencyToken + 1
            };
            operations[id] = claimed;
            return Task.FromResult<LlmChatOperation?>(claimed);
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

    public void Seed(LlmChatOperation operation)
    {
        lock (gate)
        {
            operations[operation.Id] = operation;
        }
    }
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

internal sealed class EvidenceAwareLlmChatConversationEngine(
    ILlmChatOperationEvidenceSink evidence,
    TimeProvider timeProvider,
    bool blockDispatch,
    bool blockUntilCancelled) : ILlmChatConversationEngine
{
    private readonly Dictionary<LlmChatConversationId, LlmChatConversationEngineState> states = [];
    private readonly Dictionary<LlmChatOperationId, LlmChatConversationTurnEvidence> turnEvidence = [];
    private readonly TaskCompletionSource<bool> releaseDispatch =
        new(TaskCreationOptions.RunContinuationsAsynchronously);

    public TaskCompletionSource<bool> DispatchStarted { get; } =
        new(TaskCreationOptions.RunContinuationsAsynchronously);

    public int SendCount { get; private set; }

    public int AbandonCount { get; private set; }

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
        int offset,
        CancellationToken cancellationToken = default)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(take, 1);
        ArgumentOutOfRangeException.ThrowIfNegative(offset);
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

    public async Task<LlmChatConversationEngineTurnResult> SendAsync(
        LlmChatConversationId conversationId,
        LlmChatOperationId operationId,
        LlmChatDefinition definition,
        LlmChatDefinitionRevision definitionRevision,
        string userText,
        long expectedTranscriptRevision,
        CancellationToken cancellationToken = default)
    {
        SendCount++;
        var now = timeProvider.GetUtcNow();
        await evidence.MarkTurnAdmittedAsync(operationId, now, cancellationToken);
        await evidence.MarkProviderDispatchStartedAsync(operationId, now, cancellationToken);
        DispatchStarted.TrySetResult(true);
        if (blockUntilCancelled)
        {
            await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
        }

        if (blockDispatch)
        {
            await releaseDispatch.Task.WaitAsync(cancellationToken);
        }

        var usage = new LlmUsage(2, 1);
        await evidence.RecordInvocationAsync(new LlmChatInvocationRecord(
            operationId,
            definitionRevision.ProviderProfileId,
            definitionRevision.ProviderKind,
            definitionRevision.ProviderName,
            definitionRevision.Model,
            definitionRevision.Settings.ThinkingEffort,
            definitionRevision.Settings.ThinkingEffort ?? AgentReasoningEffortLevel.Low,
            1,
            usage,
            LlmChatInvocationOutcome.Succeeded,
            string.Empty,
            now,
            now,
            operationId.ToString()), cancellationToken);
        var current = states[conversationId];
        var updated = current with
        {
            TranscriptRevision = expectedTranscriptRevision + 2,
            UpdatedAtUtc = now
        };
        states[conversationId] = updated;
        var assistantEntryId = Guid.NewGuid();
        turnEvidence[operationId] = new LlmChatConversationTurnEvidence(
            updated,
            operationId,
            false,
            new LlmChatAssistantTurnEvidence(assistantEntryId, "answer", definitionRevision.Model, usage, now));
        return new LlmChatConversationEngineTurnResult(
            updated,
            operationId,
            assistantEntryId,
            "answer",
            definitionRevision.Model,
            usage);
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
    {
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
    private LlmChatOperationHarness(
        DateTimeOffset now,
        LlmChatConversationId conversationId,
        LlmChatDefinitionRevision revision,
        InMemoryLlmChatOperationRepository operations,
        InMemoryLlmChatInvocationRecordRepository invocations,
        EvidenceAwareLlmChatConversationEngine engine,
        LlmChatOperationCancellationRegistry cancellations,
        LlmChatOperationApplicationService service)
    {
        Now = now;
        ConversationId = conversationId;
        Revision = revision;
        Operations = operations;
        Invocations = invocations;
        Engine = engine;
        Cancellations = cancellations;
        Service = service;
    }

    public DateTimeOffset Now { get; }

    public LlmChatConversationId ConversationId { get; }

    public LlmChatDefinitionRevision Revision { get; }

    public InMemoryLlmChatOperationRepository Operations { get; }

    public InMemoryLlmChatInvocationRecordRepository Invocations { get; }

    public EvidenceAwareLlmChatConversationEngine Engine { get; }

    public LlmChatOperationCancellationRegistry Cancellations { get; }

    public LlmChatOperationApplicationService Service { get; }

    public static async Task<LlmChatOperationHarness> CreateAsync(
        bool blockDispatch = false,
        bool blockUntilCancelled = false)
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
        var evidence = new LlmChatOperationEvidenceService(operations, invocations, unitOfWork);
        var engine = new EvidenceAwareLlmChatConversationEngine(
            evidence,
            timeProvider,
            blockDispatch,
            blockUntilCancelled);
        engine.SeedConversation(conversationId);
        var cancellations = new LlmChatOperationCancellationRegistry();
        var service = new LlmChatOperationApplicationService(
            definitions,
            conversations,
            operations,
            invocations,
            unitOfWork,
            engine,
            evidence,
            cancellations,
            timeProvider,
            NullLogger<LlmChatOperationApplicationService>.Instance);
        return new LlmChatOperationHarness(
            now,
            conversationId,
            revision,
            operations,
            invocations,
            engine,
            cancellations,
            service);
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
        var evidence = new LlmChatOperationEvidenceService(
            operations,
            invocations,
            new InlineLlmChatUnitOfWork());
        var scope = new LlmChatOperationScopeAccessor();
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
