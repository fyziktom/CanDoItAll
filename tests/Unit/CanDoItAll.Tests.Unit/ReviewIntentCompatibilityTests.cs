using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Modules.Projects;
using CanDoItAll.Modules.Workbench;
using CanDoItAll.Processes.Application;
using CanDoItAll.Processes.Core;
using CanDoItAll.Processes.Runtime;
using CanDoItAll.SharedKernel;
using static CanDoItAll.Tests.Unit.ProcessDecisionReviewTestData;

namespace CanDoItAll.Tests.Unit;

public sealed class ReviewIntentCompatibilityTests {
    [Theory]
    [InlineData(AgentPlanningReviewChoice.Approve, ReadBasisMode.RunSnapshot, "039c79acd0bc957a4666a8de1022c23569cfcb0c9ca49e1e30ec6217f4d96651")]
    [InlineData(AgentPlanningReviewChoice.Reject, ReadBasisMode.RunSnapshot, "471b33d8809d8fa58481a23e16ef77ea6cad4dbd1af51194b2046f61b0eb3707")]
    [InlineData(AgentPlanningReviewChoice.Approve, ReadBasisMode.StepSnapshot, "407830c61a8d9a20454ece1de36c4fa43999c025ba2894deb8860ec907d4922b")]
    [InlineData(AgentPlanningReviewChoice.Reject, ReadBasisMode.StepSnapshot, "d14c10b7f0054e8c3ae6b7234757663118295e0ba3661ad4ca2ba2a55a920eea")]
    public void V1_Core_intent_keeps_its_saved_digest(AgentPlanningReviewChoice choice, ReadBasisMode mode, string digest) {
        var (request, run, parent, process) = Original(choice, mode);
        var owner = ProjectTaskPlanProcessReviewService.ComputeIntent(process, parent,
            new(ProcessEventActorKind.Manager, new("manager-ž\"\\\n")));
        Verify(digest, new AgentPlanningReviewIntent(request, run, owner).Digest.Value);
    }

    [Theory]
    [InlineData(ReadBasisMode.RunSnapshot, "3e52342333eccdc41d71246a53b5704d80b9071117d5a28a5838cf4813eac785")]
    [InlineData(ReadBasisMode.StepSnapshot, "07d6fce83f6d4d4be264915590f9aee605c1424684ba777e5ef00db1b5e05902")]
    public void V1_Process_intent_keeps_its_saved_digest(ReadBasisMode mode, string digest) {
        var (_, _, parent, process) = Original(AgentPlanningReviewChoice.Approve, mode);
        Verify(digest, ProjectTaskPlanProcessReviewService.ComputeIntent(process, parent,
            new(ProcessEventActorKind.Manager, new("manager-ž\"\\\n"))).Value);
    }

    [Theory]
    [InlineData(ProcessLaunchOperatorSurface.Api, "e03926a81aad062e0abdc2fad12e3643f6de13eddf39d796a75780a225628167")]
    [InlineData(ProcessLaunchOperatorSurface.UserInterface, "88ac41b8750498b4e1edec8f48a04b363a379ac7fd1954bc0b723a72ed420677")]
    public void V1_local_manager_identity_keeps_its_saved_digest(ProcessLaunchOperatorSurface surface, string digest) {
        ProcessLaunchAuthority authority = new(new ProcessLaunchPrincipal.LocalOperator(surface), Id(16), null, true, false, "policy");
        Verify(digest, new ProcessPlanningManagerAuthority(authority, null, new Allowed()).Actor.Id.Value);
    }

    [Fact]
    public void V1_authenticated_manager_identity_survives_credential_renewal() {
        ProcessLaunchAuthority authority = new(new ProcessLaunchPrincipal.AuthenticatedOperator("manager-ž\"\\\n", Now.AddHours(1)),
            Id(16), null, true, false, "policy");
        var original = new ProcessPlanningManagerAuthority(authority, "issuer-ž", new Allowed()).Actor;
        Verify("eeed60a7fd43e90055405f72ad6d0e1658d9ce68e866075ea0f00bd99e394d44", original.Id.Value);
        Assert.Equal(original, new ProcessPlanningManagerAuthority(authority with {
            Principal = new ProcessLaunchPrincipal.AuthenticatedOperator("manager-ž\"\\\n", Now.AddHours(2))
        }, "issuer-ž", new Allowed()).Actor);
        Assert.NotEqual(original, new ProcessPlanningManagerAuthority(authority with {
            Principal = new ProcessLaunchPrincipal.AuthenticatedOperator("another-manager", Now.AddHours(1))
        }, "issuer-ž", new Allowed()).Actor);
        Assert.NotEqual(original, new ProcessPlanningManagerAuthority(authority, "another-issuer", new Allowed()).Actor);
        Assert.NotEqual(original, new ProcessPlanningManagerAuthority(authority with { DatabaseProfileId = Id(99) },
            "issuer-ž", new Allowed()).Actor);
    }

    private static (AgentInheritedPlanningReviewRequest Request, ExecutionRunRecord Run,
        ProcessDecisionChildMaterialBinding Parent, ProcessDecisionReviewRequest Process) Original(
        AgentPlanningReviewChoice choice, ReadBasisMode mode) {
        var fixture = new ProcessDecisionReviewCoordinatorFixture();
        var parent = fixture.Selection.Child!.Original;
        if (mode == ReadBasisMode.RunSnapshot) {
            parent = parent with { Scope = parent.Scope with { OwnerRunId = parent.Child.Address.RootRunId, StepInstanceId = null } };
        }
        var process = fixture.Request with {
            Decision = fixture.Request.Decision with { Revision = long.MaxValue },
            Budget = fixture.Request.Budget with { Revision = 9_007_199_254_740_993 }
        };
        var parentEnvelope = AgentToolProtocolEnvelope.Create(ProcessDecisionChildMaterialBindingCodec.Format,
            ProcessDecisionChildMaterialBindingCodec.Version, ProcessDecisionChildMaterialBindingCodec.Serialize(parent));
        var source = new AgentToolBackgroundSourceBinding("source-ž", "step\"\\\n",
            Envelope("{\"owner\":1}").Digest, Envelope("{\"execution\":1}").Digest);
        AgentToolSessionAdmission session = new(new(parent.Child.Address.ChildId.Value, Guid.Empty, default, source),
            Id(23), AgentRuntimeContextPurpose.GovernedProcessAutomation, new(Id(16), "profile-ž\"\\\n", new(7)));
        var policy = new DecisionDataPolicy(mode, PublicationFreshness.RequireCurrent, OnBasisChange.Replan,
            new(2, TimeSpan.FromMinutes(1), TimeSpan.FromMinutes(5)));
        var origin = new AgentPlanningInheritedOrigin(session.Profile, Id(17), session.AgentId, source, parentEnvelope,
            Id(24), Now.AddMinutes(-1), Envelope("{\"basis\":\"ž\"}").Digest, policy,
            new(parent.Child.BudgetId, parent.Child.AllocationId, parent.Child.Grant));
        var decision = AgentPlanningDecisionTransitions.CreateInherited(session.Reference, Envelope("{\"target\":\"ž\\\"\"}"),
            AgentPlanningOwner.ProjectTaskPlan, origin, Now) with { DecisionId = Id(25) };
        AgentToolJournalRecord journal = new(AgentToolJournalRecord.InheritedPlanningSchemaVersion, long.MaxValue, session, [], [],
            Support: AgentToolAdmissionSupport.EffectJournalOnly, BackgroundInput: new("Original input"), PlanningDecision: decision);
        ExecutionRunRecord run = new(session.Reference.ExecutionRunId, session.AgentId, null, "Review", source.SourceKind, source.SourceId,
            "", "", "", "", "{\"ordered\":[2,1],\"text\":\"ž\\\"\\\\\\n\"}", "", "", "", "",
            ExecutionState.WaitingOnTool, null, Now, Now, null, null, "", null, [],
            ProcessRunId: parent.Child.Address.RunId.ToString(), ProcessStepId: parent.Child.Address.StepInstanceId.ToString(),
            Revision: 9_007_199_254_740_993) { ToolAdmission = journal };
        AgentInheritedPlanningReviewRequest request = new(process.CompletionKey.Value, choice, AgentPlanningDecisionExpectation.From(run),
            new(new(Id(26)), new(Id(27)), 2, Envelope("{\"proposal\":\"original\"}").Digest), "approval-ž\"\\\n");
        return (request, run, parent, process);
    }

    private static AgentToolProtocolEnvelope Envelope(string json) => AgentToolProtocolEnvelope.Create("review-v1-fixture", 1, json);

    private static void Verify(string expected, string actual)
        => Assert.True(StringComparer.Ordinal.Equals(expected, actual), $"Saved v1 digest changed. Expected: {expected}; Actual: {actual}");

    private sealed class Allowed : IProjectDefinitionCurrentWriteAccess {
        public ValueTask<ProjectDefinitionWriteAccessDecision> ObserveAsync(CancellationToken cancellationToken)
            => ValueTask.FromResult(ProjectDefinitionWriteAccessDecision.Allowed);
    }
}
