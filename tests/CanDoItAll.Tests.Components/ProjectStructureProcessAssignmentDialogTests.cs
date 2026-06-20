using Bunit;
using CanDoItAll.Components.BaseLib;
using CanDoItAll.Modules.Workbench.Pages;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;

namespace CanDoItAll.Tests.Components;

public sealed class ProjectStructureProcessAssignmentDialogTests
{
    [Fact]
    public void Assignment_dialog_renders_fullscreen_role_assignment_surface()
    {
        using var context = CreateContext();
        var assignedCandidate = CreateCandidate(
            "Release Readiness Manager",
            isSelected: true,
            isRecommended: true,
            score: "469.0 score");
        var assignedRole = CreateRole("Feature implementation manager", isResolved: true, assignedCandidate);
        var unassignedRole = CreateRole("Blazor application developer", isResolved: false);
        var state = CreateDialogState(
        [
            assignedRole,
            unassignedRole
        ]);

        var cut = context.RenderComponent<ProjectStructureProcessAssignmentDialog>(parameters => parameters
            .Add(component => component.Dialog, state));

        Assert.NotNull(cut.Find("[data-testid='project-structure-process-assignment-dialog']"));
        Assert.Contains("Assign roles for Delivery project", cut.Markup);
        Assert.Contains("1 of 2 roles assigned", cut.Markup);
        Assert.Contains("Process roles (2)", cut.Markup);
        Assert.Equal(
            "project-structure-process-assignment-role-row-all",
            cut.Find("[data-testid='project-structure-process-assignment-role-list']")
                .Children[0]
                .GetAttribute("data-testid"));
        Assert.Contains("Release Readiness Manager", cut.Markup);
        Assert.Contains("No agent assigned", cut.Markup);
        Assert.Contains("Summary review", cut.Markup);
        Assert.Empty(cut.FindAll("[data-testid='project-structure-process-assignment-feedback']"));
        Assert.Empty(cut.FindAll(".project-structure-assignment-toolbar"));
        Assert.NotNull(cut.Find($"[data-testid='project-structure-process-assignment-summary-{assignedRole.LaunchPlanRoleId:D}-{assignedCandidate.CandidateId:D}-model-badge']"));
        Assert.NotNull(cut.Find($"[data-testid='project-structure-process-assignment-summary-{assignedRole.LaunchPlanRoleId:D}-{assignedCandidate.CandidateId:D}-tools-badge']"));
        Assert.NotNull(cut.Find($"[data-testid='project-structure-process-assignment-summary-{assignedRole.LaunchPlanRoleId:D}-{assignedCandidate.CandidateId:D}-skills-badge']"));
        Assert.NotNull(cut.Find($"[data-testid='project-structure-process-assignment-summary-{assignedRole.LaunchPlanRoleId:D}-{assignedCandidate.CandidateId:D}-details-badge']"));
        Assert.NotNull(cut.Find("[data-testid='project-structure-process-assignment-selected-detail']"));
        Assert.NotNull(cut.Find("[data-testid='project-structure-process-assignment-review-start']"));
    }

    [Fact]
    public void Assignment_dialog_opens_agent_picker_dialog_for_role()
    {
        using var context = CreateContext();
        var dialogService = context.Services.GetRequiredService<DialogService>();
        var alternateCandidate = CreateCandidate(
            "Blazor Agent",
            isSelected: false,
            isRecommended: true,
            score: "388.0 score");
        var unresolvedRole = CreateRole("Blazor application developer", isResolved: false) with
        {
            DirectoryCandidates = [alternateCandidate]
        };
        var state = CreateDialogState([unresolvedRole]);
        Guid? requestedRoleId = null;

        var cut = context.RenderComponent<ProjectStructureProcessAssignmentDialog>(parameters => parameters
            .Add(component => component.Dialog, state)
            .Add(
                component => component.AssignProcessStartAgent,
                EventCallback.Factory.Create<Guid>(
                    new object(),
                    roleId => requestedRoleId = roleId)));

        cut.Find($"[data-testid='project-structure-process-assignment-assign-agent-{unresolvedRole.LaunchPlanRoleId:D}']").Click();

        cut.WaitForAssertion(() =>
        {
            Assert.Equal(unresolvedRole.LaunchPlanRoleId, requestedRoleId);
            var dialog = Assert.Single(dialogService.Dialogs);
            Assert.Equal("project-structure-process-assignment-agent-picker-dialog", dialog.Options.TestId);
            Assert.Equal(typeof(ProjectStructureProcessAgentPickerDialog), dialog.ComponentType);
            Assert.Equal(unresolvedRole, dialog.Parameters[nameof(ProjectStructureProcessAgentPickerDialog.Role)]);
        });
    }

    [Fact]
    public void Assignment_dialog_shows_role_specific_candidates_in_assignment_order()
    {
        using var context = CreateContext();
        var selectedCandidate = CreateCandidate(
            "Selected Release Manager",
            isSelected: true,
            isRecommended: true,
            score: "410.0 score");
        var higherScoreCandidate = CreateCandidate(
            ".NET Solution Architect",
            isSelected: false,
            isRecommended: true,
            score: "502.0 score");
        var lowerScoreCandidate = CreateCandidate(
            "Deployment Assistant",
            isSelected: false,
            isRecommended: true,
            score: "265.0 score");
        var role = CreateRole(
            "Feature implementation manager",
            isResolved: true,
            lowerScoreCandidate,
            selectedCandidate,
            higherScoreCandidate);
        var state = CreateDialogState([role]);
        var dialogService = context.Services.GetRequiredService<DialogService>();

        var cut = context.RenderComponent<ProjectStructureProcessAssignmentDialog>(parameters => parameters
            .Add(component => component.Dialog, state));

        cut.Find($"[data-testid='project-structure-process-assignment-role-row-{role.LaunchPlanRoleId:D}']").Click();

        var cards = cut.FindAll(".project-structure-assignment-candidate-option");
        Assert.Equal(3, cards.Count);
        Assert.Contains("Selected Release Manager", cards[0].TextContent);
        Assert.Contains(".NET Solution Architect", cards[1].TextContent);
        Assert.Contains("Deployment Assistant", cards[2].TextContent);
        Assert.NotNull(cut.Find($"[data-testid='project-structure-process-assignment-candidate-add-agent-{role.LaunchPlanRoleId:D}']"));

        cut.Find($"[data-testid='project-structure-process-assignment-candidate-add-agent-{role.LaunchPlanRoleId:D}']").Click();

        cut.WaitForAssertion(() =>
        {
            var dialog = Assert.Single(dialogService.Dialogs);
            Assert.Equal("project-structure-process-assignment-agent-picker-dialog", dialog.Options.TestId);
            Assert.Equal(typeof(ProjectStructureProcessAgentPickerDialog), dialog.ComponentType);
        });
    }

    [Fact]
    public void Assignment_dialog_selects_directory_candidate_returned_from_agent_picker()
    {
        using var context = CreateContext();
        var dialogService = context.Services.GetRequiredService<DialogService>();
        var selectedCandidate = CreateCandidate(
            "Selected Release Manager",
            isSelected: true,
            isRecommended: true,
            score: "410.0 score");
        var directoryOnlyCandidate = CreateCandidate(
            "Architecture Agent",
            isSelected: false,
            isRecommended: true,
            score: "503.0 score");
        var role = CreateRole(
            "Feature implementation manager",
            isResolved: true,
            selectedCandidate) with
        {
            DirectoryCandidates = [selectedCandidate, directoryOnlyCandidate]
        };
        var state = CreateDialogState([role]);
        ProjectStructureProcessStartCandidateSelection? selection = null;

        var cut = context.RenderComponent<ProjectStructureProcessAssignmentDialog>(parameters => parameters
            .Add(component => component.Dialog, state)
            .Add(
                component => component.SelectProcessStartCandidate,
                EventCallback.Factory.Create<ProjectStructureProcessStartCandidateSelection>(
                    new object(),
                    value => selection = value)));

        cut.Find($"[data-testid='project-structure-process-assignment-change-agent-{role.LaunchPlanRoleId:D}']").Click();
        cut.WaitForAssertion(() =>
        {
            var picker = Assert.Single(dialogService.Dialogs);
            Assert.Equal(typeof(ProjectStructureProcessAgentPickerDialog), picker.ComponentType);
        });

        dialogService.Close(directoryOnlyCandidate.CandidateId);

        cut.WaitForAssertion(() =>
        {
            var confirmation = Assert.Single(dialogService.Dialogs);
            Assert.Equal("project-structure-process-assignment-agent-switch-confirmation-dialog", confirmation.Options.TestId);
        });
        dialogService.Close(true);

        cut.WaitForAssertion(() => Assert.NotNull(selection));
        Assert.Equal(role.LaunchPlanRoleId, selection!.LaunchPlanRoleId);
        Assert.Equal(directoryOnlyCandidate.CandidateId, selection.CandidateId);
    }

    [Fact]
    public void Assignment_dialog_confirms_switch_before_double_clicked_candidate_is_selected()
    {
        using var context = CreateContext();
        var dialogService = context.Services.GetRequiredService<DialogService>();
        var selectedCandidate = CreateCandidate(
            "Selected Release Manager",
            isSelected: true,
            isRecommended: true,
            score: "410.0 score");
        var alternativeCandidate = CreateCandidate(
            ".NET Solution Architect",
            isSelected: false,
            isRecommended: true,
            score: "502.0 score");
        var role = CreateRole(
            "Feature implementation manager",
            isResolved: true,
            selectedCandidate,
            alternativeCandidate);
        var state = CreateDialogState([role]);
        ProjectStructureProcessStartCandidateSelection? selection = null;

        var cut = context.RenderComponent<ProjectStructureProcessAssignmentDialog>(parameters => parameters
            .Add(component => component.Dialog, state)
            .Add(
                component => component.SelectProcessStartCandidate,
                EventCallback.Factory.Create<ProjectStructureProcessStartCandidateSelection>(
                    new object(),
                    value => selection = value)));

        cut.Find($"[data-testid='project-structure-process-assignment-role-row-{role.LaunchPlanRoleId:D}']").Click();
        cut.Find($"[data-testid='project-structure-process-assignment-candidate-{role.LaunchPlanRoleId:D}-{alternativeCandidate.CandidateId:D}-card']")
            .TriggerEvent("ondblclick", new MouseEventArgs());

        var dialog = Assert.Single(dialogService.Dialogs);
        Assert.Equal("project-structure-process-assignment-agent-switch-confirmation-dialog", dialog.Options.TestId);
        Assert.Equal(typeof(ProjectStructureProcessAgentSwitchConfirmationDialog), dialog.ComponentType);
        Assert.Null(selection);

        dialogService.Close(true);

        cut.WaitForAssertion(() => Assert.NotNull(selection));
        Assert.Equal(role.LaunchPlanRoleId, selection!.LaunchPlanRoleId);
        Assert.Equal(alternativeCandidate.CandidateId, selection.CandidateId);
    }

    [Fact]
    public void Assignment_dialog_details_badge_opens_readonly_details_dialog()
    {
        using var context = CreateContext();
        var dialogService = context.Services.GetRequiredService<DialogService>();
        var candidate = CreateCandidate(
            "Release Readiness Manager",
            isSelected: true,
            isRecommended: true,
            score: "469.0 score");
        var role = CreateRole("Feature implementation manager", isResolved: true, candidate);
        var state = CreateDialogState([role]);

        var cut = context.RenderComponent<ProjectStructureProcessAssignmentDialog>(parameters => parameters
            .Add(component => component.Dialog, state));

        cut.Find($"[data-testid='project-structure-process-assignment-summary-{role.LaunchPlanRoleId:D}-{candidate.CandidateId:D}-details-badge']").Click();

        var dialog = Assert.Single(dialogService.Dialogs);
        Assert.Equal("project-structure-process-assignment-agent-details-dialog", dialog.Options.TestId);
        Assert.Equal(typeof(ProjectStructureProcessAgentDetailsDialog), dialog.ComponentType);
        dialogService.CloseAll();
    }

    private static TestContext CreateContext()
    {
        var context = new TestContext();
        context.Services.AddCanDoItAllBaseLib();
        return context;
    }

    private static ProjectStructureProcessStartDialogState CreateDialogState(
        IReadOnlyList<ProjectStructureProcessStartRoleState> roles)
    {
        return new ProjectStructureProcessStartDialogState(
            Guid.NewGuid(),
            Guid.NewGuid(),
            "process-node",
            "Release readiness process",
            "project-root",
            "Delivery project",
            Guid.NewGuid(),
            ProjectStructureProcessStartStage.Staffing,
            false,
            false,
            "Assign the required roles before the process can start.",
            roles,
            "HR Staffing Manager",
            DateTimeOffset.UtcNow,
            false,
            string.Empty);
    }

    private static ProjectStructureProcessStartRoleState CreateRole(
        string displayName,
        bool isResolved,
        params ProjectStructureProcessStartCandidateState[] candidates)
    {
        var selectedCandidate = candidates.FirstOrDefault(candidate => candidate.IsSelected);
        return new ProjectStructureProcessStartRoleState(
            Guid.NewGuid(),
            displayName,
            "AI agent",
            true,
            isResolved,
            false,
            selectedCandidate is null ? "No confirmed match yet." : $"{selectedCandidate.DisplayName} / AiResource",
            selectedCandidate is null ? "Manual correction is required." : "Selected: candidate is ready for approval and execution.",
            candidates)
        {
            DirectoryCandidates = candidates
        };
    }

    private static ProjectStructureProcessStartCandidateState CreateCandidate(
        string displayName,
        bool isSelected,
        bool isRecommended,
        string score)
    {
        return new ProjectStructureProcessStartCandidateState(
            Guid.NewGuid(),
            Guid.NewGuid(),
            displayName,
            "AiResource",
            "AI agent",
            score,
            isSelected,
            isRecommended,
            false,
            true,
            "Strong fit for release management.",
            "AI resource is available in the shared agent directory.",
            "crmhr-ai-agent:test",
            AgentProviderName: "OpenAI default",
            AgentModel: "gpt-5-mini",
            AgentRoleTitle: "Release specialist",
            AgentSummary: "Agent specialized in release management and deployment readiness.",
            AgentStatusLabel: "Active",
            AgentWorkloadLabel: "Programming",
            ToolNames: ["playwright-local-mcp", "workspace-files"],
            SkillNames: ["candoitall-bundle-workflow", "aspnet-core-skill"]);
    }
}
