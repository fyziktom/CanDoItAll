using System.Reflection;
using Bunit;
using CanDoItAll.Editing;
using CanDoItAll.Modules.AgentFramework;
using CanDoItAll.Modules.AgentFramework.Pages.Components;
using CanDoItAll.Modules.AgentFramework.ProviderManagement;
using CanDoItAll.Tests.Support;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;

namespace CanDoItAll.Tests.Components.AgentFramework;

public sealed class SharedProviderSharingAccessTests {
    [Fact]
    public async Task Reading_sharing_status_cannot_adopt_a_peer_configuration_that_the_parent_editor_has_not_reviewed() {
        await using var harness = await ComponentTestHarness.CreateAsync();
        var registry = harness.Context.Services.GetRequiredService<CanDoItAll.AgentFramework.Core.IProviderProfileRegistry>();
        var id = await registry.SaveProviderAsync(new() {
            Name = "Owned publication parent configuration", Kind = CanDoItAll.AgentFramework.Models.ProviderKind.Ollama,
            Transport = CanDoItAll.AgentFramework.Models.ProviderTransportKind.ChatCompletions,
            BaseUrl = "http://127.0.0.1:11434", DefaultModel = "owned-model", SuggestedModels = ["owned-model"], IsEnabled = true
        });
        var cut = harness.Context.Render<AgentProviderProfilesPanel>();
        cut.WaitForElement("[data-testid='providers-name-input']");
        await cut.FindAll("[data-testid='providers-tree-provider']")
            .First(node => node.TextContent.Contains("Owned publication parent configuration", StringComparison.Ordinal)).ClickAsync();
        var original = cut.Instance.Editor.Model.ExpectedConcurrencyToken;
        await cut.FindAll("[role='tab']").Single(tab => tab.TextContent.Contains("Sharing", StringComparison.Ordinal)).ClickAsync();
        cut.WaitForElement("[data-testid='shared-provider-publish']");
        var peer = await registry.GetProviderEditorAsync(id);
        peer.Name = "Peer configuration awaiting parent review";
        await registry.SaveProviderAsync(peer);
        cut.WaitForElement("[data-testid='shared-provider-warning']");
        await cut.Find("[data-testid='shared-provider-retry']").ClickAsync();
        Assert.Equal(original, cut.Instance.Editor.Model.ExpectedConcurrencyToken);
        var sharing = cut.FindComponent<SharedProviderManagementPanel>().Instance;
        await cut.InvokeAsync(() => sharing.PublishAsync(sharing.Presentation.Origin));
        Assert.Null((await harness.Context.Services.GetRequiredService<ISharedProviderManagementService>().GetProfileSharingAsync(id)).Publication);
        Assert.True(cut.Find("[data-testid='shared-provider-publish']").HasAttribute("disabled"));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Sharing_does_not_read_during_prerender_or_without_access(bool interactive) {
        var service = DispatchProxy.Create<ISharedProviderManagementService, Owner>();
        var proxy = (Owner)(object)service;
        var access = new EditingUiTestAccess { Allowed = !interactive };
        await using var harness = await ComponentTestHarness.CreateAsync(services => {
            services.AddSingleton(service);
            services.AddSingleton<IEditingUiAccess>(access);
        });
        harness.Context.SetRendererInfo(new RendererInfo(interactive ? "Server" : "Static", interactive));
        var cut = harness.Context.Render<SharedProviderManagementPanel>(p => p.Add(c => c.ProviderProfileId, proxy.Id));
        Assert.Equal(0, proxy.Reads);
        Assert.DoesNotContain("Protected sharing eligibility", cut.Markup, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_held_sharing_read_cannot_reveal_its_payload_to_a_different_actor() {
        var service = DispatchProxy.Create<ISharedProviderManagementService, Owner>();
        var proxy = (Owner)(object)service;
        var access = new EditingUiTestAccess();
        proxy.PendingRead = new(TaskCreationOptions.RunContinuationsAsynchronously);
        await using var harness = await ComponentTestHarness.CreateAsync(services => {
            services.AddSingleton(service);
            services.AddSingleton<IEditingUiAccess>(access);
        });
        var cut = harness.Context.Render<SharedProviderManagementPanel>(p => p.Add(c => c.ProviderProfileId, proxy.Id));
        cut.WaitForAssertion(() => Assert.Equal(1, proxy.Reads));
        access.Principal = "another-sharing-actor";
        await cut.InvokeAsync(() => proxy.PendingRead.SetResult(proxy.State));
        cut.WaitForAssertion(() => Assert.DoesNotContain("Protected sharing eligibility", cut.Markup, StringComparison.Ordinal));
        Assert.Empty(cut.FindAll("[data-testid='shared-provider-publish']"));
    }

    [Fact]
    public async Task Actor_loss_before_publication_dispatch_starts_neither_a_write_nor_a_recovery_attempt() {
        var service = DispatchProxy.Create<ISharedProviderManagementService, Owner>();
        var proxy = (Owner)(object)service;
        var access = new EditingUiTestAccess();
        await using var harness = await ComponentTestHarness.CreateAsync(services => {
            services.AddSingleton(service);
            services.AddSingleton<IEditingUiAccess>(access);
        });
        var cut = harness.Context.Render<SharedProviderManagementPanel>(p => p.Add(c => c.ProviderProfileId, proxy.Id));
        cut.WaitForElement("[data-testid='shared-provider-publish']");
        access.Principal = "another-sharing-actor";
        await cut.Find("[data-testid='shared-provider-publish']").ClickAsync();
        Assert.Equal(0, proxy.Writes);
        Assert.Null(harness.Context.Services.GetRequiredService<SharedProviderRecovery>().FindTarget(proxy.Id));
        Assert.Empty(cut.FindAll("[data-testid='shared-provider-publish']"));
    }

    [Fact]
    public async Task Matching_current_publication_does_not_confirm_an_unknown_historical_write_or_deliver_a_fake_commit() {
        var service = DispatchProxy.Create<ISharedProviderManagementService, Owner>();
        var proxy = (Owner)(object)service;
        var deliveries = 0;
        await using var harness = await ComponentTestHarness.CreateAsync(services => services.AddSingleton(service));
        var cut = harness.Context.Render<SharedProviderManagementPanel>(p => p.Add(c => c.ProviderProfileId, proxy.Id)
            .Add(c => c.ProvidersChanged, (SharedProviderChangeDelivery delivery) => {
                deliveries++;
                return delivery.ReconcileAsync(() => Task.CompletedTask);
            }));
        await cut.WaitForElement("[data-testid='shared-provider-publish']").ClickAsync();
        await cut.Find("[data-testid='shared-provider-retry']").ClickAsync();
        Assert.NotNull(harness.Context.Services.GetRequiredService<SharedProviderRecovery>().FindTarget(proxy.Id));
        Assert.Equal(0, deliveries);
        Assert.Equal(1, proxy.Writes);
    }

    public class Owner : DispatchProxy {
        public Guid Id { get; } = Guid.NewGuid();
        public int Reads { get; private set; }
        public int Writes { get; private set; }
        public TaskCompletionSource<SharedProviderProfileSharingSnapshot>? PendingRead { get; set; }
        private SharedProviderProfileSharingSnapshot? state;
        public SharedProviderProfileSharingSnapshot State => state ??= SharedProviderPublicationPanelTests.CreateLocalState(Id, false, false) with {
            Eligibility = new(SharedProviderPublicationEligibilityCode.Eligible, "Protected sharing eligibility",
                CanDoItAll.SharedProviders.Abstractions.SharedProviderPurpose.Chat,
                CanDoItAll.SharedProviders.Abstractions.SharedProviderTransport.OpenAiCompatible, [])
        };
        protected override object? Invoke(MethodInfo? method, object?[]? args) {
            if (method?.Name == nameof(ISharedProviderManagementService.GetProfileSharingAsync)) {
                Reads++;
                return PendingRead?.Task ?? Task.FromResult(State);
            }
            if (method?.Name == nameof(ISharedProviderManagementService.SetPublicationAsync)) {
                Writes++;
                state = State with { Publication = State.Publication! with { IsPublished = true, ConcurrencyToken = Guid.NewGuid() } };
                return Task.FromException<SharedProviderProfileSharingSnapshot>(new IOException("Unknown response after dispatch."));
            }
            throw new InvalidOperationException("Unexpected sharing admission operation.");
        }
    }
}
