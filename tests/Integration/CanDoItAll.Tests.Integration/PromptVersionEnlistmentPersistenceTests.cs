using CanDoItAll.Infrastructure.Editing;
using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.Modules.Projects;
using CanDoItAll.Modules.Prompts;
using CanDoItAll.Tests.Support;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using System.Data;

namespace CanDoItAll.Tests.Integration;

public sealed class PromptVersionEnlistmentPersistenceTests {
    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public async Task Fresh_version_witness_refuses_early_completion_and_follows_the_outer_commit(bool update, bool commit) {
        await using var application = await TestApplication.CreateAsync();
        await using var scope = application.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var gallery = services.GetRequiredService<IPromptGalleryService>();
        var changes = services.GetRequiredService<PromptGalleryChanges>();
        var feed = services.GetRequiredService<EditingChangeFeed>();
        var mutation = services.GetRequiredService<IPromptGalleryMutationService>();
        var transactions = services.GetRequiredService<CoordinatedDatabaseTransaction>();
        var ownerFactory = services.GetRequiredService<IDbContextFactory<ProjectsDbContext>>();
        var request = CreateRequest();
        PromptGalleryItemDetails? original = null;
        if (update) {
            var saved = await services.GetRequiredService<IPromptGalleryImportService>().ImportVersionAsync(request);
            Assert.True(saved.IsSuccess);
            original = (await gallery.GetItemAsync(saved.Value!.PromptArtifactId)).Value!;
        }
        using var subscription = changes.Subscribe();
        var baseline = feed.DeliveredHints;
        var origin = new EditingOperationIdentity(Guid.NewGuid(), Guid.NewGuid());
        await using var owner = await ownerFactory.CreateDbContextAsync();
        await using var transaction = await owner.Database.BeginTransactionAsync(IsolationLevel.Serializable);
        PromptVersionPreparation preparation;
        using (changes.Enter(origin))
        using (transactions.Enter(owner)) {
            var result = original is null
                ? await mutation.StageCreateImportedVersionAsync(request)
                : await mutation.StageUpdateAndVersionAsync(ToDraft(original, "Staged updated content"), new("Staged update"));
            Assert.True(result.IsSuccess);
            preparation = result.Value!;
            Assert.True((await gallery.GetVersionSnapshotAsync(preparation.Snapshot.PromptVersionId)).IsFailure);
            await Assert.ThrowsAsync<InvalidOperationException>(() => mutation.CompleteVersionAsync(preparation));
            Assert.Equal(baseline, feed.DeliveredHints);
        }
        if (!commit) {
            await transaction.RollbackAsync();
            await Assert.ThrowsAsync<InvalidOperationException>(() => mutation.CompleteVersionAsync(preparation));
            Assert.True((await gallery.GetVersionSnapshotAsync(preparation.Snapshot.PromptVersionId)).IsFailure);
            Assert.Equal(baseline, feed.DeliveredHints);
            if (original is not null) {
                var retained = (await gallery.GetItemAsync(original.Id)).Value!;
                Assert.Equal(original.DraftContent, retained.DraftContent);
                Assert.Equal(original.UpdatedAtUtc, retained.UpdatedAtUtc);
                Assert.Single(retained.Versions);
            }
            return;
        }
        await transaction.CommitAsync();
        await transaction.DisposeAsync();
        await mutation.CompleteVersionAsync(preparation);
        await mutation.CompleteVersionAsync(preparation);
        Assert.Equal(baseline + 1, feed.DeliveredHints);
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        var hint = Assert.Single((await subscription.ReadAsync(deadline.Token)).Changes);
        Assert.Equal(origin.EditorId, hint.OriginEditorId);
        Assert.Equal(origin.OperationId, hint.OperationId);
        var version = (await gallery.GetVersionSnapshotAsync(preparation.Snapshot.PromptVersionId)).Value!;
        Assert.Equal(update ? "Staged updated content" : request.Draft.Content, version.Content);
        Assert.Equal(update ? 2 : 1, version.VersionNumber);
    }

    [Fact]
    public async Task Enlisted_import_creation_cannot_use_an_existing_version_as_a_commit_witness() {
        await using var application = await TestApplication.CreateAsync();
        await using var scope = application.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var request = CreateRequest();
        var saved = await services.GetRequiredService<IPromptGalleryImportService>().ImportVersionAsync(request);
        Assert.True(saved.IsSuccess);
        var gallery = services.GetRequiredService<IPromptGalleryService>();
        var original = (await gallery.GetItemAsync(saved.Value!.PromptArtifactId)).Value!;
        await using var owner = await services.GetRequiredService<IDbContextFactory<ProjectsDbContext>>().CreateDbContextAsync();
        await using var transaction = await owner.Database.BeginTransactionAsync(IsolationLevel.Serializable);
        using (services.GetRequiredService<CoordinatedDatabaseTransaction>().Enter(owner)) {
            var refused = await services.GetRequiredService<IPromptGalleryMutationService>().StageCreateImportedVersionAsync(request);
            Assert.True(refused.IsFailure);
            Assert.Equal(PromptGalleryErrorCodes.ImportExpectationRequired, Assert.Single(refused.Errors).Code);
        }
        await transaction.RollbackAsync();
        var retained = (await gallery.GetItemAsync(original.Id)).Value!;
        Assert.Equal(original.UpdatedAtUtc, retained.UpdatedAtUtc);
        Assert.Single(retained.Versions);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Weaker_isolation_is_refused_before_a_prompt_write_even_if_the_owner_commits(bool update) {
        await using var application = await TestApplication.CreateAsync();
        await using var scope = application.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var request = CreateRequest();
        var gallery = services.GetRequiredService<IPromptGalleryService>();
        PromptGalleryItemDetails? original = null;
        if (update) {
            var saved = await services.GetRequiredService<IPromptGalleryImportService>().ImportVersionAsync(request);
            Assert.True(saved.IsSuccess);
            original = (await gallery.GetItemAsync(saved.Value!.PromptArtifactId)).Value!;
        }
        await using var owner = await services.GetRequiredService<IDbContextFactory<ProjectsDbContext>>().CreateDbContextAsync();
        await using var transaction = await owner.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted);
        using (services.GetRequiredService<CoordinatedDatabaseTransaction>().Enter(owner)) {
            var mutation = services.GetRequiredService<IPromptGalleryMutationService>();
            var failure = original is null
                ? await Assert.ThrowsAsync<InvalidOperationException>(() => mutation.StageCreateImportedVersionAsync(request))
                : await Assert.ThrowsAsync<InvalidOperationException>(() => mutation.StageUpdateAndVersionAsync(ToDraft(original, "Refused content"), new("Refused update")));
            Assert.Contains("isolation", failure.Message, StringComparison.OrdinalIgnoreCase);
        }
        await transaction.CommitAsync();
        await using var verify = await services.GetRequiredService<IDbContextFactory<PromptsDbContext>>().CreateDbContextAsync();
        Assert.Equal(update ? 1 : 0, await verify.Set<PromptArtifact>().CountAsync(item => item.SourceKey == request.SourceKey));
        if (original is not null) {
            var retained = (await gallery.GetItemAsync(original.Id)).Value!;
            Assert.Equal(original.DraftContent, retained.DraftContent);
            Assert.Equal(original.UpdatedAtUtc, retained.UpdatedAtUtc);
            Assert.Single(retained.Versions);
        }
    }

    private static PromptGalleryImportRequest CreateRequest() => new(PromptArtifactProvenance.WorkflowCreated,
        $"version-stage:{Guid.NewGuid():D}", "workflow-version-stage-proof",
        new(null, null, null, "Version staging proof", "Independent owner transaction", PromptGalleryItemKind.FullPrompt,
            "workflow", "Original staged content", SupportedConsumers: [PromptGalleryConsumer.Workflow]), new("Initial staged version"));

    private static PromptGalleryDraft ToDraft(PromptGalleryItemDetails item, string content) => new(item.Id,
        item.ProjectId, item.CollectionId, item.Title, item.Summary, item.Kind, item.Phase, content, item.Tags,
        item.SupportedModels, item.SupportedConsumers, item.Recommendations, item.UpdatedAtUtc);
}
