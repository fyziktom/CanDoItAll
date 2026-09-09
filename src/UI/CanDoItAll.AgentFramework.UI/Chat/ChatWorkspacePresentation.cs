using System.Collections.Immutable;
using CanDoItAll.Conversations.Components.Presentation;

namespace CanDoItAll.AgentFramework.UI.Chat;

public sealed record ChatSessionHeader(Guid Id, string Title);
public sealed record ChatApprovalPresentation(string ApprovalId, string ToolKind, string ToolName, string Details, string ArgumentSummary);
public sealed record ChatApprovalDecision(string ApprovalId, bool Approved);
public sealed record ChatExecutionStep(Guid Id, string State, string Tone, string Phase, string Time, string Preview);

public enum ChatWorkspaceAction {
    DraftChanged, Send, Approvals, ApproveRemaining, AttachArtifacts, SessionTitleChanged,
    VoiceModeChanged, ToggleRecording, SpeakLatest, ExecutionEntry, ExecutionHistory
}

public sealed record ChatWorkspaceIntent(long Revision, ChatWorkspaceAction Action, string? Text = null,
    bool Enabled = false, Guid? EntryId = null, ImmutableArray<ChatApprovalDecision> Decisions = default);

public sealed record ChatWorkspacePresentation {
    public long Revision { get; init; }
    public ConversationHeaderPresentation Header { get; init; } = new(new("Selected agent", null, "AI", "Selected agent"), []);
    public ChatSessionHeader? Session { get; init; }
    public ImmutableArray<ConversationMessagePresentation> Messages { get; init; } = [];
    public ImmutableArray<ConversationMessagePresentation> TransientMessages { get; init; } = [];
    public ConversationEmptyStatePresentation? EmptyState { get; init; }
    public ImmutableArray<ChatApprovalPresentation> Approvals { get; init; } = [];
    public ImmutableArray<ChatExecutionStep> ExecutionSteps { get; init; } = [];
    public int ExecutionStepCount { get; init; }
    public bool CollapseExecution { get; init; }
    public string ExecutionSummary { get; init; } = "";
    public string ExecutionTone { get; init; } = "neutral";
    public string ExecutionPreview { get; init; } = "";
    public string DraftPrompt { get; init; } = "";
    public int ComposerKey { get; init; }
    public bool IsBusy { get; init; }
    public bool AutoApproval { get; init; }
    public string ComposerGuidance { get; init; } = "";
    public ImmutableArray<string> Attachments { get; init; } = [];
    public bool CanUseVoiceMode { get; init; }
    public bool IsVoiceModeEnabled { get; init; }
    public bool IsVoiceRecording { get; init; }
    public bool IsVoiceTranscribing { get; init; }
    public bool IsVoiceSpeaking { get; init; }
    public string VoiceStatusText { get; init; } = "";
    public string VoiceStatusTone { get; init; } = "neutral";
}

public sealed record ChatAgentHeader(Guid Id, string Name, string? AvatarImageUrl, string Initials);
public enum AgentChatNavigationAction { SwitchAgent, Refresh, NewThread, SelectThread }
public sealed record AgentChatNavigationIntent(long Generation, AgentChatNavigationAction Action, Guid? SessionId = null);
public sealed record AgentChatNavigationPresentation {
    public long Generation { get; init; }
    public bool Focused { get; init; }
    public bool IsLoading { get; init; }
    public bool IsBusy { get; init; }
    public bool HasAgents { get; init; }
    public ChatAgentHeader? Agent { get; init; }
    public string ErrorMessage { get; init; } = "";
    public ImmutableArray<ConversationThreadPresentation> Threads { get; init; } = [];
}
