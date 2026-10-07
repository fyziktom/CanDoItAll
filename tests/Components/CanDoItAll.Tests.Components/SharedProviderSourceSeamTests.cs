using System.Reflection;
using AngleSharp.Html.Dom;
using Bunit;
using CanDoItAll.AgentFramework.SharedProviders.UI;
using CanDoItAll.Modules.AgentFramework;
using CanDoItAll.Modules.AgentFramework.Pages.Components;
using CanDoItAll.Modules.AgentFramework.ProviderManagement;
using CanDoItAll.Modules.Security;
using CanDoItAll.SharedProviders.Abstractions;
using Microsoft.Extensions.DependencyInjection;

namespace CanDoItAll.Tests.Components.AgentFramework;

[Trait("Category", "HostPlatform")]
public sealed class SharedProviderSourceSeamTests {
    [Theory]
    [InlineData(CatalogRecovery.Readback)]
    [InlineData(CatalogRecovery.Delivery)]
    [InlineData(CatalogRecovery.Unknown)]
    public async Task Native_catalog_receipt_preserves_later_selection_and_rebinds_only_the_original_dialog(CatalogRecovery failure) {
        var remote = new CatalogResponse();
        NativeSourceProxy? proxy = null;
        await using var harness = await HarnessAsync(value => proxy = value, remote);
        var seed = await SharedProviderLocalConcurrencyTests.CreateImportAsync(harness.Context.Services);
        remote.Catalog = seed.Catalog;
        var failDelivery = false;
        var cut = harness.Context.Render<SharedProviderSourcesDialog>(p => p.Add(c => c.ProvidersChanged,
            (SharedProviderChangeDelivery delivery) => failDelivery ? throw new IOException("Fixture delivery failure.")
                : delivery.ReconcileAsync(() => Task.CompletedTask)));
        await cut.WaitForElement("[data-testid='shared-provider-source-discover']").ClickAsync();
        var dialog = Assert.IsType<SharedProviderCatalogSelection>(cut.Instance.Presentation.Catalog);
        var firstOrigin = dialog.Origin;
        var changed = dialog.Publications[1].PublicationId;
        cut.FindAll("[data-testid='shared-provider-catalog-selection']")[1].Change(false);
        proxy!.HoldSync = true;
        var applying = cut.Find("[data-testid='shared-provider-catalog-apply']").ClickAsync();
        await proxy.SyncCommitted.Task.WaitAsync(TimeSpan.FromSeconds(15));
        cut.FindAll("[data-testid='shared-provider-catalog-selection']")[1].Change(true);
        proxy.FailReads = failure == CatalogRecovery.Readback;
        proxy.LoseAcknowledgement = failure == CatalogRecovery.Unknown;
        failDelivery = failure == CatalogRecovery.Delivery;
        proxy.Release.SetResult();
        await applying;
        Assert.Same(dialog, cut.Instance.Presentation.Catalog);
        Assert.True(dialog.IsSelected(changed));
        Assert.Single(proxy.Synchronizations);
        proxy.FailReads = false;
        failDelivery = false;
        if (failure == CatalogRecovery.Readback) {
            await cut.Find("[data-testid='shared-provider-source-refresh']").ClickAsync();
        } else {
            await cut.Find("[data-testid='shared-provider-source-verify']").ClickAsync();
        }
        var native = Assert.Single(await proxy.Owner.ListSourcesAsync());
        Assert.Equal(SharedProviderSelectionState.Retired, native.Imports.Single(item => item.RemotePublicationId == changed).SelectionState);
        Assert.Same(dialog, cut.Instance.Presentation.Catalog);
        Assert.Equal(native.Source.ConcurrencyToken, dialog.Origin.ConcurrencyToken);
        Assert.NotEqual(firstOrigin.SnapshotId, dialog.Origin.SnapshotId);
        Assert.Single(proxy.Synchronizations);
        proxy.HoldSync = false;
        proxy.LoseAcknowledgement = false;
        await cut.Find("[data-testid='shared-provider-catalog-apply']").ClickAsync();
        Assert.Equal(2, proxy.Synchronizations.Count);
        Assert.Null(cut.Instance.Presentation.Catalog);
        Assert.All(Assert.Single(await proxy.Owner.ListSourcesAsync()).Imports, item => Assert.Equal(SharedProviderSelectionState.Selected, item.SelectionState));
    }

    public enum CatalogRecovery { Readback, Delivery, Unknown }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Native_save_keeps_later_text_context_identity_and_normalized_accepted_fields(bool failReadback) {
        NativeSourceProxy? proxy = null;
        await using var harness = await HarnessAsync(value => proxy = value);
        var secret = await CredentialAsync(harness);
        var cut = Render(harness, secret);
        await cut.WaitForElement("[data-testid='shared-provider-source-add']").ClickAsync();
        var draft = cut.Instance.Presentation.Editor!;
        var context = draft.Context;
        cut.Find("[data-testid='shared-provider-source-name']").Input("  Operations source  ");
        cut.Find("[data-testid='shared-provider-source-uri']").Input("https://CENTRAL.example.test");
        proxy!.HoldWrite = true;
        var saving = cut.Find("[data-testid='shared-provider-source-save']").ClickAsync();
        var committed = await proxy.Committed.Task.WaitAsync(TimeSpan.FromSeconds(15));
        cut.Find("[data-testid='shared-provider-source-name']").Input("Later name without blur");
        await cut.Find("[data-testid='shared-provider-source-save']").ClickAsync();
        await cut.Find("form").SubmitAsync();
        Assert.Single(proxy.Writes);
        proxy.FailReads = failReadback;
        proxy.Release.SetResult();
        await saving;
        Assert.Same(draft, cut.Instance.Presentation.Editor);
        Assert.Same(context, draft.Context);
        Assert.Equal(committed.Id, draft.SourceId);
        Assert.Equal(committed.ConcurrencyToken, draft.ExpectedToken);
        Assert.Equal("Later name without blur", draft.Name);
        if (failReadback) {
            Assert.NotNull(cut.Find("[data-testid='shared-provider-sources-error']"));
            await cut.Find("[data-testid='shared-provider-source-save']").ClickAsync();
            Assert.Single(proxy.Writes);
            proxy.FailReads = false;
            await cut.Find("[data-testid='shared-provider-source-refresh']").ClickAsync();
        }
        Assert.Equal("https://central.example.test/", draft.BaseUri);
        var source = Assert.Single(await proxy.Owner.ListSourcesAsync()).Source;
        Assert.Equal("Operations source", source.Name);
        Assert.Equal(secret.Id, source.ApiTokenSecretId);
        proxy.HoldWrite = false;
        await cut.Find("[data-testid='shared-provider-source-save']").ClickAsync();
        Assert.Equal(2, proxy.Writes.Count);
        Assert.Equal(committed.ConcurrencyToken, proxy.Writes[1].ExpectedConcurrencyToken);
        Assert.Equal("Later name without blur", Assert.Single(await proxy.Owner.ListSourcesAsync()).Source.Name);
        Assert.Null(cut.Instance.Presentation.Editor);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Late_native_save_or_unknown_verification_cannot_close_or_change_a_replacement_editor(bool loseAcknowledgement) {
        NativeSourceProxy? proxy = null;
        await using var harness = await HarnessAsync(value => proxy = value);
        var secret = await CredentialAsync(harness);
        var native = harness.Context.Services.GetRequiredService<SharedProviderSourceService>();
        var first = await native.CreateAsync(new("Source A", new Uri("https://a.example.test/"), secret.Id, true, false));
        var second = await native.CreateAsync(new("Source B", new Uri("https://b.example.test/"), secret.Id, true, false));
        var cut = Render(harness, secret);
        await cut.WaitForElement($"[data-source-id='{first.Id}'] [data-testid='shared-provider-source-edit']").ClickAsync();
        cut.Find("[data-testid='shared-provider-source-name']").Input("Accepted A");
        proxy!.HoldWrite = true;
        proxy.LoseAcknowledgement = loseAcknowledgement;
        var saving = cut.Find("[data-testid='shared-provider-source-save']").ClickAsync();
        await proxy.Committed.Task.WaitAsync(TimeSpan.FromSeconds(15));
        await cut.Find("[data-testid='shared-provider-source-cancel']").ClickAsync();
        await cut.Find($"[data-source-id='{second.Id}'] [data-testid='shared-provider-source-edit']").ClickAsync();
        var replacement = cut.Instance.Presentation.Editor!;
        var context = replacement.Context;
        cut.Find("[data-testid='shared-provider-source-name']").Input("Unsaved B");
        proxy.Release.SetResult();
        await saving;
        if (loseAcknowledgement) {
            Assert.NotNull(cut.Find("[data-testid='shared-provider-source-unresolved']"));
            await cut.Find("[data-testid='shared-provider-source-verify']").ClickAsync();
        }
        Assert.Single(proxy.Writes);
        Assert.Same(replacement, cut.Instance.Presentation.Editor);
        Assert.Same(context, replacement.Context);
        Assert.Equal(second.Id, replacement.SourceId);
        Assert.Equal(second.ConcurrencyToken, replacement.ExpectedToken);
        Assert.Equal("Unsaved B", replacement.Name);
        Assert.Empty(cut.FindAll("[data-testid='shared-provider-source-editor-error']"));
        Assert.Equal("Accepted A", (await native.GetAsync(first.Id)).Name);
        Assert.Equal("Source B", (await native.GetAsync(second.Id)).Name);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Missing_credential_metadata_preserves_stored_choice_and_refuses_dispatch(bool metadataFailure) {
        NativeSourceProxy? proxy = null;
        await using var harness = await HarnessAsync(value => proxy = value);
        var secret = await CredentialAsync(harness);
        var source = await harness.Context.Services.GetRequiredService<SharedProviderSourceService>()
            .CreateAsync(new("Existing source", new Uri("https://central.example.test/"), secret.Id, true, false));
        var cut = harness.Context.Render<SharedProviderSourcesDialog>(p => p
            .Add(c => c.Secrets, Array.Empty<SecretListItem>())
            .Add(c => c.SecretMetadataError, metadataFailure ? "Fixture metadata failure" : null));
        await cut.WaitForElement("[data-testid='shared-provider-source-edit']").ClickAsync();
        var draft = cut.Instance.Presentation.Editor!;
        Assert.Equal(source.Id, draft.SourceId);
        Assert.Equal(secret.Id, draft.CredentialReference);
        Assert.Contains("Unavailable credential reference", cut.Markup, StringComparison.Ordinal);
        cut.Find("[data-testid='shared-provider-source-name']").Input("Retained raw name");
        await cut.InvokeAsync(() => cut.Instance.SaveAsync(draft.Capture()));
        Assert.Empty(proxy!.Writes);
        cut.Render(p => p.Add(c => c.Secrets, new[] { secret }).Add(c => c.SecretMetadataError, null));
        Assert.Same(draft, cut.Instance.Presentation.Editor);
        Assert.Equal("Retained raw name", draft.Name);
        Assert.Equal(secret.Id.ToString(), ((IHtmlSelectElement)cut.Find("[data-testid='shared-provider-source-secret']")).Value);
        Assert.Equal("Existing source", Assert.Single(await proxy.Owner.ListSourcesAsync()).Source.Name);
    }

    [Fact]
    public async Task Delete_confirmation_keeps_displayed_revision_and_old_callback_cannot_apply_after_refresh() {
        NativeSourceProxy? proxy = null;
        await using var harness = await HarnessAsync(value => proxy = value);
        var secret = await CredentialAsync(harness);
        var native = harness.Context.Services.GetRequiredService<SharedProviderSourceService>();
        var source = await native.CreateAsync(new("Unused source", new Uri("https://central.example.test/"), secret.Id, true, false));
        var cut = Render(harness, secret);
        await cut.WaitForElement("[data-testid='shared-provider-source-delete']").ClickAsync();
        var first = cut.Instance.Presentation.Confirmation!;
        await cut.Find("[data-testid='shared-provider-source-refresh']").ClickAsync();
        await cut.InvokeAsync(() => cut.Instance.ConfirmDeleteAsync(first));
        Assert.Equal(0, proxy!.Deletes);
        Assert.Equal(source.Id, Assert.Single(await proxy.Owner.ListSourcesAsync()).Source.Id);
        await cut.Find("[data-testid='shared-provider-source-delete']").ClickAsync();
        var second = cut.Instance.Presentation.Confirmation!;
        await cut.InvokeAsync(() => cut.Instance.CloseConfirmation(first));
        Assert.Same(second, cut.Instance.Presentation.Confirmation);
        await cut.Find("[data-testid='shared-provider-source-confirm-apply']").ClickAsync();
        Assert.Equal(1, proxy.Deletes);
        Assert.Empty(await proxy.Owner.ListSourcesAsync());
    }

    private static Task<ComponentTestHarness> HarnessAsync(Action<NativeSourceProxy> capture, ISharedProviderCatalogClient? catalog = null) =>
        ComponentTestHarness.CreateAsync(services => {
            if (catalog is not null) {
                services.AddSingleton(catalog);
            }
            services.AddScoped(provider => {
                var service = DispatchProxy.Create<ISharedProviderManagementService, NativeSourceProxy>();
                var proxy = (NativeSourceProxy)(object)service;
                proxy.Owner = ActivatorUtilities.CreateInstance<SharedProviderManagementService>(provider);
                capture(proxy);
                return service;
            });
        });

    private sealed class CatalogResponse : ISharedProviderCatalogClient {
        public SharedProviderCatalogDocument Catalog { get; set; } = null!;
        public ValueTask<SharedProviderCatalogFetchResult> FetchAsync(SharedProviderCatalogFetchRequest request, CancellationToken cancellationToken = default) =>
            ValueTask.FromResult<SharedProviderCatalogFetchResult>(new SharedProviderCatalogFetchResult.Succeeded(
                Catalog, SharedProviderCatalogEntityTag.FromRevision(Catalog.CatalogRevision)));
    }

    private static async Task<SecretListItem> CredentialAsync(ComponentTestHarness harness) {
        var saved = await harness.Context.Services.GetRequiredService<SecretService>().SaveAsync(new() {
            Name = "Source reference", Kind = SecretKind.Token, SecretValue = "test-only-unused-source-credential"
        });
        Assert.True(saved.IsSuccess);
        return new(saved.Value, "Source reference", SecretKind.Token, "workspace", DateTimeOffset.UtcNow);
    }

    private static IRenderedComponent<SharedProviderSourcesDialog> Render(ComponentTestHarness harness, SecretListItem secret) =>
        harness.Context.Render<SharedProviderSourcesDialog>(p => p.Add(c => c.Secrets, new[] { secret })
            .Add(c => c.ProvidersChanged, (SharedProviderChangeDelivery delivery) => delivery.ReconcileAsync(() => Task.CompletedTask)));

    public class NativeSourceProxy : DispatchProxy {
        public ISharedProviderManagementService Owner { get; set; } = null!;
        public List<SharedProviderSourceEditorRequest> Writes { get; } = [];
        public int Deletes { get; private set; }
        public bool HoldWrite { get; set; }
        public bool HoldSync { get; set; }
        public bool FailReads { get; set; }
        public bool LoseAcknowledgement { get; set; }
        public TaskCompletionSource<SharedProviderSourceWriteResult> Committed { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public List<IReadOnlySet<SharedProviderPublicationId>> Synchronizations { get; } = [];
        public TaskCompletionSource SyncCommitted { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        protected override object? Invoke(MethodInfo? method, object?[]? args) {
            if (method!.Name == nameof(ISharedProviderManagementService.SaveSourceAsync)) {
                var request = (SharedProviderSourceEditorRequest)args![0]!;
                Writes.Add(request);
                return SaveAsync(request, (CancellationToken)args[1]!);
            }
            if (method.Name == nameof(ISharedProviderManagementService.SynchronizeSourceAsync)) {
                var selected = (IReadOnlySet<SharedProviderPublicationId>)args![1]!;
                Synchronizations.Add(selected);
                return SynchronizeAsync((Guid)args[0]!, selected, (CancellationToken)args[2]!);
            }
            if (method.Name == nameof(ISharedProviderManagementService.ListSourcesAsync) && FailReads) {
                return Task.FromException<IReadOnlyList<SharedProviderSourceManagementSnapshot>>(new IOException("Fixture read-back unavailable."));
            }
            if (method.Name == nameof(ISharedProviderManagementService.DeleteSourceAsync)) {
                Deletes++;
            }
            return method.Invoke(Owner, args);
        }
        private async Task<SharedProviderSourceWriteResult> SaveAsync(SharedProviderSourceEditorRequest request, CancellationToken token) {
            var result = await Owner.SaveSourceAsync(request, token);
            if (HoldWrite) {
                Committed.TrySetResult(result);
                await Release.Task;
            }
            return LoseAcknowledgement ? throw new IOException("Fixture acknowledgement lost after commit.") : result;
        }
        private async Task<SharedProviderSourceOperationResult> SynchronizeAsync(Guid sourceId, IReadOnlySet<SharedProviderPublicationId> selected, CancellationToken token) {
            var result = await Owner.SynchronizeSourceAsync(sourceId, selected, token);
            if (HoldSync) {
                SyncCommitted.TrySetResult();
                await Release.Task;
            }
            return LoseAcknowledgement ? throw new IOException("Fixture sync acknowledgement lost after commit.") : result;
        }
    }
}
