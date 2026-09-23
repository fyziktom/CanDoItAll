using Bunit;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Components.BaseLib;
using CanDoItAll.Modules.AgentFramework;
using CanDoItAll.Modules.AgentFramework.Pages.Components;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.Extensions.DependencyInjection;

namespace CanDoItAll.Tests.Components.AgentFramework;

public sealed class AgentEditorCapabilityCompositionTests {
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Existing_capability_toggle_saves_whole_unsaved_draft_new_agent_stages(bool existing) {
        var probe = AgentEditorLoadCharacterizationTests.CreateProbe(out var workspace);
        await using var harness = await AgentEditorLoadCharacterizationTests.CreateHarnessAsync(workspace, probe);
        workspace = harness.Context.Services.GetRequiredService<IAgentFrameworkWorkspaceService>();
        var agentId = existing ? await workspace.SaveAgentAsync(new() { Name = "Capability toggle agent" }) : (Guid?)null;
        var cut = harness.Context.Render<AgentDetailsDialog>(parameters => parameters
            .Add(component => component.AgentId, agentId)
            .Add(component => component.InitialProviders, Array.Empty<ProviderProfile>())
            .Add(component => component.Section, AgentEditorSection.Capabilities));
        cut.WaitForElement("[data-testid='agents-details-capability-toggle']");
        var draft = (AgentEditorModel)cut.FindComponent<EditForm>().Instance.EditContext!.Model;
        draft.Name = "Unsaved whole draft";
        draft.Summary = "Must be saved with assignment";
        draft.VoiceAccess = new() { CanUseVoiceMode = true, PreferredVoiceId = "alloy" };
        probe.Save = request => probe.Target.SaveAgentAsync(request);
        await cut.FindAll("[data-testid='agents-details-capability-toggle']").First().ClickAsync();
        var capabilityId = Assert.Single(draft.SelectedCapabilityIds);
        Assert.Equal(existing ? 1 : 0, probe.AcceptedSaves);
        if (existing) {
            var saved = await workspace.GetAgentEditorAsync(agentId);
            Assert.Equal("Unsaved whole draft", saved.Name);
            Assert.Equal("Must be saved with assignment", saved.Summary);
            Assert.Equal("alloy", saved.VoiceAccess.PreferredVoiceId);
            Assert.Contains(capabilityId, saved.SelectedCapabilityIds);
        } else {
            Assert.Null(cut.Instance.CurrentTarget.AgentId);
            Assert.Contains(harness.Context.Services.GetRequiredService<NotificationService>().Messages,
                message => message.Summary == "Capability staged");
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Real_capability_wizard_creates_and_assigns(bool existing) {
        var probe = AgentEditorLoadCharacterizationTests.CreateProbe(out var workspace);
        await using var harness = await AgentEditorLoadCharacterizationTests.CreateHarnessAsync(workspace, probe);
        workspace = harness.Context.Services.GetRequiredService<IAgentFrameworkWorkspaceService>();
        var agentId = existing ? await workspace.SaveAgentAsync(new() { Name = "Wizard agent" }) : (Guid?)null;
        var host = harness.Context.Render<DialogHost>();
        var cut = harness.Context.Render<AgentDetailsDialog>(parameters => parameters
            .Add(component => component.AgentId, agentId)
            .Add(component => component.InitialProviders, Array.Empty<ProviderProfile>())
            .Add(component => component.Section, AgentEditorSection.Capabilities));
        cut.WaitForElement("[data-testid='agents-details-new-skill']");
        var draft = (AgentEditorModel)cut.FindComponent<EditForm>().Instance.EditContext!.Model;
        draft.Name = "Wizard whole draft";
        draft.Instructions = "Unsaved instructions travel with assignment";
        probe.Save = request => probe.Target.SaveAgentAsync(request);
        var opened = cut.Find("[data-testid='agents-details-new-skill']").ClickAsync();
        var wizard = host.WaitForComponent<CapabilitySetupWizardDialog>();
        wizard.Find("[data-testid='agents-capability-setup-name']").Change("Seams inline skill");
        wizard.Find("[data-testid='agents-capability-setup-description']").Change("Disposable catalog proof");
        await wizard.Find("[data-testid='agents-capability-setup-next']").ClickAsync();
        wizard.Find("[data-testid='agents-capability-setup-skill-mode']").Change("Inline");
        wizard.Find("[data-testid='agents-capability-setup-inline-name']").Change("Seams inline skill");
        wizard.Find("[data-testid='agents-capability-setup-inline-description']").Change("Disposable catalog proof");
        wizard.Find("[data-testid='agents-capability-setup-inline-instructions']").Change("Return a short review.");
        await wizard.Find("[data-testid='agents-capability-setup-next']").ClickAsync();
        await wizard.Find("[data-testid='agents-capability-setup-create']").ClickAsync();
        await opened.WaitAsync(TimeSpan.FromSeconds(10));
        var capability = Assert.Single(await workspace.ListCapabilitiesAsync(), item => item.Name == "Seams inline skill");
        Assert.Equal(CapabilityKind.Skill, capability.Kind);
        Assert.Contains(capability.Id, draft.SelectedCapabilityIds);
        Assert.Equal(existing ? 1 : 0, probe.AcceptedSaves);
        Assert.Empty(harness.Context.Services.GetRequiredService<DialogService>().Dialogs);
        if (existing) {
            var saved = await workspace.GetAgentEditorAsync(agentId);
            Assert.Equal("Wizard whole draft", saved.Name);
            Assert.Equal("Unsaved instructions travel with assignment", saved.Instructions);
            Assert.Contains(capability.Id, saved.SelectedCapabilityIds);
        } else {
            Assert.Null(cut.Instance.CurrentTarget.AgentId);
        }
    }
}
