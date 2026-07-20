using System.Collections.Concurrent;
using System.Data.Common;
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
using Microsoft.Extensions.Logging.Abstractions;

namespace CanDoItAll.Tests.Integration;

public sealed class WorkflowCatalogSearchPersistenceIntegrationTests
{
    private static readonly DateTimeOffset BaselineUtc = new(2026, 7, 19, 18, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task PostgreSql_SearchDefinitions_FiltersCurrentHeadAndExecutesStableBoundedMetadataPages()
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
        IWorkflowCatalogSearchService searchService = new PersistentWorkflowCatalogService(
            factory,
            new WorkflowDefinitionValidator(),
            gallery,
            gallery);
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
        Assert.DoesNotContain(firstPage.Items, item => item.Id == seededCatalog.HistoricalMatchId);
        Assert.DoesNotContain(firstPage.Items, item => item.Id == seededCatalog.WrongStatusId);

        var catalogCommands = commandInterceptor.Commands
            .Where(command => command.CommandText.Contains(
                "AgentFramework_WorkflowDefinitionHeads",
                StringComparison.Ordinal))
            .ToArray();
        Assert.Equal(6, catalogCommands.Length);
        Assert.All(catalogCommands, command => Assert.False(
            command.CommandText.Contains(nameof(WorkflowDefinitionRecord.DefinitionJson), StringComparison.Ordinal),
            command.CommandText));

        var pageCommands = catalogCommands
            .Where(command => command.CommandText.Contains("LIMIT", StringComparison.OrdinalIgnoreCase))
            .ToArray();
        Assert.Equal(3, pageCommands.Length);
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
        => new()
        {
            WorkflowId = workflowId,
            VersionId = versionId,
            Revision = revision,
            Name = name,
            Description = description,
            Status = status,
            PreferredBackend = WorkflowRuntimeBackendKind.InProcess,
            DefinitionJson = "{\"payload\":\"catalog search must not select this column\"}",
            InstructionSnapshotSchemaVersion = 2,
            CreatedAtUtc = BaselineUtc.AddHours(-1),
            UpdatedAtUtc = updatedAtUtc
        };

    private static WorkflowDefinitionHeadRecord CreateHead(Guid workflowId, Guid versionId)
        => new()
        {
            WorkflowId = workflowId,
            VersionId = versionId
        };

    private static PromptsService CreateGallery(WorkflowUsagePostgresDbContextFactory factory)
        => new(
            factory,
            new SystemClock(),
            new NullActivityStream(),
            new EfPromptGallerySearchDriver(factory),
            new PromptGalleryProjectionCoordinator(factory, new DisabledPromptGalleryProjectionDriver()),
            new PromptGalleryCompatibilityEvaluator(),
            NullLogger<PromptsService>.Instance);

    private sealed record SeededCatalog(
        IReadOnlyList<WorkflowId> MatchingIds,
        WorkflowId HistoricalMatchId,
        WorkflowId WrongStatusId);

    private sealed record CapturedCommand(string CommandText, IReadOnlyList<object?> ParameterValues);

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
                    .ToArray()));
            return ValueTask.FromResult(result);
        }
    }
}
