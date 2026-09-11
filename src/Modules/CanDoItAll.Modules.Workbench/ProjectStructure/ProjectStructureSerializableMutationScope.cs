using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.Modules.Projects;

namespace CanDoItAll.Modules.Workbench;

internal sealed class ProjectStructureSerializableMutationScope : IAsyncDisposable {
    private readonly SerializableMutationScope? innerScope;
    private readonly ProjectProcessNativeMutationScope? processScope;

    internal ProjectStructureSerializableMutationScope(SerializableMutationScope innerScope) => this.innerScope = innerScope;
    internal ProjectStructureSerializableMutationScope(ProjectProcessNativeMutationScope processScope) => this.processScope = processScope;

    internal const string ManagedStorageBindingScopeKey = "workbench:managed-storage-bindings";

    public Task CommitAsync(CancellationToken cancellationToken) => processScope?.CommitAsync(cancellationToken) ?? innerScope!.CommitAsync(cancellationToken);
    public ValueTask DisposeAsync() => processScope?.DisposeAsync() ?? innerScope!.DisposeAsync();
    public static string ForProject(Guid projectId) => ProjectMutationScopeKeys.ForProject(projectId);
    public static IReadOnlyCollection<string> ForProjects(Guid firstProjectId, Guid secondProjectId) =>
        new[] { firstProjectId, secondProjectId }.Distinct().OrderBy(projectId => projectId).Select(ForProject).ToArray();
}

public sealed class ProjectStructureMutationScopeFactory(
    ProjectRecordQueryService projects,
    ProjectWriteAdmissionService admissions,
    CoordinatedDatabaseTransaction transactions,
    ProjectProcessExecutionMutationService? processMutations = null) {
    internal Task RequireProcessMediaPreparationAsync(ProjectProcessMutationAdmission admission, Guid projectId, CancellationToken cancellationToken)
        => (processMutations ?? throw new InvalidOperationException("Process native mutation authority is not installed."))
            .RequireMediaPreparationAsync(admission, projectId, cancellationToken);

    internal Task<ProjectStructureSerializableMutationScope> BeginAsync(WorkbenchDbContext context,
        string scopeKey, CancellationToken cancellationToken, IReadOnlyCollection<ProjectWriteAdmission>? expectedAdmissions = null,
        ProjectProcessMutationAdmission? processAdmission = null)
        => BeginAsync(context, [scopeKey], cancellationToken, expectedAdmissions, processAdmission);

    internal Task<ProjectStructureSerializableMutationScope> BeginBindingWriteAsync(WorkbenchDbContext context,
        string scopeKey, CancellationToken cancellationToken, IReadOnlyCollection<ProjectWriteAdmission>? expectedAdmissions = null,
        ProjectProcessMutationAdmission? processAdmission = null)
        => BeginBindingWriteAsync(context, [scopeKey], cancellationToken, expectedAdmissions, processAdmission);

    internal Task<ProjectStructureSerializableMutationScope> BeginBindingWriteAsync(WorkbenchDbContext context,
        IReadOnlyCollection<string> scopeKeys, CancellationToken cancellationToken,
        IReadOnlyCollection<ProjectWriteAdmission>? expectedAdmissions = null,
        ProjectProcessMutationAdmission? processAdmission = null) {
        ArgumentNullException.ThrowIfNull(scopeKeys);
        return BeginAsync(context, scopeKeys.Append(ProjectStructureSerializableMutationScope.ManagedStorageBindingScopeKey).ToArray(), cancellationToken, expectedAdmissions, processAdmission);
    }

    internal async Task<ProjectStructureSerializableMutationScope> BeginAsync(WorkbenchDbContext context,
        IReadOnlyCollection<string> scopeKeys, CancellationToken cancellationToken,
        IReadOnlyCollection<ProjectWriteAdmission>? expectedAdmissions = null,
        ProjectProcessMutationAdmission? processAdmission = null) {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(scopeKeys);
        var projectIds = scopeKeys.Select(TryParseProjectId).OfType<Guid>().Distinct().ToArray();
        if (expectedAdmissions is not null &&
                (expectedAdmissions.Count != projectIds.Length || !projectIds.ToHashSet().SetEquals(expectedAdmissions.Select(admission => admission.ProjectId)))) {
            throw new ArgumentException("Explicit admissions must cover exactly the projects in this ordered mutation scope.", nameof(expectedAdmissions));
        }
        if (processAdmission is not null) {
            var service = processMutations ?? throw new InvalidOperationException("Process native mutation authority is not installed.");
            return new(await service.BeginAsync(context, scopeKeys, processAdmission, expectedAdmissions, cancellationToken));
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
