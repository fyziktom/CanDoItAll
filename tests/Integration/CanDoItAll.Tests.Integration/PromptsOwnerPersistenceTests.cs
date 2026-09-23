using System.Data.Common;
using CanDoItAll.Infrastructure.ControlPlane;
using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.Infrastructure.Search;
using CanDoItAll.Modules.Projects;
using CanDoItAll.Modules.Prompts;
using CanDoItAll.SharedKernel;
using CanDoItAll.Tests.Support;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace CanDoItAll.Tests.Integration;

public sealed class PromptsOwnerPersistenceTests {
    private static readonly DateTimeOffset CreatedAt = new(2026, 2, 3, 4, 5, 6, TimeSpan.Zero);

    [Fact]
    public async Task Runtime_model_matches_complete_schema_without_search_or_project_membership() {
        await using var application = await TestApplication.CreateAsync();
        await using var canonical = await application.Services.GetRequiredService<IDbContextFactory<AppDbContext>>().CreateDbContextAsync();
        await using var owner = await application.Services.GetRequiredService<IDbContextFactory<PromptsDbContext>>().CreateDbContextAsync();
        var entities = owner.GetService<IDesignTimeModel>().Model.GetEntityTypes().ToArray();
        Assert.Equal(10, entities.Length);
        foreach (var entity in entities) {
            var full = Assert.IsAssignableFrom<IEntityType>(canonical.GetService<IDesignTimeModel>().Model.FindEntityType(entity.ClrType));
            Assert.Equal(full.ToDebugString(MetadataDebugStringOptions.LongDefault), entity.ToDebugString(MetadataDebugStringOptions.LongDefault));
        }
        Assert.Throws<InvalidOperationException>(() => owner.Set<SearchDocument>().ToQueryString());
        Assert.Throws<InvalidOperationException>(() => owner.Set<Project>().ToQueryString());
    }

    [Fact]
    public async Task Legacy_prompt_versions_metadata_and_usage_survive_owner_save_restart_and_profile_isolation() {
        await using var environment = CanDoItAllTestEnvironment.Create("prompts-owner-restart");
        var profile = environment.CreatePostgreSqlProfile("original");
        var otherProfile = environment.CreatePostgreSqlProfile("other");
        var harness = new TestHarnessOptions { TestEnvironment = environment, ActiveProfile = profile };
        var projectId = Guid.NewGuid();
        var artifact = new PromptArtifact {
            ProjectId = projectId, Title = "Legacy prompt", Summary = "Retained summary", Phase = "Review",
            CurrentDraftText = "Draft content", Status = PromptArtifactStatus.Final, CurrentVersionNumber = 1,
            CreatedAtUtc = CreatedAt, UpdatedAtUtc = CreatedAt, IsFavorite = true
        };
        var collection = new PromptCollection { Name = "Retained collection", Description = "Collection metadata" };
        artifact.CollectionId = collection.Id;
        var tag = new PromptTag { Name = "retained", NameKey = "RETAINED" };
        var version = new PromptVersion {
            PromptArtifactId = artifact.Id, VersionNumber = 1, Content = "Immutable published content",
            CreationReason = "Original publication", TitleSnapshot = artifact.Title, SummarySnapshot = artifact.Summary,
            CreatedAtUtc = CreatedAt
        };
        var usage = new PromptUsageRecord {
            PromptArtifactId = artifact.Id, PromptVersionNumber = 1, ProjectId = projectId,
            ProviderName = "Synthetic provider", RepositoryName = "retained", BranchName = "fixture",
            UsageNote = "Original usage", CreatedAtUtc = CreatedAt
        };
        await using (var original = await TestApplication.CreateAsync(harness)) {
            await using var context = await original.Services.GetRequiredService<IDbContextFactory<AppDbContext>>().CreateDbContextAsync();
            context.AddRange(artifact, collection, tag, version, usage,
                new PromptArtifactTag { PromptArtifactId = artifact.Id, PromptTagId = tag.Id },
                new PromptSupportedProviderModel { PromptArtifactId = artifact.Id, Provider = "OpenAI", ProviderKey = "OPENAI", Model = "fixture", ModelKey = "FIXTURE", IsPreferred = true },
                new PromptSupportedConsumer { PromptArtifactId = artifact.Id, Consumer = PromptGalleryConsumer.Workflow },
                new PromptTemplateToken { PromptArtifactId = artifact.Id, Name = "input", NameKey = "INPUT" },
                new PromptCompatibilityWarningPreference { PromptArtifactId = artifact.Id, Consumer = PromptGalleryConsumer.Chat,
                    IssueCode = PromptCompatibilityIssueCode.ConsumerNotSupported, IsSuppressed = true, UpdatedAtUtc = CreatedAt });
            await context.SaveChangesAsync();
        }
        await using (var restarted = await TestApplication.CreateAsync(harness)) {
            await using var scope = restarted.Services.CreateAsyncScope();
            var service = scope.ServiceProvider.GetRequiredService<IPromptGalleryService>();
            var details = (await service.GetItemAsync(artifact.Id)).Value!;
            Assert.Equal(collection.Id, details.CollectionId);
            Assert.Equal("retained", Assert.Single(details.Tags));
            Assert.Equal("input", Assert.Single(details.TemplateTokens));
            Assert.True(Assert.Single(details.SupportedModels).IsPreferred);
            Assert.Equal(PromptGalleryConsumer.Workflow, Assert.Single(details.SupportedConsumers));
            Assert.Equal(PromptCompatibilityIssueCode.ConsumerNotSupported, Assert.Single(details.WarningSuppressions).IssueCode);
            Assert.True(details.IsFavorite);
            Assert.Equal(CreatedAt, details.CreatedAtUtc);
            var saved = await service.SaveDraftAsync(new(artifact.Id, projectId, collection.Id, "Human-edited prompt", details.Summary,
                details.Kind, details.Phase, "Next draft", details.Tags, details.SupportedModels, details.SupportedConsumers,
                details.Recommendations, details.UpdatedAtUtc));
            Assert.True(saved.IsSuccess);
            Assert.Equal(artifact.Id, saved.Value.PromptArtifactId);
        }
        await using var final = await TestApplication.CreateAsync(harness);
        await using var finalContext = await final.Services.GetRequiredService<IDbContextFactory<PromptsDbContext>>().CreateDbContextAsync();
        Assert.Equal("Human-edited prompt", (await finalContext.Set<PromptArtifact>().SingleAsync(item => item.Id == artifact.Id)).Title);
        var retainedVersion = await finalContext.Set<PromptVersion>().SingleAsync(item => item.Id == version.Id);
        Assert.Equal(version.Content, retainedVersion.Content);
        Assert.Equal(version.TitleSnapshot, retainedVersion.TitleSnapshot);
        Assert.Equal(usage.UsageNote, (await finalContext.Set<PromptUsageRecord>().SingleAsync(item => item.Id == usage.Id)).UsageNote);
        Assert.Equal(collection.Description, (await finalContext.Set<PromptCollection>().SingleAsync(item => item.Id == collection.Id)).Description);
        await using var other = await TestApplication.CreateAsync(new TestHarnessOptions { TestEnvironment = environment, ActiveProfile = otherProfile });
        await using var otherScope = other.Services.CreateAsyncScope();
        Assert.True((await otherScope.ServiceProvider.GetRequiredService<IPromptGalleryService>().GetItemAsync(artifact.Id)).IsFailure);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Enlisted_creation_follows_the_outer_transaction_and_notifies_only_after_commit(bool commit) {
        await using var application = await TestApplication.CreateAsync();
        await using var scope = application.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var factory = services.GetRequiredService<IDbContextFactory<PromptsDbContext>>();
        var transactions = services.GetRequiredService<CoordinatedDatabaseTransaction>();
        var probe = new CommandProbe();
        var options = new DbContextOptionsBuilder<PromptsDbContext>(services.GetRequiredService<DbContextOptions<PromptsDbContext>>())
            .AddInterceptors(probe).Options;
        var activity = new RecordingActivity();
        var service = new PromptsService(factory, new SystemClock(), activity, new EfPromptGallerySearchDriver(factory),
            new PromptGalleryProjectionCoordinator(factory, new DisabledPromptGalleryProjectionDriver()),
            new PromptGalleryCompatibilityEvaluator(), NullLogger<PromptsService>.Instance);
        var mutation = new PromptGalleryMutationService(service, factory, options, transactions);
        var queries = new PromptArtifactProjectionQueryService(factory, options, transactions);
        await using var owner = await services.GetRequiredService<IDbContextFactory<ProjectsDbContext>>().CreateDbContextAsync();
        var project = new Project { Name = "Atomic prompt parent" };
        await using var transaction = await owner.Database.BeginTransactionAsync();
        owner.Add(project);
        await owner.SaveChangesAsync();
        PromptDraftCreationPreparation preparation;
        using (transactions.Enter(owner)) {
            var result = await mutation.StageCreateDraftAsync(new(null, project.Id, null, "Original title", "Exact activity summary",
                PromptGalleryItemKind.FullPrompt, "Review", "", Tags: ["atomic"], SupportedConsumers: [PromptGalleryConsumer.ProjectWorkbench]));
            Assert.True(result.IsSuccess);
            preparation = result.Value!;
            Assert.Equal(preparation.Receipt.PromptArtifactId, Assert.Single(await queries.ListProjectFactsForMutationAsync(project.Id)).Id);
            Assert.NotEmpty(probe.Commands);
            Assert.All(probe.Commands, command => {
                Assert.Same(owner.Database.GetDbConnection(), command.Connection);
                Assert.Same(transaction.GetDbTransaction(), command.Transaction);
            });
            Assert.Empty(await queries.ListProjectFactsAsync(project.Id));
            Assert.Empty(activity.Requests);
        }
        if (!commit) {
            await transaction.RollbackAsync();
            Assert.True((await service.GetItemAsync(preparation.Receipt.PromptArtifactId)).IsFailure);
            await Assert.ThrowsAsync<InvalidOperationException>(() => mutation.CompleteDraftCreationAsync(preparation));
            Assert.Empty(activity.Requests);
            return;
        }
        await transaction.CommitAsync();
        var details = (await service.GetItemAsync(preparation.Receipt.PromptArtifactId)).Value!;
        var humanEdit = await services.GetRequiredService<IPromptGalleryService>().SaveDraftAsync(new(details.Id, project.Id, null,
            "Intervening human edit", details.Summary, details.Kind, details.Phase, details.DraftContent,
            details.Tags, details.SupportedModels, details.SupportedConsumers, ExpectedUpdatedAtUtc: details.UpdatedAtUtc));
        Assert.True(humanEdit.IsSuccess);
        await mutation.CompleteDraftCreationAsync(preparation);
        Assert.Equal("Original title", Assert.Single(activity.Requests).Description);
        Assert.Equal("Intervening human edit", (await service.GetItemAsync(details.Id)).Value!.Title);
        await Assert.ThrowsAsync<InvalidOperationException>(() => queries.ListProjectFactsForMutationAsync(project.Id));
    }

    [Fact]
    public async Task Delayed_search_projection_cannot_replace_a_newer_prompt_or_revive_an_archived_prompt() {
        await using var application = await TestApplication.CreateAsync();
        await using var scope = application.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var artifact = new PromptArtifact { Title = "Current prompt", CurrentDraftText = "Current", Status = PromptArtifactStatus.Final,
            CurrentVersionNumber = 1, CreatedAtUtc = CreatedAt, UpdatedAtUtc = CreatedAt };
        await using (var owner = await services.GetRequiredService<IDbContextFactory<PromptsDbContext>>().CreateDbContextAsync()) {
            owner.Add(artifact);
            await owner.SaveChangesAsync();
        }
        var driver = new SearchIndexPromptGalleryProjectionDriver(services.GetRequiredService<SearchProjectionStore>(),
            services.GetRequiredService<IPromptArtifactProjectionQueryService>());
        var current = new PromptGalleryProjectionDocument(artifact.Id, null, "Current prompt", "Current summary", "Published content",
            artifact.Kind, artifact.Status, [], $"/prompt-gallery?promptId={artifact.Id:D}", CreatedAt);
        await driver.UpsertAsync(current);
        await driver.UpsertAsync(current with { Title = "Stale title", UpdatedAtUtc = CreatedAt.AddSeconds(-1) });
        await driver.RemoveAsync(artifact.Id, CreatedAt.AddSeconds(-1));
        await using (var search = await services.GetRequiredService<IDbContextFactory<SearchDbContext>>().CreateDbContextAsync()) {
            Assert.Equal("Current prompt", (await search.Set<SearchDocument>().SingleAsync(item => item.SourceKey == artifact.Id.ToString())).Title);
        }
        await using (var owner = await services.GetRequiredService<IDbContextFactory<PromptsDbContext>>().CreateDbContextAsync()) {
            var row = await owner.Set<PromptArtifact>().SingleAsync(item => item.Id == artifact.Id);
            row.IsArchived = true;
            row.UpdatedAtUtc = CreatedAt.AddSeconds(1);
            await owner.SaveChangesAsync();
        }
        await driver.RemoveAsync(artifact.Id, CreatedAt.AddSeconds(1));
        await driver.UpsertAsync(current);
        await using var readback = await services.GetRequiredService<IDbContextFactory<SearchDbContext>>().CreateDbContextAsync();
        Assert.False(await readback.Set<SearchDocument>().AnyAsync(item => item.SourceKey == artifact.Id.ToString()));
    }

    private sealed class RecordingActivity : IActivityStream {
        public List<ActivityWriteRequest> Requests { get; } = [];
        public Task RecordAsync(ActivityWriteRequest request, CancellationToken cancellationToken = default) {
            Requests.Add(request);
            return Task.CompletedTask;
        }
    }

    private sealed class CommandProbe : DbCommandInterceptor {
        public List<(DbConnection? Connection, DbTransaction? Transaction)> Commands { get; } = [];
        public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(DbCommand command,
            CommandEventData eventData, InterceptionResult<DbDataReader> result, CancellationToken cancellationToken = default) {
            Commands.Add((command.Connection, command.Transaction));
            return ValueTask.FromResult(result);
        }
    }
}
