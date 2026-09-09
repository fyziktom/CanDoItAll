using System.Collections.Immutable;
using CanDoItAll.AgentFramework.UI.Chat;
using CanDoItAll.Conversations.Components.Presentation;

namespace CanDoItAll.AgentFramework.UiSandbox;

public enum AgentChatScenario {
    Loading, NoAgents, MissingAgent, Threads, Transcript, Executing,
    Approvals, Attachments, Voice, Focused, Failed, Adversarial
}

public sealed class AgentChatSandboxFixture {
    public AgentChatScenario Scenario { get; private set; }
    public ChatWorkspacePresentation Presentation { get; private set; } = new();
    public AgentChatNavigationPresentation Navigation { get; private set; } = new();
    public string IntentLog { get; private set; } = "No intent";

    public AgentChatSandboxFixture() => SetScenario(AgentChatScenario.Transcript);

    public void SetScenario(AgentChatScenario scenario) {
        Scenario = scenario;
        var agentId = Guid.Parse("51000000-0000-0000-0000-000000000001");
        var sessionId = Guid.Parse("51000000-0000-0000-0000-000000000002");
        var now = new DateTimeOffset(2026, 9, 9, 12, 0, 0, TimeSpan.Zero);
        var avatar = new ConversationAvatarPresentation("Research agent", null, "RA", "research");
        var text = scenario == AgentChatScenario.Adversarial
            ? "<script>window.untrustedAgent = true</script> " + string.Join(' ', Enumerable.Repeat("Readable user and assistant product content.", 120))
            : "The fixture keeps the accepted agent and thread together. No backend execution is started.";
        Presentation = new() {
            Revision = (int)scenario + 1,
            Header = new(avatar, [new("Ready", PresentationTone.Info)]),
            Session = new(sessionId, "Research thread"),
            Messages = [new(new("fixture-user"), ConversationMessageRole.User, "User", PresentationTone.Info,
                "Explain the current work.", "12:00", copyValue: "Explain the current work."),
                new(new("fixture-assistant"), ConversationMessageRole.Assistant, "Assistant", PresentationTone.Success,
                    text, "12:01", copyValue: text)],
            DraftPrompt = "A safe sample prompt",
            CanUseVoiceMode = true,
            ComposerGuidance = "All actions update sample presentation only."
        };
        Navigation = new() {
            Generation = Presentation.Revision,
            HasAgents = true,
            Agent = new(agentId, "Research agent", null, "RA"),
            Threads = [new(new(sessionId.ToString("N")), "Research thread", now, "12:00", "2 messages", "Accepted transcript", isSelected: true)]
        };
        switch (scenario) {
            case AgentChatScenario.Loading:
                Presentation = Presentation with { Session = null, Messages = [], IsBusy = true };
                Navigation = Navigation with { IsLoading = true, Agent = null, Threads = [] };
                break;
            case AgentChatScenario.NoAgents:
                Presentation = Presentation with { Session = null, Messages = [], IsBusy = true };
                Navigation = Navigation with { HasAgents = false, Agent = null, Threads = [] };
                break;
            case AgentChatScenario.MissingAgent:
                Navigation = Navigation with { Agent = null, Threads = [], ErrorMessage = "The requested agent is not available." };
                break;
            case AgentChatScenario.Threads:
                Presentation = Presentation with { Messages = [], EmptyState = new("Conversation", "Ready for the first prompt", "Select a thread or compose a sample message.") };
                break;
            case AgentChatScenario.Executing:
                Presentation = Presentation with {
                    IsBusy = true,
                    TransientMessages = [new(new("pending"), ConversationMessageRole.User, "User", PresentationTone.Info,
                        "Pending sample prompt", "12:02", state: ConversationMessageState.Pending)],
                    ExecutionStepCount = 8,
                    ExecutionSteps = [new(Guid.Parse("51000000-0000-0000-0000-000000000003"), "Running", "info", "Reading workspace", "12:02", "Sample step with a bounded preview.")]
                };
                break;
            case AgentChatScenario.Approvals:
                Presentation = Presentation with { Approvals = [new("fixture-read", "function", "read_workspace", "Read the chosen artifact.", "path: notes/input.txt"),
                    new("fixture-write", "function", "write_summary", "Save the selected summary.", "path: notes/output.txt")] };
                break;
            case AgentChatScenario.Attachments:
                Presentation = Presentation with { Attachments = ["notes/research.md", "images/diagram.png"] };
                break;
            case AgentChatScenario.Voice:
                Presentation = Presentation with { IsVoiceModeEnabled = true, IsVoiceRecording = true, VoiceStatusText = "Recording", VoiceStatusTone = "danger" };
                break;
            case AgentChatScenario.Focused:
                Navigation = Navigation with { Focused = true };
                break;
            case AgentChatScenario.Failed:
                Presentation = Presentation with { Header = new(avatar, [new("Failed", PresentationTone.Danger), new("Refresh needed", PresentationTone.Warning)]),
                    ExecutionStepCount = 2, CollapseExecution = true, ExecutionSummary = "Worked for 2s", ExecutionTone = "danger",
                    ExecutionPreview = "The failed run was persisted; refresh to read its latest state." };
                break;
        }
    }

    public void Apply(ChatWorkspaceIntent intent) {
        if (intent.Revision != Presentation.Revision) {
            return;
        }
        IntentLog = intent.Action.ToString();
        Presentation = intent.Action switch {
            ChatWorkspaceAction.DraftChanged => Presentation with { DraftPrompt = intent.Text ?? "" },
            ChatWorkspaceAction.SessionTitleChanged when Presentation.Session is { } session => Presentation with { Session = session with { Title = intent.Text ?? "" } },
            ChatWorkspaceAction.VoiceModeChanged => Presentation with { IsVoiceModeEnabled = intent.Enabled, VoiceStatusText = intent.Enabled ? "Audio on" : "Audio off" },
            ChatWorkspaceAction.ToggleRecording => Presentation with { IsVoiceRecording = !Presentation.IsVoiceRecording },
            ChatWorkspaceAction.SpeakLatest => Presentation with { IsVoiceSpeaking = !Presentation.IsVoiceSpeaking, VoiceStatusText = "Sample speaking state" },
            ChatWorkspaceAction.Approvals or ChatWorkspaceAction.ApproveRemaining => Presentation with { Approvals = [] },
            ChatWorkspaceAction.Send => Presentation with { DraftPrompt = "", ComposerKey = Presentation.ComposerKey + 1 },
            _ => Presentation
        };
    }

    public void Navigate(AgentChatNavigationIntent intent) {
        if (intent.Generation == Navigation.Generation) {
            IntentLog = intent.Action.ToString();
        }
    }
}
