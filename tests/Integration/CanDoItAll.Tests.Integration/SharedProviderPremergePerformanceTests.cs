using System.Data.Common;
using System.Diagnostics;
using System.Net;
using System.Text.Json;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.ProviderHistory;
using CanDoItAll.AgentFramework.ProviderHistory.Persistence;
using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.Modules.AgentFramework.ProviderManagement;
using CanDoItAll.Modules.Security;
using CanDoItAll.SharedKernel;
using CanDoItAll.SharedProviders.Abstractions;
using CanDoItAll.SharedProviders.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Xunit.Abstractions;

namespace CanDoItAll.Tests.Integration;

[Trait("Category", "LongRunning")]
public sealed class SharedProviderPremergePerformanceTests(ITestOutputHelper output) {
    [Theory]
    [InlineData(1)]
    [InlineData(32)]
    [InlineData(256)]
    public void NormalizationAllocations(int messages) {
        var model = SharedProviderRoutingModelIdCodec.Create(SharedProviderPublicationId.New(), "measurement-model");
        var payload = JsonSerializer.SerializeToUtf8Bytes(new {
            model = model.Value, messages = Enumerable.Repeat(new { role = "user", content = "measure" }, messages)
        });
        var policy = new SharedProviderRelayRequestPolicy();
        var support = new SharedProviderRelaySupportDescriptor(
            new HashSet<SharedProviderRelayOperation> { SharedProviderRelayOperation.ChatCompletions },
            SharedProviderStreamingMode.ServerSentEvents, true, true, true, false, false, 4 * 1024 * 1024, 4096, 1);
        for (var warmup = 0; warmup < 20; warmup++) {
            Assert.IsType<SharedProviderRelayRequestPolicyResult.Accepted>(
                policy.Normalize(SharedProviderRelayOperation.ChatCompletions, payload, support));
        }
        const int repetitions = 1000;
        var allocated = GC.GetAllocatedBytesForCurrentThread();
        var started = Stopwatch.GetTimestamp();
        for (var iteration = 0; iteration < repetitions; iteration++) {
            policy.Normalize(SharedProviderRelayOperation.ChatCompletions, payload, support);
        }
        output.WriteLine(JsonSerializer.Serialize(new {
            Workload = "normalization", Messages = messages, Repetitions = repetitions,
            ElapsedMilliseconds = Stopwatch.GetElapsedTime(started).TotalMilliseconds,
            AllocatedBytesPerRequest = (GC.GetAllocatedBytesForCurrentThread() - allocated) / repetitions
        }));
    }

    [Theory]
    [InlineData(1)]
    [InlineData(64)]
    public async Task BufferedRelayAllocations(int mebibytes) {
        var payload = JsonSerializer.SerializeToUtf8Bytes(new {
            id = "resp_measure", status = "completed", error = (string?)null, model = "upstream-model",
            padding = new string('x', mebibytes * 1024 * 1024 - 1024),
            usage = new { input_tokens = 7, output_tokens = 11 }
        });
        await using var dispatcher = DispatcherHarness.Create(_ => new(HttpStatusCode.OK) {
            Content = new ByteArrayContent(payload) { Headers = { ContentType = new("application/json") } }
        });
        await dispatcher.DispatchAsync(SharedProviderRelayOperation.Responses, stream: false);
        const int repetitions = 5;
        var elapsed = new double[repetitions];
        var allocated = GC.GetTotalAllocatedBytes(precise: true);
        for (var iteration = 0; iteration < repetitions; iteration++) {
            var started = Stopwatch.GetTimestamp();
            var result = await dispatcher.DispatchAsync(SharedProviderRelayOperation.Responses, stream: false);
            elapsed[iteration] = Stopwatch.GetElapsedTime(started).TotalMilliseconds;
            Assert.Equal(7, Assert.IsType<SharedProviderRelayDispatchResult.Buffered>(result).Usage.InputTokens);
        }
        output.WriteLine(JsonSerializer.Serialize(new {
            Workload = "buffered-relay", Mebibytes = mebibytes, Repetitions = repetitions,
            ElapsedMilliseconds = elapsed,
            AllocatedBytesPerRequest = (GC.GetTotalAllocatedBytes(precise: true) - allocated) / repetitions,
            PeakWorkingSetBytes = Process.GetCurrentProcess().PeakWorkingSet64
        }));
    }

    [Theory]
    [InlineData(1)]
    [InlineData(50)]
    [InlineData(200)]
    public async Task CatalogCacheAllocationsAndCrossScopeRevocation(int publications) {
        await using var fixture = await HistoryPersistenceTestDatabase.CreateAsync();
        await using var host = await ApiTestHost.CreateAsync(jwtEnabled: false, useInMemoryDatabase: true);
        var counter = new PremergeCommandCounter();
        var factory = fixture.Factory.WithInterceptor(counter);
        var secret = new SecretRecord {
            Id = Guid.NewGuid(), Name = "Measurement fixture", Kind = SecretKind.ApiKey,
            EncryptedPayload = "not-a-credential", CreatedAtUtc = fixture.Clock.Now, UpdatedAtUtc = fixture.Clock.Now
        };
        var models = Enumerable.Range(0, 32).Select(index => $"model-{index}").ToArray();
        await using (var db = fixture.Factory.CreateDbContext()) {
            db.Add(secret);
            for (var index = 0; index < publications; index++) {
                var profile = new CanDoItAll.Modules.AgentFramework.ProviderManagement.ProviderProfile {
                    Name = $"Measured provider {index}", ConnectorPluginKey = ProviderConnectorKeys.OpenAi,
                    ConfigSchemaVersion = "1.0", BaseUrl = "https://example.invalid/v1", ApiKeySecretId = secret.Id,
                    DefaultModel = models[0], IsEnabled = true, SupportsStreaming = true,
                    SupportsToolCalling = true, SupportsStructuredOutput = true,
                    ExtraSettingsJson = SharedProviderProfilePublicationMetadataWriter.Write(
                        JsonSerializer.Serialize(new { fixturePadding = new string('x', 8192) }),
                        CanDoItAll.AgentFramework.Models.ProviderKind.OpenAi, ProviderTransportKind.Responses,
                        ProviderProfilePurpose.Chat, models[0], models)
                };
                var publication = SharedProviderPublicationTransitions.Create(profile.Id, SharedProviderPublicationId.New(), fixture.Clock.Now);
                SharedProviderPublicationTransitions.Publish(publication, fixture.Clock.Now);
                db.AddRange(profile, publication);
            }
            await db.SaveChangesAsync();
        }
        using var scope = host.App.Services.CreateScope();
        var clock = scope.ServiceProvider.GetRequiredService<IClock>();
        var service = new SharedProviderCatalogQueryService(factory, new(factory, clock),
            scope.ServiceProvider.GetRequiredService<IProviderManifestCatalog>(),
            scope.ServiceProvider.GetRequiredService<SharedProviderPublicationEligibilityPolicy>(), new());
        var first = await service.GetSnapshotAsync();
        Assert.Equal(publications, first.Catalog.Providers.Count);
        var route = first.Catalog.Providers[0].Models[0].Id;
        const int repetitions = 20;
        await service.ResolveAsync(route);
        counter.Commands.Clear();
        var allocated = GC.GetTotalAllocatedBytes(precise: true);
        var elapsed = new double[repetitions];
        for (var iteration = 0; iteration < repetitions; iteration++) {
            var started = Stopwatch.GetTimestamp();
            Assert.NotNull(await service.ResolveAsync(route));
            elapsed[iteration] = Stopwatch.GetElapsedTime(started).TotalMilliseconds;
        }
        output.WriteLine(JsonSerializer.Serialize(new {
            Workload = "catalog-cache", Publications = publications, ModelsPerPublication = models.Length, Repetitions = repetitions,
            ElapsedMilliseconds = elapsed,
            AllocatedBytesPerRequest = (GC.GetTotalAllocatedBytes(precise: true) - allocated) / repetitions,
            CommandsPerRequest = counter.Commands.Count / repetitions,
            Sql = counter.Commands.Distinct().ToArray()
        }));
        if (publications == 200) {
            await using var planDb = fixture.Factory.CreateDbContext();
            await planDb.Database.OpenConnectionAsync();
            await using var plan = planDb.Database.GetDbConnection().CreateCommand();
            plan.CommandText = "EXPLAIN (ANALYZE, BUFFERS, FORMAT JSON) " +
                counter.Commands.Last(sql => sql.Contains("Workspace_ProviderSharePublications", StringComparison.Ordinal));
            output.WriteLine("Catalog stamp EXPLAIN: " + await plan.ExecuteScalarAsync());
        }
        await using (var mutation = fixture.Factory.CreateDbContext()) {
            var profile = await mutation.Set<CanDoItAll.Modules.AgentFramework.ProviderManagement.ProviderProfile>().FirstAsync();
            profile.Name += " changed";
            await mutation.SaveChangesAsync();
        }
        Assert.NotEqual(first.EntityTag, (await service.GetSnapshotAsync()).EntityTag);
        await using (var mutation = fixture.Factory.CreateDbContext()) {
            await mutation.Set<SecretRecord>().Where(row => row.Id == secret.Id).ExecuteDeleteAsync();
        }
        Assert.Empty((await service.GetSnapshotAsync()).Catalog.Providers);
        Assert.Null(await service.ResolveAsync(route));
    }

    [Fact]
    public async Task BoundedOrphanCleanupPlanAndDrain() {
        await using var fixture = await HistoryPersistenceTestDatabase.CreateAsync();
        const int rows = 10_000;
        await using (var db = fixture.Factory.CreateDbContext()) {
            db.AddRange(Enumerable.Range(0, rows).Select(_ => new HistoryDetailRow {
                Id = Guid.NewGuid(), PartitionId = fixture.Partition.StorageLineageId,
                RequestId = Guid.NewGuid(), Part = HistoryDetailPart.Input,
                CapturedAtUtc = fixture.Clock.Now.AddDays(-40), ExpiresAtUtc = fixture.Clock.Now.AddDays(-30),
                State = HistoryDetailState.Expired
            }));
            await db.SaveChangesAsync();
            await db.Database.ExecuteSqlRawAsync("""ANALYZE "ProviderHistory_Details"; ANALYZE "ProviderHistory_Entries";""");
            await db.Database.OpenConnectionAsync();
            await using var command = db.Database.GetDbConnection().CreateCommand();
            command.CommandText = """
                EXPLAIN (ANALYZE, BUFFERS, FORMAT JSON)
                SELECT d."Id" FROM "ProviderHistory_Details" d
                WHERE d."PartitionId" = @partition AND d."Part" = 0 AND d."EntryId" IS NULL
                  AND d."ExpiresAtUtc" <= @now AND d."StoredBytes" = 0
                  AND NOT EXISTS (SELECT 1 FROM "ProviderHistory_Entries" e WHERE e."InputDetailId" = d."Id")
                ORDER BY d."ExpiresAtUtc", d."Id" LIMIT 500
                """;
            var partition = command.CreateParameter();
            partition.ParameterName = "partition";
            partition.Value = fixture.Partition.StorageLineageId;
            command.Parameters.Add(partition);
            var now = command.CreateParameter();
            now.ParameterName = "now";
            now.Value = fixture.Clock.Now;
            command.Parameters.Add(now);
            output.WriteLine("Orphan candidate EXPLAIN: " + await command.ExecuteScalarAsync());
        }
        var retention = new HistoryRetentionStore(fixture.Factory, fixture.Clock);
        var started = Stopwatch.GetTimestamp();
        var removed = 0;
        for (var pass = 0; pass < rows / 500; pass++) {
            var batch = await retention.PurgeExpiredMetadataAsync(fixture.Partition, 500, default);
            Assert.InRange(batch, 1, 500);
            removed += batch;
        }
        Assert.Equal(rows, removed);
        Assert.Equal(0, await retention.PurgeExpiredMetadataAsync(fixture.Partition, 500, default));
        output.WriteLine(JsonSerializer.Serialize(new {
            Workload = "orphan-drain", Rows = rows, BatchSize = 500,
            ElapsedMilliseconds = Stopwatch.GetElapsedTime(started).TotalMilliseconds
        }));
    }
}

internal sealed class PremergeCommandCounter : DbCommandInterceptor {
    public List<string> Commands { get; } = [];

    public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(
        DbCommand command, CommandEventData eventData, InterceptionResult<DbDataReader> result,
        CancellationToken cancellationToken = default) {
        Commands.Add(command.CommandText);
        return ValueTask.FromResult(result);
    }
}
