using Bunit;
using CanDoItAll.Components.BaseLib;
using CanDoItAll.Modules.Workspace.StorageCatalog.Contracts;
using CanDoItAll.Workspace.StorageCatalog.UI;
using CanDoItAll.Workspace.StorageCatalog.UiSandbox;
using Microsoft.Extensions.DependencyInjection;

namespace CanDoItAll.Tests.Components.WorkspaceStorageCatalogUi;

public sealed class CatalogRendererTests {
    [Fact]
    public async Task Reopened_view_observes_retired_view_completion_without_inheriting_its_create_identity() {
        using var context = Context();
        var store = new CatalogScenarioStore();
        var ledger = new CatalogOperationLedger();
        using var old = new CatalogSession(store, ledger);
        old.Draft!.Name = "Original admitted create";
        old.Draft.EndpointOrRoot = "/scenario/original-create";
        store.Gates[CatalogScenarioStage.Persistence].Hold();
        var writing = old.MutateAsync(CatalogEffect.Save);
        old.Dispose();
        using var current = new CatalogSession(store, ledger);
        var cut = context.Render<CatalogSurface>(parameters => parameters.Add(component => component.Session, current));
        Assert.Empty(cut.FindAll("[data-testid=storage-operation-facts]"));
        Assert.True(cut.Find("[data-testid=storage-settings-save]").HasAttribute("disabled"));
        store.Gates[CatalogScenarioStage.Persistence].Release();
        await writing;
        cut.WaitForAssertion(() => Assert.Contains("Catalog: Committed", cut.Find("[data-testid=storage-operation-facts]").TextContent));
        Assert.False(cut.Find("[data-testid=storage-settings-save]").HasAttribute("disabled"));
        Assert.Null(current.Draft!.Id);
        Assert.Equal(1, store.CatalogWrites);
        Assert.NotNull(Assert.Single(ledger.Receipts).Outcome!.CatalogId);
    }

    [Theory]
    [InlineData(CatalogScenario.Empty, 0)]
    [InlineData(CatalogScenario.Large, 204)]
    public async Task Empty_and_large_catalogs_render_the_actual_selection_surface_without_effects(CatalogScenario scenario, int count) {
        using var context = Context();
        var store = new CatalogScenarioStore(scenario);
        using var session = new CatalogSession(store, new());
        await session.LoadAsync();
        var cut = context.Render<CatalogSurface>(parameters => parameters.Add(component => component.Session, session));
        Assert.Equal(count, cut.FindComponents<SelectionListItem>().Count);
        Assert.Single(cut.FindComponents<Steps>());
        Assert.Equal(0, store.CatalogWrites);
        Assert.Equal(0, store.DriverCalls);
    }

    [Fact]
    public async Task Partial_reference_failure_is_visible_and_retry_preserves_the_acquired_draft() {
        using var context = Context();
        var store = new CatalogScenarioStore(CatalogScenario.PartialReferences);
        using var session = new CatalogSession(store, new());
        await session.LoadAsync();
        await session.SelectAsync(CatalogScenarioData.FtpId);
        var draft = session.Draft!;
        draft.Name = "Retained unsaved edit";
        var cut = context.Render<CatalogSurface>(parameters => parameters.Add(component => component.Session, session));
        Assert.False(session.Secrets.IsAvailable);
        Assert.Contains("Existing secret references remain unchanged", cut.Markup);
        Assert.Contains(CatalogScenarioData.SecretId.ToString(), cut.Markup);
        await cut.InvokeAsync(() => cut.Find("[data-testid=storage-refresh-secrets]").ClickAsync(new()));
        Assert.True(session.Secrets.IsAvailable);
        Assert.Same(draft, session.Draft);
        Assert.Equal("Retained unsaved edit", draft.Name);
        Assert.Equal(CatalogScenarioData.SecretId, draft.CredentialSecretId);
        Assert.Equal(0, store.CatalogWrites);
    }

    [Theory]
    [InlineData(CatalogProvider.FileSystem)]
    [InlineData(CatalogProvider.Ipfs)]
    [InlineData(CatalogProvider.Ftp)]
    public async Task Actual_wizard_captures_preblur_fields_and_raw_numeric_validation_across_steps_and_refresh(CatalogProvider provider) {
        using var context = Context();
        var store = new CatalogScenarioStore();
        using var session = new CatalogSession(store, new());
        await session.LoadAsync();
        session.New(provider);
        var cut = context.Render<CatalogSurface>(parameters => parameters.Add(component => component.Session, session));
        await cut.InvokeAsync(() => cut.Find("[data-testid=storage-settings-name]").Input("Immediate target"));
        await cut.InvokeAsync(() => cut.Find("[data-testid=storage-settings-display-order]").Input("-"));
        Assert.Equal("Immediate target", session.Draft!.Name);
        var editContext = session.Draft.EditContext;
        await Click("Next step");
        await cut.InvokeAsync(() => cut.Find("[data-testid=storage-settings-endpoint]").Input("/scenario/private"));
        if (provider == CatalogProvider.Ipfs) {
            await cut.InvokeAsync(() => cut.Find("[data-testid=storage-settings-ipfs-gateway]").Input("https://gateway.example.test"));
        }
        if (provider == CatalogProvider.Ftp) {
            await cut.InvokeAsync(() => cut.Find("[data-testid=storage-settings-ftp-port]").Input("21e"));
        }
        await Click("Next step");
        await cut.InvokeAsync(() => cut.Find("[data-testid=storage-settings-purpose-evidence]").Change(true));
        await cut.InvokeAsync(() => cut.Find("[data-testid=storage-refresh-secrets]").ClickAsync(new()));
        await cut.InvokeAsync(() => cut.Find("form").SubmitAsync());
        Assert.Empty(session.Receipts);
        Assert.Contains("complete integer", cut.Markup);
        Assert.Equal(2, session.Draft.Step);
        Assert.Same(editContext, session.Draft.EditContext);
        await Click("Previous step");
        if (provider == CatalogProvider.Ftp) {
            Assert.Equal("21e", cut.Find("[data-testid=storage-settings-ftp-port]").GetAttribute("value"));
            await cut.InvokeAsync(() => cut.Find("[data-testid=storage-settings-ftp-port]").Input("2121"));
        }
        await Click("Previous step");
        Assert.Equal("-", cut.Find("[data-testid=storage-settings-display-order]").GetAttribute("value"));
        await cut.InvokeAsync(() => cut.Find("[data-testid=storage-settings-display-order]").Input("17"));
        await cut.InvokeAsync(() => cut.Find("form").SubmitAsync());
        var saved = store.Entries[Assert.Single(session.Receipts).Outcome!.CatalogId!.Value];
        Assert.Equal("Immediate target", saved.Name);
        Assert.Equal(17, saved.DisplayOrder);
        Assert.Contains(CatalogPurpose.Evidence, saved.DefaultPurposes);
        Assert.Equal(0, store.DriverCalls);
        Assert.Single(cut.FindComponents<Steps>());
        Assert.NotEmpty(cut.FindComponents<StorageSummaryCard>());

        Task Click(string text) => cut.InvokeAsync(() => cut.FindAll("button").Single(button => button.TextContent.Trim() == text).ClickAsync(new()));
    }

    [Fact]
    public async Task Missing_secret_is_an_exact_option_and_recovery_callback_carries_persisted_or_catalog_target() {
        using var context = Context();
        var store = new CatalogScenarioStore(CatalogScenario.MissingReferences);
        using var session = new CatalogSession(store, new());
        await session.LoadAsync();
        await session.SelectAsync(CatalogScenarioData.FtpId);
        var targets = new List<Guid?>();
        var cut = context.Render<CatalogSurface>(parameters => parameters.Add(component => component.Session, session)
            .Add(component => component.RecoveryRequested, (Guid? id) => targets.Add(id)));
        Assert.Contains($"Unavailable secret ({CatalogScenarioData.SecretId})", cut.Markup);
        await cut.InvokeAsync(() => cut.Find("[data-testid=storage-settings-recovery]").ClickAsync(new()));
        await cut.InvokeAsync(() => cut.Find("[data-testid=storage-settings-new-filesystem]").ClickAsync(new()));
        await cut.InvokeAsync(() => cut.Find("[data-testid=storage-settings-recovery]").ClickAsync(new()));
        Assert.Equal(new Guid?[] { CatalogScenarioData.FtpId, null }, targets);
        Assert.Equal(0, store.DriverCalls);
        Assert.Equal(0, store.CatalogWrites);
    }

    [Fact]
    public async Task Missing_exact_editor_has_no_placeholder_form_or_mutation_buttons() {
        using var context = Context();
        var store = new CatalogScenarioStore(CatalogScenario.MissingTarget);
        using var session = new CatalogSession(store, new());
        await session.LoadAsync();
        var cut = context.Render<CatalogSurface>(parameters => parameters.Add(component => component.Session, session));
        await cut.InvokeAsync(() => session.SelectAsync(CatalogScenarioData.FileSystemId));
        Assert.Empty(cut.FindAll("[data-testid=storage-settings-save]"));
        Assert.Single(cut.FindAll("[data-testid=storage-editor-retry]"));
        Assert.Contains("exact target was not found", cut.Markup);
    }

    private static BunitContext Context() {
        var context = new BunitContext();
        context.JSInterop.Mode = JSRuntimeMode.Loose;
        context.Services.AddCanDoItAllBaseLib();
        return context;
    }

    [Fact]
    public async Task System_target_is_read_only_but_all_three_steps_remain_inspectable() {
        using var context = Context();
        var store = new CatalogScenarioStore();
        using var session = new CatalogSession(store, new());
        await session.SelectAsync(CatalogScenarioData.SystemId);
        var cut = context.Render<CatalogSurface>(parameters => parameters.Add(component => component.Session, session));
        Assert.True(cut.Find("[data-testid=storage-settings-name]").HasAttribute("disabled"));
        for (var step = 1; step <= 2; step++) {
            await cut.InvokeAsync(() => cut.FindAll("button").Single(button => button.TextContent.Trim() == "Next step").ClickAsync(new()));
            Assert.Equal(step, session.Draft!.Step);
        }
        Assert.True(cut.Find("[data-testid=storage-settings-save]").HasAttribute("disabled"));
        Assert.True(cut.Find("[data-testid=storage-settings-purpose-evidence]").HasAttribute("disabled"));
        Assert.Equal(0, store.CatalogWrites);
    }
}
