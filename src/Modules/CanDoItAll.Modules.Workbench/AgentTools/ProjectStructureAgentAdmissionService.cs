using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Modules.Projects;

namespace CanDoItAll.Modules.Workbench;

public sealed class ProjectStructureAgentAdmissionService(
    IAgentFrameworkWorkspaceService workspace,
    ProjectWriteAdmissionService projects,
    ProjectAgentSourceMutationAuthority? sourceAuthority = null,
    ProjectProcessExecutionMutationService? processAuthority = null) {
    internal ProjectAgentMutationAdmission CaptureMutationAuthority(Guid agentId, AgentProjectStructureAccessSettings access,
        AgentExecutionGovernanceSnapshot? governance, ProjectAgentMutationDomain domain)
        => new(agentId, projects.DatabaseProfileId, access, governance, domain, AgentRuntimeToolOwnershipContext.Current?.ToolName);

    internal ProjectMutationAuthorization? BindProjectSource(ProjectStructureAgentContext owner,
        ProjectProcessProjectOperation operation = ProjectProcessProjectOperation.Update) {
        if (owner.AgentMutationAdmission is not null && owner.ProcessMutationAdmission is not null) {
            throw new InvalidOperationException("A project mutation must retain exactly one original source authority.");
        }
        if (owner.AgentMutationAdmission is null && owner.ProcessMutationAdmission is null) {
            return null;
        }
        ProjectWriteAdmission[] expected = owner.ExpectedProjectAdmissions.IsDefaultOrEmpty
            ? owner.ExpectedProjectAdmission is { } single ? [single] : Array.Empty<ProjectWriteAdmission>()
            : owner.ExpectedProjectAdmissions.ToArray();
        if (owner.ProcessMutationAdmission is { } process) {
            return (processAuthority ?? throw new InvalidOperationException("The explicit Process project source authority is not installed."))
                .BindProjectSource(process, expected, operation);
        }
        return (sourceAuthority ?? throw new InvalidOperationException("The explicit Agent project source authority is not installed."))
            .Bind(owner.AgentMutationAdmission!, expected);
    }

    public Task<ProjectWriteAdmission> CaptureSubprojectParentAsync(Guid agentId, AgentProjectStructureAccessSettings invocationAccess,
        Guid projectId, CancellationToken cancellationToken = default)
        => CaptureWriteAsync(agentId, invocationAccess, projectId, taskWrite: false, cancellationToken, subprojectCreation: true);

    public Task<ProjectWriteAdmission> CaptureNonTaskWriteAsync(Guid agentId,
        AgentProjectStructureAccessSettings invocationAccess, Guid projectId, CancellationToken cancellationToken = default)
        => CaptureWriteAsync(agentId, invocationAccess, projectId, taskWrite: false, cancellationToken);

    public Task<ProjectWriteAdmission> CaptureTaskWriteAsync(Guid agentId,
        AgentProjectStructureAccessSettings invocationAccess, Guid projectId, CancellationToken cancellationToken = default)
        => CaptureWriteAsync(agentId, invocationAccess, projectId, taskWrite: true, cancellationToken);

    private async Task<ProjectWriteAdmission> CaptureWriteAsync(Guid agentId,
        AgentProjectStructureAccessSettings invocationAccess, Guid projectId, bool taskWrite, CancellationToken cancellationToken, bool subprojectCreation = false) {
        ArgumentNullException.ThrowIfNull(invocationAccess);
        var current = (await workspace.ListAgentsAsync(includeTemplates: false, cancellationToken))
            .SingleOrDefault(agent => agent.Id == agentId);
        if (current is null || current.Status != AgentLifecycleStatus.Active || current.IsTemplate || !current.Permissions.CanUseTools) {
            throw Denied(projectId);
        }
        var currentAccess = AgentProjectStructureAccessMetadata.Read(current.ConfigurationJson);
        if (!CanWrite(invocationAccess, taskWrite, subprojectCreation) || !CanWrite(currentAccess, taskWrite, subprojectCreation)) {
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

    private static bool CanWrite(AgentProjectStructureAccessSettings access, bool taskWrite, bool subprojectCreation) =>
        access.CanRead && (subprojectCreation ? access.CanCreateSubprojects : access.CanWrite || (taskWrite ? access.CanWriteTasks : access.CanWriteNonTaskStructure));

    private static bool Allows(AgentProjectStructureAccessSettings access, ProjectWriteAdmission admission) =>
        access.AllowAllProjects || access.AllowedProjectIds.Contains(admission.ProjectId) &&
        access.AllowedProjectLifetimes.Contains(new(admission.DatabaseProfileId, admission.ProjectId, admission.LifetimeId));

    private static ProjectStructureAgentException Denied(Guid projectId) => new(403, "ProjectLifetimeAccessDenied",
        $"The agent's captured and current grants do not authorize the live lifetime of project '{projectId:D}'.");
}
