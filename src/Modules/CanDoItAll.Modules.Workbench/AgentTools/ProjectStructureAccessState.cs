using System.Collections.Concurrent;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.Tooling;
using CanDoItAll.Modules.Projects;
using CanDoItAll.Modules.Workbench.ProjectStructure;

namespace CanDoItAll.Modules.Workbench;

internal sealed class ProjectStructureAccessState {
    internal const string ProjectStructureSourceKind = "project-structure";

    public ProjectStructureAccessState(
        AgentProjectStructureAccessSettings settings,
        ProjectStructureScopedProcessAccess? scopedProcessAccess,
        AgentRuntimeContextIntent contextIntent,
        AgentRuntimeToolProviderPurpose purpose,
        ProjectStructureInvocationSnapshotReadContext invocationSnapshotReadContext,
        AgentExecutionGovernanceSnapshot? governance = null) {
        ArgumentNullException.ThrowIfNull(contextIntent);
        ArgumentNullException.ThrowIfNull(invocationSnapshotReadContext);

        // The admitted execution governance snapshot is the permission
        // ceiling for a context-admitted turn: durable configuration and
        // scoped process access can only narrow within it, never widen
        // beyond it. Runs without a snapshot (governed process steps,
        // detached conversations) keep their own authority sources.
        var governanceReadCeiling = governance?.ReadAllowed ?? true;
        var governanceMutationCeiling = governance?.MutationAllowed ?? true;
        var normalized = AgentProjectStructureAccessMetadata.Normalize(settings);
        var governed = purpose == AgentRuntimeToolProviderPurpose.GovernedProcessAutomation && scopedProcessAccess is not null;
        ProjectGrantSnapshot = normalized;
        CanRead = (governed ? scopedProcessAccess!.CanRead : normalized.CanRead) && governanceReadCeiling;
        CanWrite = (governed ? scopedProcessAccess!.CanWrite : ProjectStructureNonTaskWritePolicy.CanUseStructureMutationTools(normalized)) && governanceMutationCeiling;
        CanWriteUnscoped = normalized.CanWrite && governanceMutationCeiling && (!governed || scopedProcessAccess!.CanWrite);
        CanWriteStructureUnscoped = (normalized.CanWrite || normalized.CanWriteNonTaskStructure) && governanceMutationCeiling && (!governed || scopedProcessAccess!.CanWrite);
        CanWriteTasksUnscoped = ProjectStructureNonTaskWritePolicy.CanUseTaskMutationTools(normalized) && governanceMutationCeiling &&
            (!governed || scopedProcessAccess!.CanWrite && scopedProcessAccess.ProcessMutationAdmission?.Dispatch.SourceAuthority?.CanCreateTasks == true);
        var sourceProjects = scopedProcessAccess?.ProcessMutationAdmission?.Dispatch.SourceAuthority?.ProjectMutations;
        CanCreateProjects = normalized.CanCreateProjects && governanceMutationCeiling &&
            (!governed || scopedProcessAccess!.CanWrite && sourceProjects?.CanCreateProjects == true);
        CanCreateSubprojects = normalized.CanCreateSubprojects && governanceMutationCeiling &&
            (!governed || scopedProcessAccess!.CanWrite && sourceProjects is { } ceiling &&
                (ceiling.CanCreateSubprojects || ceiling.CanChangeHierarchy || ceiling.CanMoveNodesToSubproject));
        RequiresNonTaskWriteGuard = normalized.CanWriteNonTaskStructure &&
            !normalized.CanWrite &&
            scopedProcessAccess?.CanWrite != true;
        AllowAllProjects = !governed && normalized.AllowAllProjects;
        AllowedProjectIds = governed ? [] : normalized.AllowedProjectIds.ToHashSet();
        SessionCreatedProjectIds = [];
        ScopedProcessAccess = scopedProcessAccess;
        ContextIntent = contextIntent;
        Purpose = purpose;
        InvocationSnapshotReadContext = invocationSnapshotReadContext;
        Governance = governance;
        if (scopedProcessAccess is not null) {
            AllowedProjectIds.Add(scopedProcessAccess.ProjectId);
        }
    }

    public AgentRuntimeToolProviderContext? ProviderContext { get; init; }

    public bool CanRead { get; }

    public bool CanWrite { get; }

    public bool CanWriteUnscoped { get; }

    public bool CanWriteStructureUnscoped { get; }

    public bool CanWriteTasksUnscoped { get; }

    public bool CanCreateProjects { get; }

    public bool CanCreateSubprojects { get; }

    public bool RequiresNonTaskWriteGuard { get; }

    public bool AllowAllProjects { get; }

    public HashSet<Guid> AllowedProjectIds { get; }

    public HashSet<Guid> SessionCreatedProjectIds { get; }

    public AgentProjectStructureAccessSettings ProjectGrantSnapshot { get; }
    public ConcurrentDictionary<Guid, AgentProjectStructureLifetime> SessionCreatedLifetimes { get; } = new();
    public ConcurrentDictionary<Guid, ProjectCreationReservation> SessionCreatedReservations { get; } = new();

    public ProjectStructureScopedProcessAccess? ScopedProcessAccess { get; }

    public AgentRuntimeContextIntent ContextIntent { get; }

    public AgentRuntimeToolProviderPurpose Purpose { get; }

    public ProjectStructureInvocationSnapshotReadContext InvocationSnapshotReadContext { get; }

    public AgentExecutionGovernanceSnapshot? Governance { get; }

    internal static bool IsProjectAllowedForContext(
        AgentRuntimeToolProviderPurpose purpose,
        AgentRuntimeContextIntent contextIntent,
        bool allowAllProjects,
        IReadOnlySet<Guid> allowedProjectIds,
        IReadOnlySet<Guid> sessionCreatedProjectIds,
        Guid projectId) {
        ArgumentNullException.ThrowIfNull(contextIntent);
        ArgumentNullException.ThrowIfNull(allowedProjectIds);
        ArgumentNullException.ThrowIfNull(sessionCreatedProjectIds);

        if (purpose == AgentRuntimeToolProviderPurpose.InteractiveChat &&
            string.Equals(contextIntent.SourceKind, ProjectStructureSourceKind, StringComparison.OrdinalIgnoreCase) &&
            (!Guid.TryParse(contextIntent.SourceId, out var activeProjectId) ||
             activeProjectId == Guid.Empty ||
             activeProjectId != projectId && !sessionCreatedProjectIds.Contains(projectId))) {
            return false;
        }

        return allowAllProjects || allowedProjectIds.Contains(projectId);
    }

    internal static void EnsureProjectAllowedForContext(
        AgentRuntimeToolProviderPurpose purpose,
        AgentRuntimeContextIntent contextIntent,
        bool allowAllProjects,
        IReadOnlySet<Guid> allowedProjectIds,
        IReadOnlySet<Guid> sessionCreatedProjectIds,
        Guid projectId) {
        ArgumentNullException.ThrowIfNull(contextIntent);
        ArgumentNullException.ThrowIfNull(allowedProjectIds);
        ArgumentNullException.ThrowIfNull(sessionCreatedProjectIds);

        if (purpose == AgentRuntimeToolProviderPurpose.InteractiveChat &&
            string.Equals(contextIntent.SourceKind, ProjectStructureSourceKind, StringComparison.OrdinalIgnoreCase)) {
            if (!Guid.TryParse(contextIntent.SourceId, out var activeProjectId) ||
                activeProjectId == Guid.Empty) {
                throw new ProjectStructureAgentException(
                    403,
                    "ProjectStructureContextProjectInvalid",
                    "The project-structure chat does not identify a valid active project. Reopen the chat from the intended project.");
            }

            if (activeProjectId != projectId && !sessionCreatedProjectIds.Contains(projectId)) {
                throw new ProjectStructureAgentException(
                    403,
                    "ProjectStructureContextProjectDenied",
                    $"Project '{projectId:D}' is outside the active project-structure chat project '{activeProjectId:D}'.");
            }
        }

        if (allowAllProjects || allowedProjectIds.Contains(projectId)) {
            return;
        }

        throw new ProjectStructureAgentException(
            403,
            "ProjectStructureProjectDenied",
            $"Project '{projectId:D}' is outside the agent's allowed project-structure scope.");
    }

    public void EnsureReadAllowed() {
        if (CanRead) {
            return;
        }

        throw new ProjectStructureAgentException(
            403,
            "ProjectStructureReadDenied",
            "This agent is not allowed to read project structure. Enable read access in the agent settings.");
    }

    public void EnsureWriteAllowed() {
        if (CanWriteStructureUnscoped) {
            return;
        }

        throw new ProjectStructureAgentException(
            403,
            "ProjectStructureWriteDenied",
            "This agent is not allowed to write project structure. Enable write access in the agent settings.");
    }

    public void EnsureProjectCreationAllowed() {
        if (CanCreateProjects) {
            return;
        }

        throw new ProjectStructureAgentException(
            403,
            "ProjectCreationDenied",
            "This agent is not allowed to create standalone projects. Enable project creation in the agent settings.");
    }

    public bool AllowsGovernedProjectOperation(ProjectProcessProjectOperation operation) {
        if (Purpose != AgentRuntimeToolProviderPurpose.GovernedProcessAutomation) {
            return true;
        }
        var ceiling = ScopedProcessAccess?.ProcessMutationAdmission?.Dispatch.SourceAuthority?.ProjectMutations;
        return operation switch {
            ProjectProcessProjectOperation.CreateChild => ceiling?.CanCreateSubprojects == true,
            ProjectProcessProjectOperation.ChangeHierarchy => ceiling?.CanChangeHierarchy == true,
            ProjectProcessProjectOperation.MoveToChild => ceiling?.CanMoveNodesToSubproject == true,
            _ => false
        };
    }

    public void EnsureSubprojectCreationAllowed(ProjectProcessProjectOperation operation) {
        if (CanCreateSubprojects && AllowsGovernedProjectOperation(operation)) {
            return;
        }

        throw new ProjectStructureAgentException(
            403,
            "SubprojectCreationDenied",
            "This agent is not allowed to create or attach subprojects. Enable subproject creation in the agent settings.");
    }

    public void EnsureAnyWriteAllowed() {
        if (CanWrite) {
            return;
        }

        throw new ProjectStructureAgentException(
            403,
            "ProjectStructureWriteDenied",
            "This agent is not allowed to write project structure. Enable write access in the agent settings.");
    }

    public void EnsureProjectReadAllowed(Guid projectId) {
        EnsureReadAllowed();
        EnsureProjectAllowed(projectId);
    }

    public void EnsureProjectWriteAllowed(Guid projectId) {
        EnsureAnyWriteAllowed();
        EnsureProjectAllowed(projectId);
    }

    public void EnsureProjectTaskWriteAllowed(Guid projectId) {
        if (!CanWriteTasksUnscoped) {
            throw new ProjectStructureAgentException(
                403,
                "ProjectTaskWriteDenied",
                "This agent is not allowed to create or update project tasks. Enable task write access in the agent settings.");
        }

        EnsureProjectAllowed(projectId);
    }

    public ProjectStructureScopedProcessAccess EnsureScopedProcessExternalActionAllowed() {
        if (ScopedProcessAccess is { CanWrite: true } scopedProcessAccess) {
            return scopedProcessAccess;
        }

        throw new ProjectStructureAgentException(
            403,
            "ProcessSubprocessLaunchDenied",
            $"Launching a child process from project structure requires governed process automation with {ProcessOperationContractNames.ExecuteExternalAction}.");
    }

    public void EnsureProjectAllowed(Guid projectId) {
        EnsureProjectAllowedForContext(
            Purpose,
            ContextIntent,
            AllowAllProjects,
            AllowedProjectIds,
            SessionCreatedProjectIds,
            projectId);
    }

    public bool IsProjectAllowed(Guid projectId) {
        return IsProjectAllowedForContext(
            Purpose,
            ContextIntent,
            AllowAllProjects,
            AllowedProjectIds,
            SessionCreatedProjectIds,
            projectId);
    }

    public void GrantSessionCreatedProjectAccess(ProjectCreationReservation reservation) {
        AllowedProjectIds.Add(reservation.ProjectId);
        SessionCreatedProjectIds.Add(reservation.ProjectId);
        SessionCreatedLifetimes[reservation.ProjectId] = new(reservation.DatabaseProfileId, reservation.ProjectId, reservation.LifetimeId);
        SessionCreatedReservations[reservation.ProjectId] = reservation;
    }
}

internal sealed record ProjectStructureScopedProcessAccess(
    Guid ProjectId,
    string ProcessRunId,
    string ProcessStepId,
    bool CanRead,
    bool CanWrite,
    ProjectStructureAgentContext? AgentContext,
    ProjectStructureProcessNodeContextDescriptor? ProcessNodeContext,
    ProjectWriteAdmission? ExpectedProjectAdmission = null,
    ProjectProcessMutationAdmission? ProcessMutationAdmission = null);
