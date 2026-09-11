using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.Workflows.Abstractions;

namespace CanDoItAll.Modules.Workbench;

public sealed partial class ProjectStructureWorkflowAuthorityService {
    public async Task<IAsyncDisposable> AcquireProviderDisclosureAsync(WorkflowRunSnapshot originalRun,
        CancellationToken cancellationToken = default) {
        if (originalRun.Origin is WorkflowLaunchOrigin.ProcessDispatchAssignment or WorkflowLaunchOrigin.ProcessAssignment) {
            var original = await ReadMappedChildForReadAsync(originalRun, cancellationToken);
            var mappedLease = await AcquireMappedSourceAsync(original, mutation: false, cancellationToken);
            try {
                var confirmed = await ReadMappedChildForReadAsync(originalRun, cancellationToken);
                if (confirmed.OwnerFingerprint != original.OwnerFingerprint ||
                        confirmed.SourceAuthority?.ProjectAdmission != original.SourceAuthority?.ProjectAdmission) {
                    throw Denied("The retained Process source changed before provider disclosure.");
                }
                return mappedLease;
            } catch {
                await mappedLease.DisposeAsync();
                throw;
            }
        }
        var lease = await AcquireReadAsync(originalRun, cancellationToken);
        try {
            if (originalRun.Origin?.StructureAuthority is { } authority) {
                var scope = authority.ProjectScope ?? throw new WorkflowStructureLegacyLineageException();
                var targets = scope.Projects.Where(project => scope.AdmissionProjectIds.Contains(project.ProjectId)).ToArray();
                if (authority.ProjectId != Guid.Empty && !targets.Any(project => project.ProjectId == authority.ProjectId)) {
                    throw Denied("The Workflow source has no retained original project lifetime for provider disclosure.");
                }
                await lease.RequireCurrentAsync(targets.Select(ToProjectAdmission).ToArray(), cancellationToken);
            }
            return lease;
        } catch {
            await lease.DisposeAsync();
            throw;
        }
    }
}
