using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Modules.Projects;

namespace CanDoItAll.Modules.Workbench;

public sealed class ProjectStructureAgentAdmissionService(
    IAgentFrameworkWorkspaceService workspace,
    ProjectWriteAdmissionService projects) {
    public async Task<ProjectWriteAdmission> CaptureNonTaskWriteAsync(Guid agentId,
        AgentProjectStructureAccessSettings invocationAccess, Guid projectId, CancellationToken cancellationToken = default) {
        ArgumentNullException.ThrowIfNull(invocationAccess);
        var current = (await workspace.ListAgentsAsync(includeTemplates: false, cancellationToken))
            .SingleOrDefault(agent => agent.Id == agentId);
        if (current is null || current.Status != AgentLifecycleStatus.Active || current.IsTemplate || !current.Permissions.CanUseTools) {
            throw Denied(projectId);
        }
        var currentAccess = AgentProjectStructureAccessMetadata.Read(current.ConfigurationJson);
        if (!CanWrite(invocationAccess) || !CanWrite(currentAccess)) {
            throw Denied(projectId);
        }
        var admission = await projects.CaptureAsync(projectId, cancellationToken);
        if (admission is null || !Allows(invocationAccess, admission) || !Allows(currentAccess, admission)) {
            throw Denied(projectId);
        }
        return admission;
    }

    public async Task<ProjectWriteAdmission> ValidateProducerWriteAsync(ProjectWriteAdmission admission, Guid projectId,
        CancellationToken cancellationToken = default) {
        ArgumentNullException.ThrowIfNull(admission);
        if (admission.ProjectId != projectId) {
            throw Denied(projectId);
        }
        await projects.RequireCurrentAsync(admission, cancellationToken);
        return admission;
    }

    private static bool CanWrite(AgentProjectStructureAccessSettings access) => access.CanWrite || access.CanWriteNonTaskStructure;

    private static bool Allows(AgentProjectStructureAccessSettings access, ProjectWriteAdmission admission) =>
        access.AllowAllProjects || access.AllowedProjectIds.Contains(admission.ProjectId) &&
        access.AllowedProjectLifetimes.Contains(new(admission.DatabaseProfileId, admission.ProjectId, admission.LifetimeId));

    private static ProjectStructureAgentException Denied(Guid projectId) => new(403, "ProjectLifetimeAccessDenied",
        $"The agent's captured and current grants do not authorize the live lifetime of project '{projectId:D}'.");
}
