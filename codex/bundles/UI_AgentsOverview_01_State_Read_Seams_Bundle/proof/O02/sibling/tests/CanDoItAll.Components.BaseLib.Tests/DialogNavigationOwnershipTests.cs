using Microsoft.AspNetCore.Components;

namespace CanDoItAll.Components.BaseLib.Tests;

public sealed class DialogNavigationOwnershipTests {
    [Fact]
    public async Task Default_query_navigation_preserves_existing_close_behavior() {
        var navigation = new TestNavigation();
        using var service = new DialogService(navigation);
        var result = service.OpenAsync("Default", _ => builder => builder.AddContent(0, "Content"));
        navigation.NavigateTo("/agents?tab=providers");
        Assert.Empty(service.Dialogs);
        Assert.Null(await result);
    }

    [Theory]
    [InlineData("/agents?tab=providers")]
    [InlineData("/agents#details")]
    public void Opted_in_same_page_navigation_preserves_dialog_identity(string location) {
        var navigation = new TestNavigation();
        using var service = new DialogService(navigation);
        using var owner = service.PreserveDialogsOnSamePageNavigation();
        _ = service.OpenAsync("Independent", _ => builder => builder.AddContent(0, "Content"));
        var reference = Assert.Single(service.Dialogs);
        navigation.NavigateTo(location);
        Assert.Same(reference, Assert.Single(service.Dialogs));
        Assert.False(reference.Result.IsCompleted);
    }

    [Fact]
    public void Path_navigation_still_closes_dialogs_with_an_active_owner() {
        var navigation = new TestNavigation();
        using var service = new DialogService(navigation);
        using var owner = service.PreserveDialogsOnSamePageNavigation();
        _ = service.OpenAsync("Independent", _ => builder => builder.AddContent(0, "Content"));
        navigation.NavigateTo("/projects");
        Assert.Empty(service.Dialogs);
    }

    [Fact]
    public void Last_owner_disposal_restores_default_behavior_and_is_idempotent() {
        var navigation = new TestNavigation();
        using var service = new DialogService(navigation);
        var first = service.PreserveDialogsOnSamePageNavigation();
        var second = service.PreserveDialogsOnSamePageNavigation();
        _ = service.OpenAsync("Independent", _ => builder => builder.AddContent(0, "Content"));
        first.Dispose();
        first.Dispose();
        navigation.NavigateTo("/agents?tab=providers");
        Assert.Single(service.Dialogs);
        second.Dispose();
        navigation.NavigateTo("/agents?tab=overview");
        Assert.Empty(service.Dialogs);
    }

    [Fact]
    public async Task Owner_cancellation_closes_only_its_reference_during_same_page_navigation() {
        var navigation = new TestNavigation();
        using var service = new DialogService(navigation);
        using var owner = service.PreserveDialogsOnSamePageNavigation();
        using var cancellation = new CancellationTokenSource();
        _ = service.OpenAsync("Independent", _ => builder => builder.AddContent(0, "Content"));
        var independent = Assert.Single(service.Dialogs);
        var pending = service.OpenAsync("Owned", _ => builder => builder.AddContent(0, "Content"), cancellationToken: cancellation.Token);
        cancellation.Cancel();
        navigation.NavigateTo("/agents?usageScope=simple-chats");
        Assert.Same(independent, Assert.Single(service.Dialogs));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => pending);
    }

    [Fact]
    public void Disposed_service_rejects_new_navigation_owners() {
        var service = new DialogService(new TestNavigation());
        using var owner = service.PreserveDialogsOnSamePageNavigation();
        service.Dispose();
        Assert.Throws<ObjectDisposedException>(() => service.PreserveDialogsOnSamePageNavigation());
    }

    private sealed class TestNavigation : NavigationManager {
        public TestNavigation() {
            Initialize("http://localhost/", "http://localhost/agents");
        }

        protected override void NavigateToCore(string uri, bool forceLoad) {
            Uri = ToAbsoluteUri(uri).ToString();
            NotifyLocationChanged(false);
        }
    }
}
