using System.Text.Json;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.Workflows.Abstractions;
using CanDoItAll.Modules.Projects;
using CanDoItAll.Processes.Application;
using CanDoItAll.Processes.Runtime;

namespace CanDoItAll.Modules.Workbench;

public sealed partial class ProjectStructureWorkflowAuthorityService {
    internal async Task<WorkflowStructureAuthority> CaptureForNodeAsync(Guid projectId, ProjectStructureAgentContext agent,
        CancellationToken cancellationToken) {
        if (agent.ProcessMutationAdmission is { } process) {
            if (agent.AgentMutationAdmission is not null || agent.ExpectedProjectAdmission is { } expected && expected != process.ProjectAdmission) {
                throw Denied("The Workflow producer mixed authority channels or changed its original Process project lifetime.");
            }
            return CaptureProcess(process, projectId);
        }
        var target = agent.ExpectedProjectAdmission ?? await RequireProjectAdmissions().CaptureAsync(projectId, cancellationToken)
            ?? throw Denied("The Workflow project no longer exists.");
        if (target.ProjectId != projectId || target.DatabaseProfileId != canonicalDatabase.Profile.Profile.Id) {
            throw Denied("The Workflow producer's captured project does not match its target.");
        }
        var source = agent.WorkflowAuthority ?? throw Denied("The Workflow producer has no trusted source authority.");
        WorkflowStructureAuthority authority;
        if (agent.AgentMutationAdmission is { } original) {
            if (source.Channel != WorkflowStructureAuthorityChannel.AgentExecution || original.AgentId.ToString("D") != source.Principal.SubjectId ||
                    original.DatabaseProfileId != target.DatabaseProfileId || original.Governance is null ||
                    !original.AllowAllProjects && (!original.ProjectIds.Contains(projectId) ||
                        !original.ProjectLifetimes.Contains(new(target.DatabaseProfileId, target.ProjectId, target.LifetimeId)))) {
                throw Denied("The original Agent invocation does not cover this Workflow target and lifetime.");
            }
            authority = new(WorkflowStructureAuthorityChannel.AgentExecution, source.Principal, target.DatabaseProfileId, projectId,
                original.CanWriteTasks && source.Tasks, original.CanWriteStructure && source.Assets,
                source.ExpiresAtUtc, original.Governance.PolicyFingerprint) { AgentGovernance = original.Governance };
        } else if (source.Channel != WorkflowStructureAuthorityChannel.AgentExecution) {
            authority = await CaptureAsync(Guid.Empty, source, cancellationToken);
        } else {
            throw new WorkflowStructureLegacyLineageException();
        }
        authority = authority with { ProjectId = projectId, AllProjects = false, ProjectIds = [projectId],
            ProjectScope = new([ToWorkflowLifetime(target)], [projectId]) };
        await using var current = await AcquireAsync(authority, WorkflowStructureAuthorityUse.Admission, cancellationToken: cancellationToken);
        return authority;
    }

    internal bool MatchesNodeProducer(WorkflowStructureAuthority authority, ProjectStructureAgentContext producer) {
        if (producer.ExpectedProjectAdmission is { } expected && authority.ProjectScope is { } scope &&
                scope.Find(expected.ProjectId) != ToWorkflowLifetime(expected)) {
            return false;
        }
        if (authority.ProcessAuthority is { } process) {
            return producer.ProcessMutationAdmission is { } admitted &&
                admitted.Dispatch.Evidence.ExecutionRunId == process.ExecutionRunId &&
                admitted.Dispatch.Evidence.RunId.Value == process.RunId &&
                admitted.Dispatch.Evidence.StepInstanceId.Value == process.StepInstanceId &&
                admitted.Dispatch.OwnerFingerprint == process.OwnerFingerprint;
        }
        if (producer.ProcessMutationAdmission is not null || producer.WorkflowAuthority is not { } source ||
                source.Channel != authority.Channel || source.Principal != authority.Principal) {
            return false;
        }
        return authority.AgentGovernance is null || source.Governance is { } governance &&
            governance.AuthorityId == authority.AgentGovernance.AuthorityId &&
            governance.PolicyFingerprint == authority.AgentGovernance.PolicyFingerprint;
    }

    private WorkflowStructureAuthority CaptureProcess(ProjectProcessMutationAdmission admission, Guid projectId)
        => CaptureProcess(admission.Dispatch, projectId);

    private WorkflowStructureAuthority CaptureProcess(ProcessExecutionDispatchAuthority dispatch, Guid projectId,
        WorkflowProcessToolBinding? tool = null) {
        var source = dispatch.SourceAuthority ?? throw new WorkflowStructureLegacyLineageException();
        source.Validate();
        var project = source.ProjectAdmission;
        if (project is not null && (project.ProjectId != projectId || dispatch.ProjectReference is null) ||
                project is null && (projectId != Guid.Empty || tool is null) || dispatch.Evidence.ExecutionRunId == Guid.Empty ||
                !dispatch.AllowedOperations.Contains(tool is null ? ProcessOperationContractNames.ExecuteExternalAction : ProcessOperationContractNames.LaunchRuntime,
                    StringComparer.OrdinalIgnoreCase)) {
            throw Denied("The original Process execution does not admit this Workflow project target.");
        }
        WorkflowStructureAuthorityChannel channel;
        WorkflowLaunchActor principal;
        WorkflowStructureOperatorSurface surface = default;
        DateTimeOffset? expiresAtUtc = null;
        AgentExecutionGovernanceSnapshot? governance = null;
        switch (source.Principal) {
            case ProcessLaunchPrincipal.AgentExecution agent:
                channel = WorkflowStructureAuthorityChannel.AgentExecution;
                principal = new(WorkflowLaunchActorKind.Agent, agent.Ceiling.AgentId.ToString("D"));
                var ceiling = agent.Ceiling;
                var workspace = ceiling.WorkspaceScopeKind switch {
                    ProcessLaunchSourceScopeKind.Project => WorkspaceScopeDescriptor.Project(ceiling.WorkspaceScopeKey),
                    ProcessLaunchSourceScopeKind.Organization => WorkspaceScopeDescriptor.Organization(ceiling.WorkspaceScopeKey),
                    _ => throw Denied("The saved Process source workspace cannot authorize Workflow project outputs.")
                };
                governance = new(new(ceiling.AuthorityId), ceiling.AgentId, source.DatabaseProfileId,
                    new(ceiling.DatabaseProfileGeneration), workspace, ceiling.ReadAllowed, ceiling.MutationAllowed,
                    ceiling.PolicyVersion, ceiling.PolicyFingerprint, ceiling.AllowedOperations, ceiling.AllowedCapabilityKeys,
                    ceiling.WritableExternalTargetAliases, ceiling.ReadOnlyExternalTargetAliases, ceiling.AllowedManagedArtifactReadRefs);
                break;
            case ProcessLaunchPrincipal.LocalOperator local:
                channel = WorkflowStructureAuthorityChannel.LocalOperator;
                principal = new(WorkflowLaunchActorKind.User, "local-operator");
                surface = local.Surface switch {
                    ProcessLaunchOperatorSurface.UserInterface => WorkflowStructureOperatorSurface.UserInterface,
                    ProcessLaunchOperatorSurface.Api => WorkflowStructureOperatorSurface.Api,
                    _ => throw Denied("The original Process operator surface is invalid.")
                };
                break;
            case ProcessLaunchPrincipal.AuthenticatedOperator authenticated:
                channel = WorkflowStructureAuthorityChannel.AuthenticatedOperator;
                principal = new(WorkflowLaunchActorKind.User, authenticated.SubjectId);
                surface = WorkflowStructureOperatorSurface.Api;
                expiresAtUtc = authenticated.ExpiresAtUtc;
                break;
            default:
                throw new WorkflowStructureLegacyLineageException();
        }
        var canWrite = project is not null && dispatch.AllowedOperations.Contains(ProcessOperationContractNames.ExecuteExternalAction, StringComparer.OrdinalIgnoreCase);
        return new(channel, principal, source.DatabaseProfileId, projectId, canWrite && source.CanCreateTasks, canWrite && source.CanCreateAssets,
            expiresAtUtc, governance?.PolicyFingerprint ?? OperatorPolicyFingerprint(surface)) {
            OperatorSurface = surface,
            AgentGovernance = governance,
            ProcessAuthority = new(dispatch.Evidence.RunId.Value, dispatch.Evidence.StepInstanceId.Value, dispatch.ReadinessHash) {
                ExecutionRunId = dispatch.Evidence.ExecutionRunId,
                OwnerFingerprint = dispatch.OwnerFingerprint,
                ToolInvocation = tool
            },
            ProjectIds = project is null ? [] : [project.ProjectId],
            ProjectScope = project is null ? new([])
                : new([new(project.DatabaseProfileId, project.ProjectId, project.LifetimeId)], [project.ProjectId])
        };
    }

    private async Task<ProcessExecutionDispatchAuthority?> ReadProcessDispatchAsync(WorkflowStructureAuthority authority,
        WorkflowStructureAuthorityUse use, CancellationToken cancellationToken) {
        if (authority.ProcessAuthority is not { } process) {
            return null;
        }
        if (authority.SchedulerAuthority is not null || process.ExecutionRunId is not { } executionId || executionId == Guid.Empty ||
                string.IsNullOrWhiteSpace(process.OwnerFingerprint)) {
            throw new WorkflowStructureLegacyLineageException();
        }
        var observed = await (processReader ?? throw new InvalidOperationException("Workflow Process authority requires the saved execution authority reader."))
            .ReadAsync(executionId, cancellationToken);
        var dispatch = observed.Snapshot;
        if (dispatch is null || dispatch.OwnerFingerprint != process.OwnerFingerprint || dispatch.Evidence.ExecutionRunId != executionId ||
                dispatch.Evidence.RunId.Value != process.RunId || dispatch.Evidence.StepInstanceId.Value != process.StepInstanceId ||
                dispatch.ReadinessHash != process.ReadinessHash ||
                use != WorkflowStructureAuthorityUse.Disclosure && !dispatch.ObservedCurrentDispatch) {
            throw Denied("The original Process claim or source changed; this Workflow effect requires reconciliation.");
        }
        var original = CaptureProcess(dispatch, authority.ProjectId, process.ToolInvocation);
        if (process.ToolInvocation is { } tool) {
            RequireProcessToolBinding(tool, dispatch);
        }
        var access = await (processObservation ?? throw new InvalidOperationException("Workflow Process authority requires its original source policy."))
            .ObserveAsync(dispatch.SourceAuthority!, cancellationToken);
        if (!access.ReadAllowed || use != WorkflowStructureAuthorityUse.Disclosure && !access.DispatchAllowed) {
            throw Denied("The original Process source no longer permits this Workflow operation.");
        }
        if (original.Channel != authority.Channel || original.Principal != authority.Principal ||
                original.DatabaseProfileId != authority.DatabaseProfileId || original.PolicyFingerprint != authority.PolicyFingerprint ||
                original.CanCreateTasks != authority.CanCreateTasks || original.CanCreateAssets != authority.CanCreateAssets ||
                original.ExpiresAtUtc != authority.ExpiresAtUtc || original.OperatorSurface != authority.OperatorSurface ||
                authority.AllProjects != original.AllProjects || !authority.ProjectIds.Order().SequenceEqual(original.ProjectIds.Order()) ||
                !original.ProjectScope!.Projects.SequenceEqual(authority.ProjectScope!.Projects) ||
                JsonSerializer.Serialize(original.AgentGovernance, ScopeJson) != JsonSerializer.Serialize(authority.AgentGovernance, ScopeJson)) {
            throw Denied("The Workflow source differs from its saved original Process authority.");
        }
        return dispatch;
    }
}
