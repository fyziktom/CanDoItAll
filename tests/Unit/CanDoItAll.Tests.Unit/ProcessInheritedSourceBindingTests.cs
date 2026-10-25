using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Modules.Processes;
using CanDoItAll.Processes.Application;
using CanDoItAll.Processes.Runtime;
using CanDoItAll.SharedKernel;

namespace CanDoItAll.Tests.Unit;

public sealed class ProcessInheritedSourceBindingTests {
    [Fact]
    public void Initial_and_saved_interpretation_keep_the_same_exact_parent_and_registered_content() {
        var fixture = ProjectTaskPlanProcessCodecFixture.Create();
        var run = Run(fixture);
        var initial = ProcessInheritedSourceBinding.ReadInitial(run with { ToolAdmission = null }, fixture.Origin.Profile,
            fixture.Target.Source.Evidence, fixture.Origin.ParentBinding, Contracts(fixture));
        var saved = ProcessInheritedSourceBinding.Read(run, fixture.Target.Source.Evidence, Contracts(fixture));
        Assert.Equal(fixture.Parent, initial.Parent);
        Assert.Equal(fixture.Material.ContentContract, initial.Content);
        Assert.Equal(initial, saved);
        ProcessInheritedSourceBinding.RequireOriginalPolicy(fixture.Origin,
            new(ProcessExecutionAuthorityDisposition.Bound, fixture.Target.Source) { OriginalDecisionPolicy = fixture.Origin.ParentPolicy });
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Unknown_format_or_unregistered_contract_cannot_be_interpreted(bool format) {
        var fixture = ProjectTaskPlanProcessCodecFixture.Create();
        var envelope = format ? AgentToolProtocolEnvelope.Create("unsupported-parent", 1, fixture.Origin.ParentBinding.PayloadJson)
            : fixture.Origin.ParentBinding;
        Assert.Throws<NotSupportedException>(() => ProcessInheritedSourceBinding.ReadInitial(Run(fixture),
            fixture.Origin.Profile, fixture.Target.Source.Evidence, envelope, format ? Contracts(fixture) : new Dictionary<ProcessDecisionExecutionContract, ProcessDecisionContentContract?>()));
    }

    [Theory]
    [InlineData(Identity.Execution)]
    [InlineData(Identity.Agent)]
    [InlineData(Identity.SourceKind)]
    [InlineData(Identity.SourceId)]
    [InlineData(Identity.ProcessRun)]
    [InlineData(Identity.ProcessStep)]
    [InlineData(Identity.Profile)]
    public void Initial_binding_cannot_cross_child_profile_source_or_process_evidence(Identity identity) {
        var fixture = ProjectTaskPlanProcessCodecFixture.Create();
        var run = Run(fixture);
        var evidence = fixture.Target.Source.Evidence;
        var profile = fixture.Origin.Profile;
        switch (identity) {
            case Identity.Execution:
                run = run with { Id = Guid.NewGuid() };
                break;
            case Identity.Agent:
                run = run with { AgentId = Guid.NewGuid() };
                break;
            case Identity.SourceKind:
                run = run with { SourceKind = "different-source" };
                break;
            case Identity.SourceId:
                run = run with { SourceId = "different-step" };
                break;
            case Identity.ProcessRun:
                evidence = evidence with { RunId = new(Guid.NewGuid()) };
                break;
            case Identity.ProcessStep:
                evidence = evidence with { StepInstanceId = new(Guid.NewGuid()) };
                break;
            case Identity.Profile:
                profile = new(Guid.NewGuid(), profile.Fingerprint, profile.Generation);
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(identity));
        }
        Assert.Throws<AgentToolReceiptAccessDeniedException>(() => ProcessInheritedSourceBinding.ReadInitial(run,
            profile, evidence, fixture.Origin.ParentBinding, Contracts(fixture)));
    }

    [Theory]
    [InlineData(PolicyChange.Missing)]
    [InlineData(PolicyChange.Freshness)]
    [InlineData(PolicyChange.Response)]
    [InlineData(PolicyChange.Replans)]
    [InlineData(PolicyChange.Duration)]
    [InlineData(PolicyChange.Age)]
    public void Full_parent_policy_must_equal_the_original_policy_observed_by_its_owner(PolicyChange change) {
        var fixture = ProjectTaskPlanProcessCodecFixture.Create();
        var original = fixture.Origin.ParentPolicy;
        var budget = original.Budget;
        var observed = change == PolicyChange.Missing ? null : new DecisionDataPolicy(original.ReadBasisMode,
            change == PolicyChange.Freshness ? PublicationFreshness.PinnedObservation : original.PublicationFreshness,
            change == PolicyChange.Response ? OnBasisChange.PauseForReview : original.OnBasisChange,
            new(change == PolicyChange.Response ? 0 : change == PolicyChange.Replans ? budget.MaximumReplans + 1 : budget.MaximumReplans,
                change == PolicyChange.Duration ? budget.MaximumWorkDuration.Add(TimeSpan.FromSeconds(1)) : budget.MaximumWorkDuration,
                change == PolicyChange.Age ? budget.MaximumBasisAge.Add(TimeSpan.FromSeconds(1)) : budget.MaximumBasisAge));
        Assert.Throws<AgentToolReceiptAccessDeniedException>(() => ProcessInheritedSourceBinding.RequireOriginalPolicy(fixture.Origin,
            new(ProcessExecutionAuthorityDisposition.Bound, fixture.Target.Source) { OriginalDecisionPolicy = observed }));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Original_source_and_receipt_reads_require_the_exact_child_response_grant(bool delegated) {
        var fixture = ProjectTaskPlanProcessCodecFixture.Create();
        var policy = new DecisionDataPolicy(fixture.Origin.ParentPolicy.ReadBasisMode, PublicationFreshness.RequireCurrent,
            OnBasisChange.Fail, new(0, TimeSpan.FromMinutes(5), TimeSpan.FromMinutes(10)));
        var origin = fixture.Origin with { ParentPolicy = policy, ChildOnBasisChange = delegated ? OnBasisChange.Fail : null };
        var original = new ProcessExecutionDispatchAuthorityResult(ProcessExecutionAuthorityDisposition.Bound,
            fixture.Target.Source with { ObservedCurrentDispatch = false }) {
                OriginalDecisionPolicy = policy, OriginalChildOnBasisChange = origin.ChildOnBasisChange
            };
        ProcessInheritedSourceBinding.RequireOriginalPolicy(origin, original);
        fixture.Target.RequireOriginalReceiptSource(original, origin);
        var changed = original with { OriginalChildOnBasisChange = delegated ? null : OnBasisChange.Fail };
        Assert.Throws<AgentToolReceiptAccessDeniedException>(() => ProcessInheritedSourceBinding.RequireOriginalPolicy(origin, changed));
        Assert.Throws<InvalidDataException>(() => fixture.Target.RequireOriginalReceiptSource(changed, origin));
    }

    private static Dictionary<ProcessDecisionExecutionContract, ProcessDecisionContentContract?> Contracts(ProjectTaskPlanProcessCodecFixture fixture)
        => new() { [fixture.Parent.ExecutionContract] = fixture.Material.ContentContract };

    private static ExecutionRunRecord Run(ProjectTaskPlanProcessCodecFixture fixture) {
        var session = fixture.Journal.Session;
        var source = fixture.Target.Source.Evidence;
        return new(session.Reference.ExecutionRunId, session.AgentId, null,
            "Inherited source", session.Reference.BackgroundSource!.SourceKind, source.StepKey,
            "", "", "", "", "{}", "", "", "", "", ExecutionState.Preparing, null,
            ProjectTaskPlanProcessCodecFixture.Start, ProjectTaskPlanProcessCodecFixture.Start, null, null, "", null, []) {
            ToolAdmission = fixture.Journal
        };
    }

    public enum Identity { Execution, Agent, SourceKind, SourceId, ProcessRun, ProcessStep, Profile }
    public enum PolicyChange { Missing, Freshness, Response, Replans, Duration, Age }
}
