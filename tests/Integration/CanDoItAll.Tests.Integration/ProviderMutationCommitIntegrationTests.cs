using System.Reflection;
using System.Net;
using System.Net.Http.Json;
using System.Runtime.ExceptionServices;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.Modules.AgentFramework.ProviderManagement;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Editor = CanDoItAll.AgentFramework.Models.ProviderProfileEditorModel;
using Entity = CanDoItAll.Modules.AgentFramework.ProviderManagement.ProviderProfile;
using RuntimeKind = CanDoItAll.AgentFramework.Models.ProviderKind;

namespace CanDoItAll.Tests.Integration.SharedProviders;

public sealed class ProviderMutationCommitIntegrationTests(SharedProviderRuntimeProjectionFixture fixture)
    : IClassFixture<SharedProviderRuntimeProjectionFixture> {
    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    public async Task Canonical_commit_survives_secondary_failure_and_repair_never_replays_write(bool observerFailure, bool delete) {
        await using var scope = fixture.Services.CreateAsyncScope();
        var observer = new FailingObserver();
        var store = DispatchProxy.Create<ISandboxWorkspaceStore, StoreProxy>();
        var projection = (StoreProxy)(object)store;
        projection.Inner = scope.ServiceProvider.GetRequiredService<ISandboxWorkspaceStore>();
        var observers = scope.ServiceProvider.GetServices<IProviderProfileCommitObserver>().Append(observer).ToArray();
        var registry = ActivatorUtilities.CreateInstance<DatabaseProviderProfileRegistry>(
            scope.ServiceProvider, store, (IEnumerable<IProviderProfileCommitObserver>)observers);
        var model = NewEditor();
        if (delete) {
            model.Id = await registry.SaveProviderAsync(model);
        }
        observer.Fail = observerFailure;
        projection.Fail = !observerFailure;
        var exception = await Assert.ThrowsAnyAsync<ProviderMutationCommittedException>(async () => {
            if (delete) {
                await registry.DeleteProviderAsync(model.Id!.Value);
            } else {
                await registry.SaveProviderAsync(model);
            }
        });
        Assert.True(exception.CanonicalCommitSucceeded);
        Assert.NotEqual(Guid.Empty, exception.ProviderId);
        Assert.Equal(delete ? ProviderCatalogProjectionOperationKind.Delete : ProviderCatalogProjectionOperationKind.Upsert,
            exception.OperationKind);
        await using var db = await fixture.Factory.CreateDbContextAsync();
        var row = await db.Set<Entity>().AsNoTracking().SingleOrDefaultAsync(item => item.Id == exception.ProviderId);
        Assert.Equal(delete, row is null);
        var token = row?.ConcurrencyToken;
        var count = await db.Set<Entity>().CountAsync(item => item.Name == model.Name);
        observer.Fail = false;
        projection.Fail = false;
        await registry.ReconcileAsync(exception.ProviderId);
        Assert.Equal(count, await db.Set<Entity>().CountAsync(item => item.Name == model.Name));
        Assert.Equal(token, (await db.Set<Entity>().AsNoTracking().SingleOrDefaultAsync(item => item.Id == exception.ProviderId))?.ConcurrencyToken);
        Assert.Equal(delete, await registry.GetProviderAsync(exception.ProviderId) is null);
    }

    [Fact]
    public async Task Validation_rejection_is_precommit_and_correction_can_be_saved() {
        await using var scope = fixture.Services.CreateAsyncScope();
        var registry = scope.ServiceProvider.GetRequiredService<IProviderProfileRegistry>();
        var model = NewEditor();
        model.BaseUrl = "not a URI";
        await Assert.ThrowsAsync<ProviderProfileValidationException>(() => registry.SaveProviderAsync(model));
        await using var db = await fixture.Factory.CreateDbContextAsync();
        Assert.False(await db.Set<Entity>().AnyAsync(item => item.Name == model.Name));
        model.BaseUrl = "http://127.0.0.1:11434";
        var id = await registry.SaveProviderAsync(model);
        Assert.True(await db.Set<Entity>().AnyAsync(item => item.Id == id));
    }

    [Fact]
    public async Task Owner_precancellation_does_not_create_a_provider() {
        await using var scope = fixture.Services.CreateAsyncScope();
        var registry = scope.ServiceProvider.GetRequiredService<IProviderProfileRegistry>();
        var model = NewEditor();
        using var owner = new CancellationTokenSource();
        owner.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => registry.SaveProviderAsync(model, owner.Token));
        await using var db = await fixture.Factory.CreateDbContextAsync();
        Assert.False(await db.Set<Entity>().AnyAsync(item => item.Name == model.Name));
    }

    [Fact]
    public async Task Editor_concurrency_token_rejects_a_stale_write_without_overwriting_the_winner() {
        await using var scope = fixture.Services.CreateAsyncScope();
        var registry = scope.ServiceProvider.GetRequiredService<IProviderProfileRegistry>();
        var id = await registry.SaveProviderAsync(NewEditor());
        var first = await registry.GetProviderEditorAsync(id);
        var stale = await registry.GetProviderEditorAsync(id);
        Assert.NotNull(first.ExpectedConcurrencyToken);
        first.Name += " winner";
        await registry.SaveProviderAsync(first);
        stale.Name += " stale";
        await Assert.ThrowsAsync<ProviderProfileConcurrencyException>(() => registry.SaveProviderAsync(stale));
        Assert.Equal(first.Name, (await registry.GetProviderEditorAsync(id)).Name);
    }

    [Fact]
    public async Task Opening_sharing_without_publishing_does_not_create_identity_or_block_delete() {
        await using var scope = fixture.Services.CreateAsyncScope();
        var registry = scope.ServiceProvider.GetRequiredService<IProviderProfileRegistry>();
        var id = await registry.SaveProviderAsync(NewEditor());
        var sharing = await scope.ServiceProvider.GetRequiredService<ISharedProviderManagementService>().GetProfileSharingAsync(id);
        Assert.Null(sharing.Publication);
        await using var db = await fixture.Factory.CreateDbContextAsync();
        Assert.False(await db.Set<ProviderSharePublication>().AnyAsync(item => item.ProviderProfileId == id));
        await registry.DeleteProviderAsync(id);
        Assert.Null(await registry.GetProviderAsync(id));
    }

    [Fact]
    public async Task Seam_publication_identity_is_explicit_permanent_and_retained_after_unpublish() {
        await using var scope = fixture.Services.CreateAsyncScope();
        var registry = scope.ServiceProvider.GetRequiredService<IProviderProfileRegistry>();
        var sharing = scope.ServiceProvider.GetRequiredService<ISharedProviderManagementService>();
        var id = await registry.SaveProviderAsync(NewEditor());
        var read = await sharing.GetProfileSharingAsync(id);
        Assert.Null(read.Publication);
        Assert.True(read.Eligibility!.IsEligible, read.Eligibility.SanitizedReason);
        var published = await sharing.SetPublicationAsync(id, SharedProviderPublicationAction.Publish, null);
        Assert.True(published.Publication!.IsPublished);
        await Assert.ThrowsAsync<SharedProviderProfileDeletionBlockedException>(() => registry.DeleteProviderAsync(id));
        var unpublished = await sharing.SetPublicationAsync(id, SharedProviderPublicationAction.Unpublish,
            published.Publication.ConcurrencyToken);
        Assert.False(unpublished.Publication!.IsPublished);
        Assert.Equal(published.Publication.PublicId, unpublished.Publication.PublicId);
        await Assert.ThrowsAsync<SharedProviderProfileDeletionBlockedException>(() => registry.DeleteProviderAsync(id));
        var response = await fixture.Client.DeleteAsync($"/api/agents/providers/{id:D}");
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Contains("agents.provider-reference-conflict", await response.Content.ReadAsStringAsync(), StringComparison.Ordinal);
        await using var db = await fixture.Factory.CreateDbContextAsync();
        Assert.Single(await db.Set<ProviderSharePublication>().Where(x => x.ProviderProfileId == id).ToListAsync());
    }

    [Fact]
    public async Task Seam_api_returns_stable_concurrency_and_missing_target_errors() {
        await using var scope = fixture.Services.CreateAsyncScope();
        var registry = scope.ServiceProvider.GetRequiredService<IProviderProfileRegistry>();
        var id = await registry.SaveProviderAsync(NewEditor());
        var stale = await registry.GetProviderEditorAsync(id);
        var current = await registry.GetProviderEditorAsync(id);
        current.Name += " winner";
        await registry.SaveProviderAsync(current);
        var response = await fixture.Client.PostAsJsonAsync("/api/agents/providers", stale);
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Contains("agents.provider-concurrency-conflict", await response.Content.ReadAsStringAsync(), StringComparison.Ordinal);
        var missing = await fixture.Client.GetAsync($"/api/agents/providers/{Guid.NewGuid():D}/editor");
        Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode);
    }

    private static Editor NewEditor() => new() {
        Name = $"Provider seam proof {Guid.NewGuid():N}",
        Kind = RuntimeKind.Ollama,
        Transport = ProviderTransportKind.ChatCompletions,
        BaseUrl = "http://127.0.0.1:11434",
        DefaultModel = "seam-model",
        SuggestedModels = ["seam-model"],
        IsEnabled = true
    };

    private sealed class FailingObserver : IProviderProfileCommitObserver {
        public bool Fail { get; set; }
        public Task ProviderSavedAsync(Guid providerId, CancellationToken cancellationToken = default) => Complete();
        public Task ProviderDeletedAsync(Guid providerId, CancellationToken cancellationToken = default) => Complete();
        private Task Complete() => Fail ? Task.FromException(new IOException("Synthetic observer failure.")) : Task.CompletedTask;
    }

    public class StoreProxy : DispatchProxy {
        public ISandboxWorkspaceStore Inner { get; set; } = null!;
        public bool Fail { get; set; }
        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args) {
            if (Fail && targetMethod?.Name == nameof(ISandboxWorkspaceStore.UpdateCatalogAsync)) {
                throw new IOException("Synthetic projection failure.");
            }
            try {
                return targetMethod!.Invoke(Inner, args);
            } catch (TargetInvocationException exception) when (exception.InnerException is not null) {
                ExceptionDispatchInfo.Capture(exception.InnerException).Throw();
                throw;
            }
        }
    }
}
