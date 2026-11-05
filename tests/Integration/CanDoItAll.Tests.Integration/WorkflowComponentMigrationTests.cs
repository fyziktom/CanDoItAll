using CanDoItAll.Composition;
using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.Modules.AgentFramework;
using CanDoItAll.Tests.Support;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using System.Text.Json;

namespace CanDoItAll.Tests.Integration.AgentFramework;

public sealed class WorkflowComponentMigrationTests {
    private const string PreviousMigration = "20261104001801_AddWorkflowCatalogMetadata";
    private const string ObservationMigration = "20261104134958_ProtectWorkflowComponentObservations";

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Migration_backfills_independent_revisions_without_rewriting_existing_columns_and_survives_reopen(bool retained) {
        AppDbContextModelRegistry.ConfigureAssemblies(ModuleAssemblies.All);
        await using var database = PostgresTestDatabaseLease.Create("workflowcomponentmigration");
        await using var context = new AppDbContext(database.CreateAppDbContextOptions());
        await context.GetService<IMigrator>().MigrateAsync(PreviousMigration);
        if (retained) {
            for (var index = 0; index < 2; index++) {
                var id = Guid.NewGuid();
                var payload = JsonSerializer.Serialize(new { id = new { value = id }, legacy = true,
                    unrecognized = new { text = "retained 漢字", nullable = (string?)null } });
                var timestamp = DateTimeOffset.UnixEpoch.AddMinutes(index);
                await context.Database.ExecuteSqlInterpolatedAsync($"""
                    INSERT INTO "AgentFramework_WorkflowComponents"
                    ("Id", "Name", "Model", "Modality", "ComponentJson", "PromptGalleryBindingSchemaVersion", "CreatedAtUtc", "UpdatedAtUtc")
                    VALUES ({id}, {"Retained component"}, {"retained-model"}, {0}, {payload}, {0}, {timestamp}, {timestamp})
                    """);
            }
        }
        var before = await ColumnsAsync(context, includeRevision: true);
        Guid[]? accepted = null;
        for (var reopen = 0; reopen < 2; reopen++) {
            await using var upgraded = new AppDbContext(database.CreateAppDbContextOptions());
            await upgraded.GetService<IMigrator>().MigrateAsync(ObservationMigration);
            Assert.False(upgraded.Database.HasPendingModelChanges());
            Assert.Equal(before, await ColumnsAsync(upgraded, includeRevision: false));
            var revisions = await upgraded.Set<WorkflowComponentRecord>().AsNoTracking().OrderBy(item => item.Id)
                .Select(item => item.Revision).ToArrayAsync();
            Assert.Equal(retained ? 2 : 0, revisions.Length);
            Assert.DoesNotContain(Guid.Empty, revisions);
            Assert.Equal(revisions.Length, revisions.Distinct().Count());
            if (accepted is not null) {
                Assert.Equal(accepted, revisions);
            }
            accepted = revisions;
        }
        await context.GetService<IMigrator>().MigrateAsync(PreviousMigration);
        Assert.Equal(before, await ColumnsAsync(context, includeRevision: true));
    }

    [Fact]
    public async Task A_retired_instruction_schema_original_cannot_rewrite_the_exact_version_payload() {
        await using var database = PostgresTestDatabaseLease.Create("workflowlegacyschemaoriginal");
        var factory = await WorkflowCatalogConcurrencyPersistenceIntegrationTests.CreateDatabaseFactoryAsync(database);
        var record = new WorkflowDefinitionRecord { WorkflowId = Guid.NewGuid(), VersionId = Guid.NewGuid(), Revision = 1,
            Name = "Retained legacy version", DefinitionJson = "{\"retained\":{\"nullable\":null,\"text\":\"legacy 漢字\"}}",
            InstructionSnapshotSchemaVersion = 0, CreatedAtUtc = DateTimeOffset.UnixEpoch, UpdatedAtUtc = DateTimeOffset.UnixEpoch };
        await using (var arrange = factory.CreateDbContext()) {
            arrange.Add(record);
            await arrange.SaveChangesAsync();
        }
        await using var first = factory.CreateDbContext();
        await using var second = factory.CreateDbContext();
        var current = await first.Set<WorkflowDefinitionRecord>().SingleAsync();
        var obsolete = await second.Set<WorkflowDefinitionRecord>().SingleAsync();
        current.InstructionSnapshotSchemaVersion = 3;
        await first.SaveChangesAsync();
        obsolete.InstructionSnapshotSchemaVersion = 3;
        obsolete.DefinitionJson = "{\"unacknowledgedReplacement\":true}";
        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => second.SaveChangesAsync());
        await using var verify = factory.CreateDbContext();
        var stored = await verify.Set<WorkflowDefinitionRecord>().SingleAsync();
        Assert.Equal(record.VersionId, stored.VersionId);
        Assert.Equal(record.DefinitionJson, stored.DefinitionJson);
        Assert.Equal(record.UpdatedAtUtc, stored.UpdatedAtUtc);
        Assert.Equal(3, stored.InstructionSnapshotSchemaVersion);
    }

    private static Task<string[]> ColumnsAsync(AppDbContext context, bool includeRevision) => includeRevision
        ? context.Database.SqlQueryRaw<string>("""
            SELECT to_jsonb(c)::text AS "Value" FROM "AgentFramework_WorkflowComponents" AS c ORDER BY "Id"
            """).ToArrayAsync()
        : context.Database.SqlQueryRaw<string>("""
            SELECT (to_jsonb(c) - 'Revision')::text AS "Value" FROM "AgentFramework_WorkflowComponents" AS c ORDER BY "Id"
            """).ToArrayAsync();
}
