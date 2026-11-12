using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.Tooling;
using CanDoItAll.Modules.Projects;
using CanDoItAll.Processes.Runtime;

namespace CanDoItAll.Modules.Workbench;

public sealed class ProjectDefinitionHistoricalReceiptRecovery(AgentHistoricalCatalogBasis basis,
    ProjectProcessHistoricalReadAuthority processes, ProjectWriteAdmissionService projects,
    ProjectDefinitionReceiptReader receipts, ProjectCreatedTargetProvenance createdTargets) : IAgentToolHistoricalReceiptReadProvider {
    public bool Supports(string toolName) => ProjectDefinitionToolProposalCodec.Supports(toolName);

    public async ValueTask<AgentToolHistoricalReceiptObservation> ReadAsync(AgentToolHistoricalReceiptReadClaim claim,
        CancellationToken cancellationToken = default) {
        var admitted = await claim.RequireAsync(cancellationToken);
        var (source, input) = ProjectDefinitionToolProposalCodec.Read(admitted.Payload);
        ProjectDefinitionToolReceiptRecovery.RequireSession(source, admitted.OriginalSession);
        var (lookup, _) = await LookupOriginalAsync(source, admitted.Binding.IntentId, admitted.Payload.ToolName, input, claim,
            cancellationToken, ResolveSourcesAsync);
        await claim.RequireAsync(cancellationToken);
        return lookup switch {
            ProjectDefinitionLookup.FoundFull found => new(AgentHistoricalReceiptKnowledge.Accepted, ProjectDefinitionToolReceiptRecovery.Receipt(found.Result)),
            ProjectDefinitionLookup.FoundScalars found => new(AgentHistoricalReceiptKnowledge.Accepted, ProjectDefinitionToolReceiptRecovery.Receipt(found.Result)),
            ProjectDefinitionLookup.NotObserved => new(AgentHistoricalReceiptKnowledge.NotObserved),
            ProjectDefinitionLookup.ClosedAbsent => new(AgentHistoricalReceiptKnowledge.ClosedAbsent),
            ProjectDefinitionLookup.Expired => new(AgentHistoricalReceiptKnowledge.Expired),
            ProjectDefinitionLookup.DatasetChanged => new(AgentHistoricalReceiptKnowledge.DatasetChanged),
            ProjectDefinitionLookup.DetailUnavailable { Reason: ProjectDefinitionDetailUnavailableReason.AccessDenied }
                => new(AgentHistoricalReceiptKnowledge.ReadDenied),
            ProjectDefinitionLookup.DetailUnavailable => new(AgentHistoricalReceiptKnowledge.ReadUnavailable),
            ProjectDefinitionLookup.CallerContextChanged => throw new AgentToolReceiptAccessDeniedException(),
            ProjectDefinitionLookup.InvalidContext => new(AgentHistoricalReceiptKnowledge.Unsupported),
            _ => throw new InvalidDataException("The original receipt owner returned an unknown historical lookup state.")
        };

        async Task<ProjectGovernedSourceAdmissions> ResolveSourcesAsync(CancellationToken token) {
            if (!ProjectCreatedTargetProvenance.NeedsDefinitionProof(source, admitted.Payload.ToolName, input)) {
                return ProjectCreatedTargetProvenance.Original(source, admitted.Payload.ToolName);
            }
            return await createdTargets.ResolveDefinitionAsync(source, admitted.Binding.IntentId, admitted.Payload.ToolName, input,
                await claim.RequirePriorInvocationsAsync(token),
                (prior, intent, tool, arguments, currentToken) => LookupCreationAsync(prior, intent, tool, arguments, claim, currentToken), token);
        }
    }

    internal async Task<(ProjectDefinitionLookup Lookup, ProjectCreationReservation? Reservation)> LookupOriginalAsync(ProjectDefinitionToolSource source, AgentToolBusinessIntentId intent,
        string tool, ProjectDefinitionToolArguments input, AgentToolHistoricalReceiptReadClaim claim, CancellationToken cancellationToken,
        Func<CancellationToken, Task<ProjectGovernedSourceAdmissions>>? resolveSources = null) {
        ProjectCreationReservation? reservation = null;
        if (tool != ProjectStructureToolPolicy.ProjectStructureProjectUpdate) {
            reservation = await projects.FindCreationReservationAsync(intent.Value, source.Session.AgentId, cancellationToken);
            if (reservation is not null) {
                ProjectDefinitionToolReceiptRecovery.RequireReservation(source, intent, input, reservation);
            }
        }
        var original = ProjectCreatedTargetProvenance.Original(source, tool);
        if (reservation is not null) {
            original = original.WithCreated(reservation);
        }
        IProjectDefinitionReceiptAccess Bind(ProjectGovernedSourceAdmissions sources)
            => sources.Process is { } process ? ForProcess(process, claim) : ForAgent(sources.Agent!, claim);
        IProjectDefinitionReceiptAccess access = tool == ProjectStructureToolPolicy.ProjectStructureProjectUpdate && resolveSources is not null
            ? new ProjectDeferredDefinitionReceiptAccess(resolveSources, Bind) : Bind(original);
        return (await receipts.LookupHistoricalAsync(source.Operation(intent), new ProjectDefinitionReceiptInvocation(source.Caller, access), cancellationToken), reservation);
    }

    internal async Task<(ProjectDefinitionLookup Lookup, ProjectCreationReservation? Reservation)> LookupCreationAsync(
        ProjectDefinitionToolSource source, AgentToolBusinessIntentId intent, string tool, ProjectDefinitionToolArguments input,
        AgentToolHistoricalReceiptReadClaim claim, CancellationToken cancellationToken) {
        if (tool is not (ProjectStructureToolPolicy.ProjectStructureProjectCreate or ProjectStructureToolPolicy.ProjectStructureSubprojectCreate)) {
            throw ProjectDefinitionToolProposalCodec.Denied("Historical creation provenance cannot resolve another kind of operation.");
        }
        var result = await LookupOriginalAsync(source, intent, tool, input, claim, cancellationToken);
        if (result.Lookup is ProjectDefinitionLookup.DetailUnavailable { Reason: ProjectDefinitionDetailUnavailableReason.AccessDenied }) {
            throw new AgentToolReceiptAccessDeniedException();
        }
        if (result.Lookup is not ProjectDefinitionLookup.FoundFull) {
            throw new AgentToolReceiptReadUnavailableException(new InvalidOperationException(
                $"Original creation {intent.Value} cannot establish target provenance: {result.Lookup.GetType().Name}."));
        }
        return result;
    }

    internal IProjectDefinitionReceiptAccess ForAgent(ProjectAgentMutationAdmission original, AgentToolHistoricalReceiptReadClaim claim)
        => new HistoricalAgentAccess(original, basis, projects, claim);

    internal IProjectDefinitionReceiptAccess ForProcess(ProjectProcessMutationAdmission original, AgentToolHistoricalReceiptReadClaim claim)
        => new HistoricalProcessAccess(original, processes, basis, projects, claim);

    private sealed class HistoricalAgentAccess(ProjectAgentMutationAdmission original,
        AgentHistoricalCatalogBasis basis, ProjectWriteAdmissionService projects,
        AgentToolHistoricalReceiptReadClaim claim) : IProjectDefinitionReceiptAccess {
        public async Task<bool> CanReadAsync(ProjectWriteAdmission target, CancellationToken cancellationToken) {
            if (target.DatabaseProfileId != projects.DatabaseProfileId) {
                return false;
            }
            await claim.RequireAsync(cancellationToken);
            var agent = basis.RequireSelected(original.AgentId, cancellationToken);
            if (!ProjectDefinitionGovernedReadPolicy.AgentCanRead(original, agent, target)) {
                return false;
            }
            var matches = await projects.CaptureAsync(target.ProjectId, cancellationToken) == target;
            agent = basis.RequireSelected(original.AgentId, cancellationToken);
            await claim.RequireAsync(cancellationToken);
            return matches && ProjectDefinitionGovernedReadPolicy.AgentCanRead(original, agent, target);
        }
    }

    private sealed class HistoricalProcessAccess(ProjectProcessMutationAdmission original,
        ProjectProcessHistoricalReadAuthority processes, AgentHistoricalCatalogBasis basis,
        ProjectWriteAdmissionService projects, AgentToolHistoricalReceiptReadClaim claim) : IProjectDefinitionReceiptAccess {
        public async Task<bool> CanReadAsync(ProjectWriteAdmission target, CancellationToken cancellationToken) {
            if (target.DatabaseProfileId != projects.DatabaseProfileId) {
                return false;
            }
            var current = await processes.ReadReceiptAccessAsync(original, claim, cancellationToken);
            if (!ProjectDefinitionGovernedReadPolicy.ProcessCanRead(original, current, target)) {
                return false;
            }
            bool Allows() {
                if (current!.Dispatch.SourceAuthority!.Principal is ProcessLaunchPrincipal.AgentExecution source &&
                        !ProjectDefinitionGovernedReadPolicy.CurrentAgentCanRead(source.Ceiling.AgentId,
                            basis.RequireSelected(source.Ceiling.AgentId, cancellationToken), original.ProjectAdmission)) {
                    return false;
                }
                var executor = original.Dispatch.Evidence.ExecutorAgentId;
                return target == original.ProjectAdmission || ProjectDefinitionGovernedReadPolicy.CurrentExecutorCanUseCreatedProject(
                    executor, basis.RequireSelected(executor, cancellationToken), target);
            }
            if (!Allows()) {
                return false;
            }
            var matches = await projects.CaptureAsync(target.ProjectId, cancellationToken) == target;
            await claim.RequireAsync(cancellationToken);
            return matches && Allows();
        }
    }
}
