using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.Modules.Projects;
using Microsoft.EntityFrameworkCore;

namespace CanDoItAll.Modules.Workbench;

public sealed class ProjectWorkbenchDeletionPendingInventory(DbContextOptions<WorkbenchDbContext> options,
    CoordinatedDatabaseTransaction transactions) : IProjectDeletionPendingInventory {
    internal const int MaximumPagePayloadBytes = 8 * 1024 * 1024;
    public ProjectDeletionParticipantId Id { get; } = new(ProjectWorkbenchDeletionParticipant.ParticipantIdValue);

    public async Task<bool> HasPendingAsync(CancellationToken cancellationToken = default) {
        await using var context = await transactions.CreateEnlistedAsync(options, static configured => new WorkbenchDbContext(configured), cancellationToken);
        string query = $"""
            SELECT COUNT(*) > 0 AS "Value" FROM (
                SELECT "Id" FROM "Workbench_ProjectCrossModuleMutations"
                WHERE {ProjectCrossModuleMutationRecord.PendingDeletionFilter} ORDER BY "Id" LIMIT 1
            ) AS pending
            """;
        return await context.Database.SqlQueryRaw<bool>(query).SingleAsync(cancellationToken);
    }

    public async Task<ProjectDeletionPendingPage> ReadPageAsync(Guid databaseProfileId, Guid? after,
        CancellationToken cancellationToken = default) {
        await using var context = await transactions.CreateEnlistedAsync(options, static configured => new WorkbenchDbContext(configured), cancellationToken);
        string cursor = after is null ? "" : "AND \"Id\" > {0}";
        string query = FormattableString.Invariant($$"""
            WITH headers AS MATERIALIZED (
                SELECT "Id", "ProjectId", "ScopeNodeKey", "Status", "CompletionNamespaceId", "CompletionId", octet_length("PayloadJson") AS bytes
                FROM "Workbench_ProjectCrossModuleMutations"
                WHERE {{ProjectCrossModuleMutationRecord.PendingDeletionFilter}} {{cursor}}
                ORDER BY "Id" LIMIT {{ProjectDeletionPendingPage.MaximumCount}}
            ), budget AS MATERIALIZED (
                SELECT *, row_number() OVER (ORDER BY "Id") AS ordinal,
                    sum(COALESCE(bytes, 1)) OVER (ORDER BY "Id") AS total
                FROM headers
            )
            SELECT b."Id", b."ProjectId", b."ScopeNodeKey", b."Status", b."CompletionNamespaceId", b."CompletionId",
                CASE WHEN b.bytes BETWEEN 1 AND {{ProjectWorkbenchDeletionRecoveryInspector.MaximumPayloadBytes}}
                    AND b.total <= {{MaximumPagePayloadBytes}} THEN m."PayloadJson" ELSE NULL END AS "PayloadJson"
            FROM budget AS b JOIN "Workbench_ProjectCrossModuleMutations" AS m ON m."Id" = b."Id"
            WHERE b.ordinal = 1 OR b.total <= {{MaximumPagePayloadBytes}}
            ORDER BY b."Id"
            """);
        var rows = await context.Database.SqlQueryRaw<PendingRow>(query, after is { } id ? [id] : []).ToListAsync(cancellationToken);
        return new(rows.Select(row => {
            if (row.ScopeNodeKey != ProjectWorkbenchDeletionParticipant.ProjectDeletionScopeNodeKey ||
                    !Enum.IsDefined((ProjectCrossModuleMutationStatus)row.Status) || row.PayloadJson is null) {
                throw new InvalidDataException($"Workbench deletion {row.Id}: the record is invalid or exceeds its payload bound.");
            }
            var payload = ProjectWorkbenchDeletionPayload.Read(row.PayloadJson, row.CompletionNamespaceId, row.CompletionId);
            var original = payload.CompletionContext
                ?? throw new InvalidDataException($"Workbench deletion {row.Id} has no original completion binding. Offline installation is required.");
            if (original.Completion.DatabaseProfileId != databaseProfileId || original.Project.ProjectId != row.ProjectId ||
                    !ProjectWorkbenchDeletionPayload.Matches(payload, original)) {
                throw new InvalidDataException($"Workbench deletion {row.Id}: the record has an inconsistent original completion binding.");
            }
            return new ProjectDeletionPendingBinding(original, new(Id, row.Id));
        }));
    }

    private sealed class PendingRow {
        public Guid Id { get; set; }
        public Guid ProjectId { get; set; }
        public Guid? CompletionNamespaceId { get; set; }
        public Guid? CompletionId { get; set; }
        public string ScopeNodeKey { get; set; } = "";
        public int Status { get; set; }
        public string? PayloadJson { get; set; }
    }
}
