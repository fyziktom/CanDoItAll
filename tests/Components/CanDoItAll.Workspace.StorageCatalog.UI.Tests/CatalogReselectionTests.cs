using System.Collections.Immutable;
using Bunit;
using CanDoItAll.Components.BaseLib;
using CanDoItAll.Modules.Workspace.StorageCatalog.Contracts;
using CanDoItAll.Workspace.StorageCatalog.UI;
using CanDoItAll.Workspace.StorageCatalog.UiSandbox;
using Microsoft.Extensions.DependencyInjection;

namespace CanDoItAll.Tests.Components.WorkspaceStorageCatalogUi;

public sealed class CatalogReselectionTests {
    [Fact]
    public async Task Highlighted_row_preserves_acquired_draft_context_raw_fields_step_and_validation_without_reading() {
        using var context = Context();
        var owner = new RecordingOwner();
        using var session = new CatalogSession(owner, new());
        await session.LoadAsync();
        var cut = context.Render<CatalogSurface>(parameters => parameters.Add(component => component.Session, session));
        await SelectAsync(cut);
        var draft = session.Draft!;
        await cut.InvokeAsync(() => cut.Find("[data-testid=storage-settings-name]").Input("Unsaved FTP name"));
        await cut.InvokeAsync(() => cut.Find("[data-testid=storage-settings-display-order]").Input("-"));
        await NextAsync(cut);
        await cut.InvokeAsync(() => cut.Find("[data-testid=storage-settings-ftp-port]").Input("21e"));
        await cut.InvokeAsync(() => cut.Find("form").SubmitAsync());
        var validation = draft.EditContext.GetValidationMessages().ToArray();
        Assert.NotEmpty(validation);
        Assert.Equal(1, owner.ExactReads);
        await SelectAsync(cut);
        Assert.True(ReferenceEquals(draft, session.Draft), $"Reselection replaced draft; exact reads: {owner.ExactReads}; step: {session.Draft?.Step}; port: {session.Draft?.PortText}.");
        Assert.Same(draft.EditContext, cut.FindComponent<CatalogEditor>().Instance.Draft.EditContext);
        Assert.Equal("Unsaved FTP name", draft.Name);
        Assert.Equal("-", draft.DisplayOrderText);
        Assert.Equal("21e", cut.Find("[data-testid=storage-settings-ftp-port]").GetAttribute("value"));
        Assert.Equal(1, draft.Step);
        Assert.Equal(validation, draft.EditContext.GetValidationMessages());
        Assert.Equal(1, owner.ExactReads);
        Assert.Empty(session.Receipts);
        Assert.Equal((0, 0, 0, 0), owner.Effects);
    }

    [Theory]
    [InlineData(CatalogEffect.Save, CatalogScenarioStage.Persistence)]
    [InlineData(CatalogEffect.Test, CatalogScenarioStage.Driver)]
    [InlineData(CatalogEffect.Save, CatalogScenarioStage.ReadBack)]
    public async Task Highlighted_row_during_an_accepted_effect_or_readback_preserves_original_editor_and_receipt(CatalogEffect effect, CatalogScenarioStage stage) {
        using var context = Context();
        var owner = new RecordingOwner();
        using var session = new CatalogSession(owner, new());
        await session.LoadAsync();
        var cut = context.Render<CatalogSurface>(parameters => parameters.Add(component => component.Session, session));
        await SelectAsync(cut);
        var draft = session.Draft!;
        var gate = owner.Store.Gates[stage];
        gate.Hold();
        var operation = cut.InvokeAsync(() => session.MutateAsync(effect));
        cut.WaitForState(() => gate.IsEntered);
        await cut.InvokeAsync(() => cut.Find("[data-testid=storage-settings-name]").Input("Typed after dispatch"));
        await NextAsync(cut);
        await cut.InvokeAsync(() => cut.Find("[data-testid=storage-settings-ftp-port]").Input("2e"));
        var reads = owner.ExactReads;
        var effects = owner.Effects;
        var receipt = Assert.Single(session.Receipts);
        var selection = SelectAsync(cut);
        try {
            Assert.Same(draft, session.Draft);
            Assert.Same(receipt, Assert.Single(session.Receipts));
            Assert.Equal(reads, owner.ExactReads);
            Assert.Equal(effects, owner.Effects);
            if (stage != CatalogScenarioStage.ReadBack) {
                Assert.False(session.CanMutate);
            }
        } finally {
            gate.Release();
            await Task.WhenAll(operation, selection);
        }
        Assert.Same(draft, session.Draft);
        Assert.Same(draft.EditContext, cut.FindComponent<CatalogEditor>().Instance.Draft.EditContext);
        Assert.Equal("Typed after dispatch", draft.Name);
        Assert.Equal("2e", draft.PortText);
        Assert.Equal(1, draft.Step);
        Assert.Equal(CatalogScenarioData.FtpId, draft.Id);
        Assert.Equal(CatalogWrite.Committed, receipt.Outcome!.Write);
        Assert.Single(session.Receipts);
        Assert.Equal(1, owner.Store.CatalogWrites);
        Assert.Equal(effect == CatalogEffect.Test ? 1 : 0, owner.Store.DriverCalls);
    }

    [Fact]
    public async Task Failed_missing_and_deleted_same_targets_still_require_an_exact_acquisition() {
        var owner = new RecordingOwner();
        owner.Store.Gates[CatalogScenarioStage.EditorRead].FailNext = true;
        using var session = new CatalogSession(owner, new());
        await session.SelectAsync(CatalogScenarioData.FtpId);
        Assert.Null(session.Draft);
        Assert.NotEmpty(session.EditorRead.Error);
        await session.SelectAsync(CatalogScenarioData.FtpId);
        Assert.Equal(CatalogScenarioData.FtpId, session.Draft!.Id);
        Assert.Equal(2, owner.ExactReads);
        await session.MutateAsync(CatalogEffect.Delete);
        Assert.True(session.Draft.Deleted);
        var reads = owner.ExactReads;
        await session.SelectAsync(CatalogScenarioData.FtpId);
        Assert.Null(session.Draft);
        Assert.False(session.CanMutate);
        Assert.Equal(reads + 1, owner.ExactReads);
        await session.SelectAsync(CatalogScenarioData.FtpId);
        Assert.Null(session.Draft);
        Assert.Equal(reads + 2, owner.ExactReads);
        Assert.False(owner.Store.Entries.ContainsKey(CatalogScenarioData.FtpId));
    }

    [Fact]
    public async Task An_unacquired_same_id_is_not_a_draft_and_can_start_a_successor_read() {
        var owner = new RecordingOwner();
        using var session = new CatalogSession(owner, new());
        owner.Store.Gates[CatalogScenarioStage.EditorRead].Hold();
        var first = session.SelectAsync(CatalogScenarioData.FtpId);
        var second = session.SelectAsync(CatalogScenarioData.FtpId);
        Assert.Null(session.Draft);
        Assert.Equal(2, owner.ExactReads);
        owner.Store.Gates[CatalogScenarioStage.EditorRead].Release();
        await Task.WhenAll(first, second);
        Assert.Equal(CatalogScenarioData.FtpId, session.Draft!.Id);
        var acquired = session.Draft;
        owner.Store.IsCurrent = false;
        await session.SelectAsync(CatalogScenarioData.FtpId);
        Assert.Same(acquired, session.Draft);
        Assert.Equal(2, owner.ExactReads);
        Assert.False(session.CanMutate);
    }

    private static Task SelectAsync(IRenderedComponent<CatalogSurface> cut) => cut.InvokeAsync(() =>
        cut.Find($"button[data-testid=storage-catalog-row-{CatalogScenarioData.FtpId:N}]").ClickAsync(new()));

    private static Task NextAsync(IRenderedComponent<CatalogSurface> cut) => cut.InvokeAsync(() =>
        cut.FindAll("button").Single(button => button.TextContent.Trim() == "Next step").ClickAsync(new()));

    private static BunitContext Context() {
        var context = new BunitContext();
        context.JSInterop.Mode = JSRuntimeMode.Loose;
        context.Services.AddCanDoItAllBaseLib();
        return context;
    }

    private sealed class RecordingOwner : IStorageCatalogOwner {
        public CatalogScenarioStore Store { get; } = new();
        public int ExactReads { get; private set; }
        public (int, int, int, int) Effects => (Store.CatalogWrites, Store.RoutingWrites, Store.DriverCalls, Store.ActivityCalls);
        public CatalogContext Context => Store.Context;
        public bool IsCurrent => Store.IsCurrent;
        public CatalogChoices Choices => Store.Choices;
        public Task<ImmutableArray<CatalogRow>> ReadCatalogAsync(CancellationToken cancellationToken) => Store.ReadCatalogAsync(cancellationToken);
        public Task<ImmutableArray<CatalogSecret>> ReadSecretsAsync(CancellationToken cancellationToken) => Store.ReadSecretsAsync(cancellationToken);
        public Task<ImmutableArray<CatalogRoute>> ReadRoutesAsync(CancellationToken cancellationToken) => Store.ReadRoutesAsync(cancellationToken);
        public Task<CatalogEdit?> ReadEditorAsync(Guid id, CancellationToken cancellationToken) {
            ExactReads++;
            return Store.ReadEditorAsync(id, cancellationToken);
        }
        public Task<CatalogOutcome> ExecuteAsync(CatalogCommand command, CancellationToken cancellationToken) => Store.ExecuteAsync(command, cancellationToken);
    }
}
