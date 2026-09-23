using CanDoItAll.Processes.Application;
using CanDoItAll.Processes.Runtime;

namespace CanDoItAll.Modules.Workbench;

public sealed partial class ProjectStructureProcessNodeService {
    private static void RequireInvocationInput(ProjectStructureProcessLaunchInvocation invocation, Guid projectId,
        string nodeId, ProjectStructureProcessNodeStartInput request) {
        invocation.Authority.Validate();
        if (invocation.IntentId.Value == Guid.Empty || invocation.Authority.ProjectAdmission?.ProjectId != projectId ||
                invocation.Proposal.ProjectId != projectId || invocation.Proposal.NodeId != nodeId || invocation.Proposal.Request != request ||
                new ProjectStructureProcessProposalCodec().Prepare(invocation.Proposal).Digest.Value != invocation.InputFingerprint) {
            throw new ProcessLaunchAuthorityRejectedException("The Structure Process request differs from its server-admitted tool proposal.");
        }
    }

    private static async Task<ProcessLaunchRequest?> FindRetainedLaunchRequestAsync(ProjectStructureProcessNodeScopedDependencies dependencies,
        ProjectStructureProcessLaunchInvocation invocation, bool execute, CancellationToken cancellationToken) {
        var store = dependencies.Preparations ?? throw new InvalidOperationException("Admitted Structure Process launches require the owner preparation store.");
        var saved = await store.FindByIntentAsync(invocation.IntentId, cancellationToken);
        if (saved is null) {
            return null;
        }
        if (saved.Preparation.Request.ProducerInputFingerprint != invocation.InputFingerprint) {
            throw new ProcessLaunchIntentConflictException(invocation.IntentId, "The Process business intent already belongs to a different Structure proposal.");
        }
        return ProcessLaunchProducerRequests.Restore(saved, invocation.Authority, execute);
    }

    private static async Task<ProcessLaunchRequest> BindInvocationAsync(ProjectStructureProcessNodeScopedDependencies dependencies,
        ProcessLaunchRequest request, ProjectStructureNode targetNode, ProjectStructureProcessLaunchInvocation invocation,
        CancellationToken cancellationToken) {
        var target = SupportsGenericProcessRunLink(targetNode)
            ? await (dependencies.Targets ?? throw new InvalidOperationException("Admitted Structure Process launches require the native target owner query."))
                .CaptureAsync(invocation.Proposal.ProjectId, targetNode.Id, cancellationToken)
            : null;
        return request with {
            CallerIntentId = invocation.IntentId,
            Authority = invocation.Authority,
            ProjectAdmission = invocation.Authority.ProjectAdmission,
            LinkTarget = target,
            ToolSource = invocation.ToolSource,
            ProducerInputFingerprint = invocation.InputFingerprint
        };
    }

    private static async Task<ProjectStructureProcessNodeStartResult> BuildAdmittedStartResultAsync(
        ProjectStructureProcessNodeScopedDependencies dependencies, Guid projectId, string nodeId,
        ProjectStructureProcessNodeStartInput request, ProcessLaunchResult launch, CancellationToken cancellationToken) {
        var observation = launch.Observation;
        var warnings = launch.Warnings.ToList();
        if (observation is { AcceptedRunId: not null, LinkDeliveryState: not ProcessLaunchLinkDeliveryState.NotRequested }) {
            try {
                var delivery = dependencies.Delivery ?? throw new InvalidOperationException("Admitted Structure Process links require their owner delivery service.");
                var receipt = await delivery.DeliverAsync(observation.AdmissionId, cancellationToken);
                observation = observation with {
                    LinkDeliveryState = receipt.State,
                    ObservationException = observation.ObservationException ?? receipt.ObservationException
                };
                if (receipt.State is ProcessLaunchLinkDeliveryState.Pending or ProcessLaunchLinkDeliveryState.Conflict or ProcessLaunchLinkDeliveryState.Removed) {
                    warnings.Add("The Process was accepted. Its retained Structure link receipt reports " + receipt.State + ".");
                }
            } catch (Exception exception) {
                observation = observation with { ObservationException = observation.ObservationException ?? exception };
                warnings.Add("The Process was accepted. Its Structure link delivery remains observable from the admission.");
            }
        }
        return new(projectId, nodeId, launch.DefinitionId.Value, launch.LaunchPlanId.Value, launch.RunId?.Value,
            launch.Stage.ToString(), launch.Route, request.IncludeLaunchPlan ? launch.LaunchPlan : null, warnings) {
            Observation = observation
        };
    }
}
