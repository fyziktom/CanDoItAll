using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Modules.Workbench;
using CanDoItAll.Processes.Application;
using CanDoItAll.Processes.Drivers.Abstractions;
using CanDoItAll.Processes.Runtime;
using CanDoItAll.SharedKernel;
using CanDoItAll.Tests.Support;

namespace CanDoItAll.Tests.Unit;

public sealed class ProjectTaskPlanProcessStartupOutcomeTests {
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Closed_startup_keeps_actual_overrun_and_has_no_effect(bool cancelled) {
        var (selected, request, profile) = Create();
        var start = ProjectTaskPlanProcessCodecFixture.Start;
        var work = selected.Original.Child.Grant.WorkTicks + TimeSpan.TicksPerSecond;
        var evidence = new ProcessExecutionOwnerEvidence<AgentInheritedPlanningPreparationFailure>(
            new(request, profile, start, start.AddTicks(work), work, cancelled));
        var result = ProjectTaskPlanProcessStartupOutcome.Read(selected, evidence, profile);
        Assert.Equal(cancelled ? ProcessDecisionChildTerminalStatus.Cancelled : ProcessDecisionChildTerminalStatus.Failed, result.Status);
        Assert.Equal(new ProcessDecisionChildClosure(1, ProcessDecisionChildOutcome.NoEffect, null), result.Closure);
        Assert.Equal(1, result.Work.OwnerWorkRevision);
        Assert.Equal(new DecisionBudgetAmount(work, 0), result.Work.Consumed);
    }

    [Theory]
    [InlineData(Substitution.Agent)]
    [InlineData(Substitution.Parent)]
    [InlineData(Substitution.Allocation)]
    [InlineData(Substitution.Child)]
    [InlineData(Substitution.Owner)]
    [InlineData(Substitution.Scope)]
    [InlineData(Substitution.Project)]
    [InlineData(Substitution.Profile)]
    [InlineData(Substitution.Generation)]
    [InlineData(Substitution.Wrapper)]
    [InlineData(Substitution.Start)]
    [InlineData(Substitution.Closed)]
    [InlineData(Substitution.Work)]
    public void Unrelated_identity_or_invalid_measurement_cannot_supply_a_settlement(Substitution change) {
        var (selected, request, profile) = Create();
        request = change switch {
            Substitution.Agent => request with { AgentId = Guid.NewGuid() },
            Substitution.Parent => request with { ParentBinding = AgentPlanningDecisionTestData.Envelope() },
            Substitution.Allocation => request with { Start = request.Start with {
                Allocation = request.Start.Allocation with { Id = new(Guid.NewGuid()) }
            } },
            Substitution.Child => request with { ExecutionRunId = Guid.NewGuid() },
            Substitution.Owner => request with { Owner = default },
            Substitution.Scope => request with { ContextScope = WorkspaceScopeDescriptor.Project(Guid.NewGuid().ToString("D")) },
            _ => request
        };
        var start = ProjectTaskPlanProcessCodecFixture.Start;
        var closed = start.AddSeconds(2);
        var work = TimeSpan.FromSeconds(2).Ticks;
        var failure = new AgentInheritedPlanningPreparationFailure(request, profile,
            change == Substitution.Start ? default : start,
            change == Substitution.Closed ? start.AddSeconds(-1) : closed,
            change == Substitution.Work ? work - 1 : work, false);
        ProcessExecutionOwnerEvidence evidence = change == Substitution.Wrapper
            ? new ProcessExecutionOwnerEvidence<string>("untrusted")
            : new ProcessExecutionOwnerEvidence<AgentInheritedPlanningPreparationFailure>(failure);
        if (change == Substitution.Project) {
            var project = selected.OriginalProject!;
            selected = selected with { OriginalProject = new(project.DatabaseProfileId, Guid.NewGuid(), project.LifetimeId) };
        } else if (change == Substitution.Profile) {
            profile = new(Guid.NewGuid(), profile.Fingerprint, profile.Generation);
        } else if (change == Substitution.Generation) {
            profile = new(profile.ProfileId, profile.Fingerprint, new(profile.Generation.Value + 1));
        }
        Assert.Throws<InvalidDataException>(() => ProjectTaskPlanProcessStartupOutcome.Read(selected, evidence, profile));
    }

    public enum Substitution { Agent, Parent, Allocation, Child, Owner, Scope, Project, Profile, Generation, Wrapper, Start, Closed, Work }

    private static (ProcessDecisionResultChild Selected, AgentInheritedPlanningWorkspaceRequest Request, AgentToolProfileBinding Profile) Create() {
        var fixture = ProjectTaskPlanProcessCodecFixture.Create();
        var child = fixture.Parent.Child;
        var project = fixture.Target.Project;
        var profile = fixture.Target.Session.Profile;
        ProcessDecisionGeneration head = new(child.Address.Reference, 2, ProcessDecisionPhase.Active, child.MaterialId, child.BudgetId, 1, 1);
        ProcessDecisionResultChild selected = new(fixture.Parent, new(child, head, null, null, true)) {
            OriginalAgentId = fixture.Target.Session.AgentId,
            OriginalProject = new(project.DatabaseProfileId, project.ProjectId, project.LifetimeId)
        };
        AgentInheritedPlanningWorkspaceRequest request = new(child.Address.ChildId.Value,
            new(new(child.BudgetId, child.AllocationId, child.Grant), AgentInheritedPlanningStartMode.FreshEnrollment),
            fixture.Target.Session.AgentId, "Original task plan", AgentExecutionOperationId.New(), ExecutionInvocationContext.Empty,
            AgentPlanningOwner.ProjectTaskPlan, WorkspaceScopeDescriptor.Project(project.ProjectId.ToString("D")),
            ProjectTaskPlanProcessCodecFixture.ParentEnvelope(fixture.Parent),
            (_, _) => throw new InvalidOperationException("Pure outcome verification must not prepare a workspace."),
            _ => throw new InvalidOperationException("Pure outcome verification must not read live source."));
        return (selected, request, profile);
    }
}
