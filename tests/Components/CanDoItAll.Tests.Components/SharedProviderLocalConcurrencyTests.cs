using System.Reflection;
using AngleSharp.Html.Dom;
using Bunit;
using CanDoItAll.AgentFramework.Providers.UI;
using CanDoItAll.Modules.AgentFramework;
using CanDoItAll.Modules.AgentFramework.Pages.Components;
using CanDoItAll.Modules.AgentFramework.ProviderManagement;
using CanDoItAll.Modules.Security;
using CanDoItAll.SharedProviders.Abstractions;
using Microsoft.Extensions.DependencyInjection;

namespace CanDoItAll.Tests.Components.AgentFramework;

public sealed class SharedProviderLocalConcurrencyTests {
    [Fact]
    public async Task Clean_sharing_refresh_and_save_cannot_revert_another_native_owners_local_settings() {
        NativeManagementProxy? captured = null;
        await using var harness = await ComponentTestHarness.CreateAsync(services => services.AddScoped(provider => {
            var service = DispatchProxy.Create<ISharedProviderManagementService, NativeManagementProxy>();
            captured = (NativeManagementProxy)(object)service;
            captured.Owner = ActivatorUtilities.CreateInstance<SharedProviderManagementService>(provider);
            return service;
        }));
        var seed = await CreateImportAsync(harness.Context.Services);
        var cut = harness.Context.Render<AgentProviderProfilesPanel>();
        cut.WaitForElement("[data-testid='providers-name-input']");
        await cut.FindAll("[data-testid='providers-tree-provider']").First(node => node.TextContent.Contains("Team model", StringComparison.Ordinal)).ClickAsync();
        Assert.Equal(seed.ProviderId, cut.Instance.Editor.Model.Id);
        await cut.FindAll("[role='tab']")[ProviderEditorSections.IndexOf(ProviderEditorSection.Sharing)].ClickAsync();
        cut.WaitForElement("[data-testid='shared-provider-import-alias']");
        var child = cut.FindComponent<SharedProviderImportedProfileContent>().Instance;
        Assert.Equal("Team model", ((IHtmlInputElement)cut.Find("[data-testid='shared-provider-import-alias']")).Value);

        await using var otherScope = harness.Context.Services.CreateAsyncScope();
        var otherOwner = ActivatorUtilities.CreateInstance<SharedProviderManagementService>(otherScope.ServiceProvider);
        var before = (await otherOwner.GetProfileSharingAsync(seed.ProviderId)).Import!;
        var changed = (await otherOwner.UpdateImportedProfileAsync(new(before.ImportId, before.ProviderProfileId,
            "Operations model", false, before.ImportConcurrencyToken, before.ProviderConcurrencyToken))).Import!;
        await Assert.ThrowsAsync<SharedProviderConcurrencyException>(() => otherOwner.UpdateImportedProfileAsync(
            new(before.ImportId, before.ProviderProfileId, "Old-token attempt", true, before.ImportConcurrencyToken, before.ProviderConcurrencyToken)));
        await cut.Find("[data-testid='providers-refresh']").ClickAsync();
        cut.WaitForAssertion(() => Assert.Equal(changed.ImportConcurrencyToken, child.Draft.Latest.ImportToken));
        Assert.Same(child, cut.FindComponent<SharedProviderImportedProfileContent>().Instance);
        cut.WaitForAssertion(() => Assert.False(child.IsBusy));
        await cut.Find("[data-testid='shared-provider-import-save']").ClickAsync();

        var saved = (await otherOwner.GetProfileSharingAsync(seed.ProviderId)).Import!;
        Assert.Equal("Operations model", saved.LocalAlias);
        Assert.False(saved.IsEnabled);
        var submission = Assert.Single(captured!.Updates);
        Assert.Equal(changed.ImportConcurrencyToken, submission.ExpectedImportConcurrencyToken);
        Assert.Equal(changed.ProviderConcurrencyToken, submission.ExpectedProviderConcurrencyToken);
        Assert.Equal("Operations model", submission.LocalAlias);
        Assert.False(submission.IsEnabled);
        Assert.Equal("Unrelated model", (await otherOwner.GetProfileSharingAsync(seed.OtherProviderId)).Import!.LocalAlias);
    }

    [Theory]
    [InlineData(LocalEdit.Alias, LocalEdit.Alias, true)]
    [InlineData(LocalEdit.Alias, LocalEdit.Enabled, true)]
    [InlineData(LocalEdit.Enabled, LocalEdit.Alias, true)]
    [InlineData(LocalEdit.Alias, LocalEdit.Alias, false)]
    [InlineData(LocalEdit.Alias, LocalEdit.Enabled, false)]
    [InlineData(LocalEdit.Enabled, LocalEdit.Alias, false)]
    public async Task Dirty_local_conflicts_require_review_and_preserve_the_other_owners_untouched_field(
        LocalEdit editA, LocalEdit editB, bool keepEdits) {
        NativeManagementProxy? proxy = null;
        await using var harness = await CreateHarnessAsync(value => proxy = value);
        var seed = await CreateImportAsync(harness.Context.Services);
        var cut = await OpenSharingAsync(harness, seed.ProviderId);
        var child = cut.FindComponent<SharedProviderImportedProfileContent>().Instance;
        var context = child.Draft.Context;
        if (editA == LocalEdit.Alias) {
            cut.Find("[data-testid='shared-provider-import-alias']").Input("Draft A");
        } else {
            cut.Find("[data-testid='shared-provider-import-enabled']").Change(false);
        }
        Assert.True(child.Draft.IsDirty);
        await using var otherScope = harness.Context.Services.CreateAsyncScope();
        var otherOwner = ActivatorUtilities.CreateInstance<SharedProviderManagementService>(otherScope.ServiceProvider);
        var before = (await otherOwner.GetProfileSharingAsync(seed.ProviderId)).Import!;
        var changed = (await otherOwner.UpdateImportedProfileAsync(new(before.ImportId, before.ProviderProfileId,
            editB == LocalEdit.Alias ? "Saved B" : before.LocalAlias, editB != LocalEdit.Enabled,
            before.ImportConcurrencyToken, before.ProviderConcurrencyToken))).Import!;

        await cut.Find("[data-testid='providers-refresh']").ClickAsync();
        cut.WaitForAssertion(() => Assert.Equal(changed.ImportConcurrencyToken, child.Draft.Latest.ImportToken));
        Assert.Same(child, cut.FindComponent<SharedProviderImportedProfileContent>().Instance);
        Assert.Same(context, child.Draft.Context);
        Assert.NotNull(cut.Find("[data-testid='shared-provider-import-conflict']"));
        Assert.Equal(before.ImportConcurrencyToken, child.Draft.Baseline.ImportToken);
        await cut.Find("[data-testid='shared-provider-import-save']").ClickAsync();
        await cut.InvokeAsync(() => child.Save.InvokeAsync(child.Draft.Capture()));
        Assert.Empty(proxy!.Updates);
        var unchanged = (await otherOwner.GetProfileSharingAsync(seed.ProviderId)).Import!;
        Assert.Equal(changed.LocalAlias, unchanged.LocalAlias);
        Assert.Equal(changed.IsEnabled, unchanged.IsEnabled);
        Assert.Equal(changed.ImportConcurrencyToken, unchanged.ImportConcurrencyToken);

        await cut.Find(keepEdits ? "[data-testid='shared-provider-import-keep-edits']" :
            "[data-testid='shared-provider-import-use-saved']").ClickAsync();
        await cut.Find("[data-testid='shared-provider-import-save']").ClickAsync();
        var saved = (await otherOwner.GetProfileSharingAsync(seed.ProviderId)).Import!;
        Assert.Equal(keepEdits && editA == LocalEdit.Alias ? "Draft A" : changed.LocalAlias, saved.LocalAlias);
        Assert.Equal(keepEdits && editA == LocalEdit.Enabled ? false : changed.IsEnabled, saved.IsEnabled);
        var submitted = Assert.Single(proxy.Updates);
        Assert.Equal(changed.ImportConcurrencyToken, submitted.ExpectedImportConcurrencyToken);
        Assert.Equal(changed.ProviderConcurrencyToken, submitted.ExpectedProviderConcurrencyToken);
        Assert.Equal("Unrelated model", (await otherOwner.GetProfileSharingAsync(seed.OtherProviderId)).Import!.LocalAlias);
    }

    [Fact]
    public async Task Native_metadata_only_refresh_preserves_raw_dirty_input_and_advances_safe_tokens() {
        NativeManagementProxy? proxy = null;
        await using var harness = await CreateHarnessAsync(value => proxy = value);
        var seed = await CreateImportAsync(harness.Context.Services);
        var cut = await OpenSharingAsync(harness, seed.ProviderId);
        var child = cut.FindComponent<SharedProviderImportedProfileContent>().Instance;
        var context = child.Draft.Context;
        var before = child.Draft.Baseline;
        cut.Find("[data-testid='shared-provider-import-alias']").Input("  Raw local alias  ");
        var publications = seed.Catalog.Providers.Select(item => {
            var updated = item with { DisplayName = item.DisplayName + " revision 2" };
            return updated with { Revision = SharedProviderCanonicalRevision.ComputePublication(updated) };
        }).ToArray();
        var catalog = seed.Catalog with { Providers = publications };
        catalog = catalog with { CatalogRevision = SharedProviderCanonicalRevision.ComputeCatalog(catalog) };
        var source = await harness.Context.Services.GetRequiredService<SharedProviderSourceService>().GetAsync(seed.SourceId);
        await harness.Context.Services.GetRequiredService<SharedProviderReconciliationCoordinator>().ReconcileAsync(new(seed.SourceId,
            catalog, SharedProviderCatalogEntityTag.FromRevision(catalog.CatalogRevision), publications.Select(item => item.PublicationId).ToHashSet(),
            expectedSourceConcurrencyToken: source.ConcurrencyToken));

        await cut.Find("[data-testid='providers-refresh']").ClickAsync();
        cut.WaitForAssertion(() => Assert.NotEqual(before.ImportToken, child.Draft.Latest.ImportToken));
        Assert.Same(context, child.Draft.Context);
        Assert.Equal("  Raw local alias  ", child.Draft.LocalAlias);
        Assert.False(child.Draft.HasConflict);
        Assert.NotEqual(before.ImportToken, child.Draft.Baseline.ImportToken);
        var adopted = child.Draft.Baseline;
        await cut.Find("[data-testid='shared-provider-import-save']").ClickAsync();
        var saved = (await proxy!.Owner.GetProfileSharingAsync(seed.ProviderId)).Import!;
        Assert.Equal("Raw local alias", saved.LocalAlias);
        Assert.Equal("Team model revision 2", saved.RemoteDisplayName);
        Assert.Equal(before.ImportId, saved.ImportId);
        var request = Assert.Single(proxy.Updates);
        Assert.Equal(adopted.ImportToken, request.ExpectedImportConcurrencyToken);
        Assert.Equal(adopted.ProviderToken, request.ExpectedProviderConcurrencyToken);
    }

    [Fact]
    public async Task Native_commit_normalization_preserves_later_typing_and_duplicate_submit_is_rejected() {
        NativeManagementProxy? proxy = null;
        await using var harness = await CreateHarnessAsync(value => proxy = value);
        var seed = await CreateImportAsync(harness.Context.Services);
        var cut = await OpenSharingAsync(harness, seed.ProviderId);
        var child = cut.FindComponent<SharedProviderImportedProfileContent>().Instance;
        var context = child.Draft.Context;
        proxy!.HoldUpdate = true;
        cut.Find("[data-testid='shared-provider-import-alias']").Input("  Accepted alias  ");
        var saving = cut.Find("[data-testid='shared-provider-import-save']").ClickAsync();
        var committed = await proxy.Committed.Task.WaitAsync(TimeSpan.FromSeconds(15));
        cut.Find("[data-testid='shared-provider-import-alias']").Input("Later typing without blur");
        await cut.Find("[data-testid='shared-provider-import-save']").ClickAsync();
        await cut.FindComponent<SharedProviderImportedProfileContent>().Find("form").SubmitAsync();
        Assert.Single(proxy.Updates);
        proxy.Release.SetResult();
        await saving;

        Assert.Same(context, child.Draft.Context);
        Assert.Equal("Later typing without blur", child.Draft.LocalAlias);
        Assert.Equal("Accepted alias", child.Draft.Baseline.Settings.LocalAlias);
        Assert.Equal(committed.Import!.ImportConcurrencyToken, child.Draft.Baseline.ImportToken);
        Assert.Equal("Accepted alias", (await proxy.Owner.GetProfileSharingAsync(seed.ProviderId)).Import!.LocalAlias);
        proxy.HoldUpdate = false;
        var recovery = harness.Context.Services.GetRequiredService<SharedProviderRecovery>();
        var pending = Assert.IsType<SharedProviderTargetAttempt>(recovery.FindTarget(seed.ProviderId));
        Assert.True(recovery.PendingDelivery(pending.AttemptId)!.IsAcknowledged);
        await cut.Find("[data-testid='shared-provider-retry']").ClickAsync();
        Assert.Single(proxy.Updates);
        Assert.Equal("Later typing without blur", child.Draft.LocalAlias);
        cut.WaitForAssertion(() => Assert.False(child.IsBusy));
        await cut.Find("[data-testid='shared-provider-import-save']").ClickAsync();
        Assert.Equal(2, proxy.Updates.Count);
        Assert.Equal("Later typing without blur", (await proxy.Owner.GetProfileSharingAsync(seed.ProviderId)).Import!.LocalAlias);
    }

    [Fact]
    public async Task Known_native_conflict_requires_reload_and_read_failure_cannot_authorize_old_values() {
        NativeManagementProxy? proxy = null;
        await using var harness = await CreateHarnessAsync(value => proxy = value);
        var seed = await CreateImportAsync(harness.Context.Services);
        var cut = await OpenSharingAsync(harness, seed.ProviderId);
        cut.Find("[data-testid='shared-provider-import-alias']").Input("Dirty A");
        var before = (await proxy!.Owner.GetProfileSharingAsync(seed.ProviderId)).Import!;
        await proxy.Owner.UpdateImportedProfileAsync(new(before.ImportId, before.ProviderProfileId, "Saved B", false,
            before.ImportConcurrencyToken, before.ProviderConcurrencyToken));
        await cut.Find("[data-testid='shared-provider-import-save']").ClickAsync();
        Assert.Single(proxy.Updates);
        proxy.FailRead = true;
        await cut.Find("[data-testid='shared-provider-retry']").ClickAsync();
        await cut.Find("[data-testid='shared-provider-import-save']").ClickAsync();
        Assert.Single(proxy.Updates);
        proxy.FailRead = false;
        await cut.Find("[data-testid='shared-provider-retry']").ClickAsync();
        Assert.NotNull(cut.Find("[data-testid='shared-provider-import-conflict']"));
        Assert.Equal("Dirty A", cut.FindComponent<SharedProviderImportedProfileContent>().Instance.Draft.LocalAlias);
        Assert.Equal("Saved B", (await proxy.Owner.GetProfileSharingAsync(seed.ProviderId)).Import!.LocalAlias);
    }

    private static Task<ComponentTestHarness> CreateHarnessAsync(Action<NativeManagementProxy> capture) =>
        ComponentTestHarness.CreateAsync(services => services.AddScoped(provider => {
            var service = DispatchProxy.Create<ISharedProviderManagementService, NativeManagementProxy>();
            var proxy = (NativeManagementProxy)(object)service;
            proxy.Owner = ActivatorUtilities.CreateInstance<SharedProviderManagementService>(provider);
            capture(proxy);
            return service;
        }));

    private static async Task<IRenderedComponent<AgentProviderProfilesPanel>> OpenSharingAsync(ComponentTestHarness harness, Guid providerId) {
        var cut = harness.Context.Render<AgentProviderProfilesPanel>();
        cut.WaitForElement("[data-testid='providers-name-input']");
        await cut.FindAll("[data-testid='providers-tree-provider']").First(node => node.TextContent.Contains("Team model", StringComparison.Ordinal)).ClickAsync();
        Assert.Equal(providerId, cut.Instance.Editor.Model.Id);
        await cut.FindAll("[role='tab']")[ProviderEditorSections.IndexOf(ProviderEditorSection.Sharing)].ClickAsync();
        cut.WaitForElement("[data-testid='shared-provider-import-alias']");
        return cut;
    }

    public enum LocalEdit { Alias, Enabled }

    internal static async Task<(Guid ProviderId, Guid OtherProviderId, Guid SourceId, SharedProviderCatalogDocument Catalog)> CreateImportAsync(IServiceProvider services) {
        var secret = await services.GetRequiredService<SecretService>().SaveAsync(new() {
            Name = "Local concurrency source reference", Kind = SecretKind.Token, SecretValue = "test-only-unused-source-credential"
        });
        Assert.True(secret.IsSuccess);
        var source = await services.GetRequiredService<SharedProviderSourceService>().CreateAsync(
            new("Concurrency source", new Uri("https://central.example.test/"), secret.Value, true, false));
        var publications = new[] { Publication("Team model"), Publication("Unrelated model") };
        var catalog = new SharedProviderCatalogDocument(SharedProviderProtocolVersion.Current,
            new(Guid.NewGuid()), new($"sha256:{new string('b', 64)}"), new(SharedProviderRoutes.OpenAiBase), publications);
        catalog = catalog with { CatalogRevision = SharedProviderCanonicalRevision.ComputeCatalog(catalog) };
        await services.GetRequiredService<SharedProviderReconciliationCoordinator>().ReconcileAsync(new(source.Id, catalog,
            SharedProviderCatalogEntityTag.FromRevision(catalog.CatalogRevision), publications.Select(item => item.PublicationId).ToHashSet(),
            expectedSourceConcurrencyToken: source.ConcurrencyToken));
        var imports = Assert.Single(await services.GetRequiredService<ISharedProviderManagementService>().ListSourcesAsync()).Imports;
        return (imports.Single(item => item.LocalAlias == "Team model").ProviderProfileId,
            imports.Single(item => item.LocalAlias == "Unrelated model").ProviderProfileId, source.Id, catalog);
    }

    private static SharedProviderCatalogPublication Publication(string name) {
        var id = new SharedProviderPublicationId(Guid.NewGuid());
        var model = SharedProviderRoutingModelIdCodec.Create(id, "native-model");
        var publication = new SharedProviderCatalogPublication(id, new($"sha256:{new string('a', 64)}"), name,
            SharedProviderPurpose.Chat, SharedProviderTransport.OpenAiCompatible, model,
            [new(model, "native-model", [SharedProviderCapability.Responses])], new(SharedProviderHealthState.Available));
        return publication with { Revision = SharedProviderCanonicalRevision.ComputePublication(publication) };
    }

    public class NativeManagementProxy : DispatchProxy {
        public ISharedProviderManagementService Owner { get; set; } = null!;
        public List<SharedProviderImportedProfileUpdateRequest> Updates { get; } = [];
        public bool HoldUpdate { get; set; }
        public bool FailRead { get; set; }
        public TaskCompletionSource<SharedProviderProfileSharingSnapshot> Committed { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        protected override object? Invoke(MethodInfo? method, object?[]? arguments) {
            if (method!.Name == nameof(ISharedProviderManagementService.UpdateImportedProfileAsync)) {
                var request = (SharedProviderImportedProfileUpdateRequest)arguments![0]!;
                Updates.Add(request);
                return UpdateAsync(request, (CancellationToken)arguments[1]!);
            }
            if (method.Name == nameof(ISharedProviderManagementService.GetProfileSharingAsync) && FailRead) {
                return Task.FromException<SharedProviderProfileSharingSnapshot>(new IOException("Fixture read failure."));
            }
            return method.Invoke(Owner, arguments);
        }

        private async Task<SharedProviderProfileSharingSnapshot> UpdateAsync(SharedProviderImportedProfileUpdateRequest request, CancellationToken token) {
            var result = await Owner.UpdateImportedProfileAsync(request, token);
            if (HoldUpdate) {
                Committed.TrySetResult(result);
                await Release.Task;
            }
            return result;
        }
    }
}
