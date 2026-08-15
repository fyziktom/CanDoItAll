using CanDoItAll.AgentFramework.Llm.Abstractions;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Modules.LlmChats.Application;
using CanDoItAll.Modules.LlmChats.Common;
using CanDoItAll.Modules.LlmChats.Conversations;
using CanDoItAll.Modules.LlmChats.Definitions;
using CanDoItAll.Modules.LlmChats.Ports;
using CanDoItAll.SharedKernel;

namespace CanDoItAll.Tests.Unit;

internal sealed class InMemoryLlmChatDefinitionRepository : ILlmChatDefinitionRepository
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

internal sealed class InlineLlmChatUnitOfWork : ILlmChatUnitOfWork
{
    public Task<T> ExecuteAsync<T>(
        Func<CancellationToken, Task<T>> operation,
        CancellationToken cancellationToken = default)
        => operation(cancellationToken);
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
        var state = new LlmChatConversationEngineState(conversationId, 1, false, now, now);
        states.Add(conversationId, state);
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

    public Task<LlmChatConversationEngineTurnResult> SendAsync(
        LlmChatConversationId conversationId,
        LlmChatOperationId operationId,
        LlmChatDefinition definition,
        LlmChatDefinitionRevision definitionRevision,
        string userText,
        long expectedTranscriptRevision,
        CancellationToken cancellationToken = default)
    {
        var current = states[conversationId];
        Assert.Equal(expectedTranscriptRevision, current.TranscriptRevision);
        var updated = current with
        {
            TranscriptRevision = current.TranscriptRevision + 2,
            UpdatedAtUtc = current.UpdatedAtUtc.AddMinutes(1)
        };
        states[conversationId] = updated;
        var assistantEntryId = Guid.NewGuid();
        turnEvidence[operationId] = new LlmChatConversationTurnEvidence(
            updated,
            operationId,
            false,
            new LlmChatAssistantTurnEvidence(
                assistantEntryId,
                "answer",
                definitionRevision.Model,
                LlmUsage.Zero,
                updated.UpdatedAtUtc));
        return Task.FromResult(new LlmChatConversationEngineTurnResult(
            updated,
            operationId,
            assistantEntryId,
            "answer",
            definitionRevision.Model,
            LlmUsage.Zero));
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

    public Task<LlmInvocationResult> InvokeTurnAsync(
        LlmConversationTurnAdmission admission,
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
