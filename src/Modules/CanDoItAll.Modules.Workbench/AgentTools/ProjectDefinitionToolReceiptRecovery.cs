using System.Text.Json;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.Tooling;
using CanDoItAll.Modules.Projects;

namespace CanDoItAll.Modules.Workbench;

public sealed class ProjectDefinitionToolReceiptRecovery(IAgentCatalogReadLeaseStore catalog,
    ProjectProcessExecutionMutationService processes, ProjectWriteAdmissionService projects,
    ProjectDefinitionReceiptReader receipts, ProjectCreatedTargetProvenance createdTargets) : IAgentToolReceiptReconciliationProvider {
    public bool Supports(string toolName) => ProjectDefinitionToolProposalCodec.Supports(toolName);

    public async ValueTask<AgentToolReceiptObservation> ReconcileAsync(AgentToolReceiptReconciliationClaim claim,
        CancellationToken cancellationToken = default) {
        var admitted = await claim.RequireAsync(cancellationToken);
        var (source, input) = ProjectDefinitionToolProposalCodec.Read(admitted.Payload);
        RequireSession(source, admitted.Session);
        var lookup = (await LookupOriginalAsync(source, admitted.Binding.IntentId, admitted.Payload.ToolName, input,
            admitted.PinnedEvidence is null ? null : claim, cancellationToken, ResolveSourcesAsync)).Lookup;
        await claim.RequireAsync(cancellationToken);
        return lookup switch {
            ProjectDefinitionLookup.FoundFull found => Receipt(found.Result),
            ProjectDefinitionLookup.FoundScalars found => Receipt(found.Result),
            ProjectDefinitionLookup.NotObserved or ProjectDefinitionLookup.ClosedAbsent or ProjectDefinitionLookup.Expired or
                ProjectDefinitionLookup.DetailUnavailable { Reason: ProjectDefinitionDetailUnavailableReason.TargetUnavailable }
                => AgentToolReceiptObservation.NotObserved,
            ProjectDefinitionLookup.DetailUnavailable { Reason: ProjectDefinitionDetailUnavailableReason.UnsupportedVersion or
                ProjectDefinitionDetailUnavailableReason.InvalidStoredResult }
                => throw new InvalidDataException("The original owner receipt cannot currently be interpreted."),
            _ => throw new AgentToolReceiptAccessDeniedException()
        };

        async Task<ProjectGovernedSourceAdmissions> ResolveSourcesAsync(CancellationToken token) {
            if (!ProjectCreatedTargetProvenance.NeedsDefinitionProof(source, admitted.Payload.ToolName, input)) {
                return ProjectCreatedTargetProvenance.Original(source, admitted.Payload.ToolName);
            }
            return await createdTargets.ResolveDefinitionAsync(source, admitted.Binding.IntentId, admitted.Payload.ToolName, input,
                await claim.RequirePriorInvocationsAsync(token), OriginalCreationLookup(admitted.PinnedEvidence is null ? null : claim), token);
        }
    }

    internal async Task<ProjectDefinitionLookup> LookupAsync(ProjectDefinitionToolSource source, AgentToolBusinessIntentId intent,
        string tool, ProjectDefinitionToolArguments input, CancellationToken cancellationToken,
        Func<CancellationToken, Task<ProjectGovernedSourceAdmissions>>? resolveSources = null)
        => (await LookupOriginalAsync(source, intent, tool, input, null, cancellationToken, resolveSources)).Lookup;

    internal ProjectOriginalCreationReceiptLookup OriginalCreationLookup(AgentToolReceiptReconciliationClaim? pinnedClaim = null)
        => (source, intent, tool, input, token) => tool is ProjectStructureToolPolicy.ProjectStructureProjectCreate or
            ProjectStructureToolPolicy.ProjectStructureSubprojectCreate
                ? LookupOriginalAsync(source, intent, tool, input, pinnedClaim, token)
                : throw ProjectDefinitionToolProposalCodec.Denied("Creation provenance cannot resolve another kind of operation.");

    internal async Task<(ProjectDefinitionLookup Lookup, ProjectCreationReservation? Reservation)> LookupOriginalAsync(ProjectDefinitionToolSource source, AgentToolBusinessIntentId intent,
        string tool, ProjectDefinitionToolArguments input, AgentToolReceiptReconciliationClaim? pinnedClaim, CancellationToken cancellationToken,
        Func<CancellationToken, Task<ProjectGovernedSourceAdmissions>>? resolveSources = null) {
        ProjectCreationReservation? reservation = null;
        if (tool != ProjectStructureToolPolicy.ProjectStructureProjectUpdate) {
            reservation = await FindReservationAsync(source, intent, input, cancellationToken);
        }
        var original = ProjectCreatedTargetProvenance.Original(source, tool);
        if (reservation is not null) {
            original = original.WithCreated(reservation);
        }
        IProjectDefinitionReceiptAccess Bind(ProjectGovernedSourceAdmissions sources)
            => sources.Process is { } process ? new ProjectDefinitionProcessReceiptAccess(process, processes, catalog, projects, pinnedClaim)
                : new ProjectDefinitionAgentReceiptAccess(sources.Agent!, catalog, projects);
        IProjectDefinitionReceiptAccess access = tool == ProjectStructureToolPolicy.ProjectStructureProjectUpdate && resolveSources is not null
            ? new ProjectDeferredDefinitionReceiptAccess(resolveSources, Bind) : Bind(original);
        return (await receipts.LookupAsync(source.Operation(intent), new ProjectDefinitionReceiptInvocation(source.Caller, access), cancellationToken), reservation);
    }

    internal async Task<ProjectCreationReservation?> FindReservationAsync(ProjectDefinitionToolSource source, AgentToolBusinessIntentId intent,
        ProjectDefinitionToolArguments input, CancellationToken cancellationToken) {
        var reservation = await projects.FindCreationReservationAsync(intent.Value, source.Session.AgentId, cancellationToken);
        if (reservation is not null) {
            RequireReservation(source, intent, input, reservation);
        }
        return reservation;
    }

    internal static void RequireReservation(ProjectDefinitionToolSource source, AgentToolBusinessIntentId intent,
        ProjectDefinitionToolArguments input, ProjectCreationReservation reservation) {
        if (reservation.Id != intent.Value || reservation.ProjectId != ProjectDefinitionToolProposalCodec.ReservedProjectId(source, intent) ||
            reservation.RequesterId != source.Session.AgentId || reservation.DatabaseProfileId != source.Session.Profile.ProfileId ||
            reservation.ParentProjectId != input.ParentProjectId || reservation.ParentLifetimeId != input.Request.ExpectedParentAdmission?.LifetimeId) {
            throw ProjectDefinitionToolProposalCodec.Denied("The retained creation reservation differs from the original target, parent lifetime or admitted requester.");
        }
    }

    internal static void RequireSession(ProjectDefinitionToolSource source, AgentToolSessionAdmission verified) {
        if (source.Session != verified) {
            throw ProjectDefinitionToolProposalCodec.Denied("The original definition proposal belongs to another verified execution.");
        }
    }

    internal static AgentToolReceiptObservation Receipt<T>(ProjectDefinitionAccepted<T> accepted) where T : class
        => new(new(ProjectDefinitionToolAdmission.EffectSourceKind, accepted.Operation.Id.ToString("D")),
            AgentToolProtocolEnvelope.Create("project-definition-receipt", 1,
                JsonSerializer.Serialize(accepted, ProjectDefinitionToolProposalCodec.Json)));
}
