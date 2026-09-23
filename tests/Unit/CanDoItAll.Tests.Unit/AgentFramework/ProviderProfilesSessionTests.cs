using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Modules.AgentFramework;
using ProviderConnectorKeys = CanDoItAll.Modules.AgentFramework.ProviderManagement.ProviderConnectorKeys;

namespace CanDoItAll.Tests.Unit.AgentFramework;

public sealed class ProviderProfilesSessionTests {
    [Fact]
    public async Task Bootstrap_selects_first_provider_and_new_has_independent_defaults() {
        var reads = new Reads();
        using var session = new ProviderProfilesSession(reads);
        await session.RefreshAsync();
        Assert.True(session.CanEdit);
        Assert.Equal(reads.First.Id, session.State.ProviderId);
        Assert.Equal(reads.First.Id, session.Draft.Id);
        var original = session.EditContext;
        await session.NewAsync();
        Assert.Null(session.State.ProviderId);
        Assert.Null(session.Draft.Id);
        Assert.NotSame(original, session.EditContext);
        Assert.Equal("New OpenAI provider", session.Draft.Name);
        Assert.Equal(ProviderTransportKind.Responses, session.Draft.Transport);
        Assert.Contains("openai", session.Draft.Tags);
    }

    [Theory]
    [InlineData(ProviderEditorSection.Connection, 0, "Connection")]
    [InlineData(ProviderEditorSection.Prices, 1, "Prices")]
    [InlineData(ProviderEditorSection.Runtime, 2, "Runtime")]
    [InlineData(ProviderEditorSection.Thinking, 3, "Thinking")]
    [InlineData(ProviderEditorSection.Sharing, 4, "Sharing")]
    [InlineData(ProviderEditorSection.History, 5, "History")]
    public async Task Sections_and_overlay_do_not_replace_the_draft_context(ProviderEditorSection section, int index, string label) {
        using var session = new ProviderProfilesSession(new Reads());
        await session.RefreshAsync();
        var context = session.EditContext;
        session.Draft.Name = "Unsaved";
        session.SelectSection(section);
        session.SetSharedConnectionsOpen(true);
        Assert.Equal(section, session.State.Section);
        Assert.True(session.State.SharedConnectionsOpen);
        Assert.Equal(index, ProviderEditorSections.IndexOf(section));
        Assert.Equal(label, ProviderEditorSections.At(index).Label);
        Assert.Same(context, session.EditContext);
        Assert.Equal("Unsaved", session.Draft.Name);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Replaced_editor_ignores_late_success_or_failure(bool fails) {
        var reads = new Reads();
        using var session = new ProviderProfilesSession(reads);
        await session.RefreshCatalogAsync();
        var pending = new TaskCompletionSource<ProviderProfileEditorModel>();
        CancellationToken oldToken = default;
        reads.Editor = (id, token) => {
            oldToken = token;
            return pending.Task;
        };
        var oldRead = session.SelectAsync(reads.First.Id);
        reads.Editor = (id, _) => Task.FromResult(new ProviderProfileEditorModel { Id = id, Name = "Second" });
        await session.SelectAsync(reads.Second.Id);
        Assert.True(oldToken.IsCancellationRequested);
        Complete(pending, new() { Id = reads.First.Id, Name = "Stale" }, fails);
        await oldRead;
        Assert.True(session.CanEdit);
        Assert.Equal(reads.Second.Id, session.State.ProviderId);
        Assert.Equal("Second", session.Draft.Name);
        Assert.Null(session.Error);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task New_during_initial_read_prevents_automatic_selection(bool fails) {
        var reads = new Reads();
        using var session = new ProviderProfilesSession(reads);
        var pending = new TaskCompletionSource<ProviderProfilesCatalog>();
        reads.Catalog = _ => pending.Task;
        var initial = session.RefreshAsync();
        await session.NewAsync();
        session.Draft.Name = "My new draft";
        Complete(pending, reads.Snapshot, fails);
        await initial;
        Assert.Null(session.State.ProviderId);
        Assert.Equal("My new draft", session.Draft.Name);
        Assert.Equal(!fails, session.CanEdit);
        reads.Catalog = _ => Task.FromResult(reads.Snapshot);
        Assert.False(await session.RefreshAsync());
        Assert.True(session.CanEdit);
        Assert.Equal("My new draft", session.Draft.Name);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Latest_catalog_refresh_wins_and_stale_completion_cannot_restore_loading(bool fails) {
        var reads = new Reads();
        using var session = new ProviderProfilesSession(reads);
        var pending = new TaskCompletionSource<ProviderProfilesCatalog>();
        CancellationToken oldToken = default;
        reads.Catalog = token => {
            oldToken = token;
            return pending.Task;
        };
        var old = session.RefreshAsync();
        reads.Catalog = _ => Task.FromResult(new ProviderProfilesCatalog([reads.Second], new([])));
        await session.RefreshAsync();
        Complete(pending, reads.Snapshot, fails);
        await old;
        Assert.True(oldToken.IsCancellationRequested);
        Assert.True(session.CanEdit);
        Assert.Equal(reads.Second.Id, session.State.ProviderId);
        Assert.Single(session.Catalog.Providers);
        Assert.Null(session.Error);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Disposal_cancels_target_and_ignores_uncooperative_completion(bool fails) {
        var reads = new Reads();
        using var session = new ProviderProfilesSession(reads);
        await session.RefreshCatalogAsync();
        var pending = new TaskCompletionSource<ProviderProfileEditorModel>();
        CancellationToken token = default;
        reads.Editor = (_, owner) => {
            token = owner;
            return pending.Task;
        };
        var loading = session.SelectAsync(reads.First.Id);
        var context = session.EditContext;
        session.Dispose();
        Complete(pending, new() { Id = reads.First.Id, Name = "Late" }, fails);
        await loading;
        Assert.True(token.IsCancellationRequested);
        Assert.False(session.CanEdit);
        Assert.Same(context, session.EditContext);
        Assert.Null(session.Error);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Core_failure_retains_target_and_retry_loads_it(bool catalogFails) {
        var reads = new Reads();
        using var session = new ProviderProfilesSession(reads);
        await session.RefreshAsync();
        if (catalogFails) {
            reads.Catalog = _ => throw new InvalidOperationException("Catalog unavailable");
        } else {
            reads.Editor = (_, _) => throw new InvalidOperationException("Editor unavailable");
        }
        await session.RefreshAsync();
        Assert.False(session.CanEdit);
        Assert.Equal(reads.First.Id, session.State.ProviderId);
        Assert.NotNull(session.Error);
        reads.Catalog = _ => Task.FromResult(reads.Snapshot);
        reads.Editor = (id, _) => Task.FromResult(new ProviderProfileEditorModel { Id = id, Name = "Retried" });
        await session.RefreshAsync();
        Assert.True(session.CanEdit);
        Assert.Equal(reads.First.Id, session.Draft.Id);
        Assert.Equal("Retried", session.Draft.Name);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task New_cancels_pending_editor_without_replacing_the_new_draft(bool fails) {
        var reads = new Reads();
        using var session = new ProviderProfilesSession(reads);
        await session.RefreshCatalogAsync();
        var pending = new TaskCompletionSource<ProviderProfileEditorModel>();
        CancellationToken token = default;
        reads.Editor = (_, owner) => {
            token = owner;
            return pending.Task;
        };
        var old = session.SelectAsync(reads.First.Id);
        await session.NewAsync();
        session.Draft.Name = "Keep new";
        Complete(pending, new() { Id = reads.First.Id }, fails);
        await old;
        Assert.True(token.IsCancellationRequested);
        Assert.Null(session.State.ProviderId);
        Assert.Equal("Keep new", session.Draft.Name);
        Assert.True(session.CanEdit);
    }

    [Fact]
    public async Task Catalog_removal_during_target_load_cannot_make_the_removed_provider_editable() {
        var reads = new Reads();
        using var session = new ProviderProfilesSession(reads);
        await session.RefreshAsync();
        var pending = new TaskCompletionSource<ProviderProfileEditorModel>();
        reads.Editor = (_, _) => pending.Task;
        var selected = session.SelectAsync(reads.Second.Id);
        reads.Catalog = _ => Task.FromResult(new ProviderProfilesCatalog([reads.First], new([])));
        await session.RefreshCatalogAsync();
        Assert.False(session.CanEdit);
        pending.SetResult(new() { Id = reads.Second.Id });
        await selected;
        Assert.False(session.CanEdit);
        Assert.Equal(reads.Second.Id, session.State.ProviderId);
        Assert.Equal(ProviderProfilesLoadState.Failed, session.EditorLoadState);
        Assert.False(string.IsNullOrWhiteSpace(session.Error));
    }

    [Fact]
    public async Task Missing_target_does_not_silently_select_another_provider() {
        var reads = new Reads();
        using var session = new ProviderProfilesSession(reads);
        await session.RefreshAsync();
        reads.Catalog = _ => Task.FromResult(new ProviderProfilesCatalog([reads.Second], new([])));
        await session.RefreshAsync();
        Assert.False(session.CanEdit);
        Assert.Equal(reads.First.Id, session.State.ProviderId);
        Assert.Equal(ProviderProfilesLoadState.Failed, session.EditorLoadState);
        Assert.False(string.IsNullOrWhiteSpace(session.Error));
    }

    [Fact]
    public async Task Secret_partial_failure_preserves_saved_reference_and_source_managed_state() {
        var reads = new Reads();
        var provider = reads.First with { ConnectorPluginKey = ProviderConnectorKeys.SharedImport };
        reads.Catalog = _ => Task.FromResult(new ProviderProfilesCatalog([provider], new([], "Secrets unavailable")));
        reads.Editor = (id, _) => Task.FromResult(new ProviderProfileEditorModel { Id = id, ApiKeyEnvironmentVariable = "secret:saved-reference" });
        using var session = new ProviderProfilesSession(reads);
        await session.RefreshAsync();
        Assert.True(session.CanEdit);
        Assert.True(session.IsSourceManaged);
        Assert.Equal("secret:saved-reference", session.Draft.ApiKeyEnvironmentVariable);
        Assert.Equal("Secrets unavailable", session.Catalog.Secrets.Error);
    }

    [Fact]
    public async Task Wrong_editor_identity_fails_closed() {
        var reads = new Reads { Editor = (_, _) => Task.FromResult(new ProviderProfileEditorModel()) };
        using var session = new ProviderProfilesSession(reads);
        await session.RefreshAsync();
        Assert.False(session.CanEdit);
        Assert.Equal(reads.First.Id, session.State.ProviderId);
        Assert.Equal(ProviderProfilesLoadState.Failed, session.EditorLoadState);
        Assert.False(string.IsNullOrWhiteSpace(session.Error));
    }

    private static void Complete<T>(TaskCompletionSource<T> pending, T result, bool fails) {
        if (fails) {
            pending.SetException(new InvalidOperationException("Late failure"));
        } else {
            pending.SetResult(result);
        }
    }

    private sealed class Reads : IProviderProfilesReads {
        public ProviderProfile First { get; } = Provider("First");
        public ProviderProfile Second { get; } = Provider("Second");
        public ProviderProfilesCatalog Snapshot => new([First, Second], new([]));
        public Func<CancellationToken, Task<ProviderProfilesCatalog>>? Catalog { get; set; }
        public Func<Guid, CancellationToken, Task<ProviderProfileEditorModel>> Editor { get; set; }
            = (id, _) => Task.FromResult(new ProviderProfileEditorModel { Id = id, Name = "Selected" });
        public Task<ProviderProfilesCatalog> LoadCatalogAsync(CancellationToken cancellationToken = default)
            => Catalog?.Invoke(cancellationToken) ?? Task.FromResult(Snapshot);
        public Task<ProviderProfileEditorModel> LoadEditorAsync(Guid providerId, CancellationToken cancellationToken = default)
            => Editor(providerId, cancellationToken);
        private static ProviderProfile Provider(string name) => new(Guid.NewGuid(), name, ProviderKind.OpenAi,
            string.Empty, string.Empty, "model", ProviderTransportKind.Responses, true, true, true, false, true,
            "{}", string.Empty, string.Empty, null, ["model"]);
    }
}
