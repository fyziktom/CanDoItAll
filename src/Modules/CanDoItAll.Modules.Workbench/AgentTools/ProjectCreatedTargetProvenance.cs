using System.Collections.Immutable;
using System.Text.Json;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Modules.Projects;

namespace CanDoItAll.Modules.Workbench;

public sealed class ProjectCreatedTargetProvenance(ProjectWriteAdmissionService projects) {
    internal Task<ProjectGovernedSourceAdmissions> ResolveAsync(ProjectDefinitionToolSource source, ProjectLifecycleSubmission original,
        AgentToolPriorInvocationObservation evidence, ProjectOriginalCreationReceiptLookup lookup, CancellationToken cancellationToken) {
        RequireEvidence(source, original, evidence.Session, evidence.Current);
        return ResolveCoreAsync(source, ProjectStructureToolPolicy.ProjectStructureSubprojectLink,
            ProjectHierarchyGovernedInvocationFactory.Targets(original.Command), evidence.Invocations, lookup, cancellationToken);
    }

    internal Task<ProjectGovernedSourceAdmissions> ResolveAsync(ProjectDefinitionToolSource source, ProjectLifecycleSubmission original,
        AgentToolPriorReceiptObservation evidence, ProjectOriginalCreationReceiptLookup lookup, CancellationToken cancellationToken) {
        RequireEvidence(source, original, evidence.Session, evidence.Current);
        return ResolveCoreAsync(source, ProjectStructureToolPolicy.ProjectStructureSubprojectLink,
            ProjectHierarchyGovernedInvocationFactory.Targets(original.Command), evidence.Invocations, lookup, cancellationToken);
    }

    internal Task<ProjectGovernedSourceAdmissions> ResolveHistoricalAsync(ProjectDefinitionToolSource source, ProjectLifecycleSubmission original,
        AgentToolPriorReceiptObservation evidence, ProjectOriginalCreationReceiptLookup lookup, CancellationToken cancellationToken) {
        RequireEvidence(source, original, evidence.Session, evidence.Current);
        return ResolveCoreAsync(source, ProjectStructureToolPolicy.ProjectStructureSubprojectLink,
            ProjectHierarchyGovernedInvocationFactory.Targets(original.Command), evidence.Invocations, lookup, cancellationToken);
    }

    internal static ProjectGovernedSourceAdmissions Original(ProjectDefinitionToolSource source, string tool)
        => source.Process is { } dispatch ? new(null, new(dispatch)) : new(source.AgentAdmission(tool), null);

    internal static ImmutableArray<ProjectWriteAdmission> DefinitionTargets(ProjectDefinitionToolArguments input)
        => input.Request.ExpectedDefinition is { } definition ? [definition.Project] :
            input.Request.ExpectedParentAdmission is { } parent ? [parent] : [];

    internal static bool NeedsDefinitionProof(ProjectDefinitionToolSource source, string tool, ProjectDefinitionToolArguments input)
        => Missing(Original(source, tool), DefinitionTargets(input)).Length > 0;

    internal Task<ProjectGovernedSourceAdmissions> ResolveDefinitionAsync(ProjectDefinitionToolSource source, AgentToolBusinessIntentId intent,
        string tool, ProjectDefinitionToolArguments input, AgentToolPriorInvocationObservation evidence,
        ProjectOriginalCreationReceiptLookup lookup, CancellationToken cancellationToken) {
        RequireDefinitionEvidence(source, intent, evidence.Session, evidence.Current);
        return ResolveCoreAsync(source, tool, DefinitionTargets(input), evidence.Invocations, lookup, cancellationToken);
    }

    internal Task<ProjectGovernedSourceAdmissions> ResolveDefinitionAsync(ProjectDefinitionToolSource source, AgentToolBusinessIntentId intent,
        string tool, ProjectDefinitionToolArguments input, AgentToolPriorReceiptObservation evidence,
        ProjectOriginalCreationReceiptLookup lookup, CancellationToken cancellationToken) {
        RequireDefinitionEvidence(source, intent, evidence.Session, evidence.Current);
        return ResolveCoreAsync(source, tool, DefinitionTargets(input), evidence.Invocations, lookup, cancellationToken);
    }

    private static void RequireDefinitionEvidence(ProjectDefinitionToolSource source, AgentToolBusinessIntentId intent,
        AgentToolSessionAdmission session, AgentToolApprovalBinding binding) {
        if (source.Session != session || binding.IntentId != intent) {
            throw Denied("The prior invocation evidence belongs to another original definition operation.");
        }
    }

    private static ProjectWriteAdmission[] Missing(ProjectGovernedSourceAdmissions original, ImmutableArray<ProjectWriteAdmission> targets)
        => targets.Where(target => original.Process is { } process ? target != process.ProjectAdmission :
            !OriginallyAllows(original.Agent!, target)).Distinct().ToArray();

    private async Task<ProjectGovernedSourceAdmissions> ResolveCoreAsync(ProjectDefinitionToolSource source, string tool,
        ImmutableArray<ProjectWriteAdmission> targets,
        ImmutableArray<AgentToolPriorInvocation> prefix, ProjectOriginalCreationReceiptLookup lookup, CancellationToken cancellationToken) {
        var original = Original(source, tool);
        var agent = original.Agent;
        var process = original.Process;
        var missing = Missing(original, targets);
        if (missing.Length > 3 || prefix.IsDefault) {
            throw Denied("The hierarchy provenance exceeds its supported endpoint bounds.");
        }
        if (missing.Length == 0) {
            return new(agent, process);
        }
        var candidates = new Dictionary<Guid, AgentToolProposalRecord>();
        foreach (var item in prefix) {
            cancellationToken.ThrowIfCancellationRequested();
            var proposal = item.Proposal;
            if (proposal.Payload.ToolName is not (ProjectStructureToolPolicy.ProjectStructureProjectCreate or
                    ProjectStructureToolPolicy.ProjectStructureSubprojectCreate)) {
                continue;
            }
            var targetId = ProjectDefinitionToolProposalCodec.ReservedProjectId(source, proposal.IntentId);
            if (!missing.Any(target => target.ProjectId == targetId)) {
                continue;
            }
            if (!candidates.TryAdd(targetId, proposal)) {
                throw Denied("Two prior creation intents claim the same hierarchy target.");
            }
        }
        var reservations = ImmutableArray.CreateBuilder<ProjectCreationReservation>(missing.Length);
        foreach (var target in missing) {
            if (!candidates.TryGetValue(target.ProjectId, out var proposal) || proposal.DispatchClaimId is null ||
                    proposal.ApprovalStatus != ExecutionApprovalStatus.Approved || proposal.ApprovedDigest != proposal.Payload.Digest) {
                throw Denied("A hierarchy endpoint lacks its exact approved prior creation in this execution.");
            }
            var (prior, input) = ProjectDefinitionToolProposalCodec.Read(proposal.Payload);
            RequireSameSource(source, prior);
            var observed = await lookup(prior, proposal.IntentId, proposal.Payload.ToolName, input, cancellationToken);
            if (observed.Lookup is not ProjectDefinitionLookup.FoundFull found || found.Result.Kind != ProjectDefinitionCommandKind.Create ||
                    found.Result.Operation != prior.Operation(proposal.IntentId) || found.Result.Acknowledgement.Definition.Project != target ||
                    observed.Reservation is not { } reservation || reservation.DatabaseProfileId != target.DatabaseProfileId ||
                    reservation.ProjectId != target.ProjectId || reservation.LifetimeId != target.LifetimeId) {
                throw Denied("The original creation receipt does not establish this exact live hierarchy endpoint.");
            }
            reservations.Add(reservation);
        }
        var created = reservations.ToImmutable();
        await projects.RequireConsumedCreationsForReadAsync(created, source.Namespace, cancellationToken);
        if (process is not null) {
            foreach (var reservation in created) {
                process = process.WithCreatedProject(reservation);
            }
        }
        return new(agent?.WithPriorCreations(created), process);
    }

    private static bool OriginallyAllows(ProjectAgentMutationAdmission original, ProjectWriteAdmission target)
        => original.AllowAllProjects || original.ProjectIds.Contains(target.ProjectId) &&
            original.ProjectLifetimes.Contains(new(target.DatabaseProfileId, target.ProjectId, target.LifetimeId));

    private static void RequireEvidence(ProjectDefinitionToolSource source, ProjectLifecycleSubmission original,
        AgentToolSessionAdmission session, AgentToolApprovalBinding binding) {
        if (source.Session != session || original.Operation.Id != binding.IntentId.Value ||
                original.Operation.Namespace != source.Namespace || original.Operation.Epoch != source.Epoch ||
                original.Operation.CallerBinding != source.CallerBinding) {
            throw Denied("The prior invocation evidence belongs to another original hierarchy operation.");
        }
    }

    private static void RequireSameSource(ProjectDefinitionToolSource current, ProjectDefinitionToolSource prior) {
        if (prior.Session != current.Session || prior.Namespace != current.Namespace || prior.Epoch != current.Epoch ||
                prior.CallerBinding != current.CallerBinding || prior.RequestVersion != current.RequestVersion ||
                JsonSerializer.Serialize(prior.Agent?.Governance, ProjectDefinitionToolProposalCodec.Json) !=
                    JsonSerializer.Serialize(current.Agent?.Governance, ProjectDefinitionToolProposalCodec.Json) ||
                prior.Agent?.SourceProject != current.Agent?.SourceProject ||
                JsonSerializer.Serialize(prior.Process, ProjectDefinitionToolProposalCodec.Json) !=
                    JsonSerializer.Serialize(current.Process, ProjectDefinitionToolProposalCodec.Json)) {
            throw Denied("A prior creation cannot cross the original session, source ceiling, dataset or receipt epoch.");
        }
    }

    private static AgentToolAdmissionException Denied(string message) => ProjectDefinitionToolProposalCodec.Denied(message);
}

internal sealed record ProjectGovernedSourceAdmissions(ProjectAgentMutationAdmission? Agent, ProjectProcessMutationAdmission? Process) {
    internal ProjectGovernedSourceAdmissions WithCreated(ProjectCreationReservation reservation)
        => new(Agent?.WithCreatedProject(reservation), Process?.WithCreatedProject(reservation));
}

internal delegate Task<(ProjectDefinitionLookup Lookup, ProjectCreationReservation? Reservation)> ProjectOriginalCreationReceiptLookup(
    ProjectDefinitionToolSource source, AgentToolBusinessIntentId intent, string tool, ProjectDefinitionToolArguments input, CancellationToken cancellationToken);
