using CanDoItAll.AgentFramework.Llm.SimpleChats.UI;
using CanDoItAll.Conversations.Components.Presentation;

namespace CanDoItAll.AgentFramework.UiSandbox;

public enum ConversationWorkspaceScenario { Loading, Denied, Empty, ListReady, Selected, TranscriptLoading, LongTranscript, Streaming, Recovery, Focused }

public sealed class ConversationWorkspaceSandboxFixture {
    public static readonly Guid ConversationId = Guid.Parse("33000000-0000-0000-0000-000000000001");
    public ConversationWorkspaceScenario Scenario { get; private set; } = ConversationWorkspaceScenario.Selected;
    public ConversationWorkspacePresentation Presentation { get; private set; } = Create(ConversationWorkspaceScenario.Selected);
    public string IntentLog { get; private set; } = "No intent";
    public void SetScenario(ConversationWorkspaceScenario scenario) {
        Scenario = scenario;
        Presentation = Create(scenario);
    }
    public void Apply(ConversationWorkspaceIntent intent) {
        IntentLog = intent.Action.ToString();
        if (intent.Action == ConversationWorkspaceAction.DraftChanged) {
            Presentation = Presentation with { DraftPrompt = intent.Text ?? "", IsSendDisabled = string.IsNullOrWhiteSpace(intent.Text) };
        } else if (intent.Action == ConversationWorkspaceAction.Send) {
            Presentation = Create(ConversationWorkspaceScenario.Streaming) with { ComposerKey = Presentation.ComposerKey + 1 };
        } else if (intent.Action == ConversationWorkspaceAction.Reconcile) {
            Presentation = Presentation with { CanAbandon = true };
        } else if (intent.Action == ConversationWorkspaceAction.Abandon) {
            Presentation = Create(ConversationWorkspaceScenario.Selected);
        }
    }
    public static ConversationWorkspacePresentation Create(ConversationWorkspaceScenario scenario) {
        var title = scenario == ConversationWorkspaceScenario.Focused ? "<script id='conversation-injected'>unsafe()</script> " + new string('界', 120) : "Synthetic research chat";
        var presentation = new ConversationWorkspacePresentation {
            Generation = 1, IsAuthorizing = false, CanRead = true, CanManage = true,
            Selected = new(ConversationId, title, "Synthetic research assistant", new(new("Assistant", null, "A", "sample"), [new("Revision 3", PresentationTone.Info)])),
            Threads = [new(ConversationWorkspaceMapping.ToKey(ConversationId), title, DateTimeOffset.UnixEpoch, "12:00", "Revision 3", "Synthetic transcript", isSelected: true)],
            Messages = [Message("user", "Summarize the sample.", ConversationMessageRole.User), Message("assistant", "A synthetic answer with no provider calls.", ConversationMessageRole.Assistant)],
            DraftPrompt = "Sample draft", IsSendDisabled = false
        };
        return scenario switch {
            ConversationWorkspaceScenario.Loading => presentation with { IsAuthorizing = true, IsLoading = true, Selected = null, Messages = [] },
            ConversationWorkspaceScenario.Denied => presentation with { CanRead = false, CanManage = false, Selected = null, Messages = [] },
            ConversationWorkspaceScenario.Empty => presentation with { Selected = null, Threads = [], Messages = [], EmptyState = new("Conversations", "Choose a conversation", "Start a synthetic chat."), IsSendDisabled = true },
            ConversationWorkspaceScenario.ListReady => presentation with { Selected = null, Messages = [], IsSendDisabled = true },
            ConversationWorkspaceScenario.Selected => presentation,
            ConversationWorkspaceScenario.TranscriptLoading => presentation with { IsLoading = true, Messages = [], IsSendDisabled = true },
            ConversationWorkspaceScenario.LongTranscript => presentation with { HasMoreConversations = true, HasMoreMessages = true, Messages = [.. Enumerable.Range(1, 50).Select(index => Message($"message-{index}", $"Synthetic transcript entry {index}. " + new string('W', 100), ConversationMessageRole.Assistant))] },
            ConversationWorkspaceScenario.Streaming => presentation with { DraftPrompt = "", IsSendDisabled = true, CanCancel = true, OperationStatus = "Running", TransientMessages = [Message("pending", "Next question", ConversationMessageRole.User, ConversationMessageState.Pending), Message("streaming", "A streamed synthetic answer…", ConversationMessageRole.Assistant, ConversationMessageState.Streaming)] },
            ConversationWorkspaceScenario.Recovery => presentation with { CanReconcile = true, OperationStatus = "Recovery required", IsSendDisabled = true },
            ConversationWorkspaceScenario.Focused => presentation with { Focused = true, Messages = [Message("long-answer", string.Join('\n', Enumerable.Repeat("Long synthetic transcript with <encoded> content.", 50)), ConversationMessageRole.Assistant)] },
            _ => throw new ArgumentOutOfRangeException(nameof(scenario))
        };
    }
    private static ConversationMessagePresentation Message(string key, string content, ConversationMessageRole role, ConversationMessageState state = ConversationMessageState.Normal)
        => new(new(key), role, role.ToString(), PresentationTone.Info, content, "12:00", state: state);
}
