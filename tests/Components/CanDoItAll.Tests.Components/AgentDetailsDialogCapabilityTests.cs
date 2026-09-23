using System.Reflection;
using Bunit;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Components.BaseLib;
using CanDoItAll.Infrastructure.Storage;
using CanDoItAll.Modules.AgentFramework;
using CanDoItAll.Modules.AgentFramework.Pages.Components;
using CanDoItAll.Modules.Workspace;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace CanDoItAll.Tests.Components.AgentFramework;

public sealed class AgentDetailsDialogCapabilityTests
{
    [Fact]
    public void Capabilities_tab_shows_tools_and_filters_the_shared_catalog()
    {
        using var context = CreateContext(out _);
        var tool = CreateCapability(CapabilityKind.Tool, "Repository tool", "repo.exe");
        var skill = CreateCapability(CapabilityKind.Skill, "Review skill", "inline://review");
        var mcp = CreateCapability(CapabilityKind.McpServer, "Browser MCP", "npx");
        var editor = new AgentEditorModel
        {
            SelectedCapabilityIds = [skill.Id]
        };

        var cut = RenderCapabilitiesTab(context, editor, [tool, skill, mcp]);

        Assert.Equal(3, cut.FindAll("[data-testid='agents-details-capability-card']").Count);
        var toolCard = cut.Find("[data-capability-kind='Tool']");
        Assert.Contains("Repository tool", toolCard.TextContent, StringComparison.Ordinal);
        Assert.Contains("Available", toolCard.TextContent, StringComparison.Ordinal);

        cut.Find("[data-testid='agents-details-capability-kind-filter']").Change("Tool");

        var filteredCard = Assert.Single(cut.FindAll("[data-testid='agents-details-capability-card']"));
        Assert.Equal("Tool", filteredCard.GetAttribute("data-capability-kind"));

        cut.Find("[data-testid='agents-details-capability-filter-reset']").Click();
        cut.Find("[data-testid='agents-details-capability-assignment-filter']").Change("Attached");

        filteredCard = Assert.Single(cut.FindAll("[data-testid='agents-details-capability-card']"));
        Assert.Equal("Skill", filteredCard.GetAttribute("data-capability-kind"));
        Assert.Contains("Attached", filteredCard.TextContent, StringComparison.Ordinal);

        cut.Find("[data-testid='agents-details-capability-filter-reset']").Click();
        cut.Find("[data-testid='agents-details-capability-search']").Input("browser");

        filteredCard = Assert.Single(cut.FindAll("[data-testid='agents-details-capability-card']"));
        Assert.Equal("McpServer", filteredCard.GetAttribute("data-capability-kind"));
        Assert.Contains("Browser MCP", filteredCard.TextContent, StringComparison.Ordinal);

        cut.Find("[data-testid='agents-details-capability-filter-reset']").Click();
        Assert.Equal(3, cut.FindAll("[data-testid='agents-details-capability-card']").Count);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Capability_wizard_stages_new_agents_and_persists_existing_agents(bool existingAgent)
    {
        using var context = CreateContext(out var workspaceProxy);
        var createdCapability = CreateCapability(CapabilityKind.Tool, "Created tool", "created.exe");
        workspaceProxy.Capabilities = [createdCapability];
        var agentId = existingAgent ? Guid.NewGuid() : (Guid?)null;
        var editor = new AgentEditorModel
        {
            Id = agentId,
            Name = "Capability test agent"
        };
        context.Services.GetRequiredService<AgentEditorReadFixture>().ReadCapabilities =
            _ => Task.FromResult(workspaceProxy.Capabilities);
        var cut = RenderCapabilitiesTab(context, editor, []);
        var dialogService = context.Services.GetRequiredService<DialogService>();

        var clickTask = cut.Find("[data-testid='agents-details-new-tool']").ClickAsync(new MouseEventArgs());

        cut.WaitForAssertion(() =>
        {
            var dialog = Assert.Single(dialogService.Dialogs);
            Assert.Equal(typeof(CapabilitySetupWizardDialog), dialog.ComponentType);
            Assert.Equal(CapabilityKind.Tool, dialog.Parameters[nameof(CapabilitySetupWizardDialog.InitialKind)]);
            Assert.All(
                cut.FindAll("[data-testid^='agents-details-new-'] button, button[data-testid^='agents-details-new-']"),
                button => Assert.True(button.HasAttribute("disabled")));
        });

        await dialogService.CloseAsync(new CapabilityDetailsDialogResult(createdCapability.Id));
        await clickTask.WaitAsync(TimeSpan.FromSeconds(2));

        cut.WaitForAssertion(() =>
        {
            var card = cut.Find("[data-capability-kind='Tool']");
            Assert.Contains("Created tool", card.TextContent, StringComparison.Ordinal);
            Assert.Contains("Attached", card.TextContent, StringComparison.Ordinal);
            Assert.Contains(createdCapability.Id, editor.SelectedCapabilityIds);
        });

        Assert.Equal(existingAgent ? 1 : 0, workspaceProxy.SavedModels.Count);
        if (existingAgent)
        {
            Assert.Contains(createdCapability.Id, Assert.Single(workspaceProxy.SavedModels).SelectedCapabilityIds);
        }
    }

    private static BunitContext CreateContext(out RecordingWorkspaceServiceProxy workspaceProxy)
    {
        var context = new BunitContext();
        context.JSInterop.Mode = JSRuntimeMode.Loose;
        context.Services.AddCanDoItAllBaseLib();
        context.Services.AddStubProviderRuntimeAdministration();
        context.Services.AddSingleton<IExternalTargetPathRegistryFactory>(new ExternalTargetPathRegistryFactory());
        context.Services.AddSingleton<IStorageCatalogSelectionSource>(new EmptyStorageCatalogSelectionSource());
        context.Services.AddSingleton(new AgentAvatarGenerationService(
            new UnavailableAgentImageGenerationService(),
            NullLogger<AgentAvatarGenerationService>.Instance));

        var workspaceService = DispatchProxy.Create<IAgentFrameworkWorkspaceService, RecordingWorkspaceServiceProxy>();
        workspaceProxy = (RecordingWorkspaceServiceProxy)(object)workspaceService;
        context.Services.AddSingleton(workspaceService);
        context.Services.AddAgentEditorReadFixture();
        return context;
    }

    private static IRenderedComponent<AgentDetailsDialog> RenderCapabilitiesTab(
        BunitContext context,
        AgentEditorModel editor,
        IReadOnlyList<CapabilityCatalogItem> capabilities)
    {
        return context.RenderEditor(editor, AgentEditorSection.Capabilities, capabilities: capabilities);
    }

    private static CapabilityCatalogItem CreateCapability(
        CapabilityKind kind,
        string name,
        string endpoint)
    {
        return new CapabilityCatalogItem(
            Guid.NewGuid(),
            kind,
            name.ToLowerInvariant().Replace(' ', '-'),
            name,
            $"{name} description.",
            endpoint,
            "{}",
            CapabilityProofStatus.NotRun,
            string.Empty,
            null,
            IsBuiltIn: false)
        {
            Tags = ["test", kind.ToString().ToLowerInvariant()]
        };
    }

    public class RecordingWorkspaceServiceProxy : DispatchProxy
    {
        public IReadOnlyList<CapabilityCatalogItem> Capabilities { get; set; } = [];

        public List<AgentEditorModel> SavedModels { get; } = [];

        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
        {
            return targetMethod?.Name switch
            {
                nameof(IAgentFrameworkWorkspaceService.ListCapabilitiesAsync) =>
                    Task.FromResult(Capabilities),
                nameof(IAgentFrameworkWorkspaceService.ListAgentsAsync) =>
                    Task.FromResult<IReadOnlyList<AgentDefinition>>([]),
                nameof(IAgentFrameworkWorkspaceService.SaveAgentAsync) =>
                    SaveAgent((AgentEditorModel)args![0]!),
                "add_ExecutionUpdated" or "remove_ExecutionUpdated" => null,
                _ => throw new InvalidOperationException(
                    $"Workspace service member '{targetMethod?.Name}' was not expected in this component test.")
            };
        }

        private Task<Guid> SaveAgent(AgentEditorModel model)
        {
            SavedModels.Add(model);
            return Task.FromResult(model.Id ?? Guid.NewGuid());
        }
    }
}
