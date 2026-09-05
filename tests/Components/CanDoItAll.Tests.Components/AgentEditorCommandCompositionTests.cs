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
    public async Task Known_validation_rejection_keeps_the_draft_writable_for_correction() {
        await using var harness = await ComponentTestHarness.CreateAsync();
        var workspace = harness.Context.Services.GetRequiredService<IAgentFrameworkWorkspaceService>();
        var existing = (await workspace.ListAgentsAsync(false)).First();
        var cut = harness.Context.Render<AgentDetailsDialog>(parameters => parameters
            .Add(component => component.InitialProviders, Array.Empty<ProviderProfile>()));
        cut.WaitForElement("[data-testid='agents-catalog-name']").Change("Correctable draft");
        var context = cut.FindComponent<EditForm>().Instance.EditContext!;
        var draft = (AgentEditorModel)context.Model;
        draft.TemplateKey = existing.TemplateKey;
        await cut.Find("form").SubmitAsync();
        Assert.Null(cut.Instance.CurrentTarget.AgentId);
        Assert.Same(context, cut.FindComponent<EditForm>().Instance.EditContext);
        Assert.Empty(cut.FindAll("[data-testid='agents-editor-write-unconfirmed']"));
        Assert.False(cut.Find("[data-testid='agents-catalog-save']").HasAttribute("disabled"));
        Assert.Contains(harness.Context.Services.GetRequiredService<NotificationService>().Messages,
            message => message.Summary == "Agent save failed");
        draft.TemplateKey = "corrected-editor-draft";
        await cut.Find("form").SubmitAsync();
        Assert.NotNull(cut.Instance.CurrentTarget.AgentId);
        Assert.Equal("Correctable draft", (await workspace.GetAgentEditorAsync(cut.Instance.CurrentTarget.AgentId)).Name);
    }

    [Fact]
    public async Task Committed_warning_survives_refresh_retry_and_preserves_the_saved_identity() {
        await using var harness = await ComponentTestHarness.CreateAsync(services =>
            services.AddScoped<IAgentEditorCommands>(provider =>
                new WarningCommands(ActivatorUtilities.CreateInstance<AgentEditorCommands>(provider))));
        var cut = harness.Context.Render<AgentDetailsDialog>(parameters => parameters
            .Add(component => component.InitialProviders, Array.Empty<ProviderProfile>()));
        cut.WaitForElement("[data-testid='agents-catalog-name']").Change("Saved with projection warning");
        await cut.Find("form").SubmitAsync();
        var id = cut.Instance.CurrentTarget.AgentId;
        Assert.NotNull(id);
        Assert.Contains(WarningCommands.Warning, cut.Find("[data-testid='agents-editor-commit-warning']").TextContent);
        Assert.NotNull(cut.Find("[data-testid='agents-editor-retry-refresh']"));
        Assert.Empty(cut.FindAll("[data-testid='agents-editor-write-unconfirmed']"));
        await cut.Find("[data-testid='agents-editor-retry-refresh']").ClickAsync();
        Assert.Equal(id, cut.Instance.CurrentTarget.AgentId);
        Assert.Contains(WarningCommands.Warning, cut.Find("[data-testid='agents-editor-commit-warning']").TextContent);
        Assert.False(cut.Find("[data-testid='agents-catalog-save']").HasAttribute("disabled"));
        var commands = Assert.IsType<WarningCommands>(harness.Context.Services.GetRequiredService<IAgentEditorCommands>());
        Assert.Equal(1, commands.Writes);
        Assert.Single(await harness.Context.Services.GetRequiredService<IAgentFrameworkWorkspaceService>().ListAgentsAsync(false),
            agent => agent.Id == id);
    }

    private sealed class WarningCommands(IAgentEditorCommands inner) : IAgentEditorCommands {
        public const string Warning = "The agent was saved, but the directory projection needs attention.";
        public int Writes { get; private set; }
        private int reads;
        public async Task<AgentEditorSaveOutcome> SaveAsync(AgentEditorModel request, CancellationToken cancellationToken = default) {
            Writes++;
            var committed = Assert.IsType<AgentEditorSaveOutcome.Committed>(await inner.SaveAsync(request, cancellationToken));
            return committed with { Warning = Warning };
        }
        public Task<AgentEditorCatalogRefresh> ReconcileAsync(Guid agentId, IReadOnlyList<ProviderProfile> providers,
            CancellationToken cancellationToken = default) {
            reads++;
            return reads == 1 ? Task.FromException<AgentEditorCatalogRefresh>(new IOException("Refresh temporarily unavailable."))
                : inner.ReconcileAsync(agentId, providers, cancellationToken);
        }
        public Task DeleteAsync(Guid agentId, CancellationToken cancellationToken = default)
            => inner.DeleteAsync(agentId, cancellationToken);
        public Task VerifyCapabilityAsync(Guid agentId, Guid capabilityId, CancellationToken cancellationToken = default)
            => inner.VerifyCapabilityAsync(agentId, capabilityId, cancellationToken);
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
