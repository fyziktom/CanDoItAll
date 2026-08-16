using CanDoItAll.AgentFramework.Llm.Abstractions;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Modules.LlmChats.Application;
using CanDoItAll.Modules.LlmChats.Common;
using CanDoItAll.Modules.LlmChats.Conversations;
using CanDoItAll.Modules.LlmChats.Definitions;
using CanDoItAll.Modules.LlmChats.Operations;
using CanDoItAll.Modules.LlmChats.Ports;
using CanDoItAll.SharedKernel;

namespace CanDoItAll.Tests.Unit;

internal sealed class InMemoryLlmChatDefinitionRepository :
    ILlmChatDefinitionRepository,
    ILlmChatDefinitionReadStore
{
    private readonly Dictionary<LlmChatDefinitionId, LlmChatDefinition> definitions = [];
    private readonly Dictionary<(LlmChatDefinitionId Id, int Revision), LlmChatDefinitionRevision> revisions = [];
    private readonly Dictionary<LlmChatDefinitionId, IReadOnlyList<string>> tags = [];

    public Task<LlmChatDefinition?> TryGetAsync(
        LlmChatDefinitionId id,
        CancellationToken cancellationToken = default)
        => Task.FromResult(definitions.GetValueOrDefault(id));

    public Task<LlmChatDefinitionRevision?> TryGetRevisionAsync(
        LlmChatDefinitionId id,
        LlmChatDefinitionRevisionNumber revision,
        CancellationToken cancellationToken = default)
        => Task.FromResult(revisions.GetValueOrDefault((id, revision.Value)));

    public Task<IReadOnlyList<LlmChatDefinition>> ListAsync(
        int take,
        LlmChatDefinitionStatus? status,
        CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyList<LlmChatDefinition>>(definitions.Values
            .Where(item => status is null || item.Status == status)
            .OrderBy(item => item.Name, StringComparer.OrdinalIgnoreCase)
            .Take(take)
            .ToArray());

    public Task<IReadOnlyList<LlmChatDefinition>> ListPageAsync(
        int take,
        int offset,
        LlmChatDefinitionStatus? status,
        CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyList<LlmChatDefinition>>(definitions.Values
            .Where(item => status is null || item.Status == status)
            .OrderBy(item => item.Name, StringComparer.OrdinalIgnoreCase)
            .ThenBy(item => item.Id.Value)
            .Skip(offset)
            .Take(take)
            .ToArray());

    public Task<IReadOnlyList<string>> ListTagsAsync(
        LlmChatDefinitionId id,
        CancellationToken cancellationToken = default)
        => Task.FromResult(tags.GetValueOrDefault(id, []));

    public Task ReplaceTagsAsync(
        LlmChatDefinitionId id,
        IReadOnlyList<string> values,
        CancellationToken cancellationToken = default)
    {
        tags[id] = values.ToArray();
        return Task.CompletedTask;
    }

    public Task CreateAsync(
        LlmChatDefinition definition,
        LlmChatDefinitionRevision revision,
        CancellationToken cancellationToken = default)
    {
        definitions.Add(definition.Id, definition);
        revisions.Add((revision.DefinitionId, revision.Revision.Value), revision);
        return Task.CompletedTask;
    }

    public Task ReplaceAsync(
        LlmChatDefinition definition,
        long expectedConcurrencyToken,
        LlmChatDefinitionRevision? appendedRevision,
        CancellationToken cancellationToken = default)
    {
        var current = definitions[definition.Id];
        Assert.Equal(expectedConcurrencyToken, current.ConcurrencyToken);
        definitions[definition.Id] = definition;
        if (appendedRevision is not null)
        {
            revisions.Add((appendedRevision.DefinitionId, appendedRevision.Revision.Value), appendedRevision);
        }

        return Task.CompletedTask;
    }

    async Task<LlmChatDefinitionReadModel?> ILlmChatDefinitionReadStore.TryGetAsync(
        LlmChatDefinitionId id,
        CancellationToken cancellationToken)
    {
        var definition = await TryGetAsync(id, cancellationToken);
        if (definition is null)
        {
            return null;
        }

        var revision = await TryGetRevisionAsync(id, definition.CurrentRevision, cancellationToken);
        return revision is null
            ? null
            : new LlmChatDefinitionReadModel(
                definition,
                revision,
                await ListTagsAsync(id, cancellationToken));
    }

    public async Task<LlmChatPage<LlmChatDefinitionReadModel, LlmChatDefinitionCursor>> ListPageAsync(
        int take,
        LlmChatDefinitionCursor? cursor,
        LlmChatDefinitionStatus? status,
        CancellationToken cancellationToken = default)
    {
        var ordered = definitions.Values
            .Where(item => status is null || item.Status == status)
            .Where(item => cursor is not { } position ||
                           item.UpdatedAtUtc < position.UpdatedAtUtc ||
                           item.UpdatedAtUtc == position.UpdatedAtUtc &&
                           item.Id.Value.CompareTo(position.DefinitionId.Value) > 0)
            .OrderByDescending(item => item.UpdatedAtUtc)
            .ThenBy(item => item.Id.Value)
            .Take(checked(take + 1))
            .ToArray();
        var pageDefinitions = ordered.Take(take).ToArray();
        var items = new List<LlmChatDefinitionReadModel>(pageDefinitions.Length);
        foreach (var definition in pageDefinitions)
        {
            items.Add(new LlmChatDefinitionReadModel(
                definition,
                revisions[(definition.Id, definition.CurrentRevision.Value)],
                tags.GetValueOrDefault(definition.Id, [])));
        }

        LlmChatDefinitionCursor? next = ordered.Length > take && pageDefinitions.Length > 0
            ? new LlmChatDefinitionCursor(pageDefinitions[^1].UpdatedAtUtc, pageDefinitions[^1].Id)
            : null;
        return new LlmChatPage<LlmChatDefinitionReadModel, LlmChatDefinitionCursor>(items, next);
    }
}

internal sealed class InMemoryLlmChatConversationRepository : ILlmChatConversationRepository
{
    private readonly Dictionary<LlmChatConversationId, LlmChatConversation> conversations = [];

    public Task<LlmChatConversation?> TryGetAsync(
        LlmChatConversationId id,
        CancellationToken cancellationToken = default)
        => Task.FromResult(conversations.GetValueOrDefault(id));

    public Task<IReadOnlyList<LlmChatConversation>> ListAsync(
        int take,
        LlmChatDefinitionId? definitionId,
        CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyList<LlmChatConversation>>(conversations.Values
            .Where(item => definitionId is null || item.DefinitionId == definitionId)
            .OrderByDescending(item => item.UpdatedAtUtc)
            .Take(take)
            .ToArray());

    public Task<IReadOnlyList<LlmChatConversation>> ListPageAsync(
        int take,
        int offset,
        LlmChatDefinitionId? definitionId,
        CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyList<LlmChatConversation>>(conversations.Values
            .Where(item => definitionId is null || item.DefinitionId == definitionId)
            .OrderByDescending(item => item.UpdatedAtUtc)
            .ThenBy(item => item.Id.Value)
            .Skip(offset)
            .Take(take)
            .ToArray());

    public Task CreateAsync(LlmChatConversation conversation, CancellationToken cancellationToken = default)
    {
        conversations.Add(conversation.Id, conversation);
        return Task.CompletedTask;
    }

    public Task ReplaceAsync(
        LlmChatConversation conversation,
        long expectedConcurrencyToken,
        CancellationToken cancellationToken = default)
    {
        var current = conversations[conversation.Id];
        Assert.Equal(expectedConcurrencyToken, current.ConcurrencyToken);
        conversations[conversation.Id] = conversation;
        return Task.CompletedTask;
    }

    public void Seed(LlmChatConversation conversation)
        => conversations[conversation.Id] = conversation;
}

internal sealed class StubLlmChatConversationReadStore : ILlmChatConversationReadStore
{
    public Task<LlmChatConversationReadModel?> TryGetAsync(
        LlmChatConversationId id,
        CancellationToken cancellationToken = default)
        => throw new NotSupportedException();

    public Task<LlmChatPage<LlmChatConversationReadModel, LlmChatConversationCursor>> ListPageAsync(
        int take,
        LlmChatConversationCursor? cursor,
        LlmChatDefinitionId? definitionId,
        CancellationToken cancellationToken = default)
        => throw new NotSupportedException();

    public Task<LlmChatTranscriptReadModel?> TryGetTranscriptPageAsync(
        LlmChatConversationId id,
        int take,
        LlmChatTranscriptCursor? cursor,
        CancellationToken cancellationToken = default)
        => throw new NotSupportedException();

    public Task<LlmChatConversationTurnEvidence?> TryInspectTurnAsync(
        LlmChatConversationId conversationId,
        LlmChatOperationId operationId,
        CancellationToken cancellationToken = default)
        => throw new NotSupportedException();
}

internal sealed class InlineLlmChatUnitOfWork : ILlmChatUnitOfWork
{
    public Task<T> ExecuteAsync<T>(
        Func<CancellationToken, Task<T>> operation,
        CancellationToken cancellationToken = default)
        => operation(cancellationToken);

    public void RegisterPostCommit(Action callback)
    {
        ArgumentNullException.ThrowIfNull(callback);
        callback();
    }
}

internal sealed class InMemoryLlmChatOperationEventRepository(
    InMemoryLlmChatOperationRepository operations) : ILlmChatOperationEventRepository
{
    private readonly Dictionary<LlmChatOperationId, List<LlmChatOperationEvent>> events = [];

    public async Task<LlmChatOperationEvent> AppendAsync(
        LlmChatOperationId operationId,
        Func<long, LlmChatOperationEvent> createEvent,
        CancellationToken cancellationToken = default)
    {
        var operation = await operations.TryGetAsync(operationId, cancellationToken);
        if (operation is null)
        {
            throw new InvalidOperationException("The operation does not exist.");
        }

        var journal = events.GetValueOrDefault(operationId);
        if (journal is null)
        {
            journal = [];
            events.Add(operationId, journal);
        }

        var appended = createEvent(checked(operation.LastEventSequence + 1));
        journal.Add(appended);
        operations.Seed(operation with { LastEventSequence = appended.Sequence });
        return appended;
    }

    public async Task<LlmChatOperationEventPage?> ListAfterAsync(
        LlmChatOperationId operationId,
        long afterSequence,
        int take,
        CancellationToken cancellationToken = default)
    {
        var operation = await operations.TryGetAsync(operationId, cancellationToken);
        if (operation is null)
        {
            return null;
        }

        var journal = events.GetValueOrDefault(operationId) ?? [];
        return new LlmChatOperationEventPage(
            operation,
            [.. journal.Where(item => item.Sequence > afterSequence).Take(take)],
            journal.Count == 0 ? null : journal[0].Sequence,
            operation.LastEventSequence,
            journal.OfType<LlmChatOperationTextDeltaEvent>()
                .Where(item => item.Sequence <= afterSequence)
                .Sum(item => item.Text.Length));
    }

    public async Task<long?> TryGetLatestSequenceAsync(
        LlmChatOperationId operationId,
        CancellationToken cancellationToken = default)
    {
        if (await operations.TryGetAsync(operationId, cancellationToken) is null)
        {
            return null;
        }

        return (await operations.TryGetAsync(operationId, cancellationToken))!.LastEventSequence;
    }

    public Task<int> DeleteExpiredTerminalEventsAsync(
        DateTimeOffset completedBeforeUtc,
        int take,
        CancellationToken cancellationToken = default)
        => Task.FromResult(0);
}

internal sealed class NoopLlmChatOperationEventSignal : ILlmChatOperationEventSignal
{
    public void Publish(
        LlmChatRuntimeIdentity runtimeIdentity,
        LlmChatOperationId operationId,
        long sequence)
    {
    }

    public ValueTask WaitAsync(
        LlmChatRuntimeIdentity runtimeIdentity,
        LlmChatOperationId operationId,
        long afterSequence,
        TimeSpan maximumDelay,
        CancellationToken cancellationToken = default)
        => ValueTask.CompletedTask;
}

internal static class LlmChatOperationEventTestFactory
{
    public static LlmChatOperationEventJournal Create(
        InMemoryLlmChatOperationRepository operations,
        ILlmChatUnitOfWork unitOfWork,
        ILlmChatOperationScopeAccessor operationScope,
        TimeProvider timeProvider,
        LlmChatStreamingOptions? options = null)
        => new(
            operations,
            new InMemoryLlmChatOperationEventRepository(operations),
            unitOfWork,
            new NoopLlmChatOperationEventSignal(),
            operationScope,
            options ?? new LlmChatStreamingOptions(),
            timeProvider);
}

internal sealed class StubLlmChatTurnStateRepository(
    bool exists = true,
    bool hasActiveTurn = false,
    bool hasNonterminalOperation = false) : ILlmChatTurnStateRepository
{
    public Task<LlmChatConversationTurnState> LockAsync(
        LlmChatConversationId conversationId,
        CancellationToken cancellationToken = default)
        => Task.FromResult(new LlmChatConversationTurnState(
            exists,
            hasActiveTurn,
            hasNonterminalOperation));
}

internal sealed class StubLlmChatProviderResolver : ILlmChatProviderResolver
{
    public List<(Guid ProviderProfileId, string Model, AgentReasoningEffortLevel? Effort)> Requests { get; } = [];

    public Task<Result<LlmChatResolvedProvider>> ResolveAsync(
        Guid providerProfileId,
        string model,
        AgentReasoningEffortLevel? thinkingEffort,
        CancellationToken cancellationToken = default)
    {
        Requests.Add((providerProfileId, model, thinkingEffort));
        var capability = new ProviderModelThinkingEffortCapability(
            model,
            AgentThinkingEffortSupportStatus.Supported,
            AgentThinkingEffortCapabilitySource.Defined,
            Enum.GetValues<AgentReasoningEffortLevel>());
        return Task.FromResult(Result<LlmChatResolvedProvider>.Success(new LlmChatResolvedProvider(
            providerProfileId,
            "Primary provider",
            ProviderKind.OpenAi,
            model,
            capability,
            AgentReasoningEffortLevel.Medium)));
    }

    public Task<Result<IReadOnlyList<LlmChatProviderOption>>> ListOptionsAsync(
        CancellationToken cancellationToken = default)
        => Task.FromResult(Result<IReadOnlyList<LlmChatProviderOption>>.Success([]));
}

internal sealed class StubLlmChatConversationEngine : ILlmChatConversationEngine
{
    private readonly Dictionary<LlmChatConversationId, LlmChatConversationEngineState> states = [];
    private readonly Dictionary<LlmChatOperationId, LlmChatConversationTurnEvidence> turnEvidence = [];

    public List<(LlmChatConversationId Id, LlmChatDefinitionRevision Revision, string Title)> Created { get; } = [];

    public Task<LlmChatConversationEngineState> CreateAsync(
        LlmChatConversationId conversationId,
        LlmChatDefinitionRevision definitionRevision,
        string title,
        CancellationToken cancellationToken = default)
    {
        Created.Add((conversationId, definitionRevision, title));
        var now = definitionRevision.CreatedAtUtc.AddMinutes(1);
        var state = new LlmChatConversationEngineState(conversationId, 1, null, now, now);
        states.Add(conversationId, state);
        return Task.FromResult(state);
    }

    public Task<LlmChatConversationEngineState?> TryGetAsync(
        LlmChatConversationId conversationId,
        CancellationToken cancellationToken = default)
        => Task.FromResult(states.GetValueOrDefault(conversationId));

    public void SeedActiveTurn(LlmChatConversationId conversationId, LlmChatOperationId operationId)
        => states[conversationId] = states[conversationId] with { ActiveOperationId = operationId };

    public Task<LlmChatTranscriptPage?> TryGetTranscriptPageAsync(
        LlmChatConversationId conversationId,
        int take,
        LlmChatTranscriptCursor? cursor,
        CancellationToken cancellationToken = default)
    {
        var state = states.GetValueOrDefault(conversationId);
        return Task.FromResult(state is null ? null : new LlmChatTranscriptPage(state, [], null));
    }

    public Task<LlmChatConversationEngineState> RenameAsync(
        LlmChatConversationId conversationId,
        string title,
        long expectedTranscriptRevision,
        CancellationToken cancellationToken = default)
    {
        var current = states[conversationId];
        Assert.Equal(expectedTranscriptRevision, current.TranscriptRevision);
        var renamed = current with
        {
            TranscriptRevision = current.TranscriptRevision + 1,
            UpdatedAtUtc = current.UpdatedAtUtc.AddMinutes(1)
        };
        states[conversationId] = renamed;
        return Task.FromResult(renamed);
    }

    public Task<LlmConversationTurnAdmission> AdmitTurnAsync(
        LlmChatConversationId conversationId,
        LlmChatOperationId operationId,
        LlmChatDefinition definition,
        LlmChatDefinitionRevision definitionRevision,
        string userText,
        long expectedTranscriptRevision,
        CancellationToken cancellationToken = default)
        => throw new NotSupportedException();

    public IAsyncEnumerable<LlmStreamingUpdate> StreamTurnAsync(
        LlmConversationTurnAdmission admission,
        CancellationToken cancellationToken = default)
        => throw new NotSupportedException();

    public Task<LlmConversationTurnAdmission> ResumeAdmittedTurnAsync(
        LlmChatConversationId conversationId,
        LlmChatOperationId operationId,
        LlmChatDefinition definition,
        LlmChatDefinitionRevision definitionRevision,
        CancellationToken cancellationToken = default)
        => throw new NotSupportedException();

    public Task<LlmChatConversationEngineTurnResult> CompleteTurnAsync(
        LlmConversationTurnAdmission admission,
        LlmInvocationResult invocationResult,
        CancellationToken cancellationToken = default)
        => throw new NotSupportedException();

    public Task<LlmChatConversationEngineState> CompensateTurnAsync(
        LlmChatConversationId conversationId,
        LlmChatOperationId operationId,
        CancellationToken cancellationToken = default)
        => throw new NotSupportedException();

    public Task<LlmChatConversationTurnEvidence?> InspectTurnAsync(
        LlmChatConversationId conversationId,
        LlmChatOperationId operationId,
        CancellationToken cancellationToken = default)
        => Task.FromResult(turnEvidence.TryGetValue(operationId, out var evidence)
            ? evidence
            : states.TryGetValue(conversationId, out var state)
                ? new LlmChatConversationTurnEvidence(state, operationId, false, null)
                : null);

    public Task<LlmChatConversationEngineState> AbandonActiveTurnAsync(
        LlmChatConversationId conversationId,
        LlmChatOperationId operationId,
        CancellationToken cancellationToken = default)
        => throw new NotSupportedException();
}

internal sealed class FixedTimeProvider(DateTimeOffset utcNow) : TimeProvider
{
    public override DateTimeOffset GetUtcNow()
        => utcNow;
}

internal sealed class ManualTimeProvider(DateTimeOffset utcNow) : TimeProvider
{
    private DateTimeOffset current = utcNow;

    public override DateTimeOffset GetUtcNow()
        => current;

    public void Advance(TimeSpan duration)
        => current += duration;
}
