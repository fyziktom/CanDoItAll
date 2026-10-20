using System.Text;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Modules.Projects;
using CanDoItAll.Modules.Workbench;
using CanDoItAll.Processes.Application;
using CanDoItAll.Processes.Contracts;
using CanDoItAll.Processes.Runtime;
using CanDoItAll.SharedKernel;
using CanDoItAll.Tests.Support;

namespace CanDoItAll.Tests.Unit;

internal sealed record ProjectTaskPlanProcessCodecFixture(ProjectTaskPlanProcessTarget Target,
    ProcessDecisionChildMaterialBinding Parent, ProcessDecisionMaterial Material,
    AgentPlanningInheritedOrigin Origin, AgentPlanningBasisRecord Basis, AgentToolJournalRecord Journal) {
    internal static readonly DateTimeOffset Start = AgentPlanningDecisionTestData.Start;
    internal const string Draft = """{"version":1,"tasks":[{"title":"Review","description":"Original task snapshot","notes":"","priority":2}]}""";
    internal AgentPlanningDecisionRecord Decision => Journal.PlanningDecision!;

    internal static ProjectTaskPlanProcessCodecFixture Create(bool step = true, ProcessDecisionMaterial? shared = null) {
        var profile = new AgentToolProfileBinding(shared?.Scope.DatabaseProfileId ?? Guid.NewGuid(), "process-task-codec", new(1));
        ProcessDecisionReference reference = new(new(Guid.NewGuid()), 1);
        ProcessDecisionChildAddress address = new(shared?.Scope.RootRunId ?? new(Guid.NewGuid()),
            shared?.Scope.OwnerRunId ?? new(Guid.NewGuid()), shared?.Scope.StepInstanceId ?? new(Guid.NewGuid()), reference,
            ProcessDecisionChildIdentity.Create(reference, ProcessDecisionChildOwner.Agent, 1));
        var scope = shared?.Scope ?? new ProcessDecisionMaterialScope(new(Guid.NewGuid()), profile.ProfileId, Guid.NewGuid(),
            address.RootRunId, step ? address.RunId : address.RootRunId, step ? address.StepInstanceId : null);
        var captured = Start.AddMinutes(-1);
        ProjectWriteAdmission project;
        if (shared is null) {
            project = new(profile.ProfileId, Guid.NewGuid(), Guid.NewGuid());
            ProjectTaskPlanBasis tasks = new(new(scope.WorkspaceNamespaceId),
                new(new(project, Guid.NewGuid()), new("Original project", "", "", ProjectStatus.Draft, "", null)), captured, []);
            shared = ProcessDecisionMaterial.FromContent(scope, new(scope.Id, 1, new(Guid.NewGuid())),
                ProjectTaskPlanProcessContract.Content, captured, Encoding.UTF8.GetString(ProjectTaskPlanBasisCodec.Encode(tasks)));
        } else {
            project = ProjectTaskPlanBasisCodec.Decode(Encoding.UTF8.GetBytes(shared.PayloadJson)).Project.Expectation.Project;
        }
        ProcessDecisionChildAdmission child = new(address, shared.Reference.MaterialId, ProcessDecisionChildOwner.Agent,
            new(Guid.NewGuid()), new(Guid.NewGuid()), new(TimeSpan.FromSeconds(30).Ticks, 0));
        ProcessDecisionChildMaterialBinding parent = new(child, scope, shared.Stamp, ProjectTaskPlanProcessContract.Execution);
        var agent = Guid.NewGuid();
        ProcessLaunchAuthority authority = new(new ProcessLaunchPrincipal.LocalOperator(ProcessLaunchOperatorSurface.UserInterface),
            profile.ProfileId, new(project.DatabaseProfileId, project.ProjectId, project.LifetimeId), true, false, "original-policy");
        ProcessExecutionDispatchAuthority source = new(new(address.ChildId.Value, agent, address.RunId, address.StepInstanceId,
            Guid.NewGuid(), "task-plan", Start, true), address.RootRunId, project.ProjectId, "original-process-owner",
            "original-readiness", [ProcessOperationContractNames.ExecuteExternalAction], project.ProjectId.ToString("D"),
            ProcessCapabilityScope.Empty, authority, new(new(Guid.NewGuid()), "original-preparation", address.RunId,
                address.StepInstanceId, "original-readiness"), true, Start);
        AgentToolBackgroundSourceBinding background = new(ProjectTaskPlanProcessContract.SourceKind, source.Evidence.StepKey,
            AgentToolProtocolEnvelope.ComputeDigest(source.OwnerFingerprint), AgentToolProtocolEnvelope.ComputeDigest("original-execution"));
        AgentToolSessionAdmission session = new(new(address.ChildId.Value, Guid.Empty, default, background), agent,
            AgentRuntimeContextPurpose.GovernedProcessAutomation, profile);
        ProjectTaskPlanCaller caller = new(ProjectTaskPlanCallerKind.ProcessAgent,
            ProjectDefinitionGovernedBindings.ProcessCaller(source).Key.ToArray());
        ProjectTaskPlanProcessTarget target = new(session, project, source, new(true, true),
            new(new(scope.WorkspaceNamespaceId), 1, caller.Bind(new(scope.WorkspaceNamespaceId)), Start.AddHours(1), Start.AddDays(1)));
        var value = AgentToolProtocolEnvelope.Create(ProjectTaskPlanProcessCodec.MaterialFormat, 1, shared.PayloadJson);
        DecisionDataPolicy policy = new(scope.ReadBasisMode, PublicationFreshness.RequireCurrent, OnBasisChange.Replan,
            new(2, TimeSpan.FromMinutes(5), TimeSpan.FromMinutes(10)));
        AgentPlanningInheritedOrigin origin = new(profile, scope.WorkspaceNamespaceId, agent, background,
            ParentEnvelope(parent), child.MaterialId.Value, captured, value.Digest, policy,
            new(child.BudgetId, child.AllocationId, child.Grant));
        var basis = ProjectTaskPlanProcessCodec.Basis(origin, shared);
        AgentToolJournalRecord journal = new(AgentToolJournalRecord.InheritedPlanningSchemaVersion, 1, session, [], [],
            Support: AgentToolAdmissionSupport.EffectJournalOnly, BackgroundInput: new("Prepare a bounded task plan."),
            PlanningDecision: AgentPlanningDecisionTransitions.CreateInherited(session.Reference, TargetEnvelope(target),
                AgentPlanningOwner.ProjectTaskPlan, origin, Start));
        journal = AgentPlanningDecisionTestData.Enter(AgentPlanningDecisionTestData.Attach(
            AgentPlanningDecisionTestData.Begin(journal), basis));
        return new(target, parent, shared, origin, basis, journal);
    }

    internal static AgentToolProtocolEnvelope TargetEnvelope(ProjectTaskPlanProcessTarget target)
        => ProjectTaskPlanProcessCodec.WriteTarget(target);

    internal static AgentToolProtocolEnvelope ParentEnvelope(ProcessDecisionChildMaterialBinding parent)
        => AgentToolProtocolEnvelope.Create(ProcessDecisionChildMaterialBindingCodec.Format,
            ProcessDecisionChildMaterialBindingCodec.Version, ProcessDecisionChildMaterialBindingCodec.Serialize(parent));

    internal (AgentPlanningDecisionObservation Observation, AgentToolPreparedPayload Payload, AgentToolApprovalBinding Binding) Prepare() {
        var prepared = ProjectTaskPlanProcessCodec.Prepare(Decision, Basis, Draft);
        var step = Decision.Current.Steps[^1];
        var dispatch = Journal.ProviderDispatches.Single(item => item.Id == step.ProviderDispatchId);
        var payload = prepared.Payload;
        var proposal = new AgentToolProposalRecord(new(Guid.NewGuid()), 0, prepared.CallId, payload, true,
            AgentToolProposalState.Prepared, ExecutionApprovalStatus.Pending, null);
        var batch = new AgentToolBatchRecord(new(Guid.NewGuid()), Journal.Batches.Length, dispatch.RequestDigest,
            AgentPlanningDecisionTestData.Envelope("{\"response\":true}"), [proposal], dispatch.Id);
        var binding = new AgentToolApprovalBinding(batch.Id, proposal.IntentId, payload.SemanticVersion, payload.Digest);
        var journal = AgentPlanningDecisionTestData.Changed(Journal, Journal with {
            Batches = Journal.Batches.Add(batch),
            ProviderDispatches = Journal.ProviderDispatches.SetItem(dispatch.Ordinal, dispatch with {
                State = AgentToolProviderDispatchState.ResponseAdmitted, ResponseBatchId = batch.Id
            }),
            PlanningDecision = AgentPlanningDecisionTransitions.AdmitResponse(Decision, step.Id, batch.Id,
                [new(binding, Basis.Id, Basis.Value.Digest, payload.SourcePreparation!)], Start)
        });
        journal = AgentPlanningDecisionTestData.AwaitApproval(journal, TimeSpan.FromSeconds(1));
        ExecutionRunRecord run = new(journal.Session.Reference.ExecutionRunId, journal.Session.AgentId, null,
            "Inherited task-plan codec", ProjectTaskPlanProcessContract.SourceKind, Target.Source.Evidence.StepKey,
            "", "", "", "", "{}", "", "", "", "", ExecutionState.WaitingOnTool, null,
            Start, Start.AddSeconds(1), Start, null, "", null, []) { ToolAdmission = journal };
        return (AgentPlanningDecisionObservation.From(run)!, payload, binding);
    }
}
