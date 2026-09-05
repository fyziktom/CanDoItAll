using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Modules.AgentFramework;
using CanDoItAll.Modules.CrmHr;
using CanDoItAll.SharedKernel;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace CanDoItAll.Tests.Integration.AgentFramework;

public sealed class AgentEditorAdapterIntegrationTests {
    [Fact]
    public async Task Managed_delete_is_rejected_by_registered_command_and_preserves_catalog() {
        await using var host = await AgentUiAdapterTestHost.CreateAsync();
        await using var scope = host.App.Services.CreateAsyncScope();
        var workspace = scope.ServiceProvider.GetRequiredService<IAgentFrameworkWorkspaceService>();
        var managed = (await workspace.ListAgentsAsync(false)).First(ManagedSeedProviderFallbacks.IsManagedSeedAgent);
        var commands = scope.ServiceProvider.GetRequiredService<IAgentEditorCommands>();
        await Assert.ThrowsAsync<AgentDeletionConflictException>(() => commands.DeleteAsync(managed.Id));
        Assert.Equal(managed.Id, (await workspace.GetAgentEditorAsync(managed.Id)).Id);
    }

    [Fact]
    public async Task Registered_commands_round_trip_settings_and_preserve_optimistic_version() {
        await using var host = await AgentUiAdapterTestHost.CreateAsync();
        await using var scope = host.App.Services.CreateAsyncScope();
        var commands = Assert.IsType<AgentEditorCommands>(scope.ServiceProvider.GetRequiredService<IAgentEditorCommands>());
        var workspace = scope.ServiceProvider.GetRequiredService<IAgentFrameworkWorkspaceService>();
        var projectId = Guid.NewGuid();
        var processId = Guid.NewGuid();
        var secretId = Guid.NewGuid();
        var storageId = Guid.NewGuid();
        var capability = (await workspace.ListCapabilitiesAsync()).First();
        var draft = new AgentEditorModel {
            Name = "  Seams round trip  ",
            RoleTitle = "Reviewer",
            Summary = "Summary",
            Instructions = "Preserve all settings.",
            AvatarImageUrl = "data:image/png;base64,AQ==",
            Status = AgentLifecycleStatus.Draft,
            Temperature = 0.4,
            EnableBackgroundResponses = true,
            RequirePerServiceCallChatHistoryPersistence = true,
            ConfigurationJson = """{"extensionForRoundTrip":{"enabled":true}}""",
            Permissions = AgentPermissionsPolicy.Default with { CanObserveOtherAgents = true, CanScheduleWork = true },
            AllowedSecretReferences = [new(secretId, "Reference only", AgentSecretPurposes.GeneralAgentRequest)],
            ProjectStructureAccess = new() { CanRead = true, CanWriteTasks = true, AllowedProjectIds = [projectId] },
            ProcessAccess = new() { CanRead = true, CanWrite = true, AllowedDefinitionIds = [processId] },
            WorkspaceToolAccess = new() {
                CanReadFiles = true, CanWriteFiles = true, CanRunValidationCommands = true,
                CanReadStorage = true, CanWriteStorage = true, AllowedStorageCatalogIds = [storageId]
            },
            VoiceAccess = new() { CanUseVoiceMode = true, PreferredVoiceId = "alloy" },
            SelectedCapabilityIds = [capability.Id],
            Tags = [AgentSpecialTags.Favorite]
        };
        var submission = AgentEditorDraftPolicy.Capture(draft, ["  review ", "REVIEW", "seams"], []);
        var committed = Assert.IsType<AgentEditorSaveOutcome.Committed>(await commands.SaveAsync(submission.Request));
        Assert.Null(draft.Id);
        var refreshed = await commands.ReconcileAsync(committed.AgentId, []);
        var saved = refreshed.Draft;
        Assert.Equal("Seams round trip", saved.Name);
        Assert.Equal(draft.RoleTitle, saved.RoleTitle);
        Assert.Equal(draft.Summary, saved.Summary);
        Assert.Equal(draft.Instructions, saved.Instructions);
        Assert.Equal(draft.AvatarImageUrl, saved.AvatarImageUrl);
        Assert.Equal(draft.Temperature, saved.Temperature);
        Assert.True(saved.EnableBackgroundResponses);
        Assert.True(saved.RequirePerServiceCallChatHistoryPersistence);
        Assert.True(saved.Permissions.CanObserveOtherAgents);
        Assert.True(saved.Permissions.CanScheduleWork);
        Assert.Equal(secretId, Assert.Single(saved.AllowedSecretReferences).SecretId);
        Assert.Equal(projectId, Assert.Single(saved.ProjectStructureAccess.AllowedProjectIds));
        Assert.True(saved.ProjectStructureAccess.CanWriteTasks);
        Assert.Equal(processId, Assert.Single(saved.ProcessAccess.AllowedDefinitionIds));
        Assert.True(saved.ProcessAccess.CanWrite);
        Assert.True(saved.WorkspaceToolAccess.CanWriteFiles);
        Assert.True(saved.WorkspaceToolAccess.CanRunValidationCommands);
        Assert.Equal(storageId, Assert.Single(saved.WorkspaceToolAccess.AllowedStorageCatalogIds));
        Assert.True(saved.WorkspaceToolAccess.CanWriteStorage);
        Assert.Equal("alloy", saved.VoiceAccess.PreferredVoiceId);
        Assert.True(saved.VoiceAccess.CanUseVoiceMode);
        Assert.Equal(capability.Id, Assert.Single(saved.SelectedCapabilityIds));
        Assert.Contains(AgentSpecialTags.Favorite, saved.Tags);
        Assert.Equal(3, saved.Tags.Count);
        Assert.Contains("extensionForRoundTrip", saved.ConfigurationJson);
        Assert.NotNull(saved.ExpectedUpdatedAtUtc);
        var stale = AgentEditorDraftPolicy.Copy(saved);
        saved.Name = "Updated once";
        var updated = Assert.IsType<AgentEditorSaveOutcome.Committed>(
            await commands.SaveAsync(AgentEditorDraftPolicy.Capture(saved, saved.Tags, []).Request));
        Assert.Equal(committed.AgentId, updated.AgentId);
        stale.Name = "Must not overwrite";
        var conflict = Assert.IsType<AgentEditorSaveOutcome.Rejected>(
            await commands.SaveAsync(AgentEditorDraftPolicy.Capture(stale, stale.Tags, []).Request));
        Assert.True(conflict.IsConflict);
        var current = await workspace.GetAgentEditorAsync(committed.AgentId);
        Assert.Equal("Updated once", current.Name);
        Assert.NotEqual(stale.ExpectedUpdatedAtUtc, current.ExpectedUpdatedAtUtc);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Real_save_reports_committed_identity_when_projection_fails_or_cancels(bool cancellation) {
        using var lifetime = new CancellationTokenSource();
        var bridge = new ProjectionFailureBridge();
        await using var host = await AgentUiAdapterTestHost.CreateAsync(services => {
                services.RemoveAll<IAiTechnicalAgentBridge>();
                services.AddSingleton<IAiTechnicalAgentBridge>(bridge);
            });
        await using var scope = host.App.Services.CreateAsyncScope();
        var commands = Assert.IsType<AgentEditorCommands>(scope.ServiceProvider.GetRequiredService<IAgentEditorCommands>());
        var workspace = scope.ServiceProvider.GetRequiredService<IAgentFrameworkWorkspaceService>();
        bridge.Failure = () => {
            if (cancellation) {
                lifetime.Cancel();
                return new OperationCanceledException(lifetime.Token);
            }
            return new IOException("Projection boundary unavailable.");
        };
        var outcome = Assert.IsType<AgentEditorSaveOutcome.Committed>(
            await commands.SaveAsync(new() { Name = "Committed projection warning", TemplateKey = "committed-projection-warning" }, lifetime.Token));
        Assert.NotEqual(Guid.Empty, outcome.AgentId);
        Assert.Contains("was saved", outcome.Warning);
        bridge.Failure = null;
        var refreshed = await commands.ReconcileAsync(outcome.AgentId, []);
        Assert.Equal(outcome.AgentId, refreshed.Draft.Id);
        Assert.NotNull(refreshed.Draft.ExpectedUpdatedAtUtc);
        Assert.Single(await workspace.ListAgentsAsync(false), agent => agent.Id == outcome.AgentId);
        refreshed.Draft.Name = "Updated after warning";
        var next = Assert.IsType<AgentEditorSaveOutcome.Committed>(await commands.SaveAsync(refreshed.Draft));
        Assert.Equal(outcome.AgentId, next.AgentId);
        Assert.Null(next.Warning);
    }

    [Fact]
    public async Task Cancelled_save_is_cancellation_and_does_not_create_an_agent() {
        await using var host = await AgentUiAdapterTestHost.CreateAsync();
        await using var scope = host.App.Services.CreateAsyncScope();
        var commands = scope.ServiceProvider.GetRequiredService<IAgentEditorCommands>();
        var workspace = scope.ServiceProvider.GetRequiredService<IAgentFrameworkWorkspaceService>();
        var before = (await workspace.ListAgentsAsync(false)).Select(agent => agent.Id).ToArray();
        using var lifetime = new CancellationTokenSource();
        lifetime.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            commands.SaveAsync(new() { Name = "Cancelled draft" }, lifetime.Token));
        Assert.Equal(before, (await workspace.ListAgentsAsync(false)).Select(agent => agent.Id));
    }

    [Fact]
    public async Task Duplicate_template_is_typed_pre_write_rejection_and_can_be_corrected() {
        await using var host = await AgentUiAdapterTestHost.CreateAsync();
        await using var scope = host.App.Services.CreateAsyncScope();
        var commands = scope.ServiceProvider.GetRequiredService<IAgentEditorCommands>();
        var workspace = scope.ServiceProvider.GetRequiredService<IAgentFrameworkWorkspaceService>();
        var agents = await workspace.ListAgentsAsync(false);
        var draft = new AgentEditorModel { Name = "Rejected draft", TemplateKey = agents.First().TemplateKey };
        await Assert.ThrowsAsync<AgentEditorValidationException>(() => workspace.SaveAgentAsync(draft));
        var rejected = Assert.IsType<AgentEditorSaveOutcome.Rejected>(await commands.SaveAsync(draft));
        Assert.False(rejected.IsConflict);
        Assert.Equal(agents.Select(agent => agent.Id), (await workspace.ListAgentsAsync(false)).Select(agent => agent.Id));
        draft.TemplateKey = "corrected-integration-draft";
        Assert.IsType<AgentEditorSaveOutcome.Committed>(await commands.SaveAsync(draft));
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    public async Task Cache_invalidation_failure_after_commit_keeps_the_saved_identity(int failedCall) {
        var invalidator = new FailingCacheInvalidator();
        await using var host = await AgentUiAdapterTestHost.CreateAsync(services =>
            services.AddSingleton<IAgentReferenceDataCacheInvalidator>(invalidator));
        await using var scope = host.App.Services.CreateAsyncScope();
        var commands = scope.ServiceProvider.GetRequiredService<IAgentEditorCommands>();
        invalidator.FailOnCall = invalidator.Calls + failedCall;
        var result = Assert.IsType<AgentEditorSaveOutcome.Committed>(
            await commands.SaveAsync(new() { Name = "Committed cache warning", TemplateKey = "committed-cache-warning" }));
        Assert.NotNull(result.Warning);
        invalidator.FailOnCall = null;
        Assert.Equal(result.AgentId, (await commands.ReconcileAsync(result.AgentId, [])).Draft.Id);
    }

    private sealed class FailingCacheInvalidator : IAgentReferenceDataCacheInvalidator {
        public event EventHandler? Invalidated;
        public int Calls { get; private set; }
        public int? FailOnCall { get; set; }
        public void Invalidate() {
            Calls++;
            if (Calls == FailOnCall) {
                throw new IOException("Reference cache unavailable.");
            }
            Invalidated?.Invoke(this, EventArgs.Empty);
        }
    }

    private sealed class ProjectionFailureBridge : IAiTechnicalAgentBridge {
        public Func<Exception>? Failure { get; set; }
        public Task SynchronizeDirectoryProjectionAsync(CancellationToken cancellationToken = default)
            => Failure is null ? Task.CompletedTask : Task.FromException(Failure());
        public Task<IReadOnlyDictionary<Guid, AiTechnicalAgentDirectorySummary>> GetDirectorySummariesAsync(
            IReadOnlyList<Guid> partyIds, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<AiTechnicalAgentWorkspaceModel> GetWorkspaceAsync(Guid partyId, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();
        public Task<IReadOnlyDictionary<Guid, AiAgentStaffingFactModel>> GetStaffingFactsAsync(
            IReadOnlyList<Guid> partyIds, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<Result<AiTechnicalAgentSaveResult>> SaveAsync(AiAgentProfileEditorModel model,
            CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }
}
