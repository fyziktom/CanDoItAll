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
using CanDoItAll.Editing;
using CanDoItAll.Infrastructure.Editing;
using CanDoItAll.Tests.Support;
using Microsoft.AspNetCore.Components;

namespace CanDoItAll.Tests.Components.AgentFramework;

[Trait("Category", "HostPlatform")]
public sealed class SharedProviderSourceSeamTests {
    [Fact]
    public async Task A_peer_retirement_without_a_source_hint_preserves_the_peer_and_requires_catalog_review_before_remote_work() {
        var remote = new CatalogResponse();
        NativeSourceProxy? proxy = null;
        await using var harness = await HarnessAsync(value => proxy = value, remote);
        var seed = await SharedProviderLocalConcurrencyTests.CreateImportAsync(harness.Context.Services);
        remote.Catalog = seed.Catalog;
        var cut = harness.Context.Render<SharedProviderSourcesDialog>(p => p.Add(c => c.ProvidersChanged,
            (SharedProviderChangeDelivery delivery) => delivery.ReconcileAsync(() => Task.CompletedTask)));
        await cut.WaitForElement("[data-testid='shared-provider-source-discover']").ClickAsync();
        var dialog = Assert.IsType<SharedProviderCatalogSelection>(cut.Instance.Presentation.Catalog);
        var original = dialog.Origin;
        var peer = (await proxy!.Owner.ListSourcesAsync()).Single().Imports[1];
        var peerChanges = ActivatorUtilities.CreateInstance<ProviderEditingChanges>(harness.Context.Services, new EditingChangeFeed());
        var peerOwner = ActivatorUtilities.CreateInstance<SharedProviderManagementService>(harness.Context.Services, peerChanges);
        await peerOwner.RetireImportedProfileAsync(new(peer.ImportId, peer.ProviderProfileId,
            peer.ImportConcurrencyToken, peer.ProviderConcurrencyToken));
        Assert.Equal(original.ConcurrencyToken, (await proxy.Owner.ListSourcesAsync()).Single().Source.ConcurrencyToken);
        Assert.Equal(1, remote.Fetches);
        await cut.Find("[data-testid='shared-provider-catalog-apply']").ClickAsync();
        Assert.True(dialog.ReadbackConflict);
        Assert.Equal(original, dialog.Origin);
        Assert.Equal(1, remote.Fetches);
        Assert.Equal(SharedProviderSelectionState.Retired, (await proxy.Owner.ListSourcesAsync()).Single().Imports
            .Single(import => import.ImportId == peer.ImportId).SelectionState);
        Assert.NotNull(cut.Find("[data-testid='shared-provider-catalog-compare']"));
        Assert.Single(proxy.Synchronizations);
    }

    [Fact]
    public async Task Catalog_receipt_uses_exact_accepted_source_versions_when_a_peer_commits_before_acknowledgment() {
        var remote = new CatalogResponse();
        NativeSourceProxy? proxy = null;
        await using var harness = await HarnessAsync(value => proxy = value, remote);
        var seed = await SharedProviderLocalConcurrencyTests.CreateImportAsync(harness.Context.Services);
        remote.Catalog = seed.Catalog;
        var cut = harness.Context.Render<SharedProviderSourcesDialog>(p => p.Add(c => c.ProvidersChanged,
            (SharedProviderChangeDelivery delivery) => delivery.ReconcileAsync(() => Task.CompletedTask)));
        await cut.WaitForElement("[data-testid='shared-provider-source-discover']").ClickAsync();
        var dialog = Assert.IsType<SharedProviderCatalogSelection>(cut.Instance.Presentation.Catalog);
        cut.FindAll("[data-testid='shared-provider-catalog-selection']")[1].Change(false);
        proxy!.HoldSync = true;
        var applying = cut.Find("[data-testid='shared-provider-catalog-apply']").ClickAsync();
        await proxy.SyncCommitted.Task.WaitAsync(TimeSpan.FromSeconds(15));
        var accepted = Assert.IsType<SharedProviderSourceObservation>(proxy.AcceptedSync!.AcceptedObservation);
        cut.FindAll("[data-testid='shared-provider-catalog-selection']")[1].Change(true);
        var native = harness.Context.Services.GetRequiredService<SharedProviderSourceService>();
        var source = await native.GetAsync(seed.SourceId);
        await native.UpdateAsync(source.Id, source.ConcurrencyToken, new("Peer source after synchronization", source.BaseUri,
            source.ApiTokenSecretId, true, false));
        proxy.Release.SetResult();
        await applying;
        Assert.Same(dialog, cut.Instance.Presentation.Catalog);
        Assert.True(dialog.IsSelected(seed.Catalog.Providers[1].PublicationId));
        Assert.Equal(accepted.ConcurrencyToken, dialog.Origin.ConcurrencyToken);
        Assert.True(dialog.ReadbackConflict);
        Assert.Single(proxy.Synchronizations);
    }

    [Fact]
    public async Task Targeted_source_hints_do_not_query_bodies_and_invalidate_a_review_without_replacing_raw_input() {
        NativeSourceProxy? proxy = null;
        await using var harness = await HarnessAsync(value => proxy = value);
        var credential = await CredentialAsync(harness);
        var native = harness.Context.Services.GetRequiredService<SharedProviderSourceService>();
        var created = await native.CreateAsync(new("Original source", new("https://source.example.test/"), credential.Id, true, false));
        var cut = Render(harness, credential);
        await cut.WaitForElement("[data-testid='shared-provider-source-edit']").ClickAsync();
        cut.Find("[data-testid='shared-provider-source-name']").Input("My source draft");
        cut.Find("[data-testid='shared-provider-source-uri']").Input("https://[unfinished");
        var context = cut.Instance.Presentation.Editor!.Context;
        var reads = proxy!.ListReads;
        var peer = await native.GetAsync(created.Id);
        await native.UpdateAsync(peer.Id, peer.ConcurrencyToken, new("Peer source name", peer.BaseUri, peer.ApiTokenSecretId, true, false));
        cut.WaitForElement("[data-testid='shared-sources-changed']");
        Assert.Equal(reads, proxy.ListReads);
        Assert.Equal("https://[unfinished", cut.Find("[data-testid='shared-provider-source-uri']").GetAttribute("value"));
        await cut.Find("[data-testid='shared-source-compare']").ClickAsync();
        cut.WaitForElement("[data-testid='shared-source-comparison']");
        Assert.Contains("Peer source name", cut.Markup, StringComparison.Ordinal);
        Assert.Equal(reads + 1, proxy.ListReads);
        peer = await native.GetAsync(created.Id);
        await native.UpdateAsync(peer.Id, peer.ConcurrencyToken, new("Newer source name", peer.BaseUri, peer.ApiTokenSecretId, true, false));
        cut.WaitForAssertion(() => Assert.True(cut.Instance.Presentation.Editor!.Comparison!.IsStale));
        Assert.True(cut.Find("[data-testid='shared-source-comparison-apply']").HasAttribute("disabled"));
        Assert.Equal(reads + 1, proxy.ListReads);
        Assert.Same(context, cut.Instance.Presentation.Editor!.Context);
        Assert.Equal("https://[unfinished", cut.Find("[data-testid='shared-provider-source-uri']").GetAttribute("value"));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Source_workspace_does_not_read_or_reveal_credentials_during_prerender_or_without_access(bool interactive) {
        NativeSourceProxy? proxy = null;
        var access = new EditingUiTestAccess { Allowed = !interactive };
        await using var harness = await HarnessAsync(value => proxy = value, access: access);
        harness.Context.SetRendererInfo(new RendererInfo(interactive ? "Server" : "Static", interactive));
        var cut = Render(harness, new(Guid.NewGuid(), "Protected source credential", SecretKind.Token, "workspace", DateTimeOffset.UtcNow));
        Assert.Equal(0, proxy!.ListReads);
        Assert.DoesNotContain("Protected source credential", cut.Markup, StringComparison.Ordinal);
        Assert.Empty(cut.FindAll("form"));
    }

    [Fact]
    public async Task A_held_source_read_cannot_reveal_its_payload_to_a_different_actor() {
        NativeSourceProxy? proxy = null;
        var access = new EditingUiTestAccess();
        await using var harness = await HarnessAsync(value => proxy = value, access: access);
        var credential = await CredentialAsync(harness);
        var native = harness.Context.Services.GetRequiredService<SharedProviderSourceService>();
        var created = await native.CreateAsync(new("Protected held source", new("https://source.example.test/"), credential.Id, true, false));
        var source = await native.GetAsync(created.Id);
        _ = harness.Context.Services.GetRequiredService<ISharedProviderManagementService>();
        var pending = new TaskCompletionSource<IReadOnlyList<SharedProviderSourceManagementSnapshot>>(TaskCreationOptions.RunContinuationsAsynchronously);
        proxy!.PendingSources = pending.Task;
        var cut = Render(harness, credential);
        cut.WaitForAssertion(() => Assert.Equal(1, proxy.ListReads));
        access.Principal = "different-source-actor";
        await cut.InvokeAsync(() => pending.SetResult([new(source, [])]));
        cut.WaitForElement("[data-testid='shared-provider-sources-error']");
        Assert.DoesNotContain(source.Name, cut.Markup, StringComparison.Ordinal);
        Assert.DoesNotContain(credential.Name, cut.Markup, StringComparison.Ordinal);
        Assert.Empty(cut.FindAll("form"));
    }

    [Fact]
    public async Task Actor_loss_before_source_dispatch_retains_the_local_draft_without_starting_a_write_or_attempt() {
        NativeSourceProxy? proxy = null;
        var access = new EditingUiTestAccess();
        await using var harness = await HarnessAsync(value => proxy = value, access: access);
        var credential = await CredentialAsync(harness);
        var cut = Render(harness, credential);
        await cut.WaitForElement("[data-testid='shared-provider-source-add']").ClickAsync();
        cut.Find("[data-testid='shared-provider-source-name']").Input("Retained source draft");
        cut.Find("[data-testid='shared-provider-source-uri']").Input("https://source.example.test/");
        var draft = cut.Instance.Presentation.Editor!;
        access.Principal = "different-source-actor";

        await cut.Find("[data-testid='shared-provider-source-save']").ClickAsync();

        Assert.Empty(proxy!.Writes);
        Assert.Null(harness.Context.Services.GetRequiredService<SharedProviderRecovery>().Source);
        Assert.Equal("Retained source draft", draft.Name);
        Assert.Empty(cut.FindAll("form"));
        Assert.DoesNotContain(credential.Name, cut.Markup, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Pending_shared_recovery_cannot_bind_to_a_new_actor_or_dataset(bool actor) {
        var recovery = new SharedProviderRecovery();
        var address = new ProviderRecoveryAddress(new(EditingScopeKind.Dataset, Guid.NewGuid()), "source-original-actor");
        recovery.Bind(address);
        var attempt = new SharedProviderSourceMutationAttempt(Guid.NewGuid(), SharedProviderSourceMutationKind.Create);
        recovery.BeginSource(attempt);
        var other = actor ? address with { PrincipalStamp = "another-source-actor" }
            : address with { Scope = address.Scope with { Id = Guid.NewGuid() } };
        Assert.Throws<UnauthorizedAccessException>(() => recovery.Bind(other));
        recovery.Bind(address);
        Assert.Same(attempt, recovery.Source);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Unconfirmed_source_can_review_a_later_peer_without_losing_its_retained_context_or_replaying(bool recreate) {
        NativeSourceProxy? proxy = null;
        await using var harness = await HarnessAsync(value => proxy = value);
        var credential = await CredentialAsync(harness);
        var cut = Render(harness, credential);
        await cut.WaitForElement("[data-testid='shared-provider-source-add']").ClickAsync();
        cut.Find("[data-testid='shared-provider-source-name']").Input("Owned submitted source");
        cut.Find("[data-testid='shared-provider-source-uri']").Input("https://source.example.test/");
        var draft = cut.Instance.Presentation.Editor!;
        var context = draft.Context;
        proxy!.HoldWrite = true;
        proxy.LoseAcknowledgement = true;
        var saving = cut.Find("[data-testid='shared-provider-source-save']").ClickAsync();
        var committed = await proxy.Committed.Task.WaitAsync(TimeSpan.FromSeconds(15));
        cut.Find("[data-testid='shared-provider-source-name']").Input("Retained source after dispatch");
        cut.Find("[data-testid='shared-provider-source-uri']").Input("https://[unfinished");
        proxy.Release.SetResult();
        await saving;
        var current = Assert.Single(await proxy.Owner.ListSourcesAsync()).Source;
        var peer = await proxy.Owner.SaveSourceAsync(new(current.Id, current.ConcurrencyToken, "Current peer source",
            current.BaseUri, current.ApiTokenSecretId, current.IsEnabled, false));
        if (recreate) {
            await cut.InvokeAsync(() => cut.Instance.DisposeAsync().AsTask());
            cut.Dispose();
            cut = Render(harness, credential);
        }
        await cut.WaitForElement("[data-testid='shared-provider-source-verify']").ClickAsync();
        Assert.NotNull(harness.Context.Services.GetRequiredService<SharedProviderRecovery>().Source);
        await cut.Find("[data-testid='shared-source-review-pending']").ClickAsync();
        cut.WaitForElement("[data-testid='shared-source-comparison']");
        Assert.Same(context, cut.Instance.Presentation.Editor!.Context);
        Assert.Equal("https://[unfinished", cut.Find("[data-testid='shared-provider-source-uri']").GetAttribute("value"));
        cut.Find("[data-testid='shared-source-comparison'] input[type='checkbox']").Change(true);
        await cut.Find("[data-testid='shared-source-comparison-apply']").ClickAsync();
        var recovery = harness.Context.Services.GetRequiredService<SharedProviderRecovery>();
        Assert.Null(recovery.Source);
        Assert.Single(recovery.ReviewedSources);
        Assert.Equal(peer.ConcurrencyToken, draft.ExpectedToken);
        Assert.Single(proxy.Writes);
        await cut.Find("[data-testid='shared-provider-source-save']").ClickAsync();
        Assert.Single(proxy.Writes);
        Assert.Same(context, cut.Instance.Presentation.Editor!.Context);
        proxy.HoldWrite = false;
        proxy.LoseAcknowledgement = false;
        cut.Find("[data-testid='shared-provider-source-uri']").Input("https://source.example.test/");
        await cut.Find("[data-testid='shared-provider-source-save']").ClickAsync();
        Assert.Equal(2, proxy.Writes.Count);
        Assert.Equal("Retained source after dispatch", Assert.Single(await proxy.Owner.ListSourcesAsync()).Source.Name);
    }

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
        if (failure == CatalogRecovery.Unknown) {
            await cut.InvokeAsync(() => cut.Instance.DisposeAsync().AsTask());
            cut.Dispose();
            cut = harness.Context.Render<SharedProviderSourcesDialog>(p => p.Add(c => c.ProvidersChanged,
                (SharedProviderChangeDelivery delivery) => delivery.ReconcileAsync(() => Task.CompletedTask)));
            cut.WaitForElement("[data-testid='shared-provider-source-verify']");
        }
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
        if (failure == CatalogRecovery.Unknown) {
            Assert.Equal(firstOrigin, dialog.Origin);
            Assert.NotNull(cut.Instance.Presentation.Recovery);
            Assert.Null(harness.Context.Services.GetRequiredService<SharedProviderRecovery>().PendingDelivery(
                cut.Instance.Presentation.Recovery!.AttemptId));
            await cut.Find("[data-testid='shared-provider-catalog-compare']").ClickAsync();
            cut.WaitForElement("[data-testid='shared-provider-catalog-comparison']");
            await cut.Find("[data-testid='shared-provider-catalog-review-selection'] button").ClickAsync();
            await cut.Find("[data-testid='shared-provider-catalog-review-apply']").ClickAsync();
            Assert.Null(cut.Instance.Presentation.Recovery);
            Assert.Single(harness.Context.Services.GetRequiredService<SharedProviderRecovery>().ReviewedSources);
            Assert.Single(proxy.Synchronizations);
        }
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

    private static Task<ComponentTestHarness> HarnessAsync(Action<NativeSourceProxy> capture, ISharedProviderCatalogClient? catalog = null,
        EditingUiTestAccess? access = null) =>
        ComponentTestHarness.CreateAsync(services => {
            if (access is not null) {
                services.AddSingleton<IEditingUiAccess>(access);
            }
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
        public int Fetches { get; private set; }
        public ValueTask<SharedProviderCatalogFetchResult> FetchAsync(SharedProviderCatalogFetchRequest request, CancellationToken cancellationToken = default) {
            Fetches++;
            return ValueTask.FromResult<SharedProviderCatalogFetchResult>(new SharedProviderCatalogFetchResult.Succeeded(
                Catalog, SharedProviderCatalogEntityTag.FromRevision(Catalog.CatalogRevision)));
        }
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
        public int ListReads { get; private set; }
        public Task<IReadOnlyList<SharedProviderSourceManagementSnapshot>>? PendingSources { get; set; }
        public TaskCompletionSource<SharedProviderSourceWriteResult> Committed { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public List<IReadOnlySet<SharedProviderPublicationId>> Synchronizations { get; } = [];
        public TaskCompletionSource SyncCommitted { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public SharedProviderSourceOperationResult? AcceptedSync { get; private set; }
        protected override object? Invoke(MethodInfo? method, object?[]? args) {
            if (method!.Name == nameof(ISharedProviderManagementService.ListSourcesAsync)) {
                ListReads++;
                if (PendingSources is not null) {
                    return PendingSources;
                }
            }
            if (method!.Name == nameof(ISharedProviderManagementService.SaveSourceAsync)) {
                var request = (SharedProviderSourceEditorRequest)args![0]!;
                Writes.Add(request);
                return SaveAsync(request, (CancellationToken)args[1]!);
            }
            if (method.Name == nameof(ISharedProviderManagementService.SynchronizeSourceAsync)) {
                var selected = (IReadOnlySet<SharedProviderPublicationId>)args![2]!;
                Synchronizations.Add(selected);
                return SynchronizeAsync((Guid)args[0]!, (SharedProviderSourceObservation)args[1]!, selected, (CancellationToken)args[3]!);
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
        private async Task<SharedProviderSourceOperationResult> SynchronizeAsync(Guid sourceId, SharedProviderSourceObservation original,
            IReadOnlySet<SharedProviderPublicationId> selected, CancellationToken token) {
            var result = await Owner.SynchronizeSourceAsync(sourceId, original, selected, token);
            AcceptedSync = result;
            if (HoldSync) {
                SyncCommitted.TrySetResult();
                await Release.Task;
            }
            return LoseAcknowledgement ? throw new IOException("Fixture sync acknowledgement lost after commit.") : result;
        }
    }
}
