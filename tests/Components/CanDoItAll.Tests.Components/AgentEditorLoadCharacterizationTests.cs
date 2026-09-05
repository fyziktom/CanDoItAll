using System.Reflection;
using System.Runtime.ExceptionServices;
using Bunit;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Components.BaseLib;
using CanDoItAll.Modules.AgentFramework.Pages.Components;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.Extensions.DependencyInjection;

namespace CanDoItAll.Tests.Components.AgentFramework;

public sealed class AgentEditorLoadCharacterizationTests {
    [Theory]
    [InlineData(AgentEditorProbeFailure.Agents)]
    [InlineData(AgentEditorProbeFailure.Capabilities)]
    public async Task Core_load_failure_currently_exposes_a_blank_editable_form(AgentEditorProbeFailure failure) {
        var probe = CreateProbe(out var workspace);
        await using var harness = await CreateHarnessAsync(workspace, probe);
        probe.Failure = failure;

        var cut = harness.Context.Render<AgentDetailsDialog>(parameters => parameters
            .Add(component => component.AgentId, Guid.NewGuid())
            .Add(component => component.InitialProviders, Array.Empty<ProviderProfile>()));

        cut.WaitForElement("[data-testid='agents-catalog-save']");
        Assert.Empty(cut.FindAll("[data-testid='agents-details-loading']"));
        Assert.False(cut.Find("[data-testid='agents-catalog-save']").HasAttribute("disabled"));
        var draft = Assert.IsType<AgentEditorModel>(cut.FindComponent<EditForm>().Instance.EditContext!.Model);
        Assert.Null(draft.Id);
        Assert.Empty(draft.Name);
        Assert.Contains(harness.Context.Services.GetRequiredService<NotificationService>().Messages,
            message => message.Summary == "Agent editor failed to load");
    }

    [Fact]
    public async Task Clear_on_an_existing_agent_creates_a_blank_draft_without_saving() {
        var probe = CreateProbe(out var workspace);
        await using var harness = await CreateHarnessAsync(workspace, probe);
        var currentWorkspace = harness.Context.Services.GetRequiredService<IAgentFrameworkWorkspaceService>();
        var agent = (await currentWorkspace.ListAgentsAsync(includeTemplates: false)).First();
        var cut = harness.Context.Render<AgentDetailsDialog>(parameters => parameters
            .Add(component => component.AgentId, agent.Id)
            .Add(component => component.InitialProviders, Array.Empty<ProviderProfile>()));
        cut.WaitForAssertion(() => Assert.Equal(agent.Id,
            Assert.IsType<AgentEditorModel>(cut.FindComponent<EditForm>().Instance.EditContext!.Model).Id));

        cut.FindAll("button").Single(button => button.TextContent.Trim() == "Clear").Click();

        var draft = Assert.IsType<AgentEditorModel>(cut.FindComponent<EditForm>().Instance.EditContext!.Model);
        Assert.Null(draft.Id);
        Assert.Null(draft.ExpectedUpdatedAtUtc);
        Assert.Empty(draft.Name);
        Assert.Equal(0, probe.AcceptedSaves);
        Assert.NotNull(cut.Find("[data-testid='agents-catalog-name']"));
    }

    [Fact]
    public async Task Accepted_save_followed_by_refresh_failure_retains_identity_and_blocks_repeated_write() {
        var probe = CreateProbe(out var workspace);
        await using var harness = await CreateHarnessAsync(workspace, probe);
        var completed = new List<AgentDetailsDialogResult>();
        var cut = harness.Context.Render<AgentDetailsDialog>(parameters => parameters
            .Add(component => component.InitialProviders, Array.Empty<ProviderProfile>())
            .Add(component => component.Saved, EventCallback.Factory.Create<AgentDetailsDialogResult>(this, completed.Add)));
        cut.WaitForElement("[data-testid='agents-catalog-name']");
        probe.Failure = AgentEditorProbeFailure.RefreshAfterSave;
        cut.Find("[data-testid='agents-catalog-name']").Change("Refresh characterization");

        cut.Find("form").Submit();

        cut.WaitForAssertion(() => Assert.Equal(1, probe.AcceptedSaves));
        cut.WaitForAssertion(() => Assert.Contains(
            harness.Context.Services.GetRequiredService<NotificationService>().Messages,
            message => message.Summary == "Agent saved, but the editor refresh failed"));
        Assert.Empty(completed);
        Assert.NotNull(Assert.IsType<AgentEditorModel>(cut.FindComponent<EditForm>().Instance.EditContext!.Model).Id);
        Assert.True(cut.Find("[data-testid='agents-catalog-save']").HasAttribute("disabled"));
        cut.Find("form").Submit();
        Assert.Equal(1, probe.AcceptedSaves);
        Assert.NotNull(cut.Find("[data-testid='agents-editor-retry-refresh']"));
    }

    internal static AgentEditorWorkspaceProbe CreateProbe(out IAgentFrameworkWorkspaceService workspace) {
        workspace = DispatchProxy.Create<IAgentFrameworkWorkspaceService, AgentEditorWorkspaceProbe>();
        return (AgentEditorWorkspaceProbe)(object)workspace;
    }

    internal static Task<ComponentTestHarness> CreateHarnessAsync(
        IAgentFrameworkWorkspaceService workspace,
        AgentEditorWorkspaceProbe probe) {
        return ComponentTestHarness.CreateAsync(services => {
            var factory = services.Last(descriptor => descriptor.ServiceType == typeof(IAgentFrameworkWorkspaceService))
                .ImplementationFactory ?? throw new InvalidOperationException("The production workspace factory must be registered.");
            services.AddScoped<IAgentFrameworkWorkspaceService>(provider => {
                probe.Target = (IAgentFrameworkWorkspaceService)factory(provider);
                return workspace;
            });
        });
    }
}

public enum AgentEditorProbeFailure {
    None,
    Agents,
    Capabilities,
    RefreshAfterSave
}

public class AgentEditorWorkspaceProbe : DispatchProxy {
    public IAgentFrameworkWorkspaceService Target { get; set; } = default!;
    public AgentEditorProbeFailure Failure { get; set; }
    public int AcceptedSaves { get; private set; }
    public Func<AgentEditorModel, Task<Guid>>? Save { get; set; }

    protected override object? Invoke(MethodInfo? targetMethod, object?[]? args) {
        ArgumentNullException.ThrowIfNull(targetMethod);
        if (targetMethod.Name == nameof(IAgentFrameworkWorkspaceService.ListAgentsAsync) &&
            (Failure == AgentEditorProbeFailure.Agents ||
             Failure == AgentEditorProbeFailure.RefreshAfterSave && AcceptedSaves > 0)) {
            return Task.FromException<IReadOnlyList<AgentDefinition>>(new InvalidOperationException("Agent catalog probe failure."));
        }

        if (targetMethod.Name == nameof(IAgentFrameworkWorkspaceService.ListCapabilitiesAsync) &&
            Failure == AgentEditorProbeFailure.Capabilities) {
            return Task.FromException<IReadOnlyList<CapabilityCatalogItem>>(new InvalidOperationException("Capability catalog probe failure."));
        }

        if (targetMethod.Name == nameof(IAgentFrameworkWorkspaceService.SaveAgentAsync) && Save is not null) {
            AcceptedSaves++;
            return Save((AgentEditorModel)args![0]!);
        }

        if (targetMethod.Name == nameof(IAgentFrameworkWorkspaceService.SaveAgentAsync) &&
            Failure == AgentEditorProbeFailure.RefreshAfterSave) {
            AcceptedSaves++;
            return Task.FromResult(Guid.NewGuid());
        }

        try {
            return targetMethod.Invoke(Target, args);
        } catch (TargetInvocationException exception) when (exception.InnerException is not null) {
            ExceptionDispatchInfo.Capture(exception.InnerException).Throw();
            throw;
        }
    }
}
