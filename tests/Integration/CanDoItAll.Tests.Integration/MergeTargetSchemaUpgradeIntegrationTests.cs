using CanDoItAll.Composition;
using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.Tests.Support;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace CanDoItAll.Tests.Integration;

// Upgrading a database that already holds records, from the schema a deployed instance has before this branch to the
// schema this branch expects.
//
// A fresh database proves that the chain applies; it does not prove that an existing record survives it. The rows
// below are written at the older schema, exactly as that schema stored them, and are read back after the upgrade by
// their own identifiers. The work assignment is the interesting one: this branch moved it from the CRM / HR owner to
// the Workbench owner, and the operator's rows have to arrive there with their identity, their allocation and their
// references intact, while the participation rows of the same table stay where they are.
public sealed class MergeTargetSchemaUpgradeIntegrationTests
{
    // The newest migration of the branch this one merges into.
    private const string MergeTargetMigrationId = "20260830104752_AddProviderHistoryExternalReference";

    [Fact]
    public async Task Upgrading_from_the_merge_target_schema_moves_the_work_assignment_and_keeps_participation_in_place()
    {
        AppDbContextModelRegistry.ConfigureAssemblies(ModuleAssemblies.All);
        await using var database = PostgresTestDatabaseLease.Create("integration-postgres-merge-target-upgrade");
        await using var dbContext = new AppDbContext(database.CreateAppDbContextOptions());
        var migrator = dbContext.Database.GetService<IMigrator>();

        await migrator.MigrateAsync(MergeTargetMigrationId);
        var applied = await dbContext.Database.GetAppliedMigrationsAsync();
        Assert.Contains(MergeTargetMigrationId, applied);
        Assert.DoesNotContain("20260911000227_MoveWorkItemAssignments", applied);

        var projectId = Guid.Parse("a1000000-0000-0000-0000-000000000001");
        var partyId = Guid.Parse("a1000000-0000-0000-0000-000000000002");
        var assigneeId = Guid.Parse("a1000000-0000-0000-0000-000000000003");
        var participationId = Guid.Parse("a1000000-0000-0000-0000-000000000004");
        var opportunityId = Guid.Parse("a1000000-0000-0000-0000-000000000005");
        await InsertAssignmentAsync(dbContext, assigneeId, projectId, partyId, opportunityId, "WorkItemAssignee", "task-17", 40m);
        await InsertAssignmentAsync(dbContext, participationId, projectId, partyId, opportunityId, "Participant", string.Empty, 15m);

        await migrator.MigrateAsync();

        Assert.Contains("20260911000227_MoveWorkItemAssignments", await dbContext.Database.GetAppliedMigrationsAsync());
        // The assignee row is now the Workbench owner's, with everything the operator recorded about it.
        var moved = await ReadRowAsync(
            dbContext,
            """
            SELECT "ProjectId", "PartyId", "OpportunityId", "NodeKey", "AllocationPercent", "IsPrimary"
            FROM "Workbench_WorkAssignments" WHERE "Id" = {0};
            """,
            assigneeId);
        Assert.Equal(projectId, moved[0]);
        Assert.Equal(partyId, moved[1]);
        Assert.Equal(opportunityId, moved[2]);
        Assert.Equal("task-17", moved[3]);
        Assert.Equal(40m, moved[4]);
        Assert.Equal(true, moved[5]);
        Assert.Equal(0, await CountAsync(dbContext, """SELECT count(*) FROM "CrmHr_ProjectPartyAssignments" WHERE "Id" = {0};""", assigneeId));

        // The participation of the same table is the CRM / HR owner's and did not move.
        Assert.Equal(1, await CountAsync(dbContext, """SELECT count(*) FROM "CrmHr_ProjectPartyAssignments" WHERE "Id" = {0};""", participationId));
        Assert.Equal(0, await CountAsync(dbContext, """SELECT count(*) FROM "Workbench_WorkAssignments" WHERE "Id" = {0};""", participationId));
    }

    private static async Task InsertAssignmentAsync(
        AppDbContext dbContext,
        Guid id,
        Guid projectId,
        Guid partyId,
        Guid opportunityId,
        string assignmentKind,
        string nodeKey,
        decimal allocationPercent)
    {
        await dbContext.Database.ExecuteSqlRawAsync(
            """
            INSERT INTO "CrmHr_ProjectPartyAssignments"
                ("Id", "ProjectId", "PartyId", "OpportunityId", "AssignmentKind", "NodeKey", "PhaseName",
                 "AllocationPercent", "IsPrimary", "Source", "Notes")
            VALUES ({0}, {1}, {2}, {3}, {4}, {5}, {6}, {7}, TRUE, {8}, {9});
            """,
            id,
            projectId,
            partyId,
            opportunityId,
            assignmentKind,
            nodeKey,
            "Delivery",
            allocationPercent,
            "upgrade-proof",
            "Written at the merge target schema.");
    }

    private static async Task<object?[]> ReadRowAsync(AppDbContext dbContext, string sql, Guid id)
    {
        var connection = dbContext.Database.GetDbConnection();
        await dbContext.Database.OpenConnectionAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = sql.Replace("{0}", $"'{id}'", StringComparison.Ordinal);
        await using var reader = await command.ExecuteReaderAsync();
        Assert.True(await reader.ReadAsync(), "The upgraded row was not found by its own identifier.");
        var values = new object?[reader.FieldCount];
        for (var index = 0; index < reader.FieldCount; index++)
        {
            values[index] = reader.IsDBNull(index) ? null : reader.GetValue(index);
        }

        return values;
    }

    private static async Task<int> CountAsync(AppDbContext dbContext, string sql, Guid id)
    {
        var connection = dbContext.Database.GetDbConnection();
        await dbContext.Database.OpenConnectionAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = sql.Replace("{0}", $"'{id}'", StringComparison.Ordinal);
        return Convert.ToInt32(await command.ExecuteScalarAsync());
    }
}
