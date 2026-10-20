using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Modules.Projects;

namespace CanDoItAll.Modules.Workbench;

internal sealed record ProjectTaskPlanAgentTarget(AgentToolSessionAdmission Session, ProjectWriteAdmission Project,
    ProjectDefinitionToolAgentSource Agent) {
    internal ProjectAgentMutationAdmission Admission => new(Session.AgentId, Session.Profile.ProfileId, Agent.Access,
        Agent.Governance, ProjectAgentMutationDomain.Tasks, ProjectStructureToolPolicy.ProjectTaskPlanPublish, Agent.SourceProject);
    internal ProjectTaskPlanCaller Caller => new(ProjectTaskPlanCallerKind.OrdinaryAgent,
        ProjectDefinitionGovernedBindings.AgentCaller(Session).Key.ToArray());
}

internal sealed record ProjectTaskPlanAgentBasis(JsonElement Material, ProjectTaskPlanWriteContext WriteContext);
internal sealed record ProjectTaskPlanAgentSource(Guid DecisionId, int Generation, Guid StepId, Guid BasisId,
    AgentToolSemanticDigest BasisDigest, ProjectTaskPlanOperation Operation, string Fingerprint);
internal sealed record ProjectTaskPlanAgentArguments(ProjectTaskPlanDraft Draft);
internal sealed record ProjectTaskPlanAgentCommand(ProjectTaskPlanAgentTarget Target, ProjectTaskPlanCommand Command);
internal sealed record ProjectTaskPlanPublicationTarget(AgentToolSessionAdmission Session, ProjectWriteAdmission Project,
    ProjectTaskPlanCaller Caller, Guid WorkspaceNamespaceId, ProjectTaskPlanSourceKind SourceKind);

internal static class ProjectTaskPlanAgentCodec {
    internal const string TargetFormat = "workbench-task-plan-target";
    internal const string BasisFormat = "workbench-task-plan-basis";
    internal const string SourceFormat = "workbench-task-plan-source";
    internal const string ResultFormat = "workbench-task-plan-result";
    internal const string EffectSourceKind = "workbench-task-plan";
    private const int SemanticVersion = 2;
    internal static JsonSerializerOptions Json { get; } = new(JsonSerializerDefaults.Web) {
        PropertyNameCaseInsensitive = false, UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
        RespectRequiredConstructorParameters = true, RespectNullableAnnotations = true, MaxDepth = 32,
        Converters = { new ProjectProcessStepInstanceIdJsonConverter() }
    };

    internal static AgentToolProtocolEnvelope Write<T>(string format, T value, int maximumBytes) {
        var json = JsonSerializer.Serialize(value, Json);
        RequireBytes(json, maximumBytes);
        return AgentToolProtocolEnvelope.Create(format, 1, json);
    }

    internal static T Read<T>(AgentToolProtocolEnvelope envelope, string format, int maximumBytes) {
        if (envelope.Format != format || envelope.Version != 1) {
            throw Denied("The original task-plan interpretation is unsupported.");
        }
        RequireBytes(envelope.PayloadJson, maximumBytes);
        using var document = JsonDocument.Parse(envelope.PayloadJson, new() { MaxDepth = Json.MaxDepth });
        ProjectTaskPlanBasisCodec.RequireUniqueMembers(document.RootElement);
        var value = document.Deserialize<T>(Json) ?? throw Denied("The original task-plan value is missing.");
        if (JsonSerializer.Serialize(value, Json) != envelope.PayloadJson) {
            throw Denied("The original task-plan value is not canonical.");
        }
        return value;
    }

    internal static ProjectTaskPlanAgentTarget Target(AgentPlanningDecisionRecord decision) {
        if (decision.Provenance is not { } provenance || decision.InheritedOrigin is not null) {
            throw Denied("This task-plan adapter requires the original interactive planning admission.");
        }
        var target = Read<ProjectTaskPlanAgentTarget>(decision.OriginalTarget, TargetFormat,
            AgentToolPreparedPayload.MaximumSourcePreparationUtf8Bytes);
        var session = target.Session;
        var governance = target.Agent.Governance;
        if (decision.Owner != AgentPlanningOwner.ProjectTaskPlan || decision.Session != session.Reference ||
            session.AgentId != provenance.AgentId || session.Purpose != AgentRuntimeContextPurpose.InteractiveChat ||
            session.Reference.BackgroundSource is not null || target.Project.DatabaseProfileId != session.Profile.ProfileId ||
            governance.AgentId != session.AgentId || governance.AuthorityId != session.Reference.AuthorityId ||
            governance.DatabaseProfileId != session.Profile.ProfileId || governance.DatabaseProfileGeneration != session.Profile.Generation ||
            !governance.ReadAllowed || !governance.MutationAllowed || target.Agent.Access is null) {
            throw Denied("The original task-plan target does not belong to this admitted decision.");
        }
        return target;
    }

    internal static (ProjectTaskPlanBasis Material, ProjectTaskPlanWriteContext Context) Basis(AgentPlanningBasisRecord basis) {
        var value = Read<ProjectTaskPlanAgentBasis>(basis.Value, BasisFormat, AgentPlanningProviderBounds.MaximumInputUtf8Bytes);
        var material = ProjectTaskPlanBasisCodec.Decode(Encoding.UTF8.GetBytes(value.Material.GetRawText()));
        var context = value.WriteContext;
        if (context.Namespace != material.Namespace || context.Epoch <= 0 || !ProjectTaskPlanOperationRules.IsDigest(context.CallerBinding) ||
            context.AdmissionClosesAtUtc.Offset != TimeSpan.Zero || context.ReplayUntilUtc <= context.AdmissionClosesAtUtc) {
            throw Denied("The captured material and finite-operation context differ.");
        }
        return (material, context);
    }

    internal static ProjectTaskPlanDraft ParseDraft(JsonElement value) {
        RequireBytes(value.GetRawText(), ProjectTaskPlanProposal.MaximumRawBytes);
        ProjectTaskPlanBasisCodec.RequireUniqueMembers(value);
        var draft = value.Deserialize<ProjectTaskPlanDraft>(Json) ?? throw new JsonException("A task draft is required.");
        return new(ProjectTaskPlanProposal.CurrentVersion, ProjectTaskPlanProposal.Normalize(draft).Tasks);
    }

    internal static AgentToolPreparedCall Prepare(AgentPlanningDecisionRecord decision, AgentPlanningBasisRecord basis,
        string responseText) {
        var target = Target(decision);
        return Prepare(decision, basis, responseText, PublicationTarget(target, decision), Basis(basis));
    }

    internal static AgentToolPreparedCall Prepare(AgentPlanningDecisionRecord decision, AgentPlanningBasisRecord basis,
        string responseText, ProjectTaskPlanPublicationTarget target,
        (ProjectTaskPlanBasis Material, ProjectTaskPlanWriteContext Context) original) {
        var (material, context) = original;
        var step = decision.Current.Steps.LastOrDefault() ?? throw Denied("The original planning step is missing.");
        if (step.BasisId != basis.Id || material.Project.Expectation.Project != target.Project ||
            decision.Session != target.Session.Reference || context.Namespace.Value != target.WorkspaceNamespaceId ||
            !target.Caller.MatchesSource(target.SourceKind) ||
            context.CallerBinding != target.Caller.Bind(context.Namespace)) {
            throw Denied("The publication does not retain the original step, basis and caller.");
        }
        RequireBytes(responseText, AgentPlanningProviderBounds.MaximumTextUtf8Bytes);
        using var response = JsonDocument.Parse(responseText, new() { MaxDepth = Json.MaxDepth });
        var draft = ParseDraft(response.RootElement);
        var operation = new ProjectTaskPlanOperation(context.Namespace, context.Epoch, Guid.NewGuid(), context.CallerBinding);
        var source = new ProjectTaskPlanAgentSource(decision.DecisionId, decision.CurrentGeneration, step.Id, basis.Id,
            basis.Value.Digest, operation, Fingerprint(decision, decision.CurrentGeneration, step.Id, basis, draft));
        var envelope = Write(SourceFormat, source, AgentToolPreparedPayload.MaximumSourcePreparationUtf8Bytes);
        var arguments = JsonSerializer.SerializeToElement(new ProjectTaskPlanAgentArguments(draft), Json);
        return new(operation.Id.ToString("N"), Restore(arguments, envelope), true);
    }

    internal static AgentToolPreparedPayload Restore(JsonElement arguments, AgentToolProtocolEnvelope originalSource) {
        ProjectTaskPlanBasisCodec.RequireUniqueMembers(arguments);
        var input = arguments.Deserialize<ProjectTaskPlanAgentArguments>(Json) ?? throw new JsonException();
        var draft = new ProjectTaskPlanDraft(ProjectTaskPlanProposal.CurrentVersion, ProjectTaskPlanProposal.Normalize(input.Draft).Tasks);
        var canonical = JsonSerializer.Serialize(new ProjectTaskPlanAgentArguments(draft), Json);
        RequireBytes(canonical, AgentPlanningProviderBounds.MaximumArgumentsUtf8Bytes);
        var source = Read<ProjectTaskPlanAgentSource>(originalSource, SourceFormat, AgentToolPreparedPayload.MaximumSourcePreparationUtf8Bytes);
        if (source.DecisionId == Guid.Empty || source.Generation < 1 || source.StepId == Guid.Empty || source.BasisId == Guid.Empty ||
            !ProjectTaskPlanOperationRules.IsDigest(source.BasisDigest.Value) || !ProjectTaskPlanOperationRules.IsDigest(source.Fingerprint)) {
            throw Denied("The original task-plan operation has invalid identity evidence.");
        }
        var digest = AgentToolProtocolEnvelope.ComputeDigest(JsonSerializer.Serialize(new {
            Tool = ProjectStructureToolPolicy.ProjectTaskPlanPublish, SemanticVersion, Arguments = canonical, Source = originalSource
        }, Json));
        return new(ProjectStructureToolPolicy.ProjectTaskPlanPublish, SemanticVersion, digest, canonical,
            AgentToolProposalEffect.Mutation, AgentToolProposalRecovery.OwnerReceipt, originalSource);
    }

    internal static ProjectTaskPlanAgentCommand Resolve(AgentPlanningDecisionObservation observation,
        AgentToolSessionAdmission session, AgentToolPreparedPayload payload, AgentToolApprovalBinding binding) {
        var target = Target(observation.Decision);
        return new(target, Resolve(observation, session, payload, binding, PublicationTarget(target, observation.Decision), Basis));
    }

    internal static ProjectTaskPlanCommand Resolve(AgentPlanningDecisionObservation observation, AgentToolSessionAdmission session,
        AgentToolPreparedPayload payload, AgentToolApprovalBinding binding, ProjectTaskPlanPublicationTarget target,
        Func<AgentPlanningBasisRecord, (ProjectTaskPlanBasis Material, ProjectTaskPlanWriteContext Context)> readOriginalBasis) {
        var decision = observation.Decision;
        if (target.Session != session || payload.SourcePreparation is not { } envelope ||
            decision.Session != session.Reference || !target.Caller.MatchesSource(target.SourceKind) ||
            payload.ToolName != ProjectStructureToolPolicy.ProjectTaskPlanPublish ||
            binding.Digest != payload.Digest || binding.SemanticVersion != payload.SemanticVersion) {
            throw Denied("The saved task-plan proposal does not match this original session and binding.");
        }
        using var arguments = JsonDocument.Parse(payload.ArgumentsJson, new() { MaxDepth = Json.MaxDepth });
        if (Restore(arguments.RootElement, envelope) != payload) {
            throw Denied("Restoration changed the original task-plan proposal.");
        }
        var source = Read<ProjectTaskPlanAgentSource>(envelope, SourceFormat, AgentToolPreparedPayload.MaximumSourcePreparationUtf8Bytes);
        var generation = decision.Generations.SingleOrDefault(item => item.Number == source.Generation);
        var step = generation?.Steps.SingleOrDefault(item => item.Id == source.StepId);
        var basis = generation?.Bases.SingleOrDefault(item => item.Id == source.BasisId);
        if (decision.DecisionId != source.DecisionId || step is null || basis is null || step.BasisId != basis.Id ||
            basis.Value.Digest != source.BasisDigest || !step.Publications.Any(item => item.Proposal == binding && item.OriginalOperation == envelope)) {
            throw Denied("The saved publication is not part of this original decision, generation, step and basis.");
        }
        var (material, context) = readOriginalBasis(basis);
        var draft = arguments.RootElement.Deserialize<ProjectTaskPlanAgentArguments>(Json)!.Draft;
        if (material.Project.Expectation.Project != target.Project || context.Namespace != source.Operation.Namespace ||
            context.Namespace.Value != target.WorkspaceNamespaceId ||
            context.Epoch != source.Operation.Epoch || context.CallerBinding != source.Operation.CallerBinding ||
            context.CallerBinding != target.Caller.Bind(context.Namespace) ||
            Fingerprint(decision, generation!.Number, step.Id, basis, draft) != source.Fingerprint) {
            throw Denied("The saved publication differs from its original material, operation context or semantic source.");
        }
        return new(source.Operation, material, ProjectTaskPlanProposal.Normalize(draft), decision.Policy,
            new(decision.DecisionId, generation.Number, decision.StartedAtUtc, target.SourceKind,
                ProjectTaskPlanSource.ActiveWorkVersion, source.Fingerprint), ProjectTaskPlanSource.ActiveWorkVersion);
    }

    private static ProjectTaskPlanPublicationTarget PublicationTarget(ProjectTaskPlanAgentTarget target, AgentPlanningDecisionRecord decision)
        => new(target.Session, target.Project, target.Caller, decision.Provenance!.WorkspaceNamespaceId,
            ProjectTaskPlanSourceKind.OrdinaryAgentDecision);

    private static string Fingerprint(AgentPlanningDecisionRecord decision, int generation, Guid step,
        AgentPlanningBasisRecord basis, ProjectTaskPlanDraft draft)
        => AgentToolProtocolEnvelope.ComputeDigest(JsonSerializer.Serialize(new {
            decision.DecisionId, Generation = generation, Step = step, Basis = basis.Id, basis.Value.Digest,
            Target = decision.OriginalTarget.Digest, decision.Policy, Draft = draft
        }, Json)).Value;

    private static void RequireBytes(string value, int maximum)
        => ArgumentOutOfRangeException.ThrowIfGreaterThan(Encoding.UTF8.GetByteCount(value), maximum);

    internal static AgentToolAdmissionException Denied(string message) => new("planning.task-plan-denied", message);
}
