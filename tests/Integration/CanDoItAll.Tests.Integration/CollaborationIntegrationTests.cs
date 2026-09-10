using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.Modules.Collaboration;
using CanDoItAll.Tests.Support;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.Extensions.DependencyInjection;

namespace CanDoItAll.Tests.Integration.Runtime;

public sealed class CollaborationIntegrationTests
{
    [Fact]
    public async Task Runtime_model_retains_complete_schema_mappings_without_foreign_entities() {
        await using var application = await TestApplication.CreateAsync();
        var ownerFactory = application.Services.GetRequiredService<IDbContextFactory<CollaborationDbContext>>();
        var schemaFactory = application.Services.GetRequiredService<IDbContextFactory<AppDbContext>>();
        await using var owner = await ownerFactory.CreateDbContextAsync();
        await using var schema = await schemaFactory.CreateDbContextAsync();
        var ownerModel = owner.GetService<IDesignTimeModel>().Model;
        var schemaModel = schema.GetService<IDesignTimeModel>().Model;
        Type[] ownedTypes = [typeof(CollaborationThreadRecord), typeof(CollaborationParticipantRecord),
            typeof(CollaborationMessageRecord), typeof(CollaborationInboxItemRecord)];

        Assert.Equal(ownedTypes.OrderBy(type => type.Name), ownerModel.GetEntityTypes()
            .Select(entity => entity.ClrType).OrderBy(type => type.Name));
        foreach (var entity in ownerModel.GetEntityTypes()) {
            var completeEntity = Assert.IsAssignableFrom<IEntityType>(schemaModel.FindEntityType(entity.ClrType));
            Assert.Equal(completeEntity.ToDebugString(MetadataDebugStringOptions.LongDefault),
                entity.ToDebugString(MetadataDebugStringOptions.LongDefault));
        }
    }

    [Fact]
    public async Task Canonical_records_survive_restart_and_owner_writes_remain_profile_isolated() {
        await using var environment = CanDoItAllTestEnvironment.Create("collaboration-owner-restart");
        var profile = environment.CreatePostgreSqlProfile("original");
        var otherProfile = environment.CreatePostgreSqlProfile("other");
        var threadId = Guid.NewGuid();
        var originalToken = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;
        var options = new TestHarnessOptions { TestEnvironment = environment, ActiveProfile = profile };
        await using (var beforeRestart = await TestApplication.CreateAsync(options)) {
            var factory = beforeRestart.Services.GetRequiredService<IDbContextFactory<AppDbContext>>();
            await using var schema = await factory.CreateDbContextAsync();
            schema.AddRange(
                new CollaborationThreadRecord {
                    Id = threadId, Subject = "Saved before owner cutover", ConcurrencyToken = originalToken,
                    CreatedAtUtc = now, LastActivityAtUtc = now
                },
                new CollaborationParticipantRecord {
                    ThreadId = threadId, ParticipantKey = "user:owner-test", DisplayName = "Owner test", AddedAtUtc = now
                },
                new CollaborationMessageRecord {
                    ThreadId = threadId, AuthorKey = "user:owner-test", AuthorName = "Owner test",
                    Body = "Historical message", CreatedAtUtc = now
                },
                new CollaborationInboxItemRecord {
                    ThreadId = threadId, Title = "Saved before owner cutover", PreviewText = "Historical message",
                    Route = $"/collaboration?threadId={threadId}", CreatedAtUtc = now, UpdatedAtUtc = now
                });
            await schema.SaveChangesAsync();
        }

        await using var afterRestart = await TestApplication.CreateAsync(options);
        await using var scope = afterRestart.Services.CreateAsyncScope();
        var service = scope.ServiceProvider.GetRequiredService<CollaborationService>();
        var workspace = await service.GetWorkspaceAsync(threadId);
        Assert.Equal("Historical message", Assert.Single(workspace.SelectedThread!.Messages).Body);
        var result = await service.AppendMessageAsync(new CollaborationMessageWriteRequest(
            threadId, "user:owner-test", "Owner test", CollaborationMessageAuthorKind.User,
            "Written through owner", CollaborationMessageKind.Standard));
        Assert.True(result.IsSuccess);

        var factoryAfterRestart = afterRestart.Services.GetRequiredService<IDbContextFactory<CollaborationDbContext>>();
        await using var readback = await factoryAfterRestart.CreateDbContextAsync();
        Assert.NotEqual(originalToken, (await readback.Set<CollaborationThreadRecord>().SingleAsync()).ConcurrencyToken);
        Assert.Equal(2, await readback.Set<CollaborationMessageRecord>().CountAsync());
        Assert.Equal(1, await readback.Set<CollaborationParticipantRecord>().CountAsync());

        await using var other = await TestApplication.CreateAsync(new TestHarnessOptions {
            TestEnvironment = environment, ActiveProfile = otherProfile
        });
        await using var otherScope = other.Services.CreateAsyncScope();
        var otherWorkspace = await otherScope.ServiceProvider.GetRequiredService<CollaborationService>().GetWorkspaceAsync(threadId);
        Assert.Null(otherWorkspace.SelectedThread);
        Assert.Empty(otherWorkspace.Threads);
        Assert.Equal(2, (await service.GetWorkspaceAsync(threadId)).SelectedThread!.Messages.Count);
    }

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
