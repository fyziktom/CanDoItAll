using Bunit;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AppComponents;
using CanDoItAll.Components.BaseLib;
using CanDoItAll.Modules.Workbench;
using CanDoItAll.Modules.Workbench.Pages;
using Microsoft.Extensions.DependencyInjection;

namespace CanDoItAll.Tests.Components;

public sealed class ProjectStructureResourcePickerTests : BunitContext
{
    public ProjectStructureResourcePickerTests()
    {
        Services.AddCanDoItAllBaseLib();
    }

    [Fact]
    public void Process_link_picker_raises_definition_id_without_dom_string_parsing()
    {
        var definitionId = Guid.Parse("33333333-3333-3333-3333-333333333333");
        Guid? selectedDefinitionId = null;
        var dialog = new ProjectStructureProcessLinkDialogState(
            "node-1",
            "Prepare invoice",
            [
                new ProjectStructureProcessLinkOption(
                    definitionId,
                    "Invoice approval",
                    "Project",
                    "Active",
                    true)
            ],
            null,
            string.Empty);

        var cut = Render<ProjectStructureCanvasDialogs>(parameters => parameters
            .Add(component => component.ProcessLinkDialog, dialog)
            .Add(component => component.ProcessLinkSelectionChanged, id => selectedDefinitionId = id)
            .Add(component => component.CanShowLocalOpen, _ => false)
            .Add(component => component.CanOpenInPreferredApplication, _ => false)
            .Add(component => component.CanEditPreviewNode, _ => false));

        cut.Find($"[data-testid='project-structure-process-link-option-{definitionId:N}']").Click();

        Assert.Equal(definitionId, selectedDefinitionId);
    }

    [Fact]
    public void Process_link_picker_fills_dialog_width_and_uses_dialog_scroll_owner()
    {
        var definitionId = Guid.Parse("33333333-3333-3333-3333-333333333333");
        var dialog = new ProjectStructureProcessLinkDialogState(
            "node-1",
            "Prepare invoice",
            [
                new ProjectStructureProcessLinkOption(
                    definitionId,
                    "Invoice approval",
                    "Project",
                    "Active",
                    true)
            ],
            null,
            string.Empty);

        var cut = Render<ProjectStructureCanvasDialogs>(parameters => parameters
            .Add(component => component.ProcessLinkDialog, dialog)
            .Add(component => component.CanShowLocalOpen, _ => false)
            .Add(component => component.CanOpenInPreferredApplication, _ => false)
            .Add(component => component.CanEditPreviewNode, _ => false));

        var bodyStack = cut.Find(
            "[data-testid='project-structure-process-link-dialog'] .project-structure-preview-dialog__body > .rz-stack");
        var picker = cut.FindComponent<ResourceCardPicker<Guid>>();
        var results = cut.Find("[data-testid='project-structure-process-link-select-results']");

        Assert.Contains("align-items:stretch", bodyStack.GetAttribute("style"), StringComparison.Ordinal);
        Assert.False(picker.Instance.UseBoundedResultsViewport);
        Assert.Contains("resource-card-picker__results--unbounded", results.ClassList);
        Assert.DoesNotContain("resource-card-picker__results--bounded", results.ClassList);
    }

    [Fact]
    public void Workflow_add_picker_raises_workflow_id_without_dom_string_parsing()
    {
        var workflowId = new WorkflowId(Guid.Parse("44444444-4444-4444-4444-444444444444"));
        var versionId = new WorkflowVersionId(Guid.Parse("55555555-5555-5555-5555-555555555555"));
        WorkflowId? selectedWorkflowId = null;
        var dialog = new ProjectStructureWorkflowAddDialogState(
            "node-1",
            "Prepare invoice",
            [
                new ProjectStructureWorkflowDefinitionOption(
                    workflowId,
                    versionId,
                    "Invoice workflow",
                    "Creates and reviews the invoice.",
                    WorkflowLifecycleStatus.Active,
                    WorkflowRuntimeBackendKind.InProcess,
                    true,
                    string.Empty)
            ],
            null,
            null,
            ProjectStructureWorkflowInputSettings.Default(),
            new ProjectStructureWorkflowInputPreview(string.Empty, "{}", []),
            string.Empty);

        var cut = Render<ProjectStructureCanvasDialogs>(parameters => parameters
            .Add(component => component.WorkflowAddDialog, dialog)
            .Add(component => component.WorkflowAddSelectionChanged, id => selectedWorkflowId = id)
            .Add(component => component.CanShowLocalOpen, _ => false)
            .Add(component => component.CanOpenInPreferredApplication, _ => false)
            .Add(component => component.CanEditPreviewNode, _ => false));

        cut.Find($"[data-testid='project-structure-workflow-add-option-{workflowId.Value:N}']").Click();

        Assert.Equal(workflowId, selectedWorkflowId);
    }
}
