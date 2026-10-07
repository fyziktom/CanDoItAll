using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Modules.AgentFramework;
using ProviderConnectorKeys = CanDoItAll.Modules.AgentFramework.ProviderManagement.ProviderConnectorKeys;

namespace CanDoItAll.Tests.Unit.AgentFramework;

public sealed class ProviderImportedSnapshotTests {
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Late_snapshot_read_cannot_overwrite_newer_revision(bool fails) {
        var reads = new Reads();
        using var session = new ProviderProfilesSession(reads);
        await session.RefreshAsync();
        var original = session.EditContext;
        reads.Advance("version-one");
        var oldDraft = reads.Draft(reads.Imported.Id);
        var pending = new TaskCompletionSource<ProviderProfileEditorModel>(TaskCreationOptions.RunContinuationsAsynchronously);
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        CancellationToken token = default;
        reads.Editor = (_, owner) => {
            token = owner;
            entered.SetResult();
            return pending.Task;
        };
        var old = session.RefreshAsync();
        await entered.Task;
        Assert.Same(original, session.EditContext);
        Assert.Equal("initial", session.SelectedProvider!.DefaultModel);
        Assert.Equal("initial", session.Draft.DefaultModel);
        reads.Advance("version-two");
        reads.Editor = null;
        Assert.True(await session.RefreshAsync());
        var accepted = session.EditContext;
        Complete(pending, oldDraft, fails);
        Assert.False(await old);
        Assert.True(token.IsCancellationRequested);
        Assert.Same(accepted, session.EditContext);
        Assert.Equal("version-two", session.Draft.DefaultModel);
        Assert.Equal(session.SelectedProvider!.DefaultModel, session.Draft.DefaultModel);
        Assert.Equal(reads.Revision, session.Draft.ExpectedConcurrencyToken);
        Assert.True(session.CanEdit);
        Assert.Null(session.MetadataWarning);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Imported_A_to_local_B_to_A_rejects_late_snapshot(bool fails) {
        var reads = new Reads();
        using var session = new ProviderProfilesSession(reads);
        await session.RefreshAsync();
        reads.Advance("old-request");
        var oldDraft = reads.Draft(reads.Imported.Id);
        var pending = new TaskCompletionSource<ProviderProfileEditorModel>(TaskCreationOptions.RunContinuationsAsynchronously);
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        CancellationToken token = default;
        reads.Editor = (_, owner) => {
            token = owner;
            entered.SetResult();
            return pending.Task;
        };
        var old = session.RefreshMetadataAsync();
        await entered.Task;
        reads.Editor = null;
        await session.SelectAsync(reads.Local.Id);
        reads.Advance("new-acquisition");
        await session.RefreshMetadataAsync();
        await session.SelectAsync(reads.Imported.Id);
        var accepted = session.EditContext;
        Complete(pending, oldDraft, fails);
        Assert.False(await old);
        Assert.True(token.IsCancellationRequested);
        Assert.Same(accepted, session.EditContext);
        Assert.Equal("new-acquisition", session.Draft.DefaultModel);
        Assert.True(session.CanEdit);
        Assert.Null(session.Error);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Inconsistent_editor_revision_is_unavailable_until_explicit_refresh(bool initial) {
        var reads = new Reads();
        using var session = new ProviderProfilesSession(reads);
        if (!initial) {
            await session.RefreshAsync();
            reads.Advance("changed");
        }
        reads.Editor = (id, _) => {
            var draft = reads.Draft(id);
            draft.ExpectedConcurrencyToken = Guid.NewGuid();
            return Task.FromResult(draft);
        };
        Assert.False(await session.RefreshAsync());
        Assert.False(session.CanEdit);
        Assert.Equal(reads.Imported.Id, session.State.ProviderId);
        Assert.NotNull(session.EditorError);
        reads.Editor = null;
        Assert.True(await session.RefreshAsync());
        Assert.Equal(reads.Revision, session.Draft.ExpectedConcurrencyToken);
        Assert.Equal(reads.Imported.DefaultModel, session.Draft.DefaultModel);
        Assert.True(session.CanEdit);
    }

    [Fact]
    public async Task Secret_metadata_failure_does_not_block_coherent_remote_snapshot_adoption() {
        var reads = new Reads();
        using var session = new ProviderProfilesSession(reads);
        await session.RefreshAsync();
        reads.Advance("changed");
        reads.SecretError = "Secret metadata unavailable";
        Assert.True(await session.RefreshAsync());
        Assert.Equal("changed", session.Draft.DefaultModel);
        Assert.Equal(reads.Revision, session.Draft.ExpectedConcurrencyToken);
        Assert.Equal(reads.SecretError, session.Catalog.Secrets.Error);
        Assert.True(session.CanEdit);
    }

    private static void Complete(TaskCompletionSource<ProviderProfileEditorModel> pending, ProviderProfileEditorModel draft, bool fails) {
        if (fails) {
            pending.SetException(new InvalidOperationException("Retired snapshot read"));
        } else {
            pending.SetResult(draft);
        }
    }

    private sealed class Reads : IProviderProfilesReads {
        public ProviderProfile Imported { get; private set; } = Profile("Imported") with { ConnectorPluginKey = ProviderConnectorKeys.SharedImport };
        public ProviderProfile Local { get; } = Profile("Local");
        public Guid Revision { get; private set; } = Guid.NewGuid();
        public string? SecretError { get; set; }
        public Func<Guid, CancellationToken, Task<ProviderProfileEditorModel>>? Editor { get; set; }

        public void Advance(string model) {
            Imported = Imported with { DefaultModel = model, SuggestedModels = [model] };
            Revision = Guid.NewGuid();
        }

        public Task<ProviderProfilesCatalog> LoadCatalogAsync(CancellationToken cancellationToken = default) => Task.FromResult(
            new ProviderProfilesCatalog([Imported, Local], new([], SecretError)) {
                Revisions = new Dictionary<Guid, ProviderConfigurationRevision> { [Imported.Id] = new(Revision) }
            });

        public Task<ProviderProfileEditorModel> LoadEditorAsync(Guid providerId, CancellationToken cancellationToken = default) =>
            Editor?.Invoke(providerId, cancellationToken) ?? Task.FromResult(Draft(providerId));

        public ProviderProfileEditorModel Draft(Guid id) => new() {
            Id = id, Name = id == Imported.Id ? Imported.Name : Local.Name,
            DefaultModel = id == Imported.Id ? Imported.DefaultModel : Local.DefaultModel, ExpectedConcurrencyToken = Revision
        };

        private static ProviderProfile Profile(string name) => new(Guid.NewGuid(), name, ProviderKind.OpenAi, "", "", "initial",
            ProviderTransportKind.Responses, true, true, true, false, true, "{}", "", "", null, ["initial"]);
    }
}
