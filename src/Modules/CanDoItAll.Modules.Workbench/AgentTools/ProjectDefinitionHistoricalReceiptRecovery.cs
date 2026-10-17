using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.Tooling;
using CanDoItAll.Modules.Projects;
using CanDoItAll.Processes.Runtime;

namespace CanDoItAll.Modules.Workbench;

public sealed class ProjectDefinitionHistoricalReceiptRecovery(AgentHistoricalCatalogBasis basis,
    ProjectProcessHistoricalReadAuthority processes, ProjectWriteAdmissionService projects,
    ProjectDefinitionReceiptReader receipts) : IAgentToolHistoricalReceiptReadProvider {
    public bool Supports(string toolName) => ProjectDefinitionToolProposalCodec.Supports(toolName);

    public async ValueTask<AgentToolHistoricalReceiptObservation> ReadAsync(AgentToolHistoricalReceiptReadClaim claim,
        CancellationToken cancellationToken = default) {
        var admitted = await claim.RequireAsync(cancellationToken);
        var (source, input) = ProjectDefinitionToolProposalCodec.Read(admitted.Payload);
        ProjectDefinitionToolReceiptRecovery.RequireSession(source, admitted.OriginalSession);
        var lookup = await LookupAsync(source, admitted.Binding.IntentId, admitted.Payload.ToolName, input, claim, cancellationToken);
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
    }

    private async Task<ProjectDefinitionLookup> LookupAsync(ProjectDefinitionToolSource source, AgentToolBusinessIntentId intent,
        string tool, ProjectDefinitionToolArguments input, AgentToolHistoricalReceiptReadClaim claim, CancellationToken cancellationToken) {
        ProjectCreationReservation? reservation = null;
        if (tool != ProjectStructureToolPolicy.ProjectStructureProjectUpdate) {
            reservation = await projects.FindCreationReservationAsync(intent.Value, source.Session.AgentId, cancellationToken);
            if (reservation is not null) {
                ProjectDefinitionToolReceiptRecovery.RequireReservation(source, intent, input, reservation);
            }
        }
        IProjectDefinitionReceiptAccess access;
        if (source.Process is { } dispatch) {
            var original = new ProjectProcessMutationAdmission(dispatch);
            access = new HistoricalProcessAccess(reservation is null ? original : original.WithCreatedProject(reservation),
                processes, basis, projects, claim);
        } else {
            var original = source.AgentAdmission(tool);
            access = new HistoricalAgentAccess(reservation is null ? original : original.WithCreatedProject(reservation),
                basis, projects, claim);
        }
        return await receipts.LookupHistoricalAsync(source.Operation(intent), new ProjectDefinitionReceiptInvocation(source.Caller, access), cancellationToken);
    }

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
            if (current!.Dispatch.SourceAuthority!.Principal is ProcessLaunchPrincipal.AgentExecution source) {
                var agent = basis.RequireSelected(source.Ceiling.AgentId, cancellationToken);
                if (!ProjectDefinitionGovernedReadPolicy.CurrentAgentCanRead(source.Ceiling.AgentId, agent, target)) {
                    return false;
                }
                var matches = await projects.CaptureAsync(target.ProjectId, cancellationToken) == target;
                agent = basis.RequireSelected(source.Ceiling.AgentId, cancellationToken);
                await claim.RequireAsync(cancellationToken);
                return matches && ProjectDefinitionGovernedReadPolicy.CurrentAgentCanRead(source.Ceiling.AgentId, agent, target);
            }
            return await projects.CaptureAsync(target.ProjectId, cancellationToken) == target;
        }
    }
}