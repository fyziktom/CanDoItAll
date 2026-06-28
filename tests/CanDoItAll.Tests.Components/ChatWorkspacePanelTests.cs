using Bunit;
using CanDoItAll.AgentFramework.Components;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Components.BaseLib;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.Extensions.DependencyInjection;

namespace CanDoItAll.Tests.Components;

public sealed class ChatWorkspacePanelTests
{
    [Fact]
    public void Running_execution_log_renders_compact_chat_stream_and_opens_details_dialog()
    {
        using var context = CreateContext();
        var host = context.RenderComponent<DialogHost>();
        var agentId = Guid.NewGuid();
        var sessionId = Guid.NewGuid();
        var runId = Guid.NewGuid();
        var startedAtUtc = new DateTimeOffset(2026, 4, 28, 10, 0, 0, TimeSpan.Zero);
        var longDetail = "Calling local tool with scoped workspace input. The runtime is collecting provider evidence and command output before it sends the next message. FULL_DETAIL_ONLY_IN_DIALOG";
        var run = CreateRun(agentId, sessionId, runId, ExecutionState.Running, startedAtUtc);
        var entries = new[]
        {
            CreateEntry(agentId, sessionId, runId, startedAtUtc.AddSeconds(1), ExecutionState.Preparing, "Preparing", "Opening the runtime session."),
            CreateEntry(agentId, sessionId, runId, startedAtUtc.AddSeconds(2), ExecutionState.Running, "Tool call", longDetail)
        };

        var cut = context.RenderComponent<ChatWorkspacePanel>(parameters => parameters
            .Add(item => item.Session, CreateSession(agentId, sessionId, runId, startedAtUtc))
            .Add(item => item.ActiveRun, run)
            .Add(item => item.ExecutionLog, entries)
            .Add(item => item.DraftPrompt, string.Empty));

        var stream = cut.Find("[data-testid='chat-execution-stream']");
        Assert.Contains("Tool call", stream.TextContent);
        Assert.Contains("...", stream.TextContent);
        Assert.DoesNotContain("FULL_DETAIL_ONLY_IN_DIALOG", stream.TextContent);

        cut.FindAll("[data-testid='chat-execution-entry']")[1].Click();

        host.WaitForAssertion(() =>
        {
            Assert.Contains("agent-execution-log-dialog-body", host.Markup);
            Assert.Contains("FULL_DETAIL_ONLY_IN_DIALOG", host.Markup);
        });
    }

    [Fact]
    public void Completed_execution_log_collapses_chat_stream_and_opens_history_dialog()
    {
        using var context = CreateContext();
        var host = context.RenderComponent<DialogHost>();
        var agentId = Guid.NewGuid();
        var sessionId = Guid.NewGuid();
        var runId = Guid.NewGuid();
        var startedAtUtc = new DateTimeOffset(2026, 4, 28, 10, 0, 0, TimeSpan.Zero);
        var completedAtUtc = startedAtUtc.AddMinutes(4).AddSeconds(11);
        var run = CreateRun(agentId, sessionId, runId, ExecutionState.Completed, startedAtUtc, completedAtUtc);
        var entries = new[]
        {
            CreateEntry(agentId, sessionId, runId, startedAtUtc.AddSeconds(1), ExecutionState.Preparing, "Preparing", "Created the runtime session."),
            CreateEntry(agentId, sessionId, runId, startedAtUtc.AddMinutes(1), ExecutionState.Running, "Tool call", "Called the workspace tool."),
            CreateEntry(agentId, sessionId, runId, completedAtUtc, ExecutionState.Completed, "Completed", "Stored the assistant response.")
        };

        var cut = context.RenderComponent<ChatWorkspacePanel>(parameters => parameters
            .Add(item => item.Session, CreateSession(agentId, sessionId, runId, startedAtUtc))
            .Add(item => item.ActiveRun, run)
            .Add(item => item.ExecutionLog, entries)
            .Add(item => item.DraftPrompt, string.Empty));

        Assert.Empty(cut.FindAll("[data-testid='chat-execution-entry']"));
        var summary = cut.Find("[data-testid='chat-execution-summary']");
        Assert.Contains("Worked for 4m 11s", summary.TextContent);
        Assert.Contains("3 steps", summary.TextContent);

        summary.Click();

        host.WaitForAssertion(() =>
        {
            Assert.Contains("Created the runtime session.", host.Markup);
            Assert.Contains("Called the workspace tool.", host.Markup);
            Assert.Contains("Stored the assistant response.", host.Markup);
        });
    }

    [Fact]
    public void Thread_title_renders_as_editable_and_raises_title_change()
    {
        using var context = CreateContext();
        var agentId = Guid.NewGuid();
        var sessionId = Guid.NewGuid();
        var runId = Guid.NewGuid();
        var createdAtUtc = new DateTimeOffset(2026, 4, 28, 10, 0, 0, TimeSpan.Zero);
        string? updatedTitle = null;

        var cut = context.RenderComponent<ChatWorkspacePanel>(parameters => parameters
            .Add(item => item.Session, CreateSession(agentId, sessionId, runId, createdAtUtc))
            .Add(item => item.DraftPrompt, string.Empty)
            .Add(item => item.SessionTitleChanged, title => updatedTitle = title));

        cut.Find("button[aria-label='Edit Title']").Click();
        cut.Find("input[aria-label='Editing Title']").Change("Renamed runtime thread");
        cut.Find("button[aria-label='Save Title']").Click();

        Assert.Equal("Renamed runtime thread", updatedTitle);
    }

    [Fact]
    public void Voice_controls_render_enabled_audio_state()
    {
        using var context = CreateContext();
        var agentId = Guid.NewGuid();
        var sessionId = Guid.NewGuid();
        var runId = Guid.NewGuid();
        var createdAtUtc = new DateTimeOffset(2026, 4, 28, 10, 0, 0, TimeSpan.Zero);

        var cut = context.RenderComponent<ChatWorkspacePanel>(parameters => parameters
            .Add(item => item.Session, CreateSession(agentId, sessionId, runId, createdAtUtc))
            .Add(item => item.DraftPrompt, string.Empty)
            .Add(item => item.CanUseVoiceMode, true)
            .Add(item => item.IsVoiceModeEnabled, true)
            .Add(item => item.VoiceStatusText, "Audio on")
            .Add(item => item.VoiceStatusTone, "primary"));

        Assert.NotEmpty(cut.FindAll("[data-testid='chat-voice-mode-button']"));
        Assert.NotEmpty(cut.FindAll("[data-testid='chat-voice-record-button']"));
        Assert.NotEmpty(cut.FindAll("[data-testid='chat-voice-speak-button']"));
        Assert.Contains("Audio mode", cut.Markup);
        Assert.Contains("Audio on", cut.Markup);
    }

    [Fact]
    public void Image_attachment_upload_control_renders_only_when_upload_handler_is_bound()
    {
        using var context = CreateContext();
        var withoutUpload = context.RenderComponent<ChatWorkspacePanel>(parameters => parameters
            .Add(item => item.DraftPrompt, string.Empty));

        Assert.Empty(withoutUpload.FindAll("[data-testid='chat-image-attachment-button']"));

        var withUpload = context.RenderComponent<ChatWorkspacePanel>(parameters => parameters
            .Add(item => item.DraftPrompt, string.Empty)
            .Add(
                item => item.AttachmentFilesSelected,
                EventCallback.Factory.Create<InputFileChangeEventArgs>(
                    this,
                    _ => Task.CompletedTask)));

        Assert.NotEmpty(withUpload.FindAll("[data-testid='chat-image-attachment-button']"));
        Assert.NotEmpty(withUpload.FindAll("[data-testid='chat-image-attachment-input']"));
    }

    [Fact]
    public void Prompt_composer_raises_draft_prompt_changed_on_input()
    {
        using var context = CreateContext();
        var agentId = Guid.NewGuid();
        var sessionId = Guid.NewGuid();
        var runId = Guid.NewGuid();
        var createdAtUtc = new DateTimeOffset(2026, 4, 28, 10, 0, 0, TimeSpan.Zero);
        var changedPrompt = string.Empty;

        var cut = context.RenderComponent<ChatWorkspacePanel>(parameters => parameters
            .Add(item => item.Session, CreateSession(agentId, sessionId, runId, createdAtUtc))
            .Add(item => item.DraftPrompt, string.Empty)
            .Add(item => item.DraftPromptChanged, value => changedPrompt = value));

        cut.Find("[data-testid='chat-prompt-input']").Input("Analyze this screenshot.");

        Assert.Equal("Analyze this screenshot.", changedPrompt);
    }

    private static TestContext CreateContext()
    {
        var context = new TestContext();
        context.Services.AddCanDoItAllBaseLib();
        return context;
    }

    private static ChatSessionRecord CreateSession(
        Guid agentId,
        Guid sessionId,
        Guid runId,
        DateTimeOffset createdAtUtc)
    {
        return new ChatSessionRecord(
            Id: sessionId,
            AgentId: agentId,
            Title: "Runtime thread",
            CreatedAtUtc: createdAtUtc,
            UpdatedAtUtc: createdAtUtc,
            Messages: [],
            LatestExecutionRunId: runId);
    }

    private static ExecutionRunRecord CreateRun(
        Guid agentId,
        Guid sessionId,
        Guid runId,
        ExecutionState state,
        DateTimeOffset startedAtUtc,
        DateTimeOffset? completedAtUtc = null)
    {
        return new ExecutionRunRecord(
            Id: runId,
            AgentId: agentId,
            ChatSessionId: sessionId,
            Title: "Runtime thread",
            SourceKind: "chat",
            SourceId: sessionId.ToString(),
            CorrelationId: runId.ToString(),
            CausationId: string.Empty,
            RequestedBy: "test",
            RequestedByKind: "test",
            MetadataJson: "{}",
            InputSummary: "Test prompt",
            ResultSummary: string.Empty,
            ProviderName: "TestProvider",
            Model: "test-model",
            State: state,
            Outcome: state == ExecutionState.Completed ? RunOutcome.Succeeded : null,
            CreatedAtUtc: startedAtUtc,
            UpdatedAtUtc: completedAtUtc ?? startedAtUtc,
            StartedAtUtc: startedAtUtc,
            CompletedAtUtc: completedAtUtc,
            RuntimeSessionKey: string.Empty,
            SerializedSessionStateJson: null,
            PendingApprovals: []);
    }

    private static ExecutionLogEntry CreateEntry(
        Guid agentId,
        Guid sessionId,
        Guid runId,
        DateTimeOffset createdAtUtc,
        ExecutionState state,
        string phase,
        string message)
    {
        return new ExecutionLogEntry(
            Id: Guid.NewGuid(),
            AgentId: agentId,
            ChatSessionId: sessionId,
            CreatedAtUtc: createdAtUtc,
            State: state,
            Phase: phase,
            Message: message)
        {
            ExecutionRunId = runId
        };
    }
}
