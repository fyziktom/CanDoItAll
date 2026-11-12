using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.Modules.Projects;
using Microsoft.EntityFrameworkCore;

namespace CanDoItAll.Modules.AgentFramework;

public sealed class AgentProjectDeletionPendingInventory(DbContextOptions<AgentProjectAccessDbContext> options,
    CoordinatedDatabaseTransaction transactions) : IProjectDeletionPendingInventory {
    public ProjectDeletionParticipantId Id { get; } = new(AgentProjectStructureAccessDeletionParticipant.ParticipantIdValue);

    public async Task<bool> HasPendingAsync(CancellationToken cancellationToken = default) {
        await using var context = await transactions.CreateEnlistedAsync(options, static configured => new AgentProjectAccessDbContext(configured), cancellationToken);
        string query = $"""
            SELECT COUNT(*) > 0 AS "Value" FROM (
                SELECT "Id" FROM "AgentFramework_ProjectAccessRevocations"
                WHERE {AgentProjectStructureAccessRevocationRecord.PendingFilter} ORDER BY "Id" LIMIT 1
            ) AS pending
            """;
        return await context.Database.SqlQueryRaw<bool>(query).SingleAsync(cancellationToken);
    }

    public async Task<ProjectDeletionPendingPage> ReadPageAsync(Guid databaseProfileId, Guid? after,
        CancellationToken cancellationToken = default) {
        await using var context = await transactions.CreateEnlistedAsync(options, static configured => new AgentProjectAccessDbContext(configured), cancellationToken);
        string cursor = after is null ? "" : "AND \"Id\" > {0}";
        string query = FormattableString.Invariant($"""
            SELECT * FROM "AgentFramework_ProjectAccessRevocations"
            WHERE {AgentProjectStructureAccessRevocationRecord.PendingFilter} {cursor}
            ORDER BY "Id" LIMIT {ProjectDeletionPendingPage.MaximumCount}
            """);
        var rows = await context.Set<AgentProjectStructureAccessRevocationRecord>()
            .FromSqlRaw(query, after is { } id ? [id] : []).AsNoTracking().ToListAsync(cancellationToken);
        return new(rows.Select(row => {
            if (!Enum.IsDefined(row.Status)) {
                throw new InvalidDataException("The pending Agent deletion status is unsupported.");
            }
            var original = AgentProjectDeletionBinding.Read(row, databaseProfileId)
                ?? throw new InvalidDataException("Pending Agent deletion has no original completion binding. Offline installation is required.");
            return new ProjectDeletionPendingBinding(original, new(Id, row.Id));
        }));
    }
}
