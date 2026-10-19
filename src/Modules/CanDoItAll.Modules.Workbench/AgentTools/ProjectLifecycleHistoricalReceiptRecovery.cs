using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Modules.Projects;

namespace CanDoItAll.Modules.Workbench;

public sealed class ProjectLifecycleHistoricalReceiptRecovery(ProjectDefinitionHistoricalReceiptRecovery definitions,
    ProjectCreatedTargetProvenance targets, ProjectLifecycleReceiptReader receipts,
    IProjectLifecycleRecoveryCodec codec) : IAgentToolHistoricalReceiptReadProvider {
    public bool Supports(string toolName) => ProjectLifecycleToolProposalCodec.Supports(toolName);

    public async ValueTask<AgentToolHistoricalReceiptObservation> ReadAsync(AgentToolHistoricalReceiptReadClaim claim,
        CancellationToken cancellationToken = default) {
        var admitted = await claim.RequireAsync(cancellationToken);
        var (source, input) = ProjectLifecycleToolProposalCodec.Read(admitted.Payload);
        ProjectDefinitionToolReceiptRecovery.RequireSession(source, admitted.OriginalSession);
        var original = ProjectLifecycleToolProposalCodec.Submission(source, admitted.Binding.IntentId, input);
        var lookup = await receipts.LookupAsync(original.Operation,
            new(source.Caller, new HistoricalAccess(source, original, claim, definitions, targets)), cancellationToken);
        await claim.RequireAsync(cancellationToken);
        return lookup switch {
            ProjectLifecycleLookup.Found found when found.Accepted.Original == original => new(AgentHistoricalReceiptKnowledge.Accepted,
                ProjectLifecycleToolReceiptRecovery.Receipt(found.Accepted, codec)),
            ProjectLifecycleLookup.Found => throw new InvalidDataException("The historical hierarchy receipt differs from its exact original intent."),
            ProjectLifecycleLookup.NotObserved => new(AgentHistoricalReceiptKnowledge.NotObserved),
            ProjectLifecycleLookup.ClosedAbsent => new(AgentHistoricalReceiptKnowledge.ClosedAbsent),
            ProjectLifecycleLookup.Expired => new(AgentHistoricalReceiptKnowledge.Expired),
            ProjectLifecycleLookup.ContextChanged { Change: ProjectLifecycleContextChange.Dataset } => new(AgentHistoricalReceiptKnowledge.DatasetChanged),
            ProjectLifecycleLookup.ContextChanged => throw new AgentHistoricalReceiptContextChangedException(),
            ProjectLifecycleLookup.Unavailable { Reason: ProjectLifecycleUnavailableReason.AccessDenied } => new(AgentHistoricalReceiptKnowledge.ReadDenied),
            ProjectLifecycleLookup.Unavailable => new(AgentHistoricalReceiptKnowledge.ReadUnavailable),
            _ => throw new InvalidDataException("The historical lifecycle owner returned an unsupported observation.")
        };
    }

    private sealed class HistoricalAccess(ProjectDefinitionToolSource source, ProjectLifecycleSubmission original,
        AgentToolHistoricalReceiptReadClaim claim, ProjectDefinitionHistoricalReceiptRecovery definitions,
        ProjectCreatedTargetProvenance targets) : IProjectLifecycleReceiptAccess {
        public async Task<bool> CanReadAsync(ProjectLifecycleSubmission submission, CancellationToken cancellationToken) {
            if (submission != original) {
                throw new InvalidDataException("The hierarchy owner returned another original submission.");
            }
            try {
                var prefix = await claim.RequirePriorInvocationsAsync(cancellationToken);
                var sources = await targets.ResolveHistoricalAsync(source, original, prefix, LookupCreationAsync, cancellationToken);
                var access = sources.Process is { } process ? definitions.ForProcess(process, claim) : definitions.ForAgent(sources.Agent!, claim);
                return await new ProjectHierarchyReceiptAccess(access).CanReadAsync(original, cancellationToken);
            } catch (Exception failure) when (failure is AgentToolReceiptAccessDeniedException or AgentToolAdmissionException) {
                return false;
            }
        }

        private Task<(ProjectDefinitionLookup Lookup, ProjectCreationReservation? Reservation)> LookupCreationAsync(
            ProjectDefinitionToolSource prior, AgentToolBusinessIntentId intent, string tool, ProjectDefinitionToolArguments arguments,
            CancellationToken cancellationToken)
            => definitions.LookupCreationAsync(prior, intent, tool, arguments, claim, cancellationToken);
    }
}
