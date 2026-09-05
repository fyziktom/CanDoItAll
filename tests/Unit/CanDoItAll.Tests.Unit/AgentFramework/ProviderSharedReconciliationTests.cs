using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Modules.AgentFramework;
using CanDoItAll.Modules.AgentFramework.ProviderManagement;
using Editor = CanDoItAll.AgentFramework.Models.ProviderProfileEditorModel;
using Profile = CanDoItAll.AgentFramework.Models.ProviderProfile;
using Kind = CanDoItAll.AgentFramework.Models.ProviderKind;

namespace CanDoItAll.Tests.Unit.AgentFramework;

public sealed class ProviderSharedReconciliationTests {
    public static TheoryData<bool, SharedProviderChangeKind> EditableCases {
        get {
            var cases = new TheoryData<bool, SharedProviderChangeKind>();
            foreach (var kind in Enum.GetValues<SharedProviderChangeKind>()) {
                cases.Add(false, kind);
                cases.Add(true, kind);
            }
            return cases;
        }
    }

    [Theory]
    [MemberData(nameof(EditableCases))]
    public async Task Every_unrelated_shared_change_preserves_existing_or_new_draft(bool newDraft, SharedProviderChangeKind kind) {
        var reads = new Reads();
        using var session = new ProviderProfilesSession(reads);
        await session.RefreshAsync();
        if (newDraft) {
            await session.NewAsync();
        }
        session.SelectSection(ProviderEditorSection.Runtime);
        session.SetSharedConnectionsOpen(true);
        var context = session.EditContext;
        var editorReads = reads.EditorReads;
        session.Draft.Name = "Unsaved name";
        session.Draft.Notes = "Unsaved notes";
        session.Draft.SuggestedModels = ["typed-model"];
        session.Draft.Tags = ["unsaved-tag"];
        Assert.False(await session.ReconcileSharedAsync(new(kind, [Guid.NewGuid()], remoteOwnedFieldsChanged: true,
            catalogMembershipMayHaveChanged: true)));
        Assert.Same(context, session.EditContext);
        Assert.Equal("Unsaved name", session.Draft.Name);
        Assert.Equal("Unsaved notes", session.Draft.Notes);
        Assert.Equal(["typed-model"], session.Draft.SuggestedModels);
        Assert.Equal(["unsaved-tag"], session.Draft.Tags);
        Assert.Equal(ProviderEditorSection.Runtime, session.State.Section);
        Assert.True(session.State.SharedConnectionsOpen);
        Assert.Equal(editorReads, reads.EditorReads);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Imported_editor_reloads_only_when_its_id_is_affected(bool affected) {
        var reads = new Reads { Imported = true };
        using var session = new ProviderProfilesSession(reads);
        await session.RefreshAsync();
        var context = session.EditContext;
        var count = reads.EditorReads;
        var applied = await session.ReconcileSharedAsync(new(SharedProviderChangeKind.SourceEnablement,
            [affected ? reads.Id : Guid.NewGuid()], remoteOwnedFieldsChanged: true));
        Assert.Equal(affected, applied);
        Assert.Equal(count + (affected ? 1 : 0), reads.EditorReads);
        Assert.Equal(!affected, ReferenceEquals(context, session.EditContext));
    }

    [Fact]
    public async Task Retired_target_is_failed_without_selecting_another_provider() {
        var reads = new Reads { Imported = true };
        using var session = new ProviderProfilesSession(reads);
        await session.RefreshAsync();
        var count = reads.EditorReads;
        await session.ReconcileSharedAsync(new(SharedProviderChangeKind.ImportRetirement, [reads.Id], [reads.Id]));
        Assert.Equal(reads.Id, session.State.ProviderId);
        Assert.Equal(ProviderProfilesLoadState.Failed, session.EditorLoadState);
        Assert.False(session.CanEdit);
        Assert.Equal(count, reads.EditorReads);
    }

    [Fact]
    public async Task Unknown_scope_keeps_the_draft_and_exposes_stale_state() {
        var reads = new Reads { Imported = true };
        using var session = new ProviderProfilesSession(reads);
        await session.RefreshAsync();
        var context = session.EditContext;
        await session.ReconcileSharedAsync(new(SharedProviderChangeKind.SourceAvailability, [], unknownScope: true));
        Assert.Same(context, session.EditContext);
        Assert.NotNull(session.MetadataWarning);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Missing_or_malformed_selected_import_fails_closed(bool malformed) {
        var reads = new Reads { Imported = true };
        using var session = new ProviderProfilesSession(reads);
        await session.RefreshAsync();
        reads.Missing = !malformed;
        reads.Malformed = malformed;
        await session.ReconcileSharedAsync(new(SharedProviderChangeKind.Reconciliation, [reads.Id],
            remoteOwnedFieldsChanged: true, catalogMembershipMayHaveChanged: true));
        Assert.Equal(reads.Id, session.State.ProviderId);
        Assert.Equal(ProviderProfilesLoadState.Failed, session.EditorLoadState);
        Assert.False(session.CanEdit);
    }

    [Fact]
    public void Change_scope_owns_caller_collections() {
        var id = Guid.NewGuid();
        var affected = new List<Guid> { id };
        var retired = new List<Guid> { id };
        var change = new SharedProviderChange(SharedProviderChangeKind.ImportRetirement, affected, retired);
        affected.Clear();
        retired.Clear();
        Assert.Equal([id], change.AffectedProviderProfileIds);
        Assert.Equal([id], change.RetiredProviderProfileIds);
    }

    private sealed class Reads : IProviderProfilesReads {
        public Guid Id { get; } = Guid.NewGuid();
        public bool Imported { get; init; }
        public bool Missing { get; set; }
        public bool Malformed { get; set; }
        public int EditorReads { get; private set; }
        public Task<ProviderProfilesCatalog> LoadCatalogAsync(CancellationToken token = default) {
            var profile = new Profile(Id, "Saved provider", Kind.OpenAi, "", "", "model", ProviderTransportKind.Responses,
                true, true, true, false, true, "{}", "", "", null, ["model"]) {
                ConnectorPluginKey = Imported ? ProviderConnectorKeys.SharedImport : ProviderConnectorKeys.OpenAi
            };
            return Task.FromResult(new ProviderProfilesCatalog(Missing ? [] : [profile], new([])));
        }
        public Task<Editor> LoadEditorAsync(Guid id, CancellationToken token = default) {
            EditorReads++;
            return Malformed ? Task.FromException<Editor>(new InvalidOperationException("Malformed projection"))
                : Task.FromResult(new Editor { Id = id, Name = "Saved provider", DefaultModel = "model", ExpectedConcurrencyToken = Guid.NewGuid() });
        }
    }
}
