using System.Reflection;
using Bunit;
using CanDoItAll.AgentFramework.SharedProviders.UI;
using CanDoItAll.Modules.AgentFramework.Pages.Components;
using CanDoItAll.Modules.AgentFramework.ProviderManagement;
using Microsoft.Extensions.DependencyInjection;

namespace CanDoItAll.Tests.Components.AgentFramework;

public sealed class SharedProviderSharingSeamTests {
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Retained_child_callback_cannot_publish_after_new_snapshot_or_A_B_A(bool changeTarget) {
        var service = DispatchProxy.Create<ISharedProviderManagementService, Owner>();
        var owner = (Owner)(object)service;
        await using var harness = await ComponentTestHarness.CreateAsync(services => services.AddSingleton(service));
        var id = Guid.NewGuid();
        var cut = harness.Context.Render<SharedProviderManagementPanel>(p => p.Add(c => c.ProviderProfileId, id));
        cut.WaitForElement("[data-testid='shared-provider-publish']");
        var oldCallback = cut.FindComponent<SharedProviderLocalPublicationContent>().Instance.Publish;
        if (changeTarget) {
            cut.Render(p => p.Add(c => c.ProviderProfileId, Guid.NewGuid()));
        }
        cut.Render(p => p.Add(c => c.ProviderProfileId, id).Add(c => c.Revision, 1));
        await cut.InvokeAsync(() => oldCallback.InvokeAsync());
        Assert.Empty(owner.Writes);
        await cut.Find("[data-testid='shared-provider-publish']").ClickAsync();
        Assert.Equal(id, Assert.Single(owner.Writes).Id);
    }

    [Fact]
    public async Task Confirmation_keeps_exact_target_versions_and_old_cancel_cannot_close_its_replacement() {
        var service = DispatchProxy.Create<ISharedProviderManagementService, Owner>();
        var owner = (Owner)(object)service;
        owner.Published = true;
        await using var harness = await ComponentTestHarness.CreateAsync(services => services.AddSingleton(service));
        var id = Guid.NewGuid();
        var cut = harness.Context.Render<SharedProviderManagementPanel>(p => p.Add(c => c.ProviderProfileId, id));
        await cut.WaitForElement("[data-testid='shared-provider-unpublish']").ClickAsync();
        var first = cut.Instance.Presentation.Confirmation!;
        await cut.InvokeAsync(() => cut.Instance.CloseConfirmation(first));
        await cut.Find("[data-testid='shared-provider-unpublish']").ClickAsync();
        var second = cut.Instance.Presentation.Confirmation!;
        await cut.InvokeAsync(() => cut.Instance.CloseConfirmation(first));
        await cut.InvokeAsync(() => cut.Instance.ConfirmAsync(first));
        Assert.Same(second, cut.Instance.Presentation.Confirmation);
        Assert.Empty(owner.Writes);
        await cut.Find("[data-testid='shared-provider-confirmation-apply']").ClickAsync();
        var write = Assert.Single(owner.Writes);
        Assert.Equal(id, write.Id);
        Assert.Equal(second.Origin.PublicationToken, write.Token);
        Assert.Equal(SharedProviderPublicationAction.Unpublish, write.Action);
    }

    [Fact]
    public async Task Handler_refuses_ineligible_publication_even_when_invoked_without_the_disabled_button() {
        var service = DispatchProxy.Create<ISharedProviderManagementService, Owner>();
        var owner = (Owner)(object)service;
        owner.Eligible = false;
        await using var harness = await ComponentTestHarness.CreateAsync(services => services.AddSingleton(service));
        var cut = harness.Context.Render<SharedProviderManagementPanel>(p => p.Add(c => c.ProviderProfileId, Guid.NewGuid()));
        cut.WaitForElement("[data-testid='shared-provider-publish']");
        await cut.InvokeAsync(() => cut.Instance.PublishAsync(cut.Instance.Presentation.Origin));
        Assert.Empty(owner.Writes);
    }

    public class Owner : DispatchProxy {
        public bool Published { get; set; }
        public bool Eligible { get; set; } = true;
        public List<(Guid Id, SharedProviderPublicationAction Action, Guid? Token)> Writes { get; } = [];
        private readonly Dictionary<Guid, SharedProviderProfileSharingSnapshot> states = [];
        protected override object? Invoke(MethodInfo? method, object?[]? args) {
            var id = (Guid)args![0]!;
            if (!states.TryGetValue(id, out var state)) {
                state = SharedProviderPublicationPanelTests.CreateLocalState(id, Published, Eligible);
                states[id] = state;
            }
            if (method!.Name == nameof(ISharedProviderManagementService.SetPublicationAsync)) {
                var action = (SharedProviderPublicationAction)args[1]!;
                Writes.Add((id, action, (Guid?)args[2]));
                state = state with { Publication = state.Publication! with { IsPublished = action == SharedProviderPublicationAction.Publish } };
                states[id] = state;
            }
            return Task.FromResult(state);
        }
    }
}
