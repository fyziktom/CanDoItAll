using Bunit;
using CanDoItAll.Components.BaseLib;
using CanDoItAll.Modules.Workbench.Pages;
using Microsoft.AspNetCore.Components;

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
        var state = CreateDialogState(
        [
            CreateRole("Feature implementation manager", isResolved: true, assignedCandidate),
            CreateRole("Blazor application developer", isResolved: false)
        ]);

        var cut = context.RenderComponent<ProjectStructureProcessAssignmentDialog>(parameters => parameters
            .Add(component => component.Dialog, state));

        Assert.NotNull(cut.Find("[data-testid='project-structure-process-assignment-dialog']"));
        Assert.Contains("Assign AI agents to process roles", cut.Markup);
        Assert.Contains("1 of 2 roles assigned", cut.Markup);
        Assert.Contains("Process roles (2)", cut.Markup);
        Assert.Contains("Release Readiness Manager", cut.Markup);
        Assert.Contains("No agent assigned", cut.Markup);
        Assert.NotNull(cut.Find("[data-testid='project-structure-process-assignment-selected-detail']"));
        Assert.NotNull(cut.Find("[data-testid='project-structure-process-assignment-review-start']"));
    }

    [Fact]
    public void Assignment_dialog_invokes_manual_agent_assignment_for_role()
    {
        using var context = CreateContext();
        var unresolvedRole = CreateRole("Blazor application developer", isResolved: false);
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

        Assert.Equal(unresolvedRole.LaunchPlanRoleId, requestedRoleId);
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
            candidates);
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
            "crmhr-ai-agent:test");
    }
}
