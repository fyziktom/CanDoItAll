using System.Data;
using CanDoItAll.Infrastructure.ControlPlane;
using CanDoItAll.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace CanDoItAll.Modules.Projects;

public enum ProjectLifecycleHierarchyReadRefusal { DegreeLimitExceeded, OriginalContextChanged, OriginalProjectChanged, OriginalEdgeChanged }

public sealed class ProjectLifecycleHierarchyReadRefusedException(ProjectLifecycleHierarchyReadRefusal reason)
    : InvalidOperationException($"The original hierarchy capture was refused: {reason}.") {
    public ProjectLifecycleHierarchyReadRefusal Reason { get; } = reason;
}

public sealed class ProjectLifecycleHierarchyReader(IDbContextFactory<ProjectsDbContext> factory,
    ICanonicalRuntimeDatabase database, DatasetActivationService activation) {
    public const int MaximumIncidentEdges = 256;

    public Task<ProjectHierarchyEdgeExpectation> ReadEdgeAsync(ProjectLifecycleWriteContext original,
        ProjectWriteAdmission parent, ProjectWriteAdmission child, CancellationToken cancellationToken = default) {
        RequireProject(original, parent);
        RequireProject(original, child);
        if (parent.ProjectId == child.ProjectId) {
            throw new ArgumentException("Original hierarchy endpoints must be distinct.");
        }
        return CaptureAsync(original, async (context, binding) => {
            var edge = await context.Database.SqlQuery<EdgeRow>($"""
                SELECT d."NamespaceId", d."MaintenanceId", d."SchemaVersion", d."GuardProtocol", d."State",
                    link."Id", link."ParentProjectId", link."ChildProjectId",
                    parent."LifetimeId" AS "ParentLifetime", child."LifetimeId" AS "ChildLifetime",
                    EXISTS (SELECT 1 FROM "Projects_ProjectRetirements" r WHERE r."LifetimeId" = parent."LifetimeId") AS "ParentRetired",
                    EXISTS (SELECT 1 FROM "Projects_ProjectRetirements" r WHERE r."LifetimeId" = child."LifetimeId") AS "ChildRetired",
                    NULL::uuid AS "OriginalLifetime", FALSE AS "OriginalRetired"
                FROM "Infrastructure_DatasetLifecycle" d
                LEFT JOIN "Projects_ProjectHierarchyLinks" link ON link."ParentProjectId" = {parent.ProjectId}
                    AND link."ChildProjectId" = {child.ProjectId}
                LEFT JOIN "Projects_Projects" parent ON parent."Id" = link."ParentProjectId"
                LEFT JOIN "Projects_Projects" child ON child."Id" = link."ChildProjectId"
                WHERE d."Id" = 1
                """).SingleOrDefaultAsync(cancellationToken);
            RequireBinding(edge, binding);
            if (edge!.Id is not { } edgeId || edge.ParentLifetime != parent.LifetimeId || edge.ChildLifetime != child.LifetimeId ||
                    edge.ParentRetired || edge.ChildRetired) {
                throw Refused(ProjectLifecycleHierarchyReadRefusal.OriginalEdgeChanged);
            }
            return new ProjectHierarchyEdgeExpectation(edgeId, parent, child);
        }, cancellationToken);
    }

    public Task<IReadOnlyList<ProjectHierarchyEdgeExpectation>> ReadHierarchyAsync(ProjectLifecycleWriteContext original,
        ProjectWriteAdmission project, CancellationToken cancellationToken = default) {
        RequireProject(original, project);
        return CaptureAsync<IReadOnlyList<ProjectHierarchyEdgeExpectation>>(original, async (context, binding) => {
            var rows = await context.Database.SqlQuery<EdgeRow>($"""
                SELECT d."NamespaceId", d."MaintenanceId", d."SchemaVersion", d."GuardProtocol", d."State",
                    link."Id", link."ParentProjectId", link."ChildProjectId",
                    parent."LifetimeId" AS "ParentLifetime", child."LifetimeId" AS "ChildLifetime",
                    EXISTS (SELECT 1 FROM "Projects_ProjectRetirements" r WHERE r."LifetimeId" = parent."LifetimeId") AS "ParentRetired",
                    EXISTS (SELECT 1 FROM "Projects_ProjectRetirements" r WHERE r."LifetimeId" = child."LifetimeId") AS "ChildRetired",
                    original."LifetimeId" AS "OriginalLifetime",
                    EXISTS (SELECT 1 FROM "Projects_ProjectRetirements" r WHERE r."LifetimeId" = original."LifetimeId") AS "OriginalRetired"
                FROM "Infrastructure_DatasetLifecycle" d
                LEFT JOIN "Projects_Projects" original ON original."Id" = {project.ProjectId}
                LEFT JOIN "Projects_ProjectHierarchyLinks" link ON link."ParentProjectId" = {project.ProjectId}
                    OR link."ChildProjectId" = {project.ProjectId}
                LEFT JOIN "Projects_Projects" parent ON parent."Id" = link."ParentProjectId"
                LEFT JOIN "Projects_Projects" child ON child."Id" = link."ChildProjectId"
                WHERE d."Id" = 1
                ORDER BY link."Id"
                LIMIT {MaximumIncidentEdges + 1}
                """).ToArrayAsync(cancellationToken);
            if (rows.Length == 0) {
                throw Refused(ProjectLifecycleHierarchyReadRefusal.OriginalContextChanged);
            }
            foreach (var row in rows) {
                RequireBinding(row, binding);
                if (row.OriginalLifetime != project.LifetimeId || row.OriginalRetired) {
                    throw Refused(ProjectLifecycleHierarchyReadRefusal.OriginalProjectChanged);
                }
            }
            if (rows.Length > MaximumIncidentEdges) {
                throw Refused(ProjectLifecycleHierarchyReadRefusal.DegreeLimitExceeded);
            }
            if (rows.Length == 1 && rows[0].Id is null) {
                return Array.Empty<ProjectHierarchyEdgeExpectation>();
            }
            var result = new ProjectHierarchyEdgeExpectation[rows.Length];
            for (var index = 0; index < rows.Length; index++) {
                var row = rows[index];
                if (row.Id is not { } edge || row.ParentProjectId is not { } parent || row.ChildProjectId is not { } child ||
                        row.ParentLifetime is not { } parentLifetime || row.ChildLifetime is not { } childLifetime ||
                        row.ParentRetired || row.ChildRetired || parent != project.ProjectId && child != project.ProjectId) {
                    throw Refused(ProjectLifecycleHierarchyReadRefusal.OriginalEdgeChanged);
                }
                result[index] = new(edge, new(original.DatabaseProfileId, parent, parentLifetime),
                    new(original.DatabaseProfileId, child, childLifetime));
            }
            return Array.AsReadOnly(result);
        }, cancellationToken);
    }

    private async Task<T> CaptureAsync<T>(ProjectLifecycleWriteContext original,
        Func<ProjectsDbContext, DatasetActivationBinding, Task<T>> read, CancellationToken cancellationToken) {
        var profile = database.Profile;
        var generation = database.Generation;
        if (profile.Profile.Id != original.DatabaseProfileId || profile.Profile.ProviderKind != DatabaseProviderKind.PostgreSql) {
            throw Refused(ProjectLifecycleHierarchyReadRefusal.OriginalContextChanged);
        }
        var before = await activation.ObserveActiveBindingAsync(original.DatabaseProfileId, cancellationToken);
        RequireOriginal();
        if (before.Record.Plan.Binding.Namespace != original.Namespace) {
            throw Refused(ProjectLifecycleHierarchyReadRefusal.OriginalContextChanged);
        }
        ProjectsDbContext? context = null;
        Exception? primary = null;
        T result;
        try {
            context = await factory.CreateDbContextAsync(cancellationToken);
            if (!context.Database.IsNpgsql() || context.Database.CurrentTransaction is not null ||
                    System.Transactions.Transaction.Current is not null ||
                    System.Transactions.TransactionsDatabaseFacadeExtensions.GetEnlistedTransaction(context.Database) is not null ||
                    context.ChangeTracker.Entries().Any() || context.Database.GetDbConnection() is not NpgsqlConnection connection ||
                    connection.State != ConnectionState.Closed) {
                throw new InvalidOperationException("Original hierarchy capture requires an independent PostgreSQL context.");
            }
            CoordinatedDatabaseTransaction.ForProfile(profile).RequireOwnerProfile(context);
            RequireOriginal();
            result = await read(context, before.Record.Plan.Binding);
        } catch (Exception failure) {
            primary = failure;
            throw;
        } finally {
            if (context is not null) {
                try {
                    await context.DisposeAsync();
                } catch (Exception cleanup) when (primary is not null) {
                    throw new AggregateException("The original hierarchy read and context release failed.", primary, cleanup);
                }
            }
        }
        RequireOriginal();
        var after = await activation.ObserveActiveBindingAsync(original.DatabaseProfileId, cancellationToken);
        RequireOriginal();
        if (after != before) {
            throw Refused(ProjectLifecycleHierarchyReadRefusal.OriginalContextChanged);
        }
        return result;

        void RequireOriginal() {
            if (database.Profile != profile || database.Generation != generation) {
                throw Refused(ProjectLifecycleHierarchyReadRefusal.OriginalContextChanged);
            }
        }
    }

    private static void RequireProject(ProjectLifecycleWriteContext original, ProjectWriteAdmission project) {
        ArgumentNullException.ThrowIfNull(original);
        ArgumentNullException.ThrowIfNull(project);
        if (project.DatabaseProfileId != original.DatabaseProfileId) {
            throw Refused(ProjectLifecycleHierarchyReadRefusal.OriginalContextChanged);
        }
    }

    private static void RequireBinding(EdgeRow? row, DatasetActivationBinding binding) {
        if (row is null || row.NamespaceId != binding.Namespace.Value || row.MaintenanceId != binding.MaintenanceId ||
                row.SchemaVersion != binding.SchemaVersion || row.GuardProtocol != (int)binding.GuardProtocol ||
                row.State != (int)DatasetLifecycleState.Active) {
            throw Refused(ProjectLifecycleHierarchyReadRefusal.OriginalContextChanged);
        }
    }

    private static ProjectLifecycleHierarchyReadRefusedException Refused(ProjectLifecycleHierarchyReadRefusal reason) => new(reason);

    private sealed class EdgeRow {
        public Guid NamespaceId { get; set; }
        public Guid MaintenanceId { get; set; }
        public int SchemaVersion { get; set; }
        public int GuardProtocol { get; set; }
        public int State { get; set; }
        public Guid? Id { get; set; }
        public Guid? ParentProjectId { get; set; }
        public Guid? ChildProjectId { get; set; }
        public Guid? ParentLifetime { get; set; }
        public Guid? ChildLifetime { get; set; }
        public bool ParentRetired { get; set; }
        public bool ChildRetired { get; set; }
        public Guid? OriginalLifetime { get; set; }
        public bool OriginalRetired { get; set; }
    }
}
