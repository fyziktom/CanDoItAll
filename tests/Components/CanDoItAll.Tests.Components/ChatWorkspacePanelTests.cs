using CanDoItAll.Modules.AgentFramework;
using System.Globalization;
using System.Text.Json;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.UI.Chat;
using Bunit;
using CanDoItAll.AgentFramework.Components;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Components.BaseLib;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.Extensions.DependencyInjection;

namespace CanDoItAll.Tests.Components.AgentFramework;

public sealed class ChatWorkspacePanelTests
{
    [Theory]
    [InlineData("cs-CZ")]
    [InlineData("ar-SA")]
    public void Pending_thread_and_execution_times_are_stable_UTC_in_markup_and_dialogs(string culture) {
        var previous = CultureInfo.CurrentCulture;
        try {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo(culture);
            using var context = CreateContext();
            var host = context.Render<DialogHost>();
            var agentId = Guid.NewGuid();
            var sessionId = Guid.NewGuid();
            var runId = Guid.NewGuid();
            var now = new DateTimeOffset(2026, 9, 9, 12, 34, 56, TimeSpan.FromHours(5));
            var run = CreateRun(agentId, sessionId, runId, ExecutionState.Running, now);
            var cut = context.Render<ChatWorkspacePanel>(p => p.Add(x => x.DraftPrompt, "")
                .Add(x => x.Session, CreateSession(agentId, sessionId, runId, now)).Add(x => x.ActiveRun, run)
                .Add(x => x.PendingUserPrompt, "Pending message").Add(x => x.PendingUserCreatedAtUtc, now)
                .Add(x => x.ExecutionLog, [CreateEntry(agentId, sessionId, runId, now, ExecutionState.Running, "Read", "Reading a safe artifact.")]));
            var timestamp = "2026-09-09 07:34:56 UTC";
            Assert.Contains(timestamp, cut.Markup, StringComparison.Ordinal);
            var pending = cut.Find("[data-testid='conversation-message']").TextContent;
            cut.Render(p => p.Add(x => x.DraftPrompt, "Unrelated rerender"));
            Assert.Equal(pending, cut.Find("[data-testid='conversation-message']").TextContent);
            cut.Find("[data-testid='chat-execution-entry']").Click();
            Assert.Contains(timestamp, host.Markup, StringComparison.Ordinal);
        } finally {
            CultureInfo.CurrentCulture = previous;
        }
    }

    [Fact]
    public void Approval_and_execution_poison_is_sanitized_before_surface_dialog_and_copy() {
        using var context = CreateContext();
        var host = context.Render<DialogHost>();
        var agentId = Guid.NewGuid();
        var sessionId = Guid.NewGuid();
        var runId = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;
        var arguments = JsonSerializer.Serialize(new {
            path = "notes/<b>encoded</b>.txt", content = "test-only-PRIVATE_TOOL_CONTENT_482", apiToken = "test-only-PRIVATE_TOP_482",
            request = new { projectId = "project-42", password = "test-only-PRIVATE_NESTED_482" },
            children = new[] { new { nodeId = "node-7", secret = "test-only-PRIVATE_ARRAY_482" } }
        });
        var run = CreateRun(agentId, sessionId, runId, ExecutionState.WaitingOnTool, now) with {
            PendingApprovals = [new("approval", "call", "workspace_write_file", "function", "MCP: Research server", arguments)]
        };
        var cut = context.Render<ChatWorkspacePanel>(p => p.Add(x => x.DraftPrompt, "").Add(x => x.ActiveRun, run)
            .Add(x => x.Session, CreateSession(agentId, sessionId, runId, now))
            .Add(x => x.ExecutionLog, [CreateEntry(agentId, sessionId, runId, now, ExecutionState.Running,
                "token=test-only-PRIVATE_PHASE_482", "Read artifact. password=test-only-PRIVATE_LOG_482")])
            .Add(x => x.DraftAttachmentPaths, ["  notes\\safe.txt  ", "../test-only-PRIVATE_PATH_482", "C:\\test-only-PRIVATE_PATH_482", "notes//test-only-PRIVATE_PATH_482"]));
        var presentation = cut.FindComponent<AgentChatSurface>().Instance.Presentation;
        Assert.Equal(AgentToolArgumentDisplayFormatter.DescribeArguments(arguments, "workspace_write_file"),
            Assert.Single(presentation.Approvals).ArgumentSummary);
        Assert.Contains("MCP: Research server", cut.Markup, StringComparison.Ordinal);
        Assert.Equal("notes/safe.txt", Assert.Single(presentation.Attachments));
        Assert.DoesNotContain("PRIVATE_", cut.Markup, StringComparison.Ordinal);
        Assert.Empty(cut.FindAll("script"));
        Assert.Contains("&lt;b&gt;", cut.Markup, StringComparison.Ordinal);
        cut.Find("[data-testid='chat-execution-entry']").Click();
        Assert.DoesNotContain("PRIVATE_", host.Markup, StringComparison.Ordinal);
        Assert.Contains("REDACTED", host.Markup, StringComparison.Ordinal);
        var runtime = context.Render<AgentRuntimeDetailsDialog>(p => p.Add(x => x.Run, run)
            .Add(x => x.ExecutionLog, [CreateEntry(agentId, sessionId, runId, now, ExecutionState.Running,
                "token=test-only-PRIVATE_PHASE_482", "password=test-only-PRIVATE_RUNTIME_482")]));
        Assert.DoesNotContain("PRIVATE_", runtime.Markup, StringComparison.Ordinal);
        Assert.Contains("UTC", runtime.Markup, StringComparison.Ordinal);
    }

    [Fact]
    public void Running_execution_log_renders_compact_chat_stream_and_opens_details_dialog()
    {
        using var context = CreateContext();
        var host = context.Render<DialogHost>();
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

        var cut = context.Render<ChatWorkspacePanel>(parameters => parameters
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
        var host = context.Render<DialogHost>();
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

        var cut = context.Render<ChatWorkspacePanel>(parameters => parameters
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

        var cut = context.Render<ChatWorkspacePanel>(parameters => parameters
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

        var cut = context.Render<ChatWorkspacePanel>(parameters => parameters
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
        var withoutUpload = context.Render<ChatWorkspacePanel>(parameters => parameters
            .Add(item => item.DraftPrompt, string.Empty));

        Assert.Empty(withoutUpload.FindAll("[data-testid='chat-image-attachment-button']"));

        var withUpload = context.Render<ChatWorkspacePanel>(parameters => parameters
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

        var cut = context.Render<ChatWorkspacePanel>(parameters => parameters
            .Add(item => item.Session, CreateSession(agentId, sessionId, runId, createdAtUtc))
            .Add(item => item.DraftPrompt, string.Empty)
            .Add(item => item.DraftPromptChanged, value => changedPrompt = value));

        cut.Find("[data-testid='chat-prompt-input']").Input("Analyze this screenshot.");

        Assert.Equal("Analyze this screenshot.", changedPrompt);
    }

    [Fact]
    public void Pending_agent_prompt_uses_the_transient_user_projection_and_preserves_hidden_context()
    {
        using var context = CreateContext();
        const string pendingPrompt = "Workspace context that must remain hidden.\n\nUser request: Summarize the selected file.";

        var cut = context.Render<ChatWorkspacePanel>(parameters => parameters
            .Add(item => item.PendingUserPrompt, pendingPrompt)
            .Add(item => item.DraftPrompt, string.Empty));

        var pendingMessage = cut.Find("[data-testid='conversation-message']");
        Assert.Contains("justify-end", pendingMessage.ClassList);
        Assert.Equal("true", pendingMessage.GetAttribute("aria-busy"));
        var visibleContent = pendingMessage.QuerySelector("p");
        Assert.NotNull(visibleContent);
        Assert.Equal("Summarize the selected file.", visibleContent.TextContent);
        Assert.Contains(
            "Workspace context that must remain hidden.",
            cut.Find("[data-testid='chat-pending-hidden-context']").TextContent);
    }

    [Theory]
    [InlineData(PromptGalleryToolPolicy.PromptGalleryCatalogSearch)]
    [InlineData(PromptGalleryToolPolicy.PromptGalleryDraftCreate)]
    [InlineData(PromptGalleryToolPolicy.PromptGalleryDraftUpdate)]
    [InlineData(PromptGalleryToolPolicy.PromptGalleryVersionCreate)]
    public void Rendered_prompt_approval_uses_the_host_catalog_and_preserves_safe_fields(string toolName) {
        using var context = CreateContext();
        var policies = new AgentToolPolicyCatalog(PromptGalleryToolPolicy.Capabilities);
        context.Services.AddSingleton(policies);
        var arguments = JsonSerializer.Serialize(new {
            request = new { promptArtifactId = "item-42", includeArchived = true, content = "test-only-private-prompt-body" }
        });
        var expected = AgentToolArgumentDisplayFormatter.DescribeArguments(arguments, toolName, policies);
        Assert.NotEqual(AgentToolArgumentDisplayFormatter.DescribeArguments(arguments, toolName), expected);
        var approval = new PendingToolApprovalRecord("approval", "call", toolName, "function", "Review Prompt operation", arguments);
        var run = CreateRun(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), ExecutionState.WaitingOnTool, DateTimeOffset.UtcNow) with {
            PendingApprovals = [approval]
        };

        var cut = context.Render<ChatWorkspacePanel>(parameters => parameters
            .Add(component => component.DraftPrompt, string.Empty)
            .Add(component => component.Session, CreateSession(run.AgentId, run.ChatSessionId!.Value, run.Id, run.CreatedAtUtc))
            .Add(component => component.ActiveRun, run));

        var presentation = cut.FindComponent<AgentChatSurface>().Instance.Presentation;
        Assert.Equal(expected, Assert.Single(presentation.Approvals).ArgumentSummary);
        Assert.Contains("item-42", cut.Markup, StringComparison.Ordinal);
        Assert.DoesNotContain("test-only-private-prompt-body", cut.Markup, StringComparison.Ordinal);
        Assert.Contains("redacted", cut.Markup, StringComparison.OrdinalIgnoreCase);
    }

    private static BunitContext CreateContext()
    {
        var context = new BunitContext();
        context.JSInterop.Mode = JSRuntimeMode.Loose;
        context.Services.AddCanDoItAllBaseLib();
        context.Services.AddSingleton<AgentToolPolicyCatalog>();
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
