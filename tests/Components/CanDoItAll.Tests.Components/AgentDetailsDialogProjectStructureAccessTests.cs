using System.Reflection;
using System.Runtime.CompilerServices;
using Bunit;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Components.BaseLib;
using CanDoItAll.Modules.AgentFramework.Pages.Components;
using CanDoItAll.Modules.Projects;
using CanDoItAll.Modules.Security;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;

namespace CanDoItAll.Tests.Components;

public sealed class AgentDetailsDialogProjectStructureAccessTests
{
    [Fact]
    public void Enabling_all_projects_clears_an_existing_explicit_allowlist()
    {
        using var context = CreateContext(out _);
        var editor = new AgentEditorModel
        {
            ProjectStructureAccess = new AgentProjectStructureAccessSettings
            {
                AllowedProjectIds = [Guid.NewGuid(), Guid.NewGuid()]
            }
        };
        var cut = RenderProjectStructureTab(context, editor);

        cut.Find("[data-testid='agents-catalog-project-structure-all']").Change(true);

        Assert.True(editor.ProjectStructureAccess.CanRead);
        Assert.True(editor.ProjectStructureAccess.AllowAllProjects);
        Assert.Empty(editor.ProjectStructureAccess.AllowedProjectIds);
    }

    [Fact]
    public void Save_normalizes_a_stale_mixed_project_scope_before_persistence()
    {
        using var context = CreateContext(out var workspaceProxy);
        var editor = new AgentEditorModel
        {
            Name = "Project access test agent",
            ProjectStructureAccess = new AgentProjectStructureAccessSettings
            {
                AllowAllProjects = true,
                AllowedProjectIds = [Guid.NewGuid()]
            }
        };
        var cut = RenderProjectStructureTab(context, editor);

        cut.Find("[data-testid='agents-catalog-save']").Click();

        cut.WaitForAssertion(() =>
        {
            var saved = Assert.Single(workspaceProxy.SavedModels);
            Assert.True(saved.ProjectStructureAccess.CanRead);
            Assert.True(saved.ProjectStructureAccess.AllowAllProjects);
            Assert.Empty(saved.ProjectStructureAccess.AllowedProjectIds);
        });
    }

    private static BunitContext CreateContext(out RecordingWorkspaceServiceProxy workspaceProxy)
    {
        var context = new BunitContext();
        context.JSInterop.Mode = JSRuntimeMode.Loose;
        context.Services.AddCanDoItAllBaseLib();

        var workspaceService = DispatchProxy.Create<IAgentFrameworkWorkspaceService, RecordingWorkspaceServiceProxy>();
        workspaceProxy = (RecordingWorkspaceServiceProxy)(object)workspaceService;
        context.Services.AddSingleton(workspaceService);
        context.Services.AddSingleton(
            (ProjectsService)RuntimeHelpers.GetUninitializedObject(typeof(ProjectsService)));
        context.Services.AddSingleton(
            (SecretService)RuntimeHelpers.GetUninitializedObject(typeof(SecretService)));
        return context;
    }

    private static IRenderedComponent<TestAgentDetailsDialog> RenderProjectStructureTab(
        BunitContext context,
        AgentEditorModel editor)
    {
        return context.Render<TestAgentDetailsDialog>(parameters => parameters
            .Add(component => component.TestEditor, editor));
    }

    public sealed class TestAgentDetailsDialog : AgentDetailsDialog
    {
        [Parameter]
        public AgentEditorModel TestEditor { get; set; } = new();

        protected override Task OnInitializedAsync()
        {
            SetBaseField("editorModel", TestEditor);
            SetBaseField("selectedTabIndex", 4);
            SetBaseField("isLoading", false);
            return Task.CompletedTask;
        }

        private void SetBaseField(string fieldName, object value)
        {
            var field = typeof(AgentDetailsDialog).GetField(
                fieldName,
                BindingFlags.Instance | BindingFlags.NonPublic)
                ?? throw new MissingFieldException(typeof(AgentDetailsDialog).FullName, fieldName);
            field.SetValue(this, value);
        }
    }

    public class RecordingWorkspaceServiceProxy : DispatchProxy
    {
        public List<AgentEditorModel> SavedModels { get; } = [];

        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
        {
            return targetMethod?.Name switch
            {
                nameof(IAgentFrameworkWorkspaceService.SaveAgentAsync) =>
                    SaveAgent((AgentEditorModel)args![0]!),
                nameof(IAgentFrameworkWorkspaceService.ListAgentsAsync) =>
                    Task.FromResult<IReadOnlyList<AgentDefinition>>([]),
                nameof(IAgentFrameworkWorkspaceService.ListCapabilitiesAsync) =>
                    Task.FromResult<IReadOnlyList<CapabilityCatalogItem>>([]),
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
