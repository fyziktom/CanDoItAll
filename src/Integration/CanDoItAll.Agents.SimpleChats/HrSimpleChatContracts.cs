using CanDoItAll.AgentFramework.Models;
using System.Collections.Immutable;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Llm.SimpleChats.Application;
using CanDoItAll.AgentFramework.Llm.SimpleChats.Common;
using CanDoItAll.AgentFramework.Llm.SimpleChats.Definitions;

namespace CanDoItAll.Agents.SimpleChats;

public sealed record HrSimpleChatSearchRequest(
    int Take = 25,
    LlmChatDefinitionStatus? Status = null,
    HrSimpleChatDefinitionCursor? Cursor = null,
    string? SearchText = null,
    IReadOnlyList<string>? Tags = null) {
    public LlmChatDefinitionQuery ToOwnerQuery() {
        if (Cursor is not null) {
            ArgumentOutOfRangeException.ThrowIfEqual(Cursor.DefinitionId, Guid.Empty);
        }

        return new(Take, Status, Cursor is { } cursor ? new(cursor.UpdatedAtUtc, new(cursor.DefinitionId)) : null, SearchText, Tags);
    }
}

public sealed record HrSimpleChatDefinitionCursor(Guid DefinitionId, DateTimeOffset UpdatedAtUtc);

public sealed record HrSimpleChatDefinitionPage(
    ImmutableArray<HrSimpleChatDefinitionSummary> Items,
    HrSimpleChatDefinitionCursor? NextCursor);

public sealed record HrSimpleChatDefinitionVersion {
    public HrSimpleChatDefinitionVersion(Guid definitionId, int revision, long concurrencyToken) {
        ArgumentOutOfRangeException.ThrowIfEqual(definitionId, Guid.Empty);
        ArgumentOutOfRangeException.ThrowIfLessThan(revision, 1);
        ArgumentOutOfRangeException.ThrowIfNegative(concurrencyToken);
        DefinitionId = definitionId;
        Revision = revision;
        ConcurrencyToken = concurrencyToken;
    }

    public Guid DefinitionId { get; }
    public int Revision { get; }
    public long ConcurrencyToken { get; }
}

public sealed record HrSimpleChatUpdateRequest(
    HrSimpleChatDefinitionVersion Expected,
    CreateLlmChatDefinitionCommand Definition);

public sealed record HrSimpleChatStatusRequest(
    HrSimpleChatDefinitionVersion Expected,
    LlmChatDefinitionStatus Status);

public sealed record HrSimpleChatReceiptRequest(Guid IntentId);

public sealed record HrSimpleChatDefinitionSummary(
    HrSimpleChatDefinitionVersion Version,
    string Name,
    string Summary,
    string AvatarImageUrl,
    LlmChatDefinitionStatus Status,
    ImmutableArray<string> Tags);

public sealed record HrSimpleChatDefinitionSettings(
    HrSimpleChatDefinitionSummary Summary,
    CreateLlmChatDefinitionCommand Settings);

public sealed record HrSimpleChatOriginalIdentity(
    Guid IntentId,
    Guid DefinitionId,
    int DefinitionRevision,
    long OriginalConcurrencyToken,
    DateTimeOffset CreatedAtUtc);

public sealed record HrSimpleChatCreateResponse(HrSimpleChatOriginalIdentity Receipt, bool WasReplay);

public sealed class HrSimpleChatAdministrationException(string code, string message) : InvalidOperationException(message), IAgentToolFailureEffectEvidence {
    public string Code { get; } = code;
    public string ErrorCode => Code;
    public string SafeMessage => Message;
    public bool IsSafeToExpose => true;
    public bool CanRetryWithCorrectedInput => false;
    public AgentToolEffectState EffectState => AgentToolEffectState.NotCommitted;
}
