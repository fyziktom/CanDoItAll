using CanDoItAll.Editing;
using CanDoItAll.Infrastructure.Editing;
using CanDoItAll.Modules.Prompts;
using CanDoItAll.Tests.Support;

namespace CanDoItAll.Tests.Unit.Infrastructure;

[Collection(AppDbContextModelRegistryTestCollectionNames.Name)]
public sealed class PromptGalleryChangePublicationTests {
    [Fact]
    public async Task Canonical_content_lifecycle_and_preferences_emit_minimal_correlated_hints() {
        var feed = new EditingChangeFeed();
        var changes = PromptGalleryObservationTestSupport.Create(feed);
        using var subscription = changes.Subscribe();
        var service = PromptGalleryTestSupport.CreateService(PromptGalleryTestSupport.CreateFactory(nameof(Canonical_content_lifecycle_and_preferences_emit_minimal_correlated_hints)), changes);
        var operation = new EditingOperationIdentity(Guid.NewGuid(), Guid.NewGuid());
        Guid id;
        using (changes.Enter(operation)) {
            var saved = (await service.SaveDraftAsync(Draft())).Value;
            id = saved.PromptArtifactId;
            await service.CreateVersionAsync(id, new("Reviewed", saved.UpdatedAtUtc));
            await service.SetFavoriteAsync(id, true);
            await service.SetWarningSuppressionAsync(id, PromptGalleryConsumer.Chat, PromptCompatibilityIssueCode.ItemKindMismatch, true);
            await service.ArchiveAsync(id, true);
        }
        var batch = await subscription.ReadAsync(default);
        Assert.Equal(new[] { EditingChangeKind.Created, EditingChangeKind.LifecycleChanged, EditingChangeKind.MetadataChanged,
            EditingChangeKind.MetadataChanged, EditingChangeKind.LifecycleChanged }, batch.Changes.Select(item => item.Kind));
        Assert.All(batch.Changes, hint => {
            Assert.Equal(id, hint.Resource.ResourceId);
            Assert.Equal(operation.EditorId, hint.OriginEditorId);
            Assert.Equal(operation.OperationId, hint.OperationId);
        });
        Assert.False(batch.RequiresResync);
    }

    [Fact]
    public async Task Observer_failure_after_commit_keeps_the_exact_save_receipt_and_does_not_retry() {
        var publisher = new FailingPublisher();
        var changes = PromptGalleryObservationTestSupport.Create(publisher: publisher);
        var service = PromptGalleryTestSupport.CreateService(PromptGalleryTestSupport.CreateFactory(nameof(Observer_failure_after_commit_keeps_the_exact_save_receipt_and_does_not_retry)), changes);
        var result = await service.SaveDraftAsync(Draft());
        Assert.True(result.IsSuccess);
        var current = (await service.GetItemAsync(result.Value.PromptArtifactId)).Value!;
        Assert.Equal(result.Value.UpdatedAtUtc, current.UpdatedAtUtc);
        Assert.Equal("Preserved content", current.DraftContent);
        Assert.Equal(1, publisher.Calls);
    }

    [Fact]
    public async Task Archived_content_requires_explicit_restore_even_with_a_fresh_stamp() {
        var service = PromptGalleryTestSupport.CreateService(PromptGalleryTestSupport.CreateFactory(nameof(Archived_content_requires_explicit_restore_even_with_a_fresh_stamp)));
        var id = (await service.SaveDraftAsync(Draft())).Value.PromptArtifactId;
        await service.ArchiveAsync(id, true);
        var archived = (await service.GetItemAsync(id)).Value!;
        var attempted = Draft() with { Id = id, ExpectedUpdatedAtUtc = archived.UpdatedAtUtc, Content = "Implicit resurrection" };
        Assert.Contains((await service.SaveDraftAsync(attempted)).Errors, error => error.Code == PromptGalleryErrorCodes.ArchivedItem);
        Assert.Equal("Preserved content", (await service.GetItemAsync(id)).Value!.DraftContent);
        await service.ArchiveAsync(id, false);
        var restored = (await service.GetItemAsync(id)).Value!;
        Assert.True((await service.SaveDraftAsync(attempted with { ExpectedUpdatedAtUtc = restored.UpdatedAtUtc })).IsSuccess);
    }

    private static PromptGalleryDraft Draft() => new(null, null, null, "Shared draft", "", PromptGalleryItemKind.FullPrompt, "", "Preserved content");

    [Fact]
    public async Task Ui_write_admission_is_checked_at_the_owner_commit_and_denial_publishes_nothing() {
        var feed = new EditingChangeFeed();
        var changes = PromptGalleryObservationTestSupport.Create(feed);
        var service = PromptGalleryTestSupport.CreateService(PromptGalleryTestSupport.CreateFactory(nameof(Ui_write_admission_is_checked_at_the_owner_commit_and_denial_publishes_nothing)), changes);
        var saved = (await service.SaveDraftAsync(Draft())).Value;
        using var subscription = changes.Subscribe(saved.PromptArtifactId);
        using (changes.EnterUiWrite(_ => ValueTask.FromException(new UnauthorizedAccessException("Grant revoked before commit.")))) {
            await Assert.ThrowsAsync<UnauthorizedAccessException>(() => service.SaveDraftAsync(Draft() with {
                Id = saved.PromptArtifactId, ExpectedUpdatedAtUtc = saved.UpdatedAtUtc,
                Title = "Denied replacement", Tags = ["Denied tag"], SupportedModels = [new("Denied", "model", true)]
            }));
        }
        var current = (await service.GetItemAsync(saved.PromptArtifactId)).Value!;
        Assert.Equal("Shared draft", current.Title);
        Assert.Empty(current.Tags);
        Assert.Empty(current.SupportedModels);
        Assert.Equal(saved.UpdatedAtUtc, current.UpdatedAtUtc);
        Assert.Equal(0, feed.DeliveredHints);
    }

    [Fact]
    public async Task Submitted_baseline_matches_every_native_editable_normalization_without_a_later_read_as_acknowledgement() {
        var service = PromptGalleryTestSupport.CreateService(PromptGalleryTestSupport.CreateFactory(nameof(Submitted_baseline_matches_every_native_editable_normalization_without_a_later_read_as_acknowledgement)));
        var draft = Draft() with { Title = "  Trimmed title  ", Summary = " Summary ", Phase = " design ",
            Tags = [" Architecture ", "architecture"], SupportedModels = [new(" OpenAI ", " model|name ", true)],
            SupportedConsumers = [PromptGalleryConsumer.Chat, PromptGalleryConsumer.Workflow],
            Recommendations = new(0.2, 800, 0.9), ProjectId = Guid.NewGuid(), CollectionId = Guid.NewGuid() };
        var receipt = (await service.SaveDraftAsync(draft)).Value;
        var details = (await service.GetItemAsync(receipt.PromptArtifactId)).Value!;
        var submitted = new CanDoItAll.Prompts.UI.Editor.PromptGalleryEditorSubmission(draft.Title, draft.Summary, draft.Kind,
            draft.Phase, draft.Content, draft.Tags!, draft.SupportedModels!, draft.SupportedConsumers!, draft.Recommendations!);
        Assert.True(CanDoItAll.Modules.Prompts.Components.PromptGalleryPersistedContent.FromSubmission(submitted)
            .Matches(CanDoItAll.Modules.Prompts.Components.PromptGalleryPersistedContent.FromDetails(details)));
        Assert.Equal(receipt.UpdatedAtUtc, details.UpdatedAtUtc);
        Assert.Equal(draft.ProjectId, details.ProjectId);
        Assert.Equal(draft.CollectionId, details.CollectionId);
        Assert.Equal("model|name", Assert.Single(details.SupportedModels).Model);
    }

    private sealed class FailingPublisher : IEditingChangePublisher {
        public int Calls { get; private set; }

        public void Publish(EditingTopic resource, EditingChangeKind kind, Guid? originEditorId = null, Guid? operationId = null) {
            Calls++;
            throw new InvalidOperationException("Injected observer failure");
        }
    }
}
