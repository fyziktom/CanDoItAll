using Bunit;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Components.BaseLib;
using CanDoItAll.Infrastructure.Storage;
using CanDoItAll.Memory.Abstractions;
using CanDoItAll.Memory.Application;
using CanDoItAll.Modules.AgentFramework;
using CanDoItAll.Modules.AgentFramework.Pages.Components;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace CanDoItAll.Tests.Components.AgentFramework;

public sealed class AgentEditorCommandCompositionTests : AgentMemorySettingsPanelTestBase {
    [Fact]
    public async Task Managed_delete_is_rejected_by_registered_command_and_preserves_catalog() {
        await using var harness = await ComponentTestHarness.CreateAsync();
        var workspace = harness.Context.Services.GetRequiredService<IAgentFrameworkWorkspaceService>();
        var managed = (await workspace.ListAgentsAsync(false)).First(ManagedSeedProviderFallbacks.IsManagedSeedAgent);
        var commands = harness.Context.Services.GetRequiredService<IAgentEditorCommands>();
        await Assert.ThrowsAsync<AgentDeletionConflictException>(() => commands.DeleteAsync(managed.Id));
        Assert.Equal(managed.Id, (await workspace.GetAgentEditorAsync(managed.Id)).Id);
    }

    [Fact]
    public async Task Registered_commands_reject_root_preparation_before_write() {
        var probe = AgentEditorLoadCharacterizationTests.CreateProbe(out var workspace);
        await using var harness = await AgentEditorLoadCharacterizationTests.CreateHarnessAsync(workspace, probe);
        workspace = harness.Context.Services.GetRequiredService<IAgentFrameworkWorkspaceService>();
        probe.Save = _ => throw new InvalidOperationException("A rejected preparation must not write.");
        var commands = new AgentEditorCommands(workspace, new RejectingRootRegistryFactory());
        var draft = new AgentEditorModel { Name = "Preserved after rejection" };
        var result = Assert.IsType<AgentEditorSaveOutcome.Rejected>(
            await commands.SaveAsync(AgentEditorDraftPolicy.Capture(draft, [], []).Request));
        Assert.False(result.IsConflict);
        Assert.Equal("Root mapping unavailable.", result.Message);
        Assert.Equal(0, probe.AcceptedSaves);
        Assert.Equal("Preserved after rejection", draft.Name);
    }

    private sealed class RejectingRootRegistryFactory : IExternalTargetPathRegistryFactory {
        public IExternalTargetPathRegistry Create(IEnumerable<ExternalTargetRootBinding> bindings)
            => throw new InvalidOperationException("Root mapping unavailable.");
    }

    [Fact]
    public async Task Registered_commands_round_trip_settings_and_preserve_optimistic_version() {
        await using var harness = await ComponentTestHarness.CreateAsync();
        var commands = Assert.IsType<AgentEditorCommands>(harness.Context.Services.GetRequiredService<IAgentEditorCommands>());
        var workspace = harness.Context.Services.GetRequiredService<IAgentFrameworkWorkspaceService>();
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

    [Fact]
    public async Task Real_save_refresh_retry_reuses_acknowledgement_without_another_write() {
        var probe = AgentEditorLoadCharacterizationTests.CreateProbe(out var workspace);
        await using var harness = await AgentEditorLoadCharacterizationTests.CreateHarnessAsync(workspace, probe);
        var completed = new List<AgentDetailsDialogResult>();
        var targets = new List<AgentEditorTarget>();
        var cut = harness.Context.Render<AgentDetailsDialog>(parameters => parameters
            .Add(component => component.InitialProviders, Array.Empty<ProviderProfile>())
            .Add(component => component.Saved, EventCallback.Factory.Create<AgentDetailsDialogResult>(this, completed.Add))
            .Add(component => component.TargetChanged, EventCallback.Factory.Create<AgentEditorTarget>(this, targets.Add)));
        cut.WaitForElement("[data-testid='agents-catalog-name']").Change("Reconciliation test");
        probe.Save = request => probe.Target.SaveAgentAsync(request);
        probe.Failure = AgentEditorProbeFailure.RefreshAfterSave;
        await cut.Find("form").SubmitAsync();
        var id = cut.Instance.CurrentTarget.AgentId;
        Assert.NotNull(id);
        Assert.Equal(id, targets.Last().AgentId);
        Assert.Equal(1, probe.AcceptedSaves);
        Assert.Empty(completed);
        Assert.True(cut.Find("[data-testid='agents-catalog-save']").HasAttribute("disabled"));
        cut.Find("[data-testid='agents-catalog-name']").Change("Edited after acknowledgement");
        var context = cut.FindComponent<EditForm>().Instance.EditContext;
        probe.Failure = AgentEditorProbeFailure.None;
        await cut.Find("[data-testid='agents-editor-retry-refresh']").ClickAsync();
        Assert.Equal(1, probe.AcceptedSaves);
        Assert.Equal(id, Assert.Single(completed).AgentId);
        Assert.Same(context, cut.FindComponent<EditForm>().Instance.EditContext);
        Assert.Equal("Edited after acknowledgement", ((AgentEditorModel)context!.Model).Name);
        Assert.NotNull(((AgentEditorModel)context.Model).ExpectedUpdatedAtUtc);
        await cut.Find("form").SubmitAsync();
        Assert.Equal(2, probe.AcceptedSaves);
        Assert.Equal(id, cut.Instance.CurrentTarget.AgentId);
        Assert.Equal(2, completed.Count);
        Assert.Equal("Edited after acknowledgement", (await workspace.GetAgentEditorAsync(id)).Name);
        Assert.Single(await workspace.ListAgentsAsync(false), agent => agent.Id == id);
        Assert.NotNull(cut.Find("[data-testid='agents-catalog-save']"));
    }

    [Fact]
    public async Task Real_save_callback_failure_keeps_current_version_and_does_not_replay() {
        var probe = AgentEditorLoadCharacterizationTests.CreateProbe(out var workspace);
        await using var harness = await AgentEditorLoadCharacterizationTests.CreateHarnessAsync(workspace, probe);
        var cut = harness.Context.Render<AgentDetailsDialog>(parameters => parameters
            .Add(component => component.InitialProviders, Array.Empty<ProviderProfile>())
            .Add(component => component.Saved, EventCallback.Factory.Create<AgentDetailsDialogResult>(this,
                _ => Task.FromException(new InvalidOperationException("Caller refresh failed.")))));
        cut.WaitForElement("[data-testid='agents-catalog-name']").Change("Callback test");
        probe.Save = request => probe.Target.SaveAgentAsync(request);
        await cut.Find("form").SubmitAsync();
        var draft = (AgentEditorModel)cut.FindComponent<EditForm>().Instance.EditContext!.Model;
        Assert.NotNull(draft.Id);
        Assert.NotNull(draft.ExpectedUpdatedAtUtc);
        Assert.Equal(1, probe.AcceptedSaves);
        Assert.False(cut.Find("[data-testid='agents-catalog-save']").HasAttribute("disabled"));
        Assert.Contains(harness.Context.Services.GetRequiredService<NotificationService>().Messages,
            message => message.Summary == "Agent saved, but the catalog refresh failed");
        Assert.Equal(draft.ExpectedUpdatedAtUtc, (await workspace.GetAgentEditorAsync(draft.Id)).ExpectedUpdatedAtUtc);
    }

    [Fact]
    public async Task Unknown_write_outcome_retains_draft_and_requires_catalog_check() {
        var probe = AgentEditorLoadCharacterizationTests.CreateProbe(out var workspace);
        await using var harness = await AgentEditorLoadCharacterizationTests.CreateHarnessAsync(workspace, probe);
        var cut = harness.Context.Render<AgentDetailsDialog>(parameters => parameters
            .Add(component => component.InitialProviders, Array.Empty<ProviderProfile>()));
        cut.WaitForElement("[data-testid='agents-catalog-name']").Change("Recoverable draft");
        var context = cut.FindComponent<EditForm>().Instance.EditContext;
        probe.Save = _ => Task.FromException<Guid>(new IOException("Acknowledgement unavailable."));
        await cut.Find("form").SubmitAsync();
        Assert.NotNull(cut.Find("[data-testid='agents-editor-write-unconfirmed']"));
        Assert.Same(context, cut.FindComponent<EditForm>().Instance.EditContext);
        Assert.Equal("Recoverable draft", ((AgentEditorModel)context!.Model).Name);
        await cut.Find("form").SubmitAsync();
        Assert.Equal(1, probe.AcceptedSaves);
    }

    [Fact]
    public async Task Editor_memory_binding_round_trips_real_child_settings() {
        var profile = CreateProvider("provider.editor", "Editor memory", isEnabled: true);
        await using var harness = await ComponentTestHarness.CreateAsync(services => {
            services.AddSingleton<IMemoryProviderProfileStore>(new TestProfileStore(profile));
            services.RemoveAll<IMemoryProviderDriver>();
            services.AddSingleton<IMemoryProviderDriver>(new TestMemoryProviderDriver(MemoryProviderDriverKind.Mock));
        });
        var cut = harness.Context.Render<AgentDetailsDialog>(parameters => parameters
            .Add(component => component.InitialProviders, Array.Empty<ProviderProfile>())
            .Add(component => component.Section, AgentEditorSection.Memory));
        cut.WaitForElement("[data-testid='agents-catalog-memory-new-provider'] option[value='provider.editor']");
        var draft = (AgentEditorModel)cut.FindComponent<EditForm>().Instance.EditContext!.Model;
        draft.Name = "Memory editor round trip";
        cut.Find("[data-testid='agents-catalog-memory-mode']").Change(nameof(AgentMemoryInvocationMode.Automatic));
        cut.Find("[data-testid='agents-catalog-memory-new-alias']").Change("review-memory");
        cut.Find("[data-testid='agents-catalog-memory-new-provider']").Change("provider.editor");
        cut.Find("[data-testid='agents-catalog-memory-new-requirement']").Change(nameof(AgentMemoryProviderRequirement.Required));
        cut.Find("[data-testid='agents-catalog-memory-add-binding']").Click();
        Assert.Equal("review-memory", Assert.Single(draft.MemoryAccess.ProviderBindings).Alias.Value);
        await cut.Find("form").SubmitAsync();
        var saved = await harness.Context.Services.GetRequiredService<IAgentFrameworkWorkspaceService>()
            .GetAgentEditorAsync(cut.Instance.CurrentTarget.AgentId);
        var binding = Assert.Single(saved.MemoryAccess.ProviderBindings);
        Assert.Equal("review-memory", binding.Alias.Value);
        Assert.Equal(profile.InstanceId, binding.ProviderInstanceId);
        Assert.Equal(AgentMemoryProviderRequirement.Required, binding.Requirement);
        Assert.Equal(AgentMemoryInvocationMode.Automatic, saved.MemoryAccess.InvocationMode);
        Assert.Empty(saved.MemoryAccess.AllowedProviderInstanceIds);
    }
}
