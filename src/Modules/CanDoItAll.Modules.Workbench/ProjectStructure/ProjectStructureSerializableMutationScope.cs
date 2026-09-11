using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.Modules.Projects;

namespace CanDoItAll.Modules.Workbench;

internal sealed class ProjectStructureSerializableMutationScope : IAsyncDisposable {
    private readonly SerializableMutationScope? innerScope;
    private readonly ProjectProcessNativeMutationScope? processScope;
    private readonly ProjectAgentNativeMutationScope? agentScope;
    private readonly ProjectWorkflowMutationScope? workflowScope;

    internal ProjectStructureSerializableMutationScope(ProjectWorkflowMutationScope workflowScope) => this.workflowScope = workflowScope;
    internal ProjectStructureSerializableMutationScope(SerializableMutationScope innerScope) => this.innerScope = innerScope;
    internal ProjectStructureSerializableMutationScope(ProjectProcessNativeMutationScope processScope) => this.processScope = processScope;
    internal ProjectStructureSerializableMutationScope(ProjectAgentNativeMutationScope agentScope) => this.agentScope = agentScope;

    internal const string ManagedStorageBindingScopeKey = "workbench:managed-storage-bindings";

    public Task CommitAsync(CancellationToken cancellationToken) => workflowScope?.CommitAsync(cancellationToken) ?? agentScope?.CommitAsync(cancellationToken) ?? processScope?.CommitAsync(cancellationToken) ?? innerScope!.CommitAsync(cancellationToken);
    public ValueTask DisposeAsync() => workflowScope?.DisposeAsync() ?? agentScope?.DisposeAsync() ?? processScope?.DisposeAsync() ?? innerScope!.DisposeAsync();
    internal bool RequiresTaskTargetGuard => agentScope?.RequiresTaskTargetGuard == true;
    internal void RequireNodeAuthority(IEnumerable<ProjectObjectRecord> nodes) => agentScope?.RequireNodeAuthority(nodes);
    public static string ForProject(Guid projectId) => ProjectMutationScopeKeys.ForProject(projectId);
    public static IReadOnlyCollection<string> ForProjects(Guid firstProjectId, Guid secondProjectId) =>
        new[] { firstProjectId, secondProjectId }.Distinct().OrderBy(projectId => projectId).Select(ForProject).ToArray();
}

public sealed class ProjectStructureMutationScopeFactory(
    ProjectRecordQueryService projects,
    ProjectWriteAdmissionService admissions,
    CoordinatedDatabaseTransaction transactions,
    ProjectProcessExecutionMutationService? processMutations = null,
    ProjectAgentNativeMutationService? agentMutations = null,
    ProjectWorkflowMutationService? workflowMutations = null) {
    internal ProjectWriteAdmission BindSnapshot(ProjectRecordQueryItem project) => project.LifetimeId is { } lifetimeId
        ? new(admissions.DatabaseProfileId, project.Id, lifetimeId)
        : throw new InvalidOperationException("The project snapshot has no lifetime binding.");

    internal async Task<bool> IsCurrentProjectAsync(ProjectWriteAdmission original, CancellationToken cancellationToken)
        => await admissions.CaptureAsync(original.ProjectId, cancellationToken) == original;

    internal Task<ProjectLifetimeObservation?> CaptureProjectObservationAsync(Guid projectId, CancellationToken cancellationToken)
        => admissions.CaptureObservationAsync(projectId, cancellationToken);

    internal Task RequireProcessMediaPreparationAsync(ProjectProcessMutationAdmission admission, Guid projectId, CancellationToken cancellationToken)
        => (processMutations ?? throw new InvalidOperationException("Process native mutation authority is not installed."))
            .RequireMediaPreparationAsync(admission, projectId, cancellationToken);

    internal Task RequireAgentMediaPreparationAsync(ProjectAgentMutationAdmission admission, ProjectWriteAdmission expected,
        CancellationToken cancellationToken)
        => (agentMutations ?? throw new InvalidOperationException("Ordinary Agent native mutation authority is not installed."))
            .RequirePreparationAsync(admission, expected, cancellationToken);

    internal Task<ProjectStructureSerializableMutationScope> BeginAsync(WorkbenchDbContext context,
        string scopeKey, CancellationToken cancellationToken, IReadOnlyCollection<ProjectWriteAdmission>? expectedAdmissions = null,
        ProjectProcessMutationAdmission? processAdmission = null, ProjectAgentMutationAdmission? agentAdmission = null,
        ProjectWorkflowMutationAdmission? workflowAdmission = null)
        => BeginAsync(context, [scopeKey], cancellationToken, expectedAdmissions, processAdmission, agentAdmission, workflowAdmission);

    internal Task<ProjectStructureSerializableMutationScope> BeginBindingWriteAsync(WorkbenchDbContext context,
        string scopeKey, CancellationToken cancellationToken, IReadOnlyCollection<ProjectWriteAdmission>? expectedAdmissions = null,
        ProjectProcessMutationAdmission? processAdmission = null, ProjectAgentMutationAdmission? agentAdmission = null,
        ProjectWorkflowMutationAdmission? workflowAdmission = null)
        => BeginBindingWriteAsync(context, [scopeKey], cancellationToken, expectedAdmissions, processAdmission, agentAdmission, workflowAdmission);

    internal Task<ProjectStructureSerializableMutationScope> BeginBindingWriteAsync(WorkbenchDbContext context,
        IReadOnlyCollection<string> scopeKeys, CancellationToken cancellationToken,
        IReadOnlyCollection<ProjectWriteAdmission>? expectedAdmissions = null,
        ProjectProcessMutationAdmission? processAdmission = null, ProjectAgentMutationAdmission? agentAdmission = null,
        ProjectWorkflowMutationAdmission? workflowAdmission = null) {
        ArgumentNullException.ThrowIfNull(scopeKeys);
        return BeginAsync(context, scopeKeys.Append(ProjectStructureSerializableMutationScope.ManagedStorageBindingScopeKey).ToArray(), cancellationToken, expectedAdmissions, processAdmission, agentAdmission, workflowAdmission);
    }

    internal async Task<ProjectStructureSerializableMutationScope> BeginAsync(WorkbenchDbContext context,
        IReadOnlyCollection<string> scopeKeys, CancellationToken cancellationToken,
        IReadOnlyCollection<ProjectWriteAdmission>? expectedAdmissions = null,
        ProjectProcessMutationAdmission? processAdmission = null, ProjectAgentMutationAdmission? agentAdmission = null,
        ProjectWorkflowMutationAdmission? workflowAdmission = null) {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(scopeKeys);
        var projectIds = scopeKeys.Select(TryParseProjectId).OfType<Guid>().Distinct().ToArray();
        if (expectedAdmissions is not null &&
                (expectedAdmissions.Count != projectIds.Length || !projectIds.ToHashSet().SetEquals(expectedAdmissions.Select(admission => admission.ProjectId)))) {
            throw new ArgumentException("Explicit admissions must cover exactly the projects in this ordered mutation scope.", nameof(expectedAdmissions));
        }
        if (workflowAdmission is not null) {
            if (processAdmission is not null || agentAdmission is not null) {
                throw new ArgumentException("A Workflow native mutation cannot substitute another producer authority.");
            }
            var service = workflowMutations ?? throw new InvalidOperationException("Workflow native mutation authority is not installed.");
            return new(await service.BeginAsync(context, scopeKeys, workflowAdmission, expectedAdmissions, cancellationToken));
        }
        if (processAdmission is not null) {
            if (agentAdmission is not null) {
                throw new ArgumentException("A native mutation cannot mix Process source authority with ordinary Agent authority.");
            }
            var service = processMutations ?? throw new InvalidOperationException("Process native mutation authority is not installed.");
            return new(await service.BeginAsync(context, scopeKeys, processAdmission, expectedAdmissions, cancellationToken));
        }
        if (agentAdmission is not null) {
            var service = agentMutations ?? throw new InvalidOperationException("Ordinary Agent native mutation authority is not installed.");
            return new(await service.BeginAsync(context, scopeKeys, agentAdmission, expectedAdmissions, cancellationToken));
        }
        var scope = new ProjectStructureSerializableMutationScope(
            await SerializableMutationScope.BeginAsync(context, scopeKeys, cancellationToken));
        try {
            if (projectIds.Length > 0) {
                using var coordination = transactions.Enter(context);
                if (expectedAdmissions is not null) {
                    await admissions.RequireManyForMutationAsync(expectedAdmissions, cancellationToken);
                    return scope;
                }
                var existing = await projects.GetManyForMutationAsync(projectIds, cancellationToken);
                var missing = projectIds.Except(existing.Select(project => project.Id)).Select(id => (Guid?)id).FirstOrDefault();
                if (missing.HasValue) {
                    throw new ProjectStructureAgentException(404, "ProjectNotFound",
                        $"Project '{missing.Value:D}' does not exist and cannot accept project-structure mutations.");
                }
            }
            return scope;
        } catch {
            await scope.DisposeAsync();
            throw;
        }
    }

    private static Guid? TryParseProjectId(string scopeKey) {
        const string prefix = "project:";
        return scopeKey.StartsWith(prefix, StringComparison.Ordinal) && Guid.TryParse(scopeKey[prefix.Length..], out var projectId)
            ? projectId : null;
    }
}
