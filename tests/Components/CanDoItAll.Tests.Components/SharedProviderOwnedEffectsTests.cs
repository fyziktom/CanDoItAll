using System.Reflection;
using AngleSharp.Html.Dom;
using Bunit;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Components.BaseLib;
using CanDoItAll.Modules.AgentFramework;
using CanDoItAll.Modules.AgentFramework.Pages.Components;
using CanDoItAll.Modules.AgentFramework.ProviderManagement;
using CanDoItAll.Modules.Security;
using CanDoItAll.SharedProviders.Abstractions;
using Microsoft.Extensions.DependencyInjection;
using Editor = CanDoItAll.AgentFramework.Models.ProviderProfileEditorModel;
using Profile = CanDoItAll.AgentFramework.Models.ProviderProfile;
using Kind = CanDoItAll.AgentFramework.Models.ProviderKind;

namespace CanDoItAll.Tests.Components.AgentFramework;

public sealed class SharedProviderOwnedEffectsTests {
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Unrelated_sync_preserves_raw_text_section_and_edit_context(bool newDraft) {
        var reads = new Reads();
        var service = DispatchProxy.Create<ISharedProviderManagementService, SourceProxy>();
        var proxy = (SourceProxy)(object)service;
        proxy.Delay = false;
        await using var harness = await ComponentTestHarness.CreateAsync(services => {
            services.AddSingleton<IProviderProfilesReads>(reads);
            services.AddSingleton(service);
        });
        var cut = harness.Context.Render<AgentProviderProfilesPanel>();
        cut.WaitForElement("[data-testid='providers-name-input']");
        if (newDraft) {
            await cut.Find("[data-testid='providers-new']").ClickAsync();
        }
        cut.Find("[data-testid='providers-name-input']").Change("Unsaved name");
        await cut.FindAll("button[role='tab']").Single(x => x.TextContent.Contains("Runtime", StringComparison.Ordinal)).ClickAsync();
        var raw = "first\n\n second \n first";
        cut.Find("[data-testid='providers-suggested-models']").Change(raw);
        cut.Find("[data-testid='providers-notes']").Change("Unsaved notes");
        var context = cut.FindComponent<ProviderProfileEditorForm>().Instance.Context;
        var count = reads.EditorReads;
        await cut.Find("[data-testid='providers-connections']").ClickAsync();
        await cut.WaitForElement("[data-testid='shared-provider-source-sync']").ClickAsync();
        await cut.Find("[data-testid='shared-provider-connections-close']").ClickAsync();
        Assert.Same(context, cut.FindComponent<ProviderProfileEditorForm>().Instance.Context);
        Assert.Equal(raw, cut.Find("[data-testid='providers-suggested-models']").GetAttribute("value"));
        Assert.Equal("Unsaved notes", ((Editor)context.Model).Notes);
        Assert.Equal("Unsaved name", ((Editor)context.Model).Name);
        Assert.Equal(count, reads.EditorReads);
    }

    [Theory]
    [InlineData(SourceAction.Save, false)]
    [InlineData(SourceAction.Test, false)]
    [InlineData(SourceAction.Sync, false)]
    [InlineData(SourceAction.Save, true)]
    [InlineData(SourceAction.Test, true)]
    [InlineData(SourceAction.Sync, true)]
    public async Task Closing_or_disposing_overlay_cancels_owned_work_and_suppresses_late_callback(SourceAction action, bool dispose) {
        var service = DispatchProxy.Create<ISharedProviderManagementService, SourceProxy>();
        var proxy = (SourceProxy)(object)service;
        var changes = new List<SharedProviderChange>();
        await using var harness = await ComponentTestHarness.CreateAsync(services => services.AddSingleton(service));
        var unrelated = harness.Context.Render<Dialog>(p => p.Add(x => x.IsOpen, true).Add(x => x.TestId, "unrelated-dialog"));
        var cut = harness.Context.Render<SharedProviderSourcesDialog>(p => p
            .Add(x => x.Secrets, [new SecretListItem(proxy.SecretId, "Fixture credential", SecretKind.Token, "workspace", DateTimeOffset.UtcNow)])
            .Add(x => x.ProvidersChanged, (SharedProviderChangeDelivery delivery) => delivery.ReconcileAsync(() => {
                changes.Add(delivery.Change);
                return Task.CompletedTask;
            })));
        Task running;
        if (action == SourceAction.Save) {
            await cut.WaitForElement("[data-testid='shared-provider-source-add']").ClickAsync();
            cut.Find("[data-testid='shared-provider-source-name']").Change("New source");
            cut.Find("[data-testid='shared-provider-source-uri']").Change("https://source.example.test/");
            running = cut.Find("[data-testid='shared-provider-source-save']").ClickAsync();
        } else {
            running = cut.WaitForElement(action == SourceAction.Test
                ? "[data-testid='shared-provider-source-test']" : "[data-testid='shared-provider-source-sync']").ClickAsync();
        }
        cut.WaitForAssertion(() => Assert.True(proxy.ReceivedToken.CanBeCanceled));
        if (dispose) {
            await cut.InvokeAsync(() => cut.Instance.Dispose());
        } else {
            await cut.Find("[data-testid='shared-provider-connections-close']").ClickAsync();
        }
        Assert.True(proxy.ReceivedToken.IsCancellationRequested);
        proxy.Complete();
        await running;
        Assert.Empty(changes);
        Assert.NotNull(unrelated.Find("[data-testid='unrelated-dialog']"));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Sharing_mutation_for_A_cannot_publish_into_B(bool failure) {
        var first = Guid.NewGuid();
        var second = Guid.NewGuid();
        var service = DispatchProxy.Create<ISharedProviderManagementService, MutationProxy>();
        var proxy = (MutationProxy)(object)service;
        var changes = new List<SharedProviderChange>();
        await using var harness = await ComponentTestHarness.CreateAsync(services => services.AddSingleton(service));
        var cut = harness.Context.Render<SharedProviderManagementPanel>(p => p.Add(x => x.ProviderProfileId, first)
            .Add(x => x.ProvidersChanged, (SharedProviderChangeDelivery delivery) => delivery.ReconcileAsync(() => {
                changes.Add(delivery.Change);
                return Task.CompletedTask;
            })));
        var operation = cut.WaitForElement("[data-testid='shared-provider-publish']").ClickAsync();
        cut.WaitForAssertion(() => Assert.Equal(first, proxy.WrittenId));
        cut.Render(p => p.Add(x => x.ProviderProfileId, second));
        Assert.True(proxy.Token.IsCancellationRequested);
        if (failure) {
            proxy.Pending.SetException(new IOException("Synthetic late failure."));
        } else {
            proxy.Pending.SetResult(SharedProviderPublicationPanelTests.CreateLocalState(first, true, true) with {
                Change = new(SharedProviderChangeKind.Publication, [first])
            });
        }
        await operation;
        Assert.Empty(changes);
        Assert.Contains("Not published", cut.Find("[data-testid='shared-provider-publication-status']").TextContent);
        Assert.False(cut.Find("[data-testid='shared-provider-publish']").HasAttribute("disabled"));
    }

    [Fact]
    public async Task Disposing_sharing_panel_cancels_pending_mutation_without_late_publication() {
        var service = DispatchProxy.Create<ISharedProviderManagementService, MutationProxy>();
        var proxy = (MutationProxy)(object)service;
        var changes = new List<SharedProviderChange>();
        await using var harness = await ComponentTestHarness.CreateAsync(services => services.AddSingleton(service));
        var id = Guid.NewGuid();
        var cut = harness.Context.Render<SharedProviderManagementPanel>(p => p.Add(x => x.ProviderProfileId, id)
            .Add(x => x.ProvidersChanged, (SharedProviderChangeDelivery delivery) => delivery.ReconcileAsync(() => {
                changes.Add(delivery.Change);
                return Task.CompletedTask;
            })));
        var operation = cut.WaitForElement("[data-testid='shared-provider-publish']").ClickAsync();
        cut.WaitForAssertion(() => Assert.Equal(id, proxy.WrittenId));
        await cut.InvokeAsync(() => cut.Instance.Dispose());
        Assert.True(proxy.Token.IsCancellationRequested);
        proxy.Pending.SetResult(SharedProviderPublicationPanelTests.CreateLocalState(id, true, true) with {
            Change = new(SharedProviderChangeKind.Publication, [id])
        });
        await operation;
        Assert.Empty(changes);
    }

    public enum SourceAction { Save, Test, Sync }

    public class MutationProxy : DispatchProxy {
        public Guid WrittenId { get; private set; }
        public CancellationToken Token { get; private set; }
        public TaskCompletionSource<SharedProviderProfileSharingSnapshot> Pending { get; } = new();
        protected override object? Invoke(MethodInfo? method, object?[]? args) {
            if (method?.Name == nameof(ISharedProviderManagementService.GetProfileSharingAsync)) {
                return Task.FromResult(SharedProviderPublicationPanelTests.CreateLocalState((Guid)args![0]!, false, true));
            }
            if (method?.Name == nameof(ISharedProviderManagementService.SetPublicationAsync)) {
                WrittenId = (Guid)args![0]!;
                Token = (CancellationToken)args[3]!;
                return Pending.Task;
            }
            throw new InvalidOperationException("Unexpected sharing call.");
        }
    }

    public class SourceProxy : DispatchProxy {
        public Guid SecretId { get; } = Guid.NewGuid();
        public Guid SourceId { get; } = Guid.NewGuid();
        public Guid ImportedId { get; } = Guid.NewGuid();
        public bool Delay { get; set; } = true;
        public int Operations { get; private set; }
        public CancellationToken ReceivedToken { get; private set; }
        private readonly TaskCompletionSource<SharedProviderSourceWriteResult> write = new();
        private readonly TaskCompletionSource<SharedProviderSourceOperationResult> operation = new();
        private SharedProviderChange Change => new(SharedProviderChangeKind.Reconciliation, [ImportedId],
            remoteOwnedFieldsChanged: true, catalogMembershipMayHaveChanged: true);
        public void Complete() {
            write.TrySetResult(new(SourceId, Guid.NewGuid()) { Change = Change });
            operation.TrySetResult(Result());
        }
        private SharedProviderSourceOperationResult Result() => SharedProviderSourceOperationResult.NotModified(
            new SharedProviderCatalogEntityTag($"\"sha256:{new string('a', 64)}\"")) with { Change = Change };

        protected override object? Invoke(MethodInfo? method, object?[]? args) {
            if (method?.Name == nameof(ISharedProviderManagementService.ListSourcesAsync)) {
                var publication = new SharedProviderPublicationId(Guid.NewGuid());
                var imported = new SharedProviderImportedProfileSnapshot(Guid.NewGuid(), SourceId, "Fixture source",
                    publication, ImportedId, "Imported alias", true, "Remote provider", SharedProviderPurpose.Chat,
                    SharedProviderTransport.OpenAiCompatible, SharedProviderRoutingModelIdCodec.Create(publication, "model"),
                    SharedProviderSelectionState.Selected, SharedProviderAvailabilityState.Available, [], Guid.NewGuid(), Guid.NewGuid());
                var source = new SharedProviderSourceSnapshot(SourceId, "Fixture source", new Uri("https://source.example.test/"),
                    SecretId, true, SharedProviderSourceNetworkPolicy.PublicOnly, SharedProviderSourceStatus.Available,
                    null, null, null, null, "", Guid.NewGuid());
                return Task.FromResult<IReadOnlyList<SharedProviderSourceManagementSnapshot>>([new(source, [imported])]);
            }
            Operations++;
            ReceivedToken = (CancellationToken)args![^1]!;
            return method?.Name switch {
                nameof(ISharedProviderManagementService.SaveSourceAsync) => write.Task,
                nameof(ISharedProviderManagementService.TestSourceAsync) or nameof(ISharedProviderManagementService.SynchronizeSourceAsync)
                    => Delay ? operation.Task : Task.FromResult(Result()),
                _ => throw new InvalidOperationException("Unexpected source call.")
            };
        }
    }

    private sealed class Reads : IProviderProfilesReads {
        private readonly Guid id = Guid.NewGuid();
        public int EditorReads { get; private set; }
        public Task<ProviderProfilesCatalog> LoadCatalogAsync(CancellationToken token = default) =>
            Task.FromResult(new ProviderProfilesCatalog([new Profile(id, "Local provider", Kind.OpenAi,
                "", "", "model", ProviderTransportKind.Responses, true, true, true, false, true,
                "{}", "", "", null, ["model"])], new([])));
        public Task<Editor> LoadEditorAsync(Guid providerId, CancellationToken token = default) {
            EditorReads++;
            return Task.FromResult(new Editor { Id = providerId, Name = "Local provider", DefaultModel = "model" });
        }
    }
}
