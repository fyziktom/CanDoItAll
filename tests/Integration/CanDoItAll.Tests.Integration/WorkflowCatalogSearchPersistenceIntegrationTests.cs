using System.Collections.Concurrent;
using System.Data.Common;
using System.Diagnostics;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.Workflows.Abstractions;
using CanDoItAll.Composition;
using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.Modules.AgentFramework;
using CanDoItAll.Modules.Prompts;
using CanDoItAll.SharedKernel;
using CanDoItAll.Tests.Support;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit.Abstractions;

namespace CanDoItAll.Tests.Integration.AgentFramework;

public sealed class WorkflowCatalogSearchPersistenceIntegrationTests(ITestOutputHelper output) {
    private static readonly DateTimeOffset BaselineUtc = new(2026, 7, 19, 18, 0, 0, TimeSpan.Zero);
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private const string PreviousMigration = "20261031225831_ProtectPartyRelationshipObservations";
    private const string MetadataMigration = "20261104001801_AddWorkflowCatalogMetadata";

    [Fact]
    public async Task PostgreSql_SearchDefinitions_FiltersCurrentHeadAndExecutesStableBoundedCatalogPages()
    {
        AppDbContextModelRegistry.ConfigureAssemblies(ModuleAssemblies.All);
        await using var database = PostgresTestDatabaseLease.Create("workflowcatalogsearch");
        var commandInterceptor = new CatalogCommandInterceptor();
        var options = new DbContextOptionsBuilder<AppDbContext>(database.CreateAppDbContextOptions())
            .AddInterceptors(commandInterceptor)
            .Options;
        var factory = new WorkflowUsagePostgresDbContextFactory(options);
        SeededCatalog seededCatalog;
        await using (var dbContext = factory.CreateDbContext())
        {
            await dbContext.Database.EnsureCreatedAsync();
            seededCatalog = await SeedCatalogAsync(dbContext);
        }

        var gallery = CreateGallery(factory);
        IWorkflowCatalogSearchService searchService = WorkflowOwnerPersistenceTestFactory.CreateCatalog(WorkflowObservationTestSupport.Create(), factory, new WorkflowDefinitionValidator(), gallery);
        var firstPageQuery = new WorkflowCatalogSearchQuery(
            text: "nEeDlE",
            status: WorkflowLifecycleStatus.Active,
            pageIndex: 0,
            pageSize: 2);
        var lastPageQuery = new WorkflowCatalogSearchQuery(
            text: "nEeDlE",
            status: WorkflowLifecycleStatus.Active,
            pageIndex: 2,
            pageSize: 2);

        commandInterceptor.Clear();
        var firstPage = await searchService.SearchDefinitionsAsync(firstPageQuery);
        var lastPage = await searchService.SearchDefinitionsAsync(lastPageQuery);
        var repeatedLastPage = await searchService.SearchDefinitionsAsync(lastPageQuery);

        Assert.Equal(5, firstPage.TotalCount);
        Assert.Equal(3, firstPage.TotalPages);
        Assert.Equal(seededCatalog.MatchingIds.Take(2), firstPage.Items.Select(item => item.Id));
        Assert.Equal(5, lastPage.TotalCount);
        Assert.Equal(3, lastPage.TotalPages);
        Assert.Equal([seededCatalog.MatchingIds[4]], lastPage.Items.Select(item => item.Id));
        Assert.Equal(lastPage.Items, repeatedLastPage.Items);
        Assert.All(
            firstPage.Items.Concat(lastPage.Items),
            item => Assert.Equal($"catalog-search:{item.Id.Value:D}", item.TemplateKey));
        Assert.DoesNotContain(firstPage.Items, item => item.Id == seededCatalog.HistoricalMatchId);
        Assert.DoesNotContain(firstPage.Items, item => item.Id == seededCatalog.WrongStatusId);

        var catalogCommands = commandInterceptor.Commands
            .Where(command => command.CommandText.Contains(
                "AgentFramework_WorkflowDefinitionHeads",
                StringComparison.Ordinal))
            .ToArray();
        Assert.Equal(6, catalogCommands.Length);

        var pageCommands = catalogCommands
            .Where(command => command.CommandText.Contains("LIMIT", StringComparison.OrdinalIgnoreCase))
            .ToArray();
        Assert.Equal(3, pageCommands.Length);
        Assert.All(
            catalogCommands.Except(pageCommands),
            command => Assert.DoesNotContain(
                nameof(WorkflowDefinitionRecord.DefinitionJson),
                command.CommandText,
                StringComparison.Ordinal));
        Assert.All(pageCommands, command => Assert.DoesNotContain(
            nameof(WorkflowDefinitionRecord.DefinitionJson),
            command.CommandText,
            StringComparison.Ordinal));
        Assert.All(pageCommands, command => Assert.Contains(
            "OFFSET",
            command.CommandText,
            StringComparison.OrdinalIgnoreCase));
        var lastPageCommands = pageCommands
            .Where(command => command.ParameterValues.Contains(lastPageQuery.Offset))
            .ToArray();
        Assert.Equal(2, lastPageCommands.Length);
        Assert.All(lastPageCommands, command => Assert.Contains(lastPageQuery.PageSize, command.ParameterValues));
    }

    [Theory]
    [InlineData(CatalogReadLane.List)]
    [InlineData(CatalogReadLane.Search)]
    [InlineData(CatalogReadLane.Lookup)]
    public async Task Large_definition_catalog_reads_return_only_current_metadata(CatalogReadLane lane) {
        AppDbContextModelRegistry.ConfigureAssemblies(ModuleAssemblies.All);
        await using var database = PostgresTestDatabaseLease.Create("workflowcatalogmetadata");
        var interceptor = new CatalogCommandInterceptor();
        var options = new DbContextOptionsBuilder<AppDbContext>(database.CreateAppDbContextOptions())
            .AddInterceptors(interceptor).Options;
        var factory = new WorkflowUsagePostgresDbContextFactory(options);
        var definition = JsonSerializer.Deserialize<WorkflowDefinition>(CreateDefinitionJson(
            Guid.NewGuid(), Guid.NewGuid(), "Large metadata workflow", "Current metadata", WorkflowLifecycleStatus.Active, BaselineUtc), JsonOptions)!;
        definition = definition with {
            Graph = definition.Graph with {
                Nodes = definition.Graph.Nodes.Select(node => node.Kind == WorkflowNodeKind.Start
                    ? node with { Settings = node.Settings with { Instructions = new string('x', 2 * 1024 * 1024) } }
                    : node).ToArray()
            },
            TemplateKey = "metadata-template",
            TemplatePackKey = "metadata-pack",
            TemplatePackVersion = "3.2",
            SourceHash = new string('a', 64),
            ExternalNamespace = "metadata.tests",
            ExternalKey = "large-definition"
        };
        var previous = definition with { VersionId = WorkflowVersionId.New(), Name = "Superseded metadata", TemplateKey = "old-template" };
        await using (var context = factory.CreateDbContext()) {
            await context.Database.EnsureCreatedAsync();
            context.AddRange(WorkflowDefinitionRecord.FromDefinition(previous, 1), WorkflowDefinitionRecord.FromDefinition(definition, 2));
            await context.SaveChangesAsync();
            context.Add(CreateHead(definition.Id.Value, definition.VersionId.Value));
            await context.SaveChangesAsync();
        }
        var gallery = CreateGallery(factory);
        var catalog = WorkflowOwnerPersistenceTestFactory.CreateCatalog(WorkflowObservationTestSupport.Create(), factory, new WorkflowDefinitionValidator(), gallery);
        interceptor.Clear();
        var started = Stopwatch.GetTimestamp();
        var items = lane switch {
            CatalogReadLane.List => await catalog.ListDefinitionsAsync(),
            CatalogReadLane.Search => (await catalog.SearchDefinitionsAsync(new WorkflowCatalogSearchQuery("metadata", definition.Status, 0, 1))).Items,
            CatalogReadLane.Lookup => await catalog.LookupDefinitionsAsync(new WorkflowCatalogLookupQuery([definition.Id])),
            _ => throw new ArgumentOutOfRangeException(nameof(lane))
        };
        var elapsed = Stopwatch.GetElapsedTime(started);
        var item = Assert.Single(items);
        Assert.Equal(new WorkflowCatalogItem(definition.Id, definition.VersionId, definition.Name, definition.Description,
            definition.Status, definition.RuntimePolicy.PreferredBackend, definition.UpdatedAtUtc) {
            TemplateKey = definition.TemplateKey,
            TemplatePackKey = definition.TemplatePackKey,
            TemplatePackVersion = definition.TemplatePackVersion,
            SourceHash = definition.SourceHash,
            ExternalNamespace = definition.ExternalNamespace,
            ExternalKey = definition.ExternalKey
        }, item);
        var commands = interceptor.Commands.ToArray();
        Assert.Equal(lane == CatalogReadLane.Search ? 2 : 1, commands.Length);
        Assert.All(commands, command => Assert.DoesNotContain(nameof(WorkflowDefinitionRecord.DefinitionJson), command.CommandText, StringComparison.Ordinal));
        var definitionJson = JsonSerializer.Serialize(definition, JsonOptions);
        var catalogBytes = Encoding.UTF8.GetByteCount(JsonSerializer.Serialize(item, JsonOptions));
        var definitionBytes = Encoding.UTF8.GetByteCount(definitionJson);
        Assert.True(catalogBytes < 4096);
        Assert.True(definitionBytes > 2 * 1024 * 1024);
        output.WriteLine("Lane={0}; Queries={1}; CatalogJsonBytes={2}; DefinitionJsonBytes={3}; ElapsedMs={4:F3}",
            lane, commands.Length, catalogBytes, definitionBytes, elapsed.TotalMilliseconds);
        await using (var planContext = factory.CreateDbContext()) {
            await planContext.Database.OpenConnectionAsync();
            foreach (var captured in commands) {
                await using var command = planContext.Database.GetDbConnection().CreateCommand();
                command.CommandText = "EXPLAIN (ANALYZE, BUFFERS, VERBOSE, FORMAT JSON) " + captured.CommandText;
                for (var index = 0; index < captured.ParameterValues.Count; index++) {
                    var parameter = command.CreateParameter();
                    parameter.ParameterName = captured.ParameterNames[index];
                    parameter.Value = captured.ParameterValues[index];
                    command.Parameters.Add(parameter);
                }
                var plan = Assert.IsType<string>(await command.ExecuteScalarAsync());
                output.WriteLine("QueryPlan={0}", plan);
                using var parsedPlan = JsonDocument.Parse(plan);
                var resultProjection = parsedPlan.RootElement[0].GetProperty("Plan").GetProperty("Output");
                Assert.All(resultProjection.EnumerateArray(), column => Assert.DoesNotContain(
                    nameof(WorkflowDefinitionRecord.DefinitionJson), column.GetString(), StringComparison.Ordinal));
            }
        }
        var detail = await catalog.GetDefinitionAsync(definition.Id);
        Assert.NotNull(detail);
        Assert.Equal(definitionJson, JsonSerializer.Serialize(detail.Definition, JsonOptions));
    }

    public enum CatalogReadLane { List, Search, Lookup }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Metadata_migration_backfills_without_changing_original_rows_and_survives_reopen(bool retained) {
        AppDbContextModelRegistry.ConfigureAssemblies(ModuleAssemblies.All);
        await using var database = PostgresTestDatabaseLease.Create("workflowcatalogmigration");
        await using var context = new AppDbContext(database.CreateAppDbContextOptions());
        await context.GetService<IMigrator>().MigrateAsync(PreviousMigration);
        var originals = new Dictionary<Guid, string>();
        if (retained) {
            foreach (var shape in Enum.GetValues<MetadataShape>()) {
                var workflowId = Guid.NewGuid();
                var versionId = Guid.NewGuid();
                var json = JsonNode.Parse(CreateDefinitionJson(workflowId, versionId, "Legacy metadata",
                    "Retained definition", WorkflowLifecycleStatus.Draft, BaselineUtc))!.AsObject();
                foreach (var property in new[] { "templateKey", "templatePackKey", "templatePackVersion", "sourceHash", "externalNamespace", "externalKey" }) {
                    if (shape == MetadataShape.Absent) {
                        json.Remove(property);
                    } else {
                        json[property] = shape == MetadataShape.Null ? null : "legacy-" + property;
                    }
                }
                json["unrecognizedSnapshot"] = new JsonObject { ["raw"] = "retained 漢字", ["nullable"] = null };
                var payload = json.ToJsonString();
                originals.Add(versionId, payload);
                await context.Database.ExecuteSqlInterpolatedAsync($"""
                    INSERT INTO "AgentFramework_WorkflowDefinitions"
                    ("WorkflowId", "VersionId", "Revision", "Name", "Description", "Status", "PreferredBackend",
                     "DefinitionJson", "InstructionSnapshotSchemaVersion", "CreatedAtUtc", "UpdatedAtUtc")
                    VALUES ({workflowId}, {versionId}, {1L}, {"Legacy metadata"}, {"Retained definition"}, {0}, {0},
                            {payload}, {3}, {BaselineUtc.AddHours(-1)}, {BaselineUtc})
                    """);
            }
        }
        var before = await OriginalColumnsAsync(context);
        for (var reopen = 0; reopen < 2; reopen++) {
            await using var upgraded = new AppDbContext(database.CreateAppDbContextOptions());
            await upgraded.GetService<IMigrator>().MigrateAsync(MetadataMigration);
            Assert.False(upgraded.Database.HasPendingModelChanges());
            Assert.Equal(before, await OriginalColumnsAsync(upgraded));
            var records = await upgraded.Set<WorkflowDefinitionRecord>().AsNoTracking().ToArrayAsync();
            Assert.Equal(originals.Count, records.Length);
            foreach (var record in records) {
                Assert.Equal(originals[record.VersionId], record.DefinitionJson);
                var json = JsonNode.Parse(record.DefinitionJson)!;
                Assert.Equal((json["templateKey"]?.GetValue<string>() ?? string.Empty,
                    json["templatePackKey"]?.GetValue<string>() ?? string.Empty,
                    json["templatePackVersion"]?.GetValue<string>() ?? string.Empty,
                    json["sourceHash"]?.GetValue<string>() ?? string.Empty,
                    json["externalNamespace"]?.GetValue<string>() ?? string.Empty,
                    json["externalKey"]?.GetValue<string>() ?? string.Empty),
                    (record.TemplateKey, record.TemplatePackKey, record.TemplatePackVersion,
                        record.SourceHash, record.ExternalNamespace, record.ExternalKey));
            }
        }
        await context.GetService<IMigrator>().MigrateAsync(PreviousMigration);
        Assert.Equal(before, await OriginalColumnsAsync(context));
    }

    private static Task<string[]> OriginalColumnsAsync(AppDbContext context) => context.Database.SqlQueryRaw<string>("""
        SELECT (to_jsonb(w) - ARRAY['TemplateKey', 'TemplatePackKey', 'TemplatePackVersion',
                                  'SourceHash', 'ExternalNamespace', 'ExternalKey'])::text AS "Value"
        FROM "AgentFramework_WorkflowDefinitions" AS w ORDER BY "VersionId"
        """).ToArrayAsync();

    private enum MetadataShape { Full, Absent, Null }

    private static async Task<SeededCatalog> SeedCatalogAsync(AppDbContext dbContext)
    {
        var matchingDefinitions = new[]
        {
            (Guid.NewGuid(), "NEEDLE Alpha", "First current match", BaselineUtc.AddMinutes(5)),
            (Guid.NewGuid(), "Beta needle", "Second current match", BaselineUtc.AddMinutes(4)),
            (Guid.NewGuid(), "Gamma", "Current description contains NEEDLE", BaselineUtc.AddMinutes(3)),
            (Guid.NewGuid(), "Needle Delta", "Fourth current match", BaselineUtc.AddMinutes(2)),
            (Guid.NewGuid(), "Epsilon NEEDLE", "Fifth current match", BaselineUtc.AddMinutes(1))
        };
        var records = matchingDefinitions
            .Select(item => CreateRecord(
                item.Item1,
                Guid.NewGuid(),
                revision: 1,
                item.Item2,
                item.Item3,
                WorkflowLifecycleStatus.Active,
                item.Item4))
            .ToList();
        var heads = records
            .Select(record => CreateHead(record.WorkflowId, record.VersionId))
            .ToList();

        var wrongStatus = CreateRecord(
            Guid.NewGuid(),
            Guid.NewGuid(),
            revision: 1,
            "Needle suspended",
            "Text matches but exact status does not",
            WorkflowLifecycleStatus.Suspended,
            BaselineUtc.AddMinutes(10));
        records.Add(wrongStatus);
        heads.Add(CreateHead(wrongStatus.WorkflowId, wrongStatus.VersionId));

        var historicalMatchId = Guid.NewGuid();
        var historicalMatch = CreateRecord(
            historicalMatchId,
            Guid.NewGuid(),
            revision: 1,
            "Needle historical head",
            "Only this superseded version matches",
            WorkflowLifecycleStatus.Active,
            BaselineUtc.AddMinutes(-2));
        var currentNonMatch = CreateRecord(
            historicalMatchId,
            Guid.NewGuid(),
            revision: 2,
            "Current replacement",
            "Current metadata no longer matches",
            WorkflowLifecycleStatus.Active,
            BaselineUtc.AddMinutes(-1));
        records.AddRange([historicalMatch, currentNonMatch]);
        heads.Add(CreateHead(currentNonMatch.WorkflowId, currentNonMatch.VersionId));

        dbContext.Set<WorkflowDefinitionRecord>().AddRange(records);
        await dbContext.SaveChangesAsync();
        dbContext.Set<WorkflowDefinitionHeadRecord>().AddRange(heads);
        await dbContext.SaveChangesAsync();

        return new SeededCatalog(
            matchingDefinitions.Select(item => new WorkflowId(item.Item1)).ToArray(),
            new WorkflowId(historicalMatchId),
            new WorkflowId(wrongStatus.WorkflowId));
    }

    private static WorkflowDefinitionRecord CreateRecord(
        Guid workflowId,
        Guid versionId,
        long revision,
        string name,
        string description,
        WorkflowLifecycleStatus status,
        DateTimeOffset updatedAtUtc)
        => WorkflowDefinitionRecord.FromDefinition(JsonSerializer.Deserialize<WorkflowDefinition>(CreateDefinitionJson(
            workflowId, versionId, name, description, status, updatedAtUtc), JsonOptions)!, revision);

    private static string CreateDefinitionJson(
        Guid workflowId,
        Guid versionId,
        string name,
        string description,
        WorkflowLifecycleStatus status,
        DateTimeOffset updatedAtUtc)
    {
        var startNode = CreateNode("start", WorkflowNodeKind.Start);
        var endNode = CreateNode("end", WorkflowNodeKind.End);
        var definition = new WorkflowDefinition(
            new WorkflowId(workflowId),
            new WorkflowVersionId(versionId),
            name,
            description,
            status,
            new WorkflowGraph(
                startNode.Id,
                [startNode, endNode],
                [
                    new WorkflowEdge(
                        new WorkflowEdgeId("start-to-end"),
                        startNode.Id,
                        SourcePortId: null,
                        endNode.Id,
                        TargetPortId: null,
                        WorkflowEdgeKind.Direct,
                        ConditionExpression: string.Empty)
                ]),
            new WorkflowRuntimePolicy(
                WorkflowRuntimeBackendKind.InProcess,
                AllowInProcessPreviewRuns: true,
                RequireDurableProductionRuns: false,
                ExposeAzureFunctionsStatusEndpoint: false,
                ExposeAzureFunctionsMcpTool: false),
            BaselineUtc.AddHours(-1),
            updatedAtUtc)
        {
            TemplateKey = $"catalog-search:{workflowId:D}"
        };

        return JsonSerializer.Serialize(definition, JsonOptions);
    }

    private static WorkflowNode CreateNode(string id, WorkflowNodeKind kind)
        => new(
            new WorkflowNodeId(id),
            kind,
            id,
            [],
            new WorkflowNodeSettings(
                ComponentId: null,
                AgentId: null,
                SubworkflowId: null,
                ExternalRequestKind: null,
                Instructions: string.Empty,
                InputShape: WorkflowValueShape.Text,
                ResultShape: WorkflowValueShape.Text));

    private static WorkflowDefinitionHeadRecord CreateHead(Guid workflowId, Guid versionId)
        => new()
        {
            WorkflowId = workflowId,
            VersionId = versionId
        };

    private static PromptsService CreateGallery(WorkflowUsagePostgresDbContextFactory canonicalFactory) {
        var factory = PromptsPersistenceTestFactory.FromCanonical(canonicalFactory);
        return new(
            factory,
            new SystemClock(),
            new NullActivityStream(),
            new EfPromptGallerySearchDriver(factory),
            new PromptGalleryProjectionCoordinator(factory, new DisabledPromptGalleryProjectionDriver()),
            new PromptGalleryCompatibilityEvaluator(),
            NullLogger<PromptsService>.Instance,
            PromptGalleryObservationTestSupport.Create());
    }

    private sealed record SeededCatalog(
        IReadOnlyList<WorkflowId> MatchingIds,
        WorkflowId HistoricalMatchId,
        WorkflowId WrongStatusId);

    private sealed record CapturedCommand(string CommandText, IReadOnlyList<object?> ParameterValues, IReadOnlyList<string> ParameterNames);

    private sealed class CatalogCommandInterceptor : DbCommandInterceptor
    {
        private readonly ConcurrentQueue<CapturedCommand> commands = new();

        public IReadOnlyList<CapturedCommand> Commands => commands.ToArray();

        public void Clear()
        {
            while (commands.TryDequeue(out _))
            {
            }
        }

        public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(
            DbCommand command,
            CommandEventData eventData,
            InterceptionResult<DbDataReader> result,
            CancellationToken cancellationToken = default)
        {
            commands.Enqueue(new CapturedCommand(
                command.CommandText,
                command.Parameters
                    .Cast<DbParameter>()
                    .Select(parameter => parameter.Value)
                    .ToArray(),
                command.Parameters.Cast<DbParameter>().Select(parameter => parameter.ParameterName).ToArray()));
            return ValueTask.FromResult(result);
        }
    }
}
