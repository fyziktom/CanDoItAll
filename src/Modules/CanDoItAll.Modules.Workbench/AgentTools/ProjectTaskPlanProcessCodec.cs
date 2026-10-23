using System.Text;
using System.Text.Json;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Modules.Projects;
using CanDoItAll.Processes.Application;
using CanDoItAll.Processes.Contracts;
using CanDoItAll.Processes.Runtime;

namespace CanDoItAll.Modules.Workbench;

internal static class ProjectTaskPlanProcessContract {
    internal const string SourceKind = "process-step";
    internal const string ProcessRequester = "process-runtime";
    internal const string SystemRequesterKind = "system";
    internal static ProcessDecisionExecutionContract Execution { get; } = new(
        new("a8a1bce7-dfbd-40a2-8b5c-6c15f1558790"), 1, ProcessDecisionChildOwner.Agent);
    internal static ProcessDecisionContentContract Content { get; } = new(new("53294440-a9fa-4964-af95-96605ec88217"), 1);
}

internal sealed record ProjectTaskPlanExecutorCeiling(bool CanUseTools, bool CanWriteTasks);
internal sealed record ProjectTaskPlanProcessTarget(AgentToolSessionAdmission Session, ProjectWriteAdmission Project,
    ProcessExecutionDispatchAuthority Source, ProjectTaskPlanExecutorCeiling Executor, ProjectTaskPlanWriteContext WriteContext) {
    internal ProjectTaskPlanCaller Caller => new(ProjectTaskPlanCallerKind.ProcessAgent,
        ProjectDefinitionGovernedBindings.ProcessCaller(Source).Key.ToArray());

    internal void RequireOriginalReceiptSource(ProcessExecutionDispatchAuthorityResult result, AgentPlanningInheritedOrigin original) {
        if (result.OriginalDecisionPolicy != original.ParentPolicy ||
                result.Snapshot is not { ObservedCurrentDispatch: false, SourceAuthority: { } authority } current ||
                current.Evidence != Source.Evidence || current.OwnerFingerprint != Source.OwnerFingerprint ||
                current.RootRunId != Source.RootRunId || current.ProjectReference != Source.ProjectReference ||
                JsonSerializer.Serialize(authority) != JsonSerializer.Serialize(Source.SourceAuthority)) {
            throw new InvalidDataException("The retained child differs from its original accepted Process source.");
        }
    }
}
internal sealed record ProjectTaskPlanProcessCommand(ProjectTaskPlanProcessTarget Target, ProjectTaskPlanCommand Command);

internal static class ProjectTaskPlanProcessCodec {
    internal const string TargetFormat = "workbench-task-plan-process-target";
    internal const string MaterialFormat = "workbench-task-plan-process-material";

    internal static AgentToolProtocolEnvelope WriteTarget(ProjectTaskPlanProcessTarget target) {
        var session = target.Session;
        if (session.Purpose != AgentRuntimeContextPurpose.GovernedProcessAutomation ||
            session.Reference.BackgroundSource is not { } source || session.Reference.ChatSessionId != Guid.Empty ||
            session.Reference.AuthorityId.Value != Guid.Empty) {
            throw ProjectTaskPlanAgentCodec.Denied("An inherited task-plan target requires its original background session.");
        }
        return ProjectTaskPlanAgentCodec.Write(TargetFormat, new TargetWire(
            new(session.Reference.ExecutionRunId, session.AgentId, session.Profile, source), target.Project,
            target.Source, target.Executor, target.WriteContext), AgentToolPreparedPayload.MaximumSourcePreparationUtf8Bytes);
    }

    internal static ProcessDecisionChildMaterialBinding Parent(AgentPlanningInheritedOrigin original) {
        ArgumentNullException.ThrowIfNull(original);
        _ = original.ResolvePolicy();
        var envelope = original.ParentBinding;
        if (envelope.Format != ProcessDecisionChildMaterialBindingCodec.Format || envelope.Version != ProcessDecisionChildMaterialBindingCodec.Version) {
            throw ProjectTaskPlanAgentCodec.Denied("The inherited task plan requires its canonical Process child binding.");
        }
        var binding = ProcessDecisionChildMaterialBindingCodec.Deserialize(envelope.PayloadJson);
        if (binding.ExecutionContract != ProjectTaskPlanProcessContract.Execution ||
            binding.Scope.DatabaseProfileId != original.Profile.ProfileId || binding.Scope.WorkspaceNamespaceId != original.WorkspaceNamespaceId ||
            binding.Scope.ReadBasisMode != original.ParentPolicy.ReadBasisMode || original.BasisId != binding.Child.MaterialId.Value ||
            original.Source.SourceKind != ProjectTaskPlanProcessContract.SourceKind ||
            binding.Child.BudgetId != original.Allocation.BudgetId || binding.Child.AllocationId != original.Allocation.Id ||
            binding.Child.Grant != original.Allocation.Grant) {
            throw ProjectTaskPlanAgentCodec.Denied("The inherited task plan differs from its original Process child, material scope or grant.");
        }
        return binding;
    }

    internal static ProjectTaskPlanProcessTarget Target(AgentPlanningDecisionRecord decision) {
        if (decision.Owner != AgentPlanningOwner.ProjectTaskPlan || decision.Provenance is not null ||
            decision.InheritedOrigin is not { } original || decision.Policy != original.ResolvePolicy()) {
            throw ProjectTaskPlanAgentCodec.Denied("This task-plan adapter requires its original inherited Process decision.");
        }
        var wire = ProjectTaskPlanAgentCodec.Read<TargetWire>(decision.OriginalTarget, TargetFormat,
            AgentToolPreparedPayload.MaximumSourcePreparationUtf8Bytes);
        AgentToolSessionAdmission session = new(new(wire.Session.ExecutionRunId, Guid.Empty, default, wire.Session.Source),
            wire.Session.AgentId, AgentRuntimeContextPurpose.GovernedProcessAutomation, wire.Session.Profile);
        ProjectTaskPlanProcessTarget target = new(session, wire.Project, wire.Source, wire.Executor, wire.WriteContext);
        if (session.Reference != decision.Session || target.Source.Evidence.ExecutionCreatedAtUtc > decision.StartedAtUtc) {
            throw ProjectTaskPlanAgentCodec.Denied("The inherited task-plan target differs from its original decision session or start.");
        }
        RequireTarget(target, original);
        return target;
    }

    internal static void RequireTarget(ProjectTaskPlanProcessTarget target, AgentPlanningInheritedOrigin original) {
        var parent = Parent(original);
        var session = target.Session;
        var source = target.Source;
        var evidence = source.Evidence;
        var address = parent.Child.Address;
        if (session.Profile != original.Profile || session.AgentId != original.AgentId ||
            session.Purpose != AgentRuntimeContextPurpose.GovernedProcessAutomation || session.Reference.BackgroundSource != original.Source ||
            session.Reference.ChatSessionId != Guid.Empty || session.Reference.AuthorityId.Value != Guid.Empty ||
            evidence.ExecutionRunId != session.Reference.ExecutionRunId || evidence.ExecutionRunId != address.ChildId.Value ||
            evidence.ExecutorAgentId != session.AgentId || evidence.RunId != address.RunId || evidence.StepInstanceId != address.StepInstanceId ||
            source.RootRunId != address.RootRunId || evidence.DispatchClaimToken == Guid.Empty || !evidence.ExecutionMayDispatch ||
            !source.ObservedCurrentDispatch ||
            original.Source.SourceId != evidence.StepKey || original.Source.OwnerFingerprint != AgentToolProtocolEnvelope.ComputeDigest(source.OwnerFingerprint) ||
            source.SourceAuthority is not { CanCreateTasks: true, ProjectAdmission: { } project } authority ||
            authority.DatabaseProfileId != session.Profile.ProfileId || target.Project.DatabaseProfileId != session.Profile.ProfileId ||
            project.DatabaseProfileId != target.Project.DatabaseProfileId || project.ProjectId != target.Project.ProjectId ||
            project.LifetimeId != target.Project.LifetimeId || source.ProjectId != target.Project.ProjectId ||
            source.ProjectReference is not { } reference || reference.RunId != evidence.RunId || reference.StepInstanceId != evidence.StepInstanceId ||
            reference.ReadinessHash != source.ReadinessHash || string.IsNullOrWhiteSpace(source.ReadinessHash) ||
            !source.AllowedOperations.Contains(ProcessOperationContractNames.ExecuteExternalAction, StringComparer.OrdinalIgnoreCase) ||
            !target.Executor.CanUseTools || !target.Executor.CanWriteTasks) {
            throw ProjectTaskPlanAgentCodec.Denied("The inherited task-plan target differs from its original session, source or permission ceiling.");
        }
        ProjectDefinitionGovernedBindings.RequireSourceBounds(null, source);
        authority.Validate();
        RequireContext(target.WriteContext, original.WorkspaceNamespaceId, target.Caller);
    }

    internal static AgentPlanningBasisRecord Basis(AgentPlanningInheritedOrigin original, ProcessDecisionMaterial material)
        => ReadBasis(original, material).Basis;

    internal static (AgentPlanningBasisRecord Basis, ProjectTaskPlanBasis Material) ReadBasis(
        AgentPlanningInheritedOrigin original, ProcessDecisionMaterial material) {
        var parent = Parent(original);
        if (material.Scope != parent.Scope || material.Stamp != parent.Material ||
            material.ContentContract != ProjectTaskPlanProcessContract.Content || material.ExecutionContract is not null ||
            material.CapturedAtUtc != original.CapturedAtUtc) {
            throw ProjectTaskPlanAgentCodec.Denied("The inherited task plan requires its original shared Process material.");
        }
        var value = AgentToolProtocolEnvelope.Create(MaterialFormat, 1, material.PayloadJson);
        var basis = new AgentPlanningBasisRecord(original.BasisId, original.CapturedAtUtc, value);
        return (basis, Material(original, basis));
    }

    internal static ProjectTaskPlanBasis Material(AgentPlanningInheritedOrigin original, AgentPlanningBasisRecord basis) {
        var parent = Parent(original);
        original.RequireOriginalBasis(basis);
        if (basis.Value.Format != MaterialFormat || basis.Value.Version != 1) {
            throw ProjectTaskPlanAgentCodec.Denied("The inherited task-plan material interpretation is unsupported.");
        }
        ArgumentOutOfRangeException.ThrowIfGreaterThan(basis.Value.PayloadJson.Length, AgentPlanningProviderBounds.MaximumInputUtf8Bytes);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(Encoding.UTF8.GetByteCount(basis.Value.PayloadJson), AgentPlanningProviderBounds.MaximumInputUtf8Bytes);
        var payload = Encoding.UTF8.GetBytes(basis.Value.PayloadJson);
        var material = ProjectTaskPlanBasisCodec.Decode(payload);
        if (!payload.AsSpan().SequenceEqual(ProjectTaskPlanBasisCodec.Encode(material)) ||
            material.Namespace.Value != original.WorkspaceNamespaceId || material.Project.Expectation.Project.DatabaseProfileId != original.Profile.ProfileId ||
            material.CapturedAtUtc != original.CapturedAtUtc || ProcessDecisionMaterial.FromContent(parent.Scope, parent.Material.Reference,
                ProjectTaskPlanProcessContract.Content, original.CapturedAtUtc, basis.Value.PayloadJson).Stamp != parent.Material) {
            throw ProjectTaskPlanAgentCodec.Denied("The inherited task-plan bytes differ from the original canonical material stamp.");
        }
        return material;
    }

    internal static AgentToolPreparedCall Prepare(AgentPlanningDecisionRecord decision, AgentPlanningBasisRecord basis, string responseText) {
        var target = Target(decision);
        return ProjectTaskPlanAgentCodec.Prepare(decision, basis, responseText, PublicationTarget(target, decision),
            (Material(decision.InheritedOrigin!, basis), target.WriteContext));
    }

    internal static ProjectTaskPlanProcessCommand Resolve(AgentPlanningDecisionObservation observation,
        AgentToolSessionAdmission session, AgentToolPreparedPayload payload, AgentToolApprovalBinding binding)
        => Resolve(observation.Decision, session, payload, binding);

    internal static ProjectTaskPlanProcessCommand Resolve(AgentPlanningDecisionRecord decision,
        AgentToolSessionAdmission session, AgentToolPreparedPayload payload, AgentToolApprovalBinding binding) {
        var target = Target(decision);
        return new(target, ProjectTaskPlanAgentCodec.Resolve(decision, session, payload, binding,
            PublicationTarget(target, decision), basis => (Material(decision.InheritedOrigin!, basis), target.WriteContext)));
    }

    private static ProjectTaskPlanPublicationTarget PublicationTarget(ProjectTaskPlanProcessTarget target, AgentPlanningDecisionRecord decision)
        => new(target.Session, target.Project, target.Caller, decision.InheritedOrigin!.WorkspaceNamespaceId,
            ProjectTaskPlanSourceKind.ProcessAgentDecision);

    private static void RequireContext(ProjectTaskPlanWriteContext context, Guid originalNamespace, ProjectTaskPlanCaller caller) {
        if (context.Namespace.Value != originalNamespace || context.Epoch <= 0 ||
            context.CallerBinding != caller.Bind(context.Namespace) || context.AdmissionClosesAtUtc == default ||
            context.AdmissionClosesAtUtc.Offset != TimeSpan.Zero || context.ReplayUntilUtc.Offset != TimeSpan.Zero ||
            context.ReplayUntilUtc <= context.AdmissionClosesAtUtc) {
            throw ProjectTaskPlanAgentCodec.Denied("The inherited finite write context differs from its original child caller.");
        }
    }

    private sealed record SessionWire(Guid ExecutionRunId, Guid AgentId, AgentToolProfileBinding Profile,
        AgentToolBackgroundSourceBinding Source);
    private sealed record TargetWire(SessionWire Session, ProjectWriteAdmission Project, ProcessExecutionDispatchAuthority Source,
        ProjectTaskPlanExecutorCeiling Executor, ProjectTaskPlanWriteContext WriteContext);
}
