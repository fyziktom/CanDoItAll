using CanDoItAll.Modules.Collaboration;
using Microsoft.Extensions.DependencyInjection;

namespace CanDoItAll.Tests.Integration;

public sealed class CollaborationIntegrationTests
{
    [Fact]
    public async Task CreateThreadAsync_persists_inbox_thread_messages_and_unread_state()
    {
        await using var application = await TestApplication.CreateAsync();
        await using var scope = application.Services.CreateAsyncScope();
        var collaborationService = scope.ServiceProvider.GetRequiredService<CollaborationService>();
        var processRunId = Guid.NewGuid();

        var createResult = await collaborationService.CreateThreadAsync(
            new CollaborationThreadCreateRequest(
                "Launch readiness review",
                CollaborationContextKind.ProcessRun,
                processRunId,
                ProjectId: null,
                "Process run / release candidate",
                "/processes",
                CollaborationInboxItemKind.Escalation,
                "user:release-manager",
                "Release manager",
                CollaborationParticipantKind.User,
                "A human approval is required before the release run can continue.",
                CollaborationMessageKind.Escalation));

        Assert.True(createResult.IsSuccess, string.Join(" | ", createResult.Errors.Select(error => error.Message)));

        var workspace = await collaborationService.GetWorkspaceAsync(createResult.Value);

        Assert.Equal(1, workspace.ShellState.UnreadCount);
        Assert.Single(workspace.InboxItems);
        Assert.Single(workspace.Escalations);
        Assert.Single(workspace.Threads);

        Assert.NotNull(workspace.SelectedThread);
        var selectedThread = workspace.SelectedThread!;
        Assert.Equal(CollaborationContextKind.ProcessRun, selectedThread.ContextKind);
        Assert.Equal(processRunId, selectedThread.ContextId);
        Assert.Equal("Process run / release candidate", selectedThread.ContextLabel);
        Assert.Equal("/processes", selectedThread.ContextRoute);
        Assert.True(selectedThread.IsUnread);
        Assert.Equal(1, selectedThread.UnreadCount);
        Assert.Single(selectedThread.Participants);
        Assert.Single(selectedThread.Messages);
        Assert.Equal("Release manager", selectedThread.Participants[0].DisplayName);
        Assert.Equal("A human approval is required before the release run can continue.", selectedThread.Messages[0].Body);

        var markReadResult = await collaborationService.MarkThreadAsReadAsync(selectedThread.ThreadId);

        Assert.True(markReadResult.IsSuccess, string.Join(" | ", markReadResult.Errors.Select(error => error.Message)));

        workspace = await collaborationService.GetWorkspaceAsync(selectedThread.ThreadId);
        Assert.NotNull(workspace.SelectedThread);
        selectedThread = workspace.SelectedThread!;
        Assert.Equal(0, workspace.ShellState.UnreadCount);
        Assert.False(selectedThread.IsUnread);
        Assert.Equal(0, selectedThread.UnreadCount);
    }

    [Fact]
    public async Task RecordAutomationSignalAsync_projects_automation_signal_into_collaboration_store()
    {
        await using var application = await TestApplication.CreateAsync();
        await using var scope = application.Services.CreateAsyncScope();
        var collaborationService = scope.ServiceProvider.GetRequiredService<CollaborationService>();

        var createResult = await collaborationService.RecordAutomationSignalAsync(
            new CollaborationAutomationSignalRequest(
                "automation-reminder-001",
                "Automation reminder worker",
                "Follow-up required on overdue account check-in",
                "The overdue account interaction needs a manual owner before the reminder can be dismissed.",
                CollaborationInboxItemKind.Notification,
                ContextLabel: "Scheduler follow-up",
                ContextRoute: "/scheduler"));

        Assert.True(createResult.IsSuccess, string.Join(" | ", createResult.Errors.Select(error => error.Message)));

        var workspace = await collaborationService.GetWorkspaceAsync(createResult.Value);
        Assert.NotNull(workspace.SelectedThread);
        var selectedThread = workspace.SelectedThread!;

        Assert.Equal(CollaborationContextKind.AutomationSignal, selectedThread.ContextKind);
        Assert.Equal("Scheduler follow-up", selectedThread.ContextLabel);
        Assert.Equal("/scheduler", selectedThread.ContextRoute);
        Assert.Equal(CollaborationInboxItemKind.Notification, selectedThread.ItemKind);
        Assert.Contains(selectedThread.Participants, item => item.ParticipantKind == CollaborationParticipantKind.System);
        Assert.Contains(selectedThread.Messages, item => item.MessageKind == CollaborationMessageKind.System);
    }
}
