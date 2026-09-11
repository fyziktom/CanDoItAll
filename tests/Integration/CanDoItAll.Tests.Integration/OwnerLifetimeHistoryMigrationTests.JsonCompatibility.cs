using System.Text.Json;
using System.Text.Json.Nodes;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Modules.AgentFramework;
using CanDoItAll.Tests.Support;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql;

namespace CanDoItAll.Tests.Integration;

public sealed partial class OwnerLifetimeHistoryMigrationTests {
    [Theory]
    [InlineData(JsonCompatibilityValue.EscapedNul, false)]
    [InlineData(JsonCompatibilityValue.EscapedNul, true)]
    [InlineData(JsonCompatibilityValue.DoubledBackslash, false)]
    [InlineData(JsonCompatibilityValue.DoubledBackslash, true)]
    [InlineData(JsonCompatibilityValue.LargeUnrelatedNumber, false)]
    [InlineData(JsonCompatibilityValue.LargeUnrelatedNumber, true)]
    public async Task Json_compatibility_preserves_valid_ordinary_and_mapped_origin_bytes_across_upgrade_down_and_restart(
        JsonCompatibilityValue value, bool mapped) {
        var processRun = Guid.NewGuid();
        var assignment = Guid.NewGuid();
        var correlation = value == JsonCompatibilityValue.EscapedNul ? "saved\0correlation" : "saved-correlation";
        WorkflowLaunchOrigin origin = mapped
            ? new WorkflowLaunchOrigin.ProcessAssignment(processRun, assignment, new WorkflowLaunchCorrelationId(correlation))
            : new WorkflowLaunchOrigin.Api(new WorkflowLaunchActor(WorkflowLaunchActorKind.User, "saved-user"),
                new WorkflowLaunchCorrelationId(correlation));
        var json = JsonSerializer.SerializeToNode(origin, WebJson)!.AsObject();
        switch (value) {
            case JsonCompatibilityValue.EscapedNul:
                json["legacyExtra"] = "Original\0text  ";
                break;
            case JsonCompatibilityValue.DoubledBackslash:
                json["legacyExtra"] = "literal \\u0000 and quote \" plus \\ tail";
                break;
            case JsonCompatibilityValue.LargeUnrelatedNumber:
                json["legacyExtra"] = JsonNode.Parse("1e1000000");
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(value));
        }
        var original = " \n" + json.ToJsonString() + "\r\n ";
        var restored = JsonSerializer.Deserialize<WorkflowLaunchOrigin>(original, WebJson)!;
        Assert.Equal(origin.CorrelationId, restored.CorrelationId);
        Assert.Equal(origin.Kind, restored.Kind);
        await AssertJsonCompatibilityRoundTripAsync([
            new(original, mapped ? null : WorkflowLaunchOriginKind.Api, mapped ? processRun : null, mapped ? assignment : null)
        ]);
    }

    [Theory]
    [InlineData(MappedNulEvidence.DiscriminatorKey, true)]
    [InlineData(MappedNulEvidence.DiscriminatorValue, true)]
    [InlineData(MappedNulEvidence.ProcessRunKey, true)]
    [InlineData(MappedNulEvidence.ProcessRunValue, true)]
    [InlineData(MappedNulEvidence.AssignmentKey, true)]
    [InlineData(MappedNulEvidence.AssignmentValue, true)]
    [InlineData(MappedNulEvidence.ProcessRunKey, false)]
    [InlineData(MappedNulEvidence.ProcessRunValue, false)]
    [InlineData(MappedNulEvidence.AssignmentKey, false)]
    [InlineData(MappedNulEvidence.AssignmentValue, false)]
    public Task Json_compatibility_does_not_repair_nul_inside_critical_mapped_keys_or_identity_values(
        MappedNulEvidence evidence, bool hasKind) {
        var json = LegacyMappedOrigin(Guid.NewGuid(), Guid.NewGuid());
        switch (evidence) {
            case MappedNulEvidence.DiscriminatorKey:
                Rename("$origin");
                break;
            case MappedNulEvidence.DiscriminatorValue:
                json["$origin"] = "process-assignment\0";
                break;
            case MappedNulEvidence.ProcessRunKey:
                Rename("processRunId");
                break;
            case MappedNulEvidence.ProcessRunValue:
                json["processRunId"] = json["processRunId"]!.GetValue<Guid>().ToString("D") + "\0";
                break;
            case MappedNulEvidence.AssignmentKey:
                Rename("assignmentId");
                break;
            case MappedNulEvidence.AssignmentValue:
                json["assignmentId"] = json["assignmentId"]!.GetValue<Guid>().ToString("D") + "\0";
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(evidence));
        }
        return AssertJsonCompatibilityUpgradeRejectedAsync(json.ToJsonString(),
            hasKind ? WorkflowLaunchOriginKind.ProcessAssignment : null);

        void Rename(string key) {
            var content = json[key]!.DeepClone();
            json.Remove(key);
            json[key + "\0"] = content;
        }
    }

    [Fact]
    public Task Json_compatibility_preserves_every_dotnet_whitespace_null_origin_without_rewriting_it() {
        const string whitespace = "\u0009\u000A\u000B\u000C\u000D\u0020\u0085\u00A0\u1680" +
            "\u2000\u2001\u2002\u2003\u2004\u2005\u2006\u2007\u2008\u2009\u200A\u2028\u2029\u202F\u205F\u3000";
        Assert.Equal(25, whitespace.Length);
        Assert.All(whitespace, character => Assert.True(char.IsWhiteSpace(character)));
        var rows = whitespace.Select(character => new JsonCompatibilityRow(character.ToString(), null, null, null, true))
            .Append(new JsonCompatibilityRow(whitespace, null, null, null, true)).ToArray();
        return AssertJsonCompatibilityRoundTripAsync(rows);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public Task Json_compatibility_does_not_turn_malformed_json_into_a_valid_inspection_copy(bool invalidOutsideString) {
        var original = LegacyMappedOrigin(Guid.NewGuid(), Guid.NewGuid()).ToJsonString();
        var malformed = invalidOutsideString ? original + "\\u0000" : original[..^1] + ",\"invalid\":\"\\q\\u0000\"}";
        return AssertJsonCompatibilityUpgradeRejectedAsync(malformed, WorkflowLaunchOriginKind.ProcessAssignment);
    }

    [Theory]
    [InlineData(EvidenceTable.WorkflowRuns)]
    [InlineData(EvidenceTable.WorkflowUsage)]
    [InlineData(EvidenceTable.WorkflowLaunches)]
    public Task Json_compatibility_keeps_new_origin_guard_when_an_unrelated_string_contains_nul(EvidenceTable table)
        => AssertDownBlockedAsync(async database => {
            var json = new JsonObject {
                ["$origin"] = "process-dispatch-assignment-v1",
                ["originalExtra"] = "Original\0evidence  "
            }.ToJsonString();
            database.Add(OriginRow(table, json));
            await database.SaveChangesAsync();
        });

    private static async Task AssertJsonCompatibilityRoundTripAsync(IReadOnlyList<JsonCompatibilityRow> rows) {
        await using var environment = CanDoItAllTestEnvironment.Create("owner-lifetime-json-compatible");
        var profile = environment.CreatePostgreSqlProfile("legacy");
        var saved = new Dictionary<Guid, JsonCompatibilityRow>();
        string[] original;
        await using (var services = CreateProvider(profile)) {
            await using var database = await ContextAsync(services);
            await database.GetService<IMigrator>().MigrateAsync(PreviousMigration);
            foreach (var row in rows) {
                saved.Add(await InsertLegacyRunAsync(database, row.Json, row.Kind), row);
            }
            original = await SnapshotDataAsync(database, legacy: true, excludeMappedProjections: true);
            await MigrateCurrentAsync(database);
            await AssertCurrentAsync(database);
            await database.GetService<IMigrator>().MigrateAsync(PreviousMigration);
            Assert.Equal(PreviousMigration, (await database.Database.GetAppliedMigrationsAsync()).Last());
            Assert.Equal(original, await SnapshotDataAsync(database, legacy: true, excludeMappedProjections: true));
        }
        await using var restarted = CreateProvider(profile);
        await using var readback = await ContextAsync(restarted);
        Assert.Equal(original, await SnapshotDataAsync(readback, legacy: true, excludeMappedProjections: true));
        await MigrateCurrentAsync(readback);
        await AssertCurrentAsync(readback);

        async Task AssertCurrentAsync(CanDoItAll.Infrastructure.Persistence.AppDbContext database) {
            Assert.Equal(original, await SnapshotDataAsync(database, legacy: true, excludeMappedProjections: true));
            var records = await database.Set<WorkflowRunRecordEntity>().AsNoTracking().ToArrayAsync();
            Assert.Equal(saved.Count, records.Length);
            foreach (var record in records) {
                var row = saved[record.RunId];
                Assert.Equal(row.Json, record.OriginJson);
                Assert.Equal(row.Kind, record.OriginKind);
                Assert.Equal(row.ProcessRun, record.OriginProcessRunId);
                Assert.Equal(row.Assignment, record.OriginProcessAssignmentId);
                if (row.NullOrigin) {
                    Assert.Null(record.ToSnapshot().Origin);
                } else {
                    Assert.NotNull(record.ToSnapshot().Origin);
                }
            }
            Assert.False(database.Database.HasPendingModelChanges());
        }
    }

    private static async Task AssertJsonCompatibilityUpgradeRejectedAsync(string json, WorkflowLaunchOriginKind? kind) {
        await using var environment = CanDoItAllTestEnvironment.Create("owner-lifetime-json-rejected");
        var profile = environment.CreatePostgreSqlProfile("legacy");
        string[] original;
        string[] schema;
        string[] migrations;
        await using (var services = CreateProvider(profile)) {
            await using var database = await ContextAsync(services);
            await database.GetService<IMigrator>().MigrateAsync(PreviousMigration);
            await InsertLegacyRunAsync(database, json, kind);
            original = await SnapshotDataAsync(database, legacy: true);
            schema = await SnapshotSchemaAsync(database);
            migrations = (await database.Database.GetAppliedMigrationsAsync()).ToArray();
            var failure = await Assert.ThrowsAsync<PostgresException>(() => MigrateCurrentAsync(database));
            Assert.Contains(failure.SqlState, new[] { PostgresErrorCodes.RaiseException, PostgresErrorCodes.InvalidTextRepresentation });
            Assert.Equal(original, await SnapshotDataAsync(database, legacy: true));
            Assert.Equal(schema, await SnapshotSchemaAsync(database));
            Assert.Equal(migrations, (await database.Database.GetAppliedMigrationsAsync()).ToArray());
        }
        await using var restarted = CreateProvider(profile);
        await using var readback = await ContextAsync(restarted);
        Assert.Equal(original, await SnapshotDataAsync(readback, legacy: true));
        Assert.Equal(schema, await SnapshotSchemaAsync(readback));
        Assert.Equal(migrations, (await readback.Database.GetAppliedMigrationsAsync()).ToArray());
    }

    private sealed record JsonCompatibilityRow(string Json, WorkflowLaunchOriginKind? Kind, Guid? ProcessRun, Guid? Assignment, bool NullOrigin = false);

    public enum JsonCompatibilityValue {
        EscapedNul, DoubledBackslash, LargeUnrelatedNumber
    }

    public enum MappedNulEvidence {
        DiscriminatorKey, DiscriminatorValue, ProcessRunKey, ProcessRunValue, AssignmentKey, AssignmentValue
    }
}
