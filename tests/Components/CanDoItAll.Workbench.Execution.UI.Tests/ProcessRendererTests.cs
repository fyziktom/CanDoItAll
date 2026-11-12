using Bunit;
using CanDoItAll.Components.BaseLib;
using CanDoItAll.Modules.Workbench.Pages;
using CanDoItAll.Workbench.Execution.UI.Processes;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;

namespace CanDoItAll.Tests.Components.WorkbenchExecution;

public sealed class ProcessRendererTests {
    [Fact]
    public async Task Accepted_review_keeps_readonly_agent_details_available() {
        using var context = Context();
        var host = context.Render<DialogHost>();
        var cut = context.Render<ProcessAssignmentDialog>(p => p.Add(c => c.Dialog, View() with { ReviewPhase = ProcessReviewPhase.Accepted }));
        Assert.Contains("Open accepted run", cut.Markup, StringComparison.Ordinal);
        await cut.Find(".project-structure-assignment-profile-button").ClickAsync(new MouseEventArgs());
        host.WaitForAssertion(() => Assert.Single(host.FindComponents<ProjectStructureProcessAgentDetailsDialog>()));
    }

    [Fact]
    public async Task Actual_picker_details_and_switch_keep_typed_candidate_identity() {
        using var context = Context();
        var view = View();
        var role = view.Roles[0];
        var next = role.DirectoryCandidates[1];
        var service = context.Services.GetRequiredService<DialogService>();
        var host = context.Render<DialogHost>();
        ProjectStructureProcessStartCandidateSelection? selected = null;
        var cut = context.Render<ProcessAssignmentDialog>(p => p.Add(c => c.Dialog, view)
            .Add(c => c.SelectProcessStartCandidate, EventCallback.Factory.Create<ProjectStructureProcessStartCandidateSelection>(
                new object(), value => selected = value)));
        await cut.Find($"[data-testid='project-structure-process-assignment-change-agent-{role.LaunchPlanRoleId:D}']").ClickAsync(new MouseEventArgs());
        host.WaitForAssertion(() => Assert.Single(host.FindComponents<ProjectStructureProcessAgentPickerDialog>()));
        var picker = host.FindComponent<ProjectStructureProcessAgentPickerDialog>();
        var detailsButton = picker.FindComponents<Button>().First(button => button.Instance.Text == "Details");
        await picker.InvokeAsync(() => detailsButton.Instance.Click.InvokeAsync());
        host.WaitForAssertion(() => Assert.Single(host.FindComponents<ProjectStructureProcessAgentDetailsDialog>()));
        Assert.Contains("Friendly provider", host.Markup, StringComparison.Ordinal);
        Assert.Contains("Friendly model", host.Markup, StringComparison.Ordinal);
        var details = service.Dialogs.Last();
        await host.InvokeAsync(() => details.CloseAsync());
        var selectButton = picker.FindComponents<Button>().Single(button => button.Instance.Text == "Select agent");
        await picker.InvokeAsync(() => selectButton.Instance.Click.InvokeAsync());
        host.WaitForAssertion(() => Assert.Single(host.FindComponents<ProjectStructureProcessAgentSwitchConfirmationDialog>()));
        var confirmation = host.FindComponent<ProjectStructureProcessAgentSwitchConfirmationDialog>();
        Assert.Equal(new ProcessCandidateChoice(view.OpeningId, role.LaunchPlanRoleId, next.CandidateId,
            role.Candidates[0].CandidateId), confirmation.Instance.Choice);
        await confirmation.Find("[data-testid='project-structure-process-assignment-agent-switch-confirm']").ClickAsync(new MouseEventArgs());
        cut.WaitForAssertion(() => Assert.Equal(new(role.LaunchPlanRoleId, next.CandidateId), selected));
        Assert.Empty(service.Dialogs);
    }

    [Fact]
    public async Task Cancelled_switch_and_changed_role_snapshot_never_select() {
        using var context = Context();
        var view = View();
        var role = view.Roles[0];
        var next = role.Candidates[1];
        var service = context.Services.GetRequiredService<DialogService>();
        var host = context.Render<DialogHost>();
        var calls = 0;
        var cut = context.Render<ProcessAssignmentDialog>(p => p.Add(c => c.Dialog, view)
            .Add(c => c.SelectProcessStartCandidate, EventCallback.Factory.Create<ProjectStructureProcessStartCandidateSelection>(
                new object(), _ => calls++)));
        await cut.Find($"[data-testid='project-structure-process-assignment-role-row-{role.LaunchPlanRoleId:D}']").ClickAsync(new MouseEventArgs());
        async Task OpenSwitch() {
            await cut.Find($"[data-testid='project-structure-process-assignment-select-candidate-{role.LaunchPlanRoleId:D}-{next.CandidateId:D}']").ClickAsync(new MouseEventArgs());
            host.WaitForAssertion(() => Assert.Single(host.FindComponents<ProjectStructureProcessAgentSwitchConfirmationDialog>()));
        }
        await OpenSwitch();
        await host.Find("[data-testid='project-structure-process-assignment-agent-switch-cancel']").ClickAsync(new MouseEventArgs());
        cut.WaitForAssertion(() => Assert.Empty(service.Dialogs));
        await OpenSwitch();
        var originalChild = Assert.Single(service.Dialogs);
        var choice = originalChild.Parameters[nameof(ProjectStructureProcessAgentSwitchConfirmationDialog.Choice)];
        cut.Render(p => p.Add(c => c.Dialog, view with { Roles = [role with { ReadinessSummary = "Review changed" }] }));
        await cut.InvokeAsync(() => originalChild.CloseAsync(choice));
        await cut.InvokeAsync(() => Task.CompletedTask);
        Assert.Equal(0, calls);
    }

    [Fact]
    public async Task Retiring_one_view_cancels_only_its_children() {
        using var context = Context();
        var service = context.Services.GetRequiredService<DialogService>();
        var first = View();
        var second = View();
        var a = context.Render<ProcessAssignmentDialog>(p => p.Add(c => c.Dialog, first));
        var b = context.Render<ProcessAssignmentDialog>(p => p.Add(c => c.Dialog, second));
        await a.Find($"[data-testid='project-structure-process-assignment-change-agent-{first.Roles[0].LaunchPlanRoleId:D}']").ClickAsync(new MouseEventArgs());
        await b.Find($"[data-testid='project-structure-process-assignment-change-agent-{second.Roles[0].LaunchPlanRoleId:D}']").ClickAsync(new MouseEventArgs());
        Assert.Equal(2, service.Dialogs.Count);
        var oldChild = service.Dialogs[0];
        var otherChild = service.Dialogs[1];
        a.Render(p => p.Add(c => c.Dialog, first with { OpeningId = Guid.NewGuid() }));
        await a.InvokeAsync(() => oldChild.CloseAsync());
        Assert.Same(otherChild, Assert.Single(service.Dialogs));
        Assert.Equal(second.OpeningId, otherChild.Parameters[nameof(ProjectStructureProcessAgentPickerDialog.OpeningId)]);
    }

    private static BunitContext Context() {
        var context = new BunitContext();
        context.JSInterop.Mode = JSRuntimeMode.Loose;
        context.Services.AddCanDoItAllBaseLib();
        return context;
    }

    private static ProcessStartView View() {
        ProjectStructureProcessStartCandidateState Candidate(string name, bool selected) => new(Guid.NewGuid(), Guid.NewGuid(), name,
            "Existing", "AI agent", "4.0 score", selected, selected, false, true, "Reviewed fit", "Available", "source-registry",
            "Friendly provider", "Friendly model", ToolNames: ["Read file"], SkillNames: ["Review"], MatchScore: 40);
        var candidates = new[] { Candidate("Current candidate", true), Candidate("Next candidate", false) };
        var role = new ProjectStructureProcessStartRoleState(Guid.NewGuid(), "Reviewer", "AI agent", true, true, false,
            "Selected", "Ready", candidates) { DirectoryCandidates = candidates };
        return new(Guid.NewGuid(), "Assignment review", "Original plan", "Continue", Guid.NewGuid(), "node", null, "node", "Target",
            Guid.NewGuid(), false, false, string.Empty, [role], "HR manager", string.Empty, false, null, string.Empty,
            ProcessReviewPhase.Unprepared, false);
    }
}
