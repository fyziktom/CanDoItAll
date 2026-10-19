using System.Data;
using System.Data.Common;
using System.Text.Json;
using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.Modules.AgentFramework;
using CanDoItAll.Modules.Projects;
using CanDoItAll.Modules.Workbench;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Storage;
using Npgsql;

namespace CanDoItAll.Tests.Integration;

public sealed class ProjectDeletionLegacyQueryPlanTests(Xunit.Abstractions.ITestOutputHelper output) {
    [Theory]
    [InlineData(ProjectDeletionRecoveryReadMode.Observe)]
    [InlineData(ProjectDeletionRecoveryReadMode.Exclusive)]
    public async Task Subject_inspection_stops_at_its_indexed_bound_despite_large_terminal_history(ProjectDeletionRecoveryReadMode mode) {
        await using var fixture = await DatasetActivationTestFixture.CreateAsync();
        var original = await ProjectDeletionLegacyTestState.SeedAsync(fixture);
        await using var context = fixture.Projects();
        await context.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO "AgentFramework_ProjectAccessRevocations"
                ("Id", "ProjectId", "DatabaseProfileId", "ProjectLifetimeId", "Status", "CreatedAtUtc", "UpdatedAtUtc", "AttemptCount", "CompletedAtUtc")
            SELECT gen_random_uuid(), {original.Project.ProjectId}, {original.Project.DatabaseProfileId}, gen_random_uuid(), 4, now(), now(), 0, now()
            FROM generate_series(1, 10000);
            INSERT INTO "Workbench_ProjectCrossModuleMutations"
                ("Id", "ProjectId", "ScopeNodeKey", "MutationKind", "Status", "ApprovalState", "PayloadJson", "ErrorMessage", "AttemptCount", "CreatedAtUtc", "UpdatedAtUtc", "CompletedAtUtc")
            SELECT gen_random_uuid(), {original.Project.ProjectId}, 'terminal-node', 1, 2, 0, repeat('x', 1024), '', 0, now(), now(), now()
            FROM generate_series(1, 10000);
            ANALYZE "AgentFramework_ProjectAccessRevocations";
            ANALYZE "Workbench_ProjectCrossModuleMutations";
            """);
        var capture = new SubjectCommands();
        var transactions = CoordinatedDatabaseTransaction.ForProfile(fixture.Profile);
        await using var transaction = await context.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted);
        using (transactions.Enter(context)) {
            var agent = new AgentProjectDeletionRecoveryInspector(new DbContextOptionsBuilder<AgentProjectAccessDbContext>()
                .UseNpgsql(fixture.Profile.ConnectionString).AddInterceptors(capture).Options, transactions);
            var failure = await Assert.ThrowsAsync<InvalidDataException>(() => agent.InspectLegacyAsync(original.Project, mode));
            Assert.Contains("excessive", failure.Message, StringComparison.Ordinal);
            var workbench = new ProjectWorkbenchDeletionRecoveryInspector(new DbContextOptionsBuilder<WorkbenchDbContext>()
                .UseNpgsql(fixture.Profile.ConnectionString).AddInterceptors(capture).Options, transactions);
            Assert.Single(await workbench.InspectLegacyAsync(original.Project, mode));
        }
        Assert.Equal(2, capture.Queries.Count);
        foreach (var query in capture.Queries) {
            await using var command = context.Database.GetDbConnection().CreateCommand();
            command.Transaction = transaction.GetDbTransaction();
            command.CommandTimeout = 5;
            command.CommandText = "EXPLAIN (ANALYZE, BUFFERS, FORMAT JSON) " + query.Text;
            command.Parameters.AddRange(query.Parameters);
            string plan = Assert.IsType<string>(await command.ExecuteScalarAsync());
            output.WriteLine(plan);
            bool agent = query.Text.Contains("AgentFramework_ProjectAccessRevocations", StringComparison.Ordinal);
            string index = agent ? "UX_AF_ProjectAccessRevocations_ProjectLifetime" : "IX_Workbench_ProjectDeletion_Subject";
            Assert.Contains(index, plan, StringComparison.Ordinal);
            using var parsed = JsonDocument.Parse(plan);
            var root = parsed.RootElement[0].GetProperty("Plan");
            var nodes = PlanNodes(root).ToArray();
            Assert.DoesNotContain(nodes, node => node.GetProperty("Node Type").GetString() is "Seq Scan" or "Sort" or "Incremental Sort");
            Assert.Contains(nodes, node => node.TryGetProperty("Index Cond", out var condition) &&
                condition.GetString()!.Contains("ProjectId", StringComparison.Ordinal));
            Assert.Equal(agent ? ProjectDeletionCompletionBinding.MaximumParticipants + 1d : 1d, root.GetProperty("Actual Rows").GetDouble());
        }
    }

    private static IEnumerable<JsonElement> PlanNodes(JsonElement node) {
        yield return node;
        if (node.TryGetProperty("Plans", out var children)) {
            foreach (var child in children.EnumerateArray().SelectMany(PlanNodes)) {
                yield return child;
            }
        }
    }

    private sealed class SubjectCommands : DbCommandInterceptor {
        internal List<SubjectQuery> Queries { get; } = [];

        public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(DbCommand command,
            CommandEventData eventData, InterceptionResult<DbDataReader> result, CancellationToken cancellationToken = default) {
            if (command.CommandText.Contains("LIMIT 17", StringComparison.Ordinal)) {
                Queries.Add(new(command.CommandText, command.Parameters.Cast<NpgsqlParameter>()
                    .Select(parameter => (NpgsqlParameter)((ICloneable)parameter).Clone()).ToArray()));
            }
            return ValueTask.FromResult(result);
        }
    }

    private sealed record SubjectQuery(string Text, NpgsqlParameter[] Parameters);
}
