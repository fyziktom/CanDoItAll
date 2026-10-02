using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Conversations.Components.Presentation;
using CanDoItAll.Modules.AgentFramework;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;

namespace CanDoItAll.AgentFramework.Editor.UI;

public readonly record struct AgentEditorOrigin(Guid Value);

public sealed record AgentEditorSlotContext(AgentEditorOrigin Origin, AgentEditorModel Draft, AgentEditorSection Section);

public sealed record AgentEditorCoreState(AgentEditorOrigin Origin, EditContext Context) {
    public AgentEditorModel Draft => Context.Model as AgentEditorModel
        ?? throw new InvalidOperationException("The agent editor requires its canonical draft context.");
    public AgentEditorLoadState LoadState { get; init; }
    public AgentEditorSection Section { get; init; }
    public string? LoadError { get; init; }
    public string? CommitWarning { get; init; }
    public string? PendingRefreshMessage { get; init; }
    public bool HasUnconfirmedWrite { get; init; }
    public string? VerificationMessage { get; init; }
    public bool CanReviewVerification { get; init; }
    public Guid? LinkedPartyId { get; init; }
    public bool IsBusy { get; init; }
    public bool IsMutationBlocked { get; init; }
    public bool CanDelete { get; init; }
    public bool IsConfirmingDelete { get; init; }
    public bool IsConfirmingAutoApproval { get; init; }
    public int AutoApprovalInputVersion { get; init; }
    public bool HasIncompatibleThinkingEffort { get; init; }
    public IReadOnlyList<string> Tags { get; init; } = [];
    public IReadOnlyList<string> TagSuggestions { get; init; } = [];
    public bool AreProvidersLoaded { get; init; }
    public string? ProviderLoadError { get; init; }
    public IReadOnlyList<ConversationProviderOption> RuntimeProviders { get; init; } = [];
    public ConversationPresentationKey? RuntimeProviderKey { get; init; }
    public ConversationProviderOption? RuntimeProvider { get; init; }
    public ConversationThinkingEffortPresentation<AgentReasoningEffortLevel?> ThinkingEffort { get; init; } = default!;
    public string? RuntimeParameterPolicy { get; init; }
    public IReadOnlyList<ConversationProviderOption> ImageProviders { get; init; } = [];
    public ConversationPresentationKey? ImageProviderKey { get; init; }
    public ConversationProviderOption? ImageProvider { get; init; }
    public string ImageProviderPolicy { get; init; } = string.Empty;
    public string? ImageWarning { get; init; }
    public EventCallback<AgentEditorCoreIntent> Changed { get; init; }
    public EventCallback<AgentEditorSection> SectionChanged { get; init; }
    public EventCallback<EditContext> Save { get; init; }
    public EventCallback Clear { get; init; }
    public EventCallback Delete { get; init; }
    public EventCallback RetryLoad { get; init; }
    public EventCallback RetryRefresh { get; init; }
    public EventCallback Close { get; init; }
}

public abstract record AgentEditorCoreIntent {
    private AgentEditorCoreIntent() { }
    public sealed record Name(string? Value) : AgentEditorCoreIntent;
    public sealed record Role(string? Value) : AgentEditorCoreIntent;
    public sealed record Summary(string? Value) : AgentEditorCoreIntent;
    public sealed record Instructions(string? Value) : AgentEditorCoreIntent;
    public sealed record Tags(IReadOnlyList<string> Value) : AgentEditorCoreIntent;
    public sealed record RuntimeProvider(ConversationPresentationKey? Value) : AgentEditorCoreIntent;
    public sealed record RuntimeModel(string? Value) : AgentEditorCoreIntent;
    public sealed record ThinkingEffort(AgentReasoningEffortLevel? Value) : AgentEditorCoreIntent;
    public sealed record ToolUse(bool Value) : AgentEditorCoreIntent;
    public sealed record ExternalCallApproval(bool Value) : AgentEditorCoreIntent;
    public sealed record AutoApproval(bool Value) : AgentEditorCoreIntent;
    public sealed record ImageGeneration(bool Value) : AgentEditorCoreIntent;
    public sealed record ImageAssetStorage(bool Value) : AgentEditorCoreIntent;
    public sealed record ImageProvider(ConversationPresentationKey? Value) : AgentEditorCoreIntent;
    public sealed record ImageModel(string? Value) : AgentEditorCoreIntent;
    public sealed record Voice(bool Value) : AgentEditorCoreIntent;
}
