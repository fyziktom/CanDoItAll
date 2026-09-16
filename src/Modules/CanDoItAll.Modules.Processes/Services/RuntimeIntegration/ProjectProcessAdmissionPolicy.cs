using CanDoItAll.Modules.Projects;
using CanDoItAll.Processes.Runtime;

namespace CanDoItAll.Modules.Processes;

public sealed class ProjectProcessAdmissionPolicy(ProjectWriteAdmissionService admissions) : IProcessProjectAdmissionPolicy {
    public Task RequireForMutationAsync(ProcessProjectAdmission admission, CancellationToken cancellationToken = default) {
        ArgumentNullException.ThrowIfNull(admission);
        return admissions.RequireUnderMutationGateAsync(
            new(admission.DatabaseProfileId, admission.ProjectId, admission.LifetimeId), cancellationToken);
    }
}
