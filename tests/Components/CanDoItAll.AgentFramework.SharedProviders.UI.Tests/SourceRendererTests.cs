using Bunit;
using CanDoItAll.AgentFramework.SharedProviders.UI;
using CanDoItAll.AgentFramework.SharedProviders.UiSandbox;
using CanDoItAll.Components.BaseLib;
using Microsoft.Extensions.DependencyInjection;

namespace CanDoItAll.Tests.Components.SharedProvidersUi;

public sealed class SourceRendererTests {
    public static TheoryData<SharingScenario> Scenarios => new(Enum.GetValues<SharingScenario>());

    [Theory]
    [MemberData(nameof(Scenarios))]
    public async Task Every_scenario_renders_actual_children_without_native_services(SharingScenario scenario) {
        using var context = Context();
        var session = new SharedProviderScenarioSession(scenario);
        var sharing = context.Render<SharedProviderSharingSurface>(p => p.Add(c => c.View, session));
        var sources = context.Render<SharedProviderSourcesSurface>(p => p.Add(c => c.View, session));
        var refresh = context.Render<SharedProviderRefreshSurface>(p => p.Add(c => c.View, session));
        Assert.NotNull(sources.Find("[data-testid='shared-provider-connections-dialog']"));
        Assert.Contains("Refresh", refresh.Markup, StringComparison.Ordinal);
        if (scenario == SharingScenario.Local) {
            Assert.Single(sharing.FindComponents<SharedProviderLocalPublicationContent>());
        } else if (scenario == SharingScenario.Imported) {
            Assert.Single(sharing.FindComponents<SharedProviderImportedProfileContent>());
        }
        Assert.Equal(0, session.Store.Writes);
        await context.DisposeRenderedComponentsAsync();
    }

    [Fact]
    public async Task Held_source_save_captures_unblurred_values_and_preserves_later_input_and_context() {
        using var context = Context();
        var session = new SharedProviderScenarioSession(SharingScenario.Held);
        var cut = Sources(context, session);
        await cut.Find("[data-testid='shared-provider-source-add']").ClickAsync();
        var draft = session.Sources.Editor!;
        var editContext = draft.Context;
        cut.Find("[data-testid='shared-provider-source-name']").Input("  New source  ");
        cut.Find("[data-testid='shared-provider-source-uri']").Input("https://NEW.example.test");
        var saving = cut.Find("[data-testid='shared-provider-source-save']").ClickAsync();
        cut.Find("[data-testid='shared-provider-source-name']").Input("Later text");
        await cut.Find("form").SubmitAsync();
        Assert.Equal(0, session.Store.Writes);
        session.Release();
        await saving;
        var stored = session.Store.Sources[draft.SourceId];
        Assert.Equal(1, session.Store.Writes);
        Assert.Equal("New source", stored.Values.Name);
        Assert.Equal("https://new.example.test/", stored.Values.BaseUri);
        Assert.Same(draft, session.Sources.Editor);
        Assert.Same(editContext, draft.Context);
        Assert.Equal("Later text", draft.Name);
        Assert.Equal(stored.Token, draft.ExpectedToken);
        Assert.Equal(stored.Values.BaseUri, draft.BaseUri);
        await context.DisposeRenderedComponentsAsync();
    }

    [Theory]
    [InlineData(SharingScenario.Unknown)]
    [InlineData(SharingScenario.CommittedReadFailure)]
    [InlineData(SharingScenario.DeliveryFailure)]
    public async Task Original_receipt_reconciles_once_without_replaying_source_or_erasing_later_text(SharingScenario scenario) {
        using var context = Context();
        var session = new SharedProviderScenarioSession(scenario);
        var cut = Sources(context, session);
        await cut.Find("[data-testid='shared-provider-source-edit']").ClickAsync();
        var draft = session.Sources.Editor!;
        cut.Find("[data-testid='shared-provider-source-name']").Input("Saved source");
        await cut.Find("[data-testid='shared-provider-source-save']").ClickAsync();
        cut.Find("[data-testid='shared-provider-source-name']").Input("After receipt");
        Assert.NotNull(session.Sources.Recovery);
        Assert.True(cut.Find("[data-testid='shared-provider-source-save']").HasAttribute("disabled"));
        var attempt = session.Sources.Recovery!.AttemptId;
        await cut.Find("[data-testid='shared-provider-source-verify']").ClickAsync();
        await session.VerifyAsync(attempt);
        Assert.Null(session.Sources.Recovery);
        Assert.Equal(1, session.Store.Writes);
        Assert.Equal("Saved source", session.Store.Sources[draft.SourceId].Values.Name);
        Assert.Equal("After receipt", draft.Name);
        Assert.Same(draft, session.Sources.Editor);
        await context.DisposeRenderedComponentsAsync();
    }

    [Fact]
    public async Task Unknown_before_commit_requires_read_only_verification_then_explicit_retry_of_original_candidate() {
        using var context = Context();
        var session = new SharedProviderScenarioSession(SharingScenario.UnknownBeforeCommit);
        var cut = Sources(context, session);
        await cut.Find("[data-testid='shared-provider-source-add']").ClickAsync();
        var draft = session.Sources.Editor!;
        cut.Find("[data-testid='shared-provider-source-name']").Input("Original candidate");
        cut.Find("[data-testid='shared-provider-source-uri']").Input("https://candidate.example.test/");
        await cut.Find("[data-testid='shared-provider-source-save']").ClickAsync();
        var recovery = session.Sources.Recovery!;
        await session.RetryVerifiedAsync(recovery.AttemptId);
        Assert.Equal(0, session.Store.Writes);
        cut.Find("[data-testid='shared-provider-source-name']").Input("Later draft");
        await cut.Find("[data-testid='shared-provider-source-verify']").ClickAsync();
        Assert.True(session.Sources.Recovery!.RetryAllowed);
        Assert.Equal(recovery.AttemptId, session.Sources.Recovery.AttemptId);
        Assert.Equal(0, session.Store.Writes);
        await cut.Find("[data-testid='shared-provider-source-retry-verified']").ClickAsync();
        Assert.Equal("Original candidate", session.Store.Sources[draft.SourceId].Values.Name);
        Assert.Equal("Later draft", draft.Name);
        Assert.Equal(1, session.Store.Writes);
        await context.DisposeRenderedComponentsAsync();
    }

    [Fact]
    public async Task Catalog_selection_is_frozen_before_await_and_later_selection_survives_readback() {
        using var context = Context();
        var session = new SharedProviderScenarioSession(SharingScenario.Held);
        var cut = Sources(context, session);
        await cut.Find("[data-testid='shared-provider-source-discover']").ClickAsync();
        var dialog = session.Sources.Catalog!;
        var next = dialog.Publications[1].PublicationId;
        var checkboxes = cut.FindAll("[data-testid='shared-provider-catalog-selection']");
        Assert.Equal(3, checkboxes.Count);
        checkboxes[1].Change(true);
        var applying = cut.Find("[data-testid='shared-provider-catalog-apply']").ClickAsync();
        cut.FindAll("[data-testid='shared-provider-catalog-selection']")[1].Change(false);
        var third = dialog.Publications[2].PublicationId;
        cut.FindAll("[data-testid='shared-provider-catalog-selection']")[2].Change(true);
        session.Release();
        await applying;
        Assert.Contains(next, session.Store.Selected);
        Assert.DoesNotContain(third, session.Store.Selected);
        Assert.Same(dialog, session.Sources.Catalog);
        Assert.False(dialog.IsSelected(next));
        Assert.True(dialog.IsSelected(third));
        Assert.Equal(session.Store.Sources[dialog.Origin.SourceId].Token, dialog.Origin.ConcurrencyToken);
        Assert.Equal(1, session.Store.Writes);
        await context.DisposeRenderedComponentsAsync();
    }

    [Fact]
    public async Task Invalid_raw_values_and_metadata_failure_keep_the_same_draft_without_writes() {
        using var context = Context();
        var session = new SharedProviderScenarioSession(SharingScenario.MetadataFailure);
        var cut = Sources(context, session);
        await cut.Find("[data-testid='shared-provider-source-edit']").ClickAsync();
        var draft = session.Sources.Editor!;
        cut.Find("[data-testid='shared-provider-source-uri']").Input("https://");
        await cut.Find("form").SubmitAsync();
        Assert.Contains("Enter an absolute HTTP or HTTPS instance URL.", cut.Markup, StringComparison.Ordinal);
        Assert.Equal(0, session.Store.Writes);
        await cut.Find("[data-testid='shared-provider-source-refresh']").ClickAsync();
        Assert.Same(draft, session.Sources.Editor);
        Assert.Equal("https://", draft.BaseUri);
        cut.Find("[data-testid='shared-provider-source-uri']").Input("https://valid.example.test/");
        await cut.InvokeAsync(() => session.SaveAsync(draft.Capture()));
        cut.Render();
        Assert.Contains("available credential", cut.Markup, StringComparison.Ordinal);
        Assert.Equal(0, session.Store.Writes);
        await context.DisposeRenderedComponentsAsync();
    }

    [Fact]
    public async Task Two_independent_editors_do_not_promote_dirty_values_after_concurrent_local_commit() {
        using var context = Context();
        var store = new SharedProviderScenarioStore();
        var first = new SharedProviderScenarioSession(store: store);
        var second = new SharedProviderScenarioSession(store: store);
        var a = context.Render<SharedProviderSharingSurface>(p => p.Add(c => c.View, first));
        var b = context.Render<SharedProviderSharingSurface>(p => p.Add(c => c.View, second));
        a.Find("[data-testid='shared-provider-import-alias']").Input("Dirty A");
        b.Find("[data-testid='shared-provider-import-alias']").Input("Saved B");
        await b.Find("[data-testid='shared-provider-import-save']").ClickAsync();
        first.CompleteRead();
        a.Render();
        Assert.True(first.ImportDraft.HasConflict);
        await a.Find("[data-testid='shared-provider-import-save']").ClickAsync();
        Assert.Equal("Saved B", store.Import.Settings.LocalAlias);
        Assert.Equal("Dirty A", first.ImportDraft.LocalAlias);
        Assert.Equal(1, store.Writes);
        await context.DisposeRenderedComponentsAsync();
    }

    [Fact]
    public async Task Changed_canonical_selection_requires_review_and_cannot_dispatch_the_preserved_draft() {
        using var context = Context();
        var session = new SharedProviderScenarioSession();
        var cut = Sources(context, session);
        await cut.Find("[data-testid='shared-provider-source-discover']").ClickAsync();
        var catalog = session.Sources.Catalog!;
        catalog.Select(catalog.Publications[1].PublicationId, true);
        catalog.ReadbackConflict = true;
        cut.Render();
        Assert.NotNull(cut.Find("[data-testid='shared-provider-catalog-conflict']"));
        Assert.True(cut.Find("[data-testid='shared-provider-catalog-apply']").HasAttribute("disabled"));
        await cut.InvokeAsync(() => session.ApplyCatalogAsync(catalog.Capture()));
        Assert.Equal(0, session.Store.Writes);
        Assert.Equal(2, catalog.SelectedCount);
        await context.DisposeRenderedComponentsAsync();
    }

    private static IRenderedComponent<SharedProviderSourcesSurface> Sources(BunitContext context, SharedProviderScenarioSession session) =>
        context.Render<SharedProviderSourcesSurface>(p => p.Add(c => c.View, session));
    private static BunitContext Context() {
        var context = new BunitContext();
        context.JSInterop.Mode = JSRuntimeMode.Loose;
        context.Services.AddCanDoItAllBaseLib();
        return context;
    }
}
