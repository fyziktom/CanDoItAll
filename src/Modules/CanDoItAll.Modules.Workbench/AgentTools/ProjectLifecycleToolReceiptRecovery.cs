using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Modules.Projects;

namespace CanDoItAll.Modules.Workbench;

public sealed class ProjectLifecycleToolReceiptRecovery(IAgentCatalogReadLeaseStore catalog,
    ProjectProcessExecutionMutationService processes, ProjectWriteAdmissionService projects,
    ProjectLifecycleReceiptReader receipts, IProjectLifecycleRecoveryCodec codec,
    ProjectCreatedTargetProvenance createdTargets, ProjectDefinitionToolReceiptRecovery definitions) : IAgentToolReceiptReconciliationProvider {
    public bool Supports(string toolName) => ProjectLifecycleToolProposalCodec.Supports(toolName);

    public async ValueTask<AgentToolReceiptObservation> ReconcileAsync(AgentToolReceiptReconciliationClaim claim,
        CancellationToken cancellationToken = default) {
        var admitted = await claim.RequireAsync(cancellationToken);
        var (source, input) = ProjectLifecycleToolProposalCodec.Read(admitted.Payload);
        ProjectDefinitionToolReceiptRecovery.RequireSession(source, admitted.Session);
        var original = ProjectLifecycleToolProposalCodec.Submission(source, admitted.Binding.IntentId, input);
        var prefix = await claim.RequirePriorInvocationsAsync(cancellationToken);
        var pinned = admitted.PinnedEvidence is null ? null : claim;
        var sources = await createdTargets.ResolveAsync(source, original, prefix, definitions.OriginalCreationLookup(pinned), cancellationToken);
        var lookup = await LookupCoreAsync(source, original, sources, pinned, cancellationToken);
        await claim.RequireAsync(cancellationToken);
        return lookup switch {
            ProjectLifecycleLookup.Found found when found.Accepted.Original == original => Receipt(found.Accepted, codec),
            ProjectLifecycleLookup.Found => throw new InvalidDataException("The owner receipt belongs to a different original hierarchy intent."),
            ProjectLifecycleLookup.NotObserved or ProjectLifecycleLookup.ClosedAbsent or ProjectLifecycleLookup.Expired
                => AgentToolReceiptObservation.NotObserved,
            ProjectLifecycleLookup.Unavailable { Reason: ProjectLifecycleUnavailableReason.InvalidStoredResult or
                ProjectLifecycleUnavailableReason.UnsupportedResult }
                => throw new InvalidDataException("The original hierarchy receipt cannot currently be interpreted."),
            _ => throw new AgentToolReceiptAccessDeniedException()
        };
    }

    internal Task<ProjectLifecycleLookup> LookupAsync(ProjectDefinitionToolSource source, ProjectLifecycleSubmission original,
        ProjectGovernedSourceAdmissions sources, CancellationToken cancellationToken)
        => LookupCoreAsync(source, original, sources, null, cancellationToken);

    private Task<ProjectLifecycleLookup> LookupCoreAsync(ProjectDefinitionToolSource source, ProjectLifecycleSubmission original,
        ProjectGovernedSourceAdmissions sources, AgentToolReceiptReconciliationClaim? pinnedClaim, CancellationToken cancellationToken) {
        IProjectDefinitionReceiptAccess access = sources.Process is { } process
            ? new ProjectDefinitionProcessReceiptAccess(process, processes, catalog, projects, pinnedClaim)
            : new ProjectDefinitionAgentReceiptAccess(sources.Agent!, catalog, projects);
        return receipts.LookupAsync(original.Operation, new(source.Caller, new ProjectHierarchyReceiptAccess(access)), cancellationToken);
    }

    internal static AgentToolReceiptObservation Receipt(ProjectLifecycleAccepted accepted, IProjectLifecycleRecoveryCodec codec)
        => new(new(ProjectLifecycleToolAdmission.EffectSourceKind, accepted.Original.Operation.Id.ToString("D")),
            AgentToolProtocolEnvelope.Create("project-hierarchy-receipt", 1, codec.EncodeAccepted(accepted.Original, accepted).Payload));
}
