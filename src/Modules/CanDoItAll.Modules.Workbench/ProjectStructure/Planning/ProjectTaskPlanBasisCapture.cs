using System.Data;
using CanDoItAll.Infrastructure.ControlPlane;
using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.Modules.Projects;
using CanDoItAll.SharedKernel;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace CanDoItAll.Modules.Workbench;

internal sealed class ProjectTaskPlanBasisCapture(
    IDbContextFactory<WorkbenchDbContext> contexts,
    ProjectDefinitionReader definitions,
    ProjectWriteAdmissionService admissions,
    CoordinatedDatabaseTransaction transactions,
    DatasetActivationService activation) {
    internal async Task<ProjectTaskPlanBasisReadResult> CaptureAsync(ProjectWriteAdmission project,
        Func<ProjectWriteAdmission, CancellationToken, Task> requireCurrentRead,
        CancellationToken cancellationToken = default) {
        ArgumentNullException.ThrowIfNull(project);
        ArgumentNullException.ThrowIfNull(requireCurrentRead);
        if (project.DatabaseProfileId != admissions.DatabaseProfileId) {
            return new ProjectTaskPlanBasisReadResult.Refused(ProjectTaskPlanBasisRefusal.LifetimeChanged);
        }
        // Only trusted local authority checks may run here; no model, network or external resource work under P.
        await requireCurrentRead(project, cancellationToken);
        var binding = await activation.ReadActiveBindingAsync(admissions.DatabaseProfileId, cancellationToken);
        try {
            await using var context = await contexts.CreateDbContextAsync(cancellationToken);
            if (!context.Database.IsNpgsql()) {
                throw new NotSupportedException("Coherent task planning capture requires PostgreSQL.");
            }
            transactions.RequireOwnerProfile(context);
            await using var transaction = await context.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted, cancellationToken);
            await context.Database.ExecuteSqlRawAsync(
                "SET LOCAL transaction_timeout = '5s'; SET LOCAL statement_timeout = '2s'; SET LOCAL lock_timeout = '2s'",
                cancellationToken);
            await DatasetLifecycleStore.RequireActiveForMutationAsync(context, binding, cancellationToken);
            await SerializableMutationScope.AcquireRelationalScopeLocksAsync(context,
                [ProjectMutationScopeKeys.ForProject(project.ProjectId)], cancellationToken);
            using var participation = transactions.Enter(context);
            var current = await ReadEnlistedAsync(context, binding, project, cancellationToken);
            var basis = current.Basis;
            await requireCurrentRead(project, cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return new ProjectTaskPlanBasisReadResult.Captured(basis);
        } catch (DatasetActivationMismatchException) {
            return new ProjectTaskPlanBasisReadResult.Refused(ProjectTaskPlanBasisRefusal.DatasetChanged);
        } catch (ProjectTaskPlanBasisException exception) {
            return new ProjectTaskPlanBasisReadResult.Refused(exception.Reason);
        }
    }

    internal async Task<ProjectTaskPlanCurrentMaterial> ReadEnlistedAsync(WorkbenchDbContext context,
        DatasetActivationBinding binding, ProjectWriteAdmission project, CancellationToken cancellationToken = default) {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(binding);
        ArgumentNullException.ThrowIfNull(project);
        if (!context.Database.IsNpgsql() || context.Database.CurrentTransaction is not { } transaction ||
                transaction.GetDbTransaction().IsolationLevel != IsolationLevel.ReadCommitted) {
            throw new InvalidOperationException("Task material reads require the caller's active PostgreSQL READ COMMITTED transaction.");
        }
        if (project.DatabaseProfileId != admissions.DatabaseProfileId) {
            throw new ProjectTaskPlanBasisException(ProjectTaskPlanBasisRefusal.LifetimeChanged);
        }
        using var participation = transactions.Enter(context);
        var scalarRead = await definitions.ReadScalarsEnlistedAsync(project, cancellationToken);
        if (scalarRead.IsFailure) {
            var reason = scalarRead.Errors.Single().Code switch {
                ProjectErrorCodes.NotFound => ProjectTaskPlanBasisRefusal.SourceMissing,
                ProjectErrorCodes.LifetimeChanged => ProjectTaskPlanBasisRefusal.LifetimeChanged,
                ProjectErrorCodes.DefinitionLimitExceeded => ProjectTaskPlanBasisRefusal.LimitExceeded,
                ProjectErrorCodes.DefinitionSourceInvalid => ProjectTaskPlanBasisRefusal.InvalidSource,
                _ => throw new InvalidOperationException("The scalar owner returned an unsupported capture refusal.")
            };
            throw new ProjectTaskPlanBasisException(reason);
        }
        var projectSnapshot = scalarRead.Value!;
        var capturedAt = await context.Database.SqlQuery<DateTimeOffset>($"SELECT clock_timestamp() AS \"Value\"")
            .SingleAsync(cancellationToken);
        var preflight = await context.Database.SqlQuery<SourcePreflight>($"""
            SELECT "Id", ARRAY[octet_length("NodeKey"), COALESCE(octet_length("ParentNodeKey"), 0),
                octet_length("ObjectSubtype"), octet_length("Title"), octet_length("Subtitle"),
                octet_length("Notes"), octet_length("Status"), octet_length("ProgressMode"),
                octet_length("MetadataJson")] AS "TextBytes"
            FROM "Workbench_ProjectObjects"
            WHERE "ProjectId" = {project.ProjectId} AND "ObjectType" = {(int)ProjectObjectType.WorkItem}
                AND NOT "IsSystemManaged"
            LIMIT {ProjectTaskPlanBasisLimits.MaximumCandidates + 1}
            """).TagWith("ProjectTaskPlanBasis.Preflight").ToArrayAsync(cancellationToken);
        ProjectTaskPlanBasisLimits.RequireBytes(preflight.Length, ProjectTaskPlanBasisLimits.MaximumCandidates);
        long sourceBytes = ProjectTaskPlanBasisLimits.ScalarBytes(projectSnapshot.Scalars);
        var lengths = new Dictionary<Guid, int[]>();
        foreach (var row in preflight) {
            if (row.Id == Guid.Empty || row.TextBytes is not { Length: 9 } || row.TextBytes.Any(value => value is null or < 0) ||
                    !lengths.TryAdd(row.Id, row.TextBytes.Select(value => value!.Value).ToArray())) {
                throw new ProjectTaskPlanBasisException(ProjectTaskPlanBasisRefusal.InvalidSource);
            }
            foreach (var bytes in lengths[row.Id]) {
                ProjectTaskPlanBasisLimits.RequireBytes(bytes, ProjectTaskPlanBasisLimits.MaximumFieldBytes);
                sourceBytes += bytes;
            }
        }
        ProjectTaskPlanBasisLimits.RequireBytes(sourceBytes, ProjectTaskPlanBasisLimits.MaximumSourceBytes);
        var ids = lengths.Keys.ToArray();
        var rows = await context.Set<ProjectObjectRecord>().AsNoTracking().TagWith("ProjectTaskPlanBasis.Payload")
            .Where(row => row.ProjectId == project.ProjectId && ids.Contains(row.Id) &&
                row.ObjectType == ProjectObjectType.WorkItem && !row.IsSystemManaged)
            .Select(row => new ProjectTaskPlanSourceRow {
                Id = row.Id, NodeKey = row.NodeKey, ParentNodeKey = row.ParentNodeKey, ObjectType = row.ObjectType,
                IsSystemManaged = row.IsSystemManaged, ObjectSubtype = row.ObjectSubtype, Title = row.Title,
                Subtitle = row.Subtitle, Notes = row.Notes, Status = row.Status, Priority = row.Priority,
                ProgressMode = row.ProgressMode, ProgressPercent = row.ProgressPercent, StartUtc = row.StartUtc,
                EndUtc = row.EndUtc, DurationSeconds = row.DurationSeconds, MetadataJson = row.MetadataJson
            }).ToArrayAsync(cancellationToken);
        if (rows.Length != ids.Length || rows.Select(row => row.Id).Distinct().Count() != rows.Length) {
            throw new InvalidOperationException("The task candidate set changed outside its required project gate.");
        }
        var facts = new List<ProjectTaskPlanTaskFact>(rows.Length);
        foreach (var row in rows) {
            if (!lengths.TryGetValue(row.Id, out var originalLengths) ||
                    ProjectTaskPlanMaterialProjection.ValidateSourceBytes(row) != originalLengths.Sum()) {
                throw new InvalidOperationException("The task candidate shape changed outside its required project gate.");
            }
            if (ProjectTaskPlanMaterialProjection.Project(row) is { } fact) {
                facts.Add(fact);
            }
        }
        var basis = new ProjectTaskPlanBasis(binding.Namespace, projectSnapshot, capturedAt, facts);
        _ = ProjectTaskPlanBasisCodec.Encode(basis);
        return new(basis, preflight.Length, sourceBytes);
    }

    private sealed class SourcePreflight {
        public Guid Id { get; set; }
        public int?[] TextBytes { get; set; } = null!;
    }
}

internal sealed class ProjectTaskPlanCurrentMaterial {
    internal ProjectTaskPlanCurrentMaterial(ProjectTaskPlanBasis basis, int candidateCount, long sourceBytes) {
        ArgumentNullException.ThrowIfNull(basis);
        ProjectTaskPlanBasisLimits.RequireBytes(candidateCount, ProjectTaskPlanBasisLimits.MaximumCandidates);
        ProjectTaskPlanBasisLimits.RequireBytes(sourceBytes, ProjectTaskPlanBasisLimits.MaximumSourceBytes);
        if (candidateCount < basis.Tasks.Count || sourceBytes < ProjectTaskPlanBasisLimits.ScalarBytes(basis.Project.Scalars)) {
            throw new ProjectTaskPlanBasisException(ProjectTaskPlanBasisRefusal.InvalidSource);
        }
        Basis = basis;
        CandidateCount = candidateCount;
        SourceBytes = sourceBytes;
    }

    internal ProjectTaskPlanBasis Basis { get; }
    internal int CandidateCount { get; }
    internal long SourceBytes { get; }
}
