using Bunit;
using CanDoItAll.Components.BaseLib;
using CanDoItAll.Modules.Workbench.Pages;
using CanDoItAll.Workbench.Execution.UI.Processes;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;

namespace CanDoItAll.Tests.Components.ProjectStructure;

public sealed partial class ProjectStructureProcessAssignmentDialogTests {
    [Fact]
    public async Task Retired_picker_cannot_select_in_a_successor_opening() {
        using var context = CreateContext();
        var service = context.Services.GetRequiredService<DialogService>();
        var candidate = CreateCandidate("Original candidate", false, true, "4.0 score");
        var role = CreateRole("Original role", false) with { DirectoryCandidates = [candidate] };
        var original = CreateDialogState([role]);
        var calls = 0;
        var cut = context.Render<ProjectStructureProcessAssignmentDialog>(parameters => parameters
            .Add(component => component.Dialog, original)
            .Add(component => component.SelectProcessStartCandidate,
                EventCallback.Factory.Create<ProjectStructureProcessStartCandidateSelection>(new object(), _ => calls++)));
        cut.Find($"[data-testid='project-structure-process-assignment-assign-agent-{role.LaunchPlanRoleId:D}']").Click();
        cut.WaitForAssertion(() => Assert.Single(service.Dialogs));
        var originalChild = Assert.Single(service.Dialogs);
        cut.Render(parameters => parameters.Add(component => component.Dialog, original with { DialogId = Guid.NewGuid() }));
        await cut.InvokeAsync(() => originalChild.CloseAsync(new ProcessCandidateChoice(original.DialogId,
            role.LaunchPlanRoleId, candidate.CandidateId, null)));
        await cut.InvokeAsync(() => Task.CompletedTask);
        Assert.Equal(0, calls);
    }

    [Fact]
    public async Task Retired_switch_cannot_confirm_a_successor_assignment() {
        using var context = CreateContext();
        var service = context.Services.GetRequiredService<DialogService>();
        var selected = CreateCandidate("Current", true, true, "4.0 score");
        var next = CreateCandidate("Next", false, false, "3.0 score");
        var role = CreateRole("Original role", true, selected, next);
        var original = CreateDialogState([role]);
        var calls = 0;
        var cut = context.Render<ProjectStructureProcessAssignmentDialog>(parameters => parameters
            .Add(component => component.Dialog, original)
            .Add(component => component.SelectProcessStartCandidate,
                EventCallback.Factory.Create<ProjectStructureProcessStartCandidateSelection>(new object(), _ => calls++)));
        cut.Find($"[data-testid='project-structure-process-assignment-role-row-{role.LaunchPlanRoleId:D}']").Click();
        cut.Find($"[data-testid='project-structure-process-assignment-select-candidate-{role.LaunchPlanRoleId:D}-{next.CandidateId:D}']").Click();
        cut.WaitForAssertion(() => Assert.Single(service.Dialogs));
        var originalChild = Assert.Single(service.Dialogs);
        cut.Render(parameters => parameters.Add(component => component.Dialog, original with { DialogId = Guid.NewGuid() }));
        await cut.InvokeAsync(() => originalChild.CloseAsync(originalChild.Parameters[nameof(ProjectStructureProcessAgentSwitchConfirmationDialog.Choice)]));
        await cut.InvokeAsync(() => Task.CompletedTask);
        Assert.Equal(0, calls);
    }

    [Fact]
    public void Repeated_picker_dispatch_has_one_owned_child() {
        using var context = CreateContext();
        var service = context.Services.GetRequiredService<DialogService>();
        var role = CreateRole("Original role", false);
        var cut = context.Render<ProjectStructureProcessAssignmentDialog>(parameters => parameters
            .Add(component => component.Dialog, CreateDialogState([role])));
        var assign = cut.Find($"[data-testid='project-structure-process-assignment-assign-agent-{role.LaunchPlanRoleId:D}']");
        assign.Click();
        cut.Find($"[data-testid='project-structure-process-assignment-candidate-add-agent-{role.LaunchPlanRoleId:D}']").Click();
        cut.WaitForAssertion(() => Assert.Single(service.Dialogs));
    }
}
