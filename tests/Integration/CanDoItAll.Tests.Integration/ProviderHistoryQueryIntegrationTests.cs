using System.Data.Common;
using System.Diagnostics;
using System.Text.Json;
using CanDoItAll.AgentFramework.ProviderHistory;
using CanDoItAll.AgentFramework.ProviderHistory.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.AspNetCore.DataProtection;
using Npgsql;
using Xunit.Abstractions;

namespace CanDoItAll.Tests.Integration;

public sealed class ProviderHistoryQueryIntegrationTests(ITestOutputHelper output) {
    [Fact]
    public async Task Scale_search_obeys_plan_row_and_latency_budgets() {
        await using var fixture = await HistoryPersistenceTestDatabase.CreateAsync();
        var seeded = await ProviderHistoryScaleFixture.SeedAsync(
            fixture, 1_000_000, "query-scale", fixture.Clock.Now.AddDays(30));
        Assert.Equal(1_000_000, seeded.Inserted);
        var commands = new Commands();
        var store = Store(fixture, commands);
        using var concurrency = new HistoryReadConcurrency();
        var service = new ProviderRequestHistoryService(fixture.Access, store,
            new HistoryCursorProtector(new EphemeralDataProtectionProvider()), [],
            new(fixture.Access, concurrency, fixture.Clock, NullLogger<HistoryAuthorizedOperation>.Instance),
            fixture.Clock);
        var global = new ProviderRequestHistoryQuery(new HistoryProviderScope.AllAuthorized(),
            fixture.Clock.Now.AddDays(-4), fixture.Clock.Now.AddDays(1));
        var provider = global with {
            Scope = new HistoryProviderScope.SingleProvider(new(seeded.ProviderId))
        };
        var credential = global with {
            CredentialId = new(seeded.CredentialId)
        };
        var samples = new List<double>();
        foreach (var query in new[] { global, provider, credential }) {
            commands.Reads.Clear();
            var cold = Stopwatch.StartNew();
            var page = await service.SearchAsync(query, default);
            cold.Stop();
            Assert.Equal(50, page.Entries.Count);
            Assert.InRange(cold.ElapsedMilliseconds, 0, 2_000);
            Assert.InRange(JsonSerializer.SerializeToUtf8Bytes(page).Length, 1, 256 * 1024);
            var read = Assert.Single(commands.Reads, command => command.Sql.Contains("ProviderHistory_Entries"));
            var plan = await ExplainAsync(fixture, read);
            Assert.DoesNotContain("\"Node Type\": \"Seq Scan\"", plan, StringComparison.Ordinal);
            Assert.Contains("\"Node Type\": \"Limit\"", plan, StringComparison.Ordinal);
            output.WriteLine(JsonSerializer.Serialize(new {
                Scope = query.Scope.GetType().Name,
                ColdMilliseconds = cold.Elapsed.TotalMilliseconds,
                Plan = JsonDocument.Parse(plan).RootElement,
                SeedMilliseconds = seeded.Elapsed.TotalMilliseconds
            }));
            for (var iteration = 0; iteration < 8; iteration++) {
                var warm = Stopwatch.StartNew();
                page = await service.SearchAsync(query, default);
                warm.Stop();
                Assert.Equal(50, page.Entries.Count);
                samples.Add(warm.Elapsed.TotalMilliseconds);
            }
        }
        Assert.InRange(Percentile95(samples), 0, 500);
        var maximum = await service.SearchAsync(global with { PageSize = 200 }, default);
        var maximumBytes = JsonSerializer.SerializeToUtf8Bytes(maximum).Length;
        Assert.Equal(200, maximum.Entries.Count);
        Assert.InRange(maximumBytes, 1, 1024 * 1024);
        output.WriteLine(JsonSerializer.Serialize(new {
            Rows = seeded.Inserted,
            WarmP95Milliseconds = Percentile95(samples),
            MaximumPageBytes = maximumBytes
        }));
    }

    [Fact]
    public async Task Equal_timestamps_page_by_entry_id_without_body_or_source_reads() {
        await using var fixture = await HistoryPersistenceTestDatabase.CreateAsync();
        var provider = new ProviderIdentity(Guid.NewGuid());
        for (var index = 0; index < 5; index++) {
            var start = fixture.Start() with { Provider = fixture.Start().Provider with { Id = provider } };
            await fixture.Capture.BeginAsync(start, null, default);
            await fixture.Capture.CompleteAsync(start, fixture.Completion(), null, default);
        }
        var commands = new Commands();
        var store = Store(fixture, commands);
        using var concurrency = new HistoryReadConcurrency();
        var service = new ProviderRequestHistoryService(fixture.Access, store,
            new HistoryCursorProtector(new EphemeralDataProtectionProvider()), [],
            new(fixture.Access, concurrency, fixture.Clock, NullLogger<HistoryAuthorizedOperation>.Instance), fixture.Clock);
        var query = new ProviderRequestHistoryQuery(new HistoryProviderScope.SingleProvider(provider),
            fixture.Clock.Now.AddHours(-1), fixture.Clock.Now.AddHours(1)) { PageSize = 2 };
        var first = await service.SearchAsync(query, default);
        var second = await service.SearchAsync(query with { Cursor = first.NextCursor }, default);
        var third = await service.SearchAsync(query with { Cursor = second.NextCursor }, default);
        Assert.Equal(new[] { 2, 2, 1 }, new[] { first.Entries.Count, second.Entries.Count, third.Entries.Count });
        Assert.Equal(5, first.Entries.Concat(second.Entries).Concat(third.Entries).Select(entry => entry.Id).Distinct().Count());
        Assert.Null(third.NextCursor);
        var pages = commands.Reads.Where(command => command.Sql.Contains("ProviderHistory_Entries")).ToArray();
        Assert.Equal(3, pages.Length);
        Assert.All(pages, command => {
            Assert.Contains("LIMIT", command.Sql);
            Assert.Contains("ORDER BY", command.Sql);
            Assert.DoesNotContain("OFFSET", command.Sql);
            Assert.DoesNotContain("ProtectedText", command.Sql);
            Assert.DoesNotContain("InputDetailId", command.Sql);
            Assert.DoesNotContain("CaptureHostId", command.Sql);
            Assert.DoesNotContain("count(", command.Sql, StringComparison.OrdinalIgnoreCase);
        });
        output.WriteLine(pages[1].Sql);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(240)]
    [InlineData(-240)]
    public async Task Provider_credential_model_and_retention_filters_are_applied_in_sql(int offsetMinutes) {
        await using var fixture = await HistoryPersistenceTestDatabase.CreateAsync();
        var first = fixture.Start();
        await fixture.Capture.BeginAsync(first, null, default);
        await fixture.Capture.CompleteAsync(first, fixture.Completion(), null, default);
        var other = fixture.Start();
        await fixture.Capture.BeginAsync(other, null, default);
        await fixture.Capture.CompleteAsync(other, fixture.Completion(), null, default);
        var store = Store(fixture);
        var query = Query(fixture) with {
            FromUtc = Query(fixture).FromUtc.ToOffset(TimeSpan.FromMinutes(offsetMinutes)),
            ToUtc = Query(fixture).ToUtc.ToOffset(TimeSpan.FromMinutes(offsetMinutes)),
            Model = first.Provider.ResolvedModel, CredentialId = first.Caller.CredentialId,
            Subject = first.Caller.Subject, Issuer = first.Caller.Issuer,
            Workload = HistoryWorkload.Direct, Operation = HistoryOperation.CompleteChat,
            Outcome = HistoryOutcome.Succeeded, PriceState = HistoryPriceState.CalculatedAtExecution
        };
        var found = await store.SearchAsync(fixture.Access.Context, query, null, default);
        Assert.Equal(first.EntryId, Assert.Single(found.Entries).Id);
        var restricted = fixture.Access.Context with { AllowedProviders = new HashSet<ProviderIdentity> { other.Provider.Id!.Value } };
        Assert.Empty((await store.SearchAsync(restricted, query, null, default)).Entries);
        Assert.Empty((await store.SearchAsync(fixture.Access.Context, query with { Model = new("EXACT-MODEL") }, null, default)).Entries);
        fixture.Clock.Now = fixture.Clock.Now.AddDays(31);
        Assert.Empty((await store.SearchAsync(fixture.Access.Context, query, null, default)).Entries);
    }

    [Fact]
    public async Task Old_canonical_rows_remain_searchable_and_legacy_facts_remain_unknown() {
        await using var fixture = await HistoryPersistenceTestDatabase.CreateAsync();
        var old = fixture.Clock.Now.AddDays(-100);
        await using (var db = fixture.Factory.CreateDbContext()) {
            db.Add(new HistoryEntryRow {
                Id = Guid.NewGuid(), PartitionId = fixture.Partition.StorageLineageId,
                Granularity = HistoryGranularity.LegacyAggregate, SortAtUtc = old, TimeBasis = HistoryTimeBasis.CanonicalRecorded,
                ProviderName = "Original provider", ProviderKind = "legacy", ResolvedModel = "old-model",
                Outcome = HistoryOutcome.Unknown, MetadataAuthority = HistoryMetadataAuthority.CanonicalProjection,
                RetentionAuthority = HistoryRetentionAuthority.CanonicalOwner
            });
            await db.SaveChangesAsync();
        }
        var page = await Store(fixture).SearchAsync(fixture.Access.Context,
            new(new HistoryProviderScope.AllAuthorized(), old.AddHours(-1), old.AddHours(1)), null, default);
        var row = Assert.Single(page.Entries);
        Assert.Null(row.AttemptId);
        Assert.Null(row.StartedAtUtc);
        Assert.Equal(HistoryTimeBasis.CanonicalRecorded, row.TimeBasis);
        Assert.Equal(HistoryPriceState.Unpriced, row.Price.State);
        Assert.Equal(HistoryAuthenticationKind.Unknown, row.Caller.Kind);
    }

    [Fact]
    public async Task Coverage_uses_registered_sources_and_reports_pending_outbox() {
        await using var fixture = await HistoryPersistenceTestDatabase.CreateAsync();
        await using (var db = fixture.Factory.CreateDbContext()) {
            await db.Set<HistoryCheckpointRow>().Where(row => row.SourceKind == HistorySourceKind.SimpleChat)
                .ExecuteUpdateAsync(update => update.SetProperty(row => row.Coverage, HistoryCoverageState.Current)
                    .SetProperty(row => row.IndexedThroughUtc, fixture.Clock.Now));
        }
        var page = await Store(fixture).SearchAsync(fixture.Access.Context, Query(fixture), null, default);
        Assert.Equal(HistoryCoverageState.Current, page.Coverage.State);
        var source = new CanonicalEvidenceReference(fixture.Partition, HistorySourceKind.SimpleChat, new("owner"), new("evidence"));
        await using (var db = fixture.Factory.CreateDbContext()) {
            fixture.Outbox.Stage(db, new(source, new(1), HistorySourceMutationKind.Delete, null, []));
            await db.SaveChangesAsync();
        }
        page = await Store(fixture).SearchAsync(fixture.Access.Context, Query(fixture), null, default);
        Assert.Equal(HistoryCoverageState.Partial, page.Coverage.State);
    }

    [Fact]
    public async Task Detail_recheck_rejects_source_delete_even_when_old_entry_id_is_known() {
        await using var fixture = await HistoryPersistenceTestDatabase.CreateAsync();
        var start = fixture.Start();
        await fixture.Capture.BeginAsync(start, null, default);
        await fixture.Capture.CompleteAsync(start, fixture.Completion(), null, default);
        var source = new CanonicalEvidenceReference(fixture.Partition, HistorySourceKind.SimpleChat, new("owner"), new("evidence"));
        var mutation = new HistorySourceMutation(source, new(1), HistorySourceMutationKind.Upsert, null, [start.EntryId]);
        await fixture.Projection.ApplyAsync(mutation, default);
        var store = Store(fixture);
        var metadata = await store.GetMetadataAsync(fixture.Access.Context, start.EntryId, default);
        Assert.NotNull(metadata);
        Assert.True(await store.IsCurrentAsync(fixture.Access.Context, metadata, source, default));
        await fixture.Projection.ApplyAsync(mutation with { Kind = HistorySourceMutationKind.Delete, Version = new(2), LinkedEntries = [] }, default);
        Assert.False(await store.IsCurrentAsync(fixture.Access.Context, metadata, source, default));
        Assert.Null(await store.GetMetadataAsync(fixture.Access.Context, start.EntryId, default));
    }

    [Fact]
    public async Task PostgreSql_query_plans_cover_all_provider_and_credential_pages() {
        await using var fixture = await HistoryPersistenceTestDatabase.CreateAsync();
        var start = fixture.Start();
        await fixture.Capture.BeginAsync(start, null, default);
        await fixture.Capture.CompleteAsync(start, fixture.Completion(), null, default);
        var commands = new Commands();
        var store = Store(fixture, commands);
        var query = Query(fixture);
        foreach (var selected in new[] {
            query,
            query with { Scope = new HistoryProviderScope.SingleProvider(start.Provider.Id!.Value) },
            query with { CredentialId = start.Caller.CredentialId }
        }) {
            commands.Reads.Clear();
            await store.SearchAsync(fixture.Access.Context, selected, null, default);
            var page = Assert.Single(commands.Reads, command => command.Sql.Contains("ProviderHistory_Entries"));
            await using var db = fixture.Factory.CreateDbContext();
            await db.Database.OpenConnectionAsync();
            await using var command = new NpgsqlCommand("EXPLAIN (ANALYZE, BUFFERS, FORMAT JSON) " + page.Sql,
                (NpgsqlConnection)db.Database.GetDbConnection());
            foreach (var parameter in page.Parameters) {
                command.Parameters.AddWithValue(parameter.Key, parameter.Value ?? DBNull.Value);
            }
            var plan = (string)(await command.ExecuteScalarAsync())!;
            using var parsed = JsonDocument.Parse(plan);
            Assert.Equal("Limit", parsed.RootElement[0].GetProperty("Plan").GetProperty("Node Type").GetString());
            output.WriteLine(JsonSerializer.Serialize(new { Sql = page.Sql, Plan = parsed.RootElement }));
        }
    }

    private static ProviderRequestHistoryQuery Query(HistoryPersistenceTestDatabase fixture) =>
        new(new HistoryProviderScope.AllAuthorized(), fixture.Clock.Now.AddHours(-1), fixture.Clock.Now.AddHours(1));

    private static HistoryReadStore Store(HistoryPersistenceTestDatabase fixture, Commands? commands = null) =>
        new(commands is null ? fixture.Factory : fixture.Factory.WithInterceptor(commands),
            new([new RegisteredSource()]), fixture.Details, fixture.Clock);

    private static async Task<string> ExplainAsync(HistoryPersistenceTestDatabase fixture, ReadCommand read) {
        await using var db = fixture.Factory.CreateDbContext();
        await db.Database.OpenConnectionAsync();
        await using var command = new NpgsqlCommand("EXPLAIN (ANALYZE, BUFFERS, FORMAT JSON) " + read.Sql,
            (NpgsqlConnection)db.Database.GetDbConnection());
        foreach (var parameter in read.Parameters) {
            command.Parameters.AddWithValue(parameter.Key, parameter.Value ?? DBNull.Value);
        }
        return (string)(await command.ExecuteScalarAsync())!;
    }

    private static double Percentile95(IReadOnlyCollection<double> values) {
        var ordered = values.Order().ToArray();
        return ordered[(int)Math.Ceiling(ordered.Length * 0.95) - 1];
    }
    private sealed class RegisteredSource : IHistorySourceMaintenance {
        public HistorySourceKind Kind => HistorySourceKind.SimpleChat;
        public Task<HistorySourceProgress> ProcessAsync(HistoryMaintenanceContext context, string? cursor, int maximumItems, CancellationToken cancellationToken) =>
            throw new InvalidOperationException("Search must never run source maintenance.");
    }

    private sealed record ReadCommand(string Sql, IReadOnlyDictionary<string, object?> Parameters);

    private sealed class Commands : DbCommandInterceptor {
        internal List<ReadCommand> Reads { get; } = [];
        public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(DbCommand command, CommandEventData eventData,
            InterceptionResult<DbDataReader> result, CancellationToken cancellationToken = default) {
            Reads.Add(new(command.CommandText, command.Parameters.Cast<DbParameter>().ToDictionary(parameter => parameter.ParameterName, parameter => parameter.Value)));
            return ValueTask.FromResult(result);
        }
    }
}



