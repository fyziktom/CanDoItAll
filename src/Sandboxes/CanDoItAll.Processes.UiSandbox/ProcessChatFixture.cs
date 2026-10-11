using System.Collections.Immutable;
using CanDoItAll.AgentFramework.UI.Chat;
using CanDoItAll.Conversations.Components.Presentation;

namespace CanDoItAll.Processes.UiSandbox;

public enum ProcessChatScenario {
    Loading, NoAgents, MissingAgent, Threads, Transcript, Executing,
    Approvals, Attachments, Voice, Failed, Adversarial
}

public sealed class ProcessChatFixture {
    public ProcessChatScenario Scenario { get; private set; }
    public ChatWorkspacePresentation Presentation { get; private set; } = new();
    public string IntentLog { get; private set; } = "No intent";

    public ProcessChatFixture() => SetScenario(ProcessChatScenario.Transcript);

    public void SetScenario(ProcessChatScenario scenario) {
        Scenario = scenario;
        var sessionId = Guid.Parse("51000000-0000-0000-0000-000000000002");
        var now = new DateTimeOffset(2026, 9, 9, 12, 0, 0, TimeSpan.Zero);
        var avatar = new ConversationAvatarPresentation("Research agent", null, "RA", "research");
        var text = scenario == ProcessChatScenario.Adversarial
            ? "<script>window.untrustedAgent = true</script> " + string.Join(' ', Enumerable.Repeat("Readable user and assistant product content.", 120))
            : "The fixture keeps the accepted agent and thread together. No backend execution is started.";
        Presentation = new() {
            Revision = (int)scenario + 1,
            Header = new(avatar, [new("Ready", PresentationTone.Info)]),
            Session = new(sessionId, "Research thread"),
            Messages = [new(new("fixture-user"), ConversationMessageRole.User, "User", PresentationTone.Info,
                "Explain the current work.", ChatPresentationTime.Format(now.AddMinutes(0)), copyValue: "Explain the current work."),
                new(new("fixture-assistant"), ConversationMessageRole.Assistant, "Assistant", PresentationTone.Success,
                    text, ChatPresentationTime.Format(now.AddMinutes(1)), copyValue: text)],
            DraftPrompt = "A safe sample prompt",
            CanUseVoiceMode = true,
            ComposerGuidance = "All actions update sample presentation only."
        };
        switch (scenario) {
            case ProcessChatScenario.Loading:
                Presentation = Presentation with { Session = null, Messages = [], IsBusy = true };
                break;
            case ProcessChatScenario.NoAgents:
                Presentation = Presentation with { Session = null, Messages = [], IsBusy = true };
                break;
            case ProcessChatScenario.MissingAgent:
                break;
            case ProcessChatScenario.Threads:
                Presentation = Presentation with { Messages = [], EmptyState = new("Conversation", "Ready for the first prompt", "Select a thread or compose a sample message.") };
                break;
            case ProcessChatScenario.Executing:
                Presentation = Presentation with {
                    IsBusy = true,
                    TransientMessages = [new(new("pending"), ConversationMessageRole.User, "User", PresentationTone.Info,
                        "Pending sample prompt", ChatPresentationTime.Format(now.AddMinutes(2)), state: ConversationMessageState.Pending)],
                    ExecutionStepCount = 8,
                    ExecutionSteps = [new(Guid.Parse("51000000-0000-0000-0000-000000000003"), "Running", "info", "Reading workspace", ChatPresentationTime.Format(now.AddMinutes(2)), "Sample step with a bounded preview.")]
                };
                break;
            case ProcessChatScenario.Approvals:
                Presentation = Presentation with { Approvals = [new("fixture-read", "function", "read_workspace", "Read the chosen artifact.", "path: notes/input.txt"),
                    new("fixture-write", "function", "write_summary", "Save the selected summary.", "path: notes/output.txt")] };
                break;
            case ProcessChatScenario.Attachments:
                Presentation = Presentation with { Attachments = ["notes/research.md", "images/diagram.png"] };
                break;
            case ProcessChatScenario.Voice:
                Presentation = Presentation with { IsVoiceModeEnabled = true, IsVoiceRecording = true, VoiceStatusText = "Recording", VoiceStatusTone = "danger" };
                break;
            case ProcessChatScenario.Failed:
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
            ChatWorkspaceAction.Send => Presentation with {
                Messages = [.. Presentation.Messages, new(new(Guid.NewGuid().ToString("N")), ConversationMessageRole.User,
                    "User", PresentationTone.Info, Presentation.DraftPrompt, "12:02", copyValue: Presentation.DraftPrompt)],
                DraftPrompt = "", ComposerKey = Presentation.ComposerKey + 1
            },
            _ => Presentation
        };
    }

}
