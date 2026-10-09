using CanDoItAll.Infrastructure.ControlPlane;
using CanDoItAll.Modules.Projects;
using CanDoItAll.Processes.Application;

namespace CanDoItAll.Modules.Processes;

public sealed class ProcessAuthoringAdmissionPolicy(ICanonicalRuntimeDatabase database, ProjectWriteAdmissionService projects)
    : IProcessAuthoringAdmissionPolicy {
    public const string LocalCallerId = "local-process-authoring-operator";

    public async Task RequireAsync(ProcessAuthoringAddress address, string? callerId, bool underMutationGate, CancellationToken cancellationToken) {
        address.Validate();
        if (address.DatabaseProfileId != database.Profile.Profile.Id || callerId is not null && callerId != LocalCallerId) {
            throw new InvalidOperationException("The authoring caller or captured database profile is no longer current.");
        }
        if (address.ProjectId == Guid.Empty) {
            return;
        }
        ProjectWriteAdmission admission = new(address.DatabaseProfileId, address.ProjectId, address.ProjectLifetimeId);
        if (underMutationGate) {
            await projects.RequireUnderMutationGateAsync(admission, cancellationToken);
        } else {
            await projects.RequireCurrentAsync(admission, cancellationToken);
        }
    }
}
