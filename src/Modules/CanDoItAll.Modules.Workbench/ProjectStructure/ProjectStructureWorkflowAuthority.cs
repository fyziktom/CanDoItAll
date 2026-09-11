using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.Workflows.Abstractions;
using CanDoItAll.Infrastructure.ControlPlane;
using CanDoItAll.Modules.Workspace.ApiAccess;
using CanDoItAll.Processes.Abstractions;
using CanDoItAll.Processes.Runtime;
using Microsoft.Extensions.Options;

namespace CanDoItAll.Modules.Workbench;

public sealed class ProjectStructureWorkflowAuthoritySource {
    private ProjectStructureWorkflowAuthoritySource(WorkflowStructureAuthorityChannel channel,
        WorkflowLaunchActor principal, WorkflowStructureOperatorSurface surface, DateTimeOffset? expiresAtUtc,
        bool tasks, bool assets, AgentExecutionGovernanceSnapshot? governance, Guid? projectId,
        Guid? processRunId, Guid? processStepId) {
        Channel = channel;
        Principal = principal;
        Surface = surface;
        ExpiresAtUtc = expiresAtUtc;
        Tasks = tasks;
        Assets = assets;
        Governance = governance;
        ProjectId = projectId;
        ProcessRunId = processRunId;
        ProcessStepId = processStepId;
    }

    internal WorkflowStructureAuthorityChannel Channel { get; }
    internal WorkflowLaunchActor Principal { get; }
    internal WorkflowStructureOperatorSurface Surface { get; }
    internal DateTimeOffset? ExpiresAtUtc { get; }
    internal bool Tasks { get; }
    internal bool Assets { get; }
    internal AgentExecutionGovernanceSnapshot? Governance { get; }
    internal Guid? ProjectId { get; }
    internal Guid? ProcessRunId { get; }
    internal Guid? ProcessStepId { get; }

    public static ProjectStructureWorkflowAuthoritySource LocalOperator(WorkflowStructureOperatorSurface surface)
        => new(WorkflowStructureAuthorityChannel.LocalOperator,
            new WorkflowLaunchActor(WorkflowLaunchActorKind.User, "local-operator"), surface, null, true, true, null, null, null, null);

    public static ProjectStructureWorkflowAuthoritySource AuthenticatedOperator(string subject, DateTimeOffset expiresAtUtc)
        => new(WorkflowStructureAuthorityChannel.AuthenticatedOperator,
            new WorkflowLaunchActor(WorkflowLaunchActorKind.User, subject), WorkflowStructureOperatorSurface.Api,
            expiresAtUtc, true, true, null, null, null, null);

    public static ProjectStructureWorkflowAuthoritySource Agent(Guid agentId, Guid projectId, bool tasks, bool assets,
        AgentExecutionGovernanceSnapshot? governance, Guid? processRunId = null, Guid? processStepId = null)
        => new(WorkflowStructureAuthorityChannel.AgentExecution,
            new WorkflowLaunchActor(WorkflowLaunchActorKind.Agent, agentId.ToString("D")), default, null,
            tasks, assets, governance, projectId, processRunId, processStepId);
}

public sealed class ProjectStructureWorkflowAuthorityService(
    ICanonicalRuntimeDatabase canonicalDatabase,
    IOptionsMonitor<ApiAccessOptions> apiOptions,
    IAgentFrameworkWorkspaceService workspace,
    IProcessRuntimeStateStore processStates,
    IProcessRuntimeStepAssignmentStore processAssignments,
    TimeProvider timeProvider,
    IWorkflowScheduledAuthorityPolicy scheduledAuthority) : IWorkflowStructureAuthorityFactory {
    public Task<WorkflowStructureAuthority> CaptureLocalOperatorAsync(WorkflowStructureOperatorSurface surface,
        CancellationToken cancellationToken = default)
        => CaptureAsync(Guid.Empty, ProjectStructureWorkflowAuthoritySource.LocalOperator(surface), cancellationToken);

    public Task<WorkflowStructureAuthority> CaptureAuthenticatedOperatorAsync(string subject, DateTimeOffset expiresAtUtc,
        CancellationToken cancellationToken = default)
        => CaptureAsync(Guid.Empty, ProjectStructureWorkflowAuthoritySource.AuthenticatedOperator(subject, expiresAtUtc), cancellationToken);

    public WorkflowStructureAuthority CaptureAgent(AgentDefinition agent, AgentExecutionGovernanceSnapshot governance) {
        if (agent.Id != governance.AgentId || governance.DatabaseProfileId != canonicalDatabase.Profile.Profile.Id) {
            throw Denied("The workflow authority source belongs to another agent or database profile.");
        }

        var access = AgentProjectStructureAccessMetadata.Read(agent.ConfigurationJson);
        var projectId = governance.WorkspaceScope.Kind == WorkspaceScopeKind.Project
            ? Guid.Parse(governance.WorkspaceScope.Key) : Guid.Empty;
        return new WorkflowStructureAuthority(WorkflowStructureAuthorityChannel.AgentExecution,
            new WorkflowLaunchActor(WorkflowLaunchActorKind.Agent, agent.Id.ToString("D")), governance.DatabaseProfileId,
            projectId, governance.MutationAllowed && ProjectStructureNonTaskWritePolicy.CanUseTaskMutationTools(access),
            governance.MutationAllowed && ProjectStructureNonTaskWritePolicy.CanUseStructureMutationTools(access),
            null, governance.PolicyFingerprint) {
            AgentGovernance = governance,
            AllProjects = projectId == Guid.Empty && access.AllowAllProjects,
            ProjectIds = projectId == Guid.Empty ? access.AllowedProjectIds.ToArray() : [projectId]
        };
    }

    public async Task<WorkflowStructureAuthority> CaptureAsync(Guid projectId,
        ProjectStructureWorkflowAuthoritySource? source, CancellationToken cancellationToken = default) {
        if (source is null || source.ProjectId.HasValue && source.ProjectId != projectId) {
            throw Denied("A trusted workflow launch authority source is required.");
        }

        WorkflowStructureProcessAuthority? processAuthority = null;
        if (source.ProcessRunId.HasValue && source.ProcessStepId.HasValue) {
            var assignment = (await processAssignments.LoadByRunAsync(new ProcessRunId(source.ProcessRunId.Value), cancellationToken))
                .SingleOrDefault(item => item.StepInstanceId.Value == source.ProcessStepId.Value)
                ?? throw Denied("The saved process assignment no longer exists.");
            processAuthority = new(source.ProcessRunId.Value, source.ProcessStepId.Value, assignment.ReadinessHash);
        }

        var authority = new WorkflowStructureAuthority(source.Channel, source.Principal,
            canonicalDatabase.Profile.Profile.Id, projectId, source.Tasks, source.Assets, source.ExpiresAtUtc,
            source.Channel == WorkflowStructureAuthorityChannel.AgentExecution
                ? source.Governance?.PolicyFingerprint ?? ProjectWorkflowContributionFingerprint.Hash(JsonSerializer.Serialize(new {
                    source.Principal, projectId, source.Tasks, source.Assets, processAuthority
                }))
                : OperatorPolicyFingerprint(source.Surface)) {
            OperatorSurface = source.Surface,
            AgentGovernance = source.Governance,
            ProcessAuthority = processAuthority,
            AllProjects = projectId == Guid.Empty && source.Channel != WorkflowStructureAuthorityChannel.AgentExecution
        };
        await EnsureCurrentAsync(authority, null, cancellationToken);
        return authority;
    }

    public Task EnsureCurrentAsync(WorkflowStructureAuthority authority, WorkflowStructureOutputKind? kind,
        CancellationToken cancellationToken = default) => EnsureCurrentCoreAsync(authority, kind, false, cancellationToken);

    public Task EnsureCurrentForMutationAsync(WorkflowStructureAuthority authority, WorkflowStructureOutputKind? kind,
        CancellationToken cancellationToken = default) => EnsureCurrentCoreAsync(authority, kind, true, cancellationToken);

    private async Task EnsureCurrentCoreAsync(WorkflowStructureAuthority authority, WorkflowStructureOutputKind? kind,
        bool forMutation, CancellationToken cancellationToken) {
        if (!Enum.IsDefined(authority.Channel) || !Enum.IsDefined(authority.OperatorSurface) ||
            string.IsNullOrWhiteSpace(authority.PolicyFingerprint) ||
            authority.DatabaseProfileId != canonicalDatabase.Profile.Profile.Id ||
            authority.ProjectId == Guid.Empty && (kind.HasValue || !authority.AllProjects) ||
            authority.ExpiresAtUtc <= timeProvider.GetUtcNow() ||
            kind == WorkflowStructureOutputKind.Task && !authority.CanCreateTasks ||
            kind == WorkflowStructureOutputKind.Asset && !authority.CanCreateAssets) {
            throw Denied("The admitted workflow authority does not permit this output.");
        }

        if (authority.SchedulerAuthority is { } scheduler) {
            if (forMutation) {
                await scheduledAuthority.RequireCurrentForMutationAsync(scheduler, cancellationToken);
            } else {
                await scheduledAuthority.RequireCurrentAsync(scheduler, cancellationToken);
            }
        }

        if (authority.Channel != WorkflowStructureAuthorityChannel.AgentExecution) {
            var options = apiOptions.CurrentValue;
            if (authority.Principal.Kind != WorkflowLaunchActorKind.User ||
                authority.PolicyFingerprint != OperatorPolicyFingerprint(authority.OperatorSurface) ||
                authority.OperatorSurface == WorkflowStructureOperatorSurface.Api && (!options.Enabled ||
                    authority.Channel == WorkflowStructureAuthorityChannel.LocalOperator && options.Authorization.Enabled ||
                    authority.Channel == WorkflowStructureAuthorityChannel.AuthenticatedOperator && (!options.Authorization.Enabled || !authority.ExpiresAtUtc.HasValue))) {
                throw Denied("The operator policy changed or no longer authorizes this workflow output.");
            }

            return;
        }

        if (authority.Principal.Kind != WorkflowLaunchActorKind.Agent || !Guid.TryParse(authority.Principal.SubjectId, out var agentId)) {
            throw Denied("The admitted workflow agent identity is invalid.");
        }

        var agent = (await workspace.ListAgentsAsync(false, cancellationToken)).SingleOrDefault(item => item.Id == agentId);
        if (agent is null || agent.IsTemplate || agent.Status != AgentLifecycleStatus.Active || !agent.Permissions.CanUseTools ||
            authority.SchedulerAuthority is not null && !agent.Permissions.CanScheduleWork) {
            throw Denied("The workflow agent is no longer active or permitted to use tools.");
        }

        if (authority.AgentGovernance is { } governance &&
            (governance.AgentId != agentId || governance.DatabaseProfileId != authority.DatabaseProfileId ||
             !governance.ReadAllowed || !governance.MutationAllowed ||
             governance.WorkspaceScope.Kind == WorkspaceScopeKind.Project &&
                (!Guid.TryParse(governance.WorkspaceScope.Key, out var admittedProject) || admittedProject != authority.ProjectId) ||
             governance.WorkspaceScope.Kind is not (WorkspaceScopeKind.Project or WorkspaceScopeKind.Organization) ||
             kind.HasValue && governance.AllowedOperations.Count > 0 && !governance.AllowedOperations.Contains(
                 kind == WorkflowStructureOutputKind.Task ? ProjectStructureToolPolicy.ProjectStructureNodeCreate : ProjectStructureToolPolicy.ProjectStructureAssetCreate))) {
            throw Denied("The admitted execution ceiling does not permit this project output.");
        }

        if (authority.ProcessAuthority is { } process) {
            var state = await processStates.LoadAsync(new ProcessRunId(process.RunId), cancellationToken);
            var assignment = (await processAssignments.LoadByRunAsync(new ProcessRunId(process.RunId), cancellationToken))
                .SingleOrDefault(item => item.StepInstanceId.Value == process.StepInstanceId);
            if (state is null || state.Status is ProcessRuntimeStatus.CancelRequested or ProcessRuntimeStatus.Cancelled or ProcessRuntimeStatus.Completed or ProcessRuntimeStatus.Failed ||
                assignment is null || assignment.ReadinessHash != process.ReadinessHash ||
                !Guid.TryParse(assignment.ExecutorId, out var assignedAgent) || assignedAgent != agentId ||
                !assignment.AllowedOperations.Contains(ProcessOperationContractNames.ExecuteExternalAction, StringComparer.Ordinal) ||
                !assignment.LaunchVariables.TryGetValue("ProjectId", out var project) || !Guid.TryParse(project, out var assignedProject) || assignedProject != authority.ProjectId ||
                state.Steps.All(step => step.StepInstanceId.Value != process.StepInstanceId || step.Status is ProcessRuntimeStepStatus.Cancelled or ProcessRuntimeStepStatus.Completed or ProcessRuntimeStepStatus.Skipped)) {
                throw Denied("The process assignment no longer permits workflow outputs.");
            }

            return;
        }

        var access = AgentProjectStructureAccessMetadata.Read(agent.ConfigurationJson);
        if ((!access.AllowAllProjects && !access.AllowedProjectIds.Contains(authority.ProjectId)) || !access.CanRead ||
            (kind == WorkflowStructureOutputKind.Task ? !ProjectStructureNonTaskWritePolicy.CanUseTaskMutationTools(access)
                : !ProjectStructureNonTaskWritePolicy.CanUseStructureMutationTools(access))) {
            throw Denied("Current agent policy no longer permits this project operation.");
        }
    }

    private string OperatorPolicyFingerprint(WorkflowStructureOperatorSurface surface) {
        if (surface == WorkflowStructureOperatorSurface.UserInterface) {
            return ProjectWorkflowContributionFingerprint.Hash("structure-local-ui-v1");
        }

        var options = apiOptions.CurrentValue;
        return ProjectWorkflowContributionFingerprint.Hash(JsonSerializer.Serialize(new {
            ApiEnabled = options.Enabled,
            AuthorizationEnabled = options.Authorization.Enabled,
            options.Authorization.Issuer,
            options.Authorization.Audience,
            SigningKeyFingerprint = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(options.Authorization.SigningKey)))
        }));
    }

    private static ProjectStructureAgentException Denied(string message) => new(403, "WorkflowAuthorityDenied", message);
}
