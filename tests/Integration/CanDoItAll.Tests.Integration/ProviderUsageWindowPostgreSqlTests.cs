using System.Data.Common;
using System.Diagnostics;
using CanDoItAll.AgentFramework.Llm.SimpleChats.Operations;
using CanDoItAll.AgentFramework.Llm.SimpleChats.Persistence;
using CanDoItAll.AgentFramework.Llm.SimpleChats.Persistence.Entities;
using CanDoItAll.AgentFramework.Llm.SimpleChats.Persistence.Usage;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.Usage;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.Logging.Abstractions;
using Npgsql;
using Xunit.Abstractions;

namespace CanDoItAll.Tests.Integration.LlmChats;

[Trait("Category", "HostPlatform")]
public sealed class ProviderUsageWindowPostgreSqlTests(ITestOutputHelper output) {
    [Fact]
    public async Task Migration_and_real_query_prune_old_invocations_before_materialization() {
        await using var database = LlmChatsPostgreSqlTestDatabase.CreateUnmigrated("usage-window-sql");
        await using var context = database.CreateDbContext();
        var migrations = context.Database.GetMigrations().ToArray();
        Assert.EndsWith("AddInvocationCompletionUsageIndex", migrations[^1]);
        await context.GetService<IMigrator>().MigrateAsync(migrations[^2]);
        await using var connection = new NpgsqlConnection(database.ConnectionString);
        await connection.OpenAsync();
        const string indexName = "IX_LlmChats_InvocationRecords_CompletedAtUtc";
        Assert.Equal(0L, await CountIndexAsync());
        await context.Database.MigrateAsync();
        Assert.Equal(1L, await CountIndexAsync());
        var now = new DateTimeOffset(2026, 9, 27, 12, 0, 0, TimeSpan.Zero);
        var window = new ProviderUsageWindow(now.AddDays(-7), now);
        var document = LlmChatsPostgreSqlTestDatabase.CreateDocument(Guid.NewGuid());
        LlmChatsPostgreSqlTestDatabase.SeedConversationRoot(context, document);
        context.Add(LlmChatsPostgreSqlTestDatabase.CreateTranscriptRow(document.ConversationId, now.AddYears(-2)));
        context.ChangeTracker.Entries<LlmChatConversationRow>().Single().Entity.CreatedAtUtc = now.AddYears(-2);
        var operationId = Guid.NewGuid();
        context.Add(new LlmChatOperationRow {
            Id = operationId, ConversationId = document.ConversationId, Kind = LlmChatOperationKind.SendTurn,
            RequestFingerprint = new string('a', 64), Status = LlmChatOperationStatus.Succeeded, StartedAtUtc = now.AddYears(-2)
        });
        context.AddRange(Invocation(1, window.FromUtc), Invocation(2, now.AddTicks(-10)),
            Invocation(3, window.FromUtc.AddTicks(-10)), Invocation(4, now));
        await context.SaveChangesAsync();
        var capture = new QueryCapture();
        var source = new SimpleChatProviderUsageProjectionSource(new ContextFactory(database, capture), NullLogger<SimpleChatProviderUsageProjectionSource>.Instance);
        var ordinal = 5;
        foreach (var oldCount in new[] { 20, 2000 }) {
            for (; ordinal < oldCount + 5; ordinal++) {
                context.Add(Invocation(ordinal, now.AddDays(-400 - ordinal)));
            }
            await context.SaveChangesAsync();
            await context.Database.ExecuteSqlRawAsync("ANALYZE \"LlmChats_InvocationRecords\"");
            var baselineTimer = Stopwatch.StartNew();
            var baseline = await source.ReadAsync();
            var baselineMs = baselineTimer.Elapsed.TotalMilliseconds;
            Assert.Equal(oldCount + 4, baseline.Contributions.Count);
            for (var pass = 0; pass < 2; pass++) {
                capture.Clear();
                var timer = Stopwatch.StartNew();
                var allocated = GC.GetTotalAllocatedBytes();
                var result = await source.ReadWindowAsync(window);
                var elapsed = timer.Elapsed.TotalMilliseconds;
                allocated = GC.GetTotalAllocatedBytes() - allocated;
                Assert.Equal(ProviderUsageSourceState.Complete, result.State);
                Assert.Equal(2, result.Contributions.Count);
                Assert.Contains(result.Contributions, item => item.OccurredAtUtc == window.FromUtc);
                Assert.All(result.Contributions, item => Assert.Equal("Revision 1", item.ConsumerName));
                var command = Assert.Single(capture.Commands);
                Assert.Contains("\"CompletedAtUtc\" >=", command.Sql);
                Assert.Contains("\"CompletedAtUtc\" <", command.Sql);
                Assert.DoesNotContain("LlmChats_Messages", command.Sql);
                Assert.DoesNotContain("SystemPrompt", command.Sql);
                output.WriteLine("old={0}, pass={1}: legacyRows={2}, legacyMs={3:F2}, boundedRows=2, boundedMs={4:F2}, allocatedBytes={5}",
                    oldCount, pass, baseline.Contributions.Count, baselineMs, elapsed, allocated);
            }
        }
        var selected = Assert.Single(capture.Commands);
        output.WriteLine(selected.Sql);
        await using var explain = new NpgsqlCommand("EXPLAIN (ANALYZE, BUFFERS) " + selected.Sql, connection);
        foreach (var parameter in selected.Parameters) {
            explain.Parameters.Add(new NpgsqlParameter(parameter.Key, parameter.Value));
        }
        await using var reader = await explain.ExecuteReaderAsync();
        var plan = new List<string>();
        while (await reader.ReadAsync()) {
            plan.Add(reader.GetString(0));
        }
        var planText = string.Join(Environment.NewLine, plan);
        output.WriteLine(planText);
        Assert.Contains(indexName, planText);

        LlmChatInvocationRecordRow Invocation(int sequence, DateTimeOffset at) => new() {
            OperationId = operationId, Ordinal = sequence, ProviderProfileId = Guid.NewGuid(), ProviderKind = ProviderKind.OpenAi,
            ProviderName = "fixture", Model = "model", InputTokens = 10, OutputTokens = 4, CachedInputTokens = 2,
            UsageStatus = LlmChatInvocationUsageEvidenceStatus.Observed, PricingStatus = LlmChatInvocationPricingEvidenceStatus.CalculatedAtExecution,
            CalculatedCostUsd = 0.25m, StartedAtUtc = at.AddSeconds(-1), CompletedAtUtc = at,
            Outcome = LlmChatInvocationOutcome.Succeeded
        };
        async Task<long> CountIndexAsync() {
            await using var command = new NpgsqlCommand("SELECT count(*) FROM pg_indexes WHERE indexname = @name", connection);
            command.Parameters.AddWithValue("name", indexName);
            return (long)(await command.ExecuteScalarAsync())!;
        }
    }

    private sealed class ContextFactory(LlmChatsPostgreSqlTestDatabase database, QueryCapture capture) : IDbContextFactory<SimpleChatsDbContext> {
        public SimpleChatsDbContext CreateDbContext() => database.CreateSimpleChatsDbContext(capture);
    }

    private sealed class QueryCapture : DbCommandInterceptor {
        public List<(string Sql, KeyValuePair<string, object>[] Parameters)> Commands { get; } = [];
        public void Clear() => Commands.Clear();
        public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(DbCommand command, CommandEventData eventData,
            InterceptionResult<DbDataReader> result, CancellationToken cancellationToken = default) {
            Commands.Add((command.CommandText, command.Parameters.Cast<DbParameter>().Select(p => new KeyValuePair<string, object>(p.ParameterName, p.Value!)).ToArray()));
            return ValueTask.FromResult(result);
        }
    }
}
