using Bunit;
using CanDoItAll.Collaboration.UI;
using CanDoItAll.Modules.Collaboration;
using CanDoItAll.Modules.Collaboration.Pages;
using CanDoItAll.Tests.Support;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;

namespace CanDoItAll.Tests.Components.Collaboration;

public sealed class CollaborationHostTests {
    [Fact]
    public async Task Routed_host_uses_real_owner_for_create_reply_read_and_filtering() {
        await using var harness = await ComponentTestHarness.CreateAsync();
        var cut = harness.Context.Render<CollaborationHomePage>();
        cut.WaitForAssertion(() => Assert.Equal("ready", cut.Find("[data-testid=collaboration-workspace]").GetAttribute("data-phase")));
        await cut.InvokeAsync(() => {
            cut.Find("[data-testid=collaboration-thread-subject]").Change("Host-created thread");
            cut.Find("[data-testid=collaboration-thread-message]").Change("Persisted first message");
        });
        await cut.InvokeAsync(() => cut.Find("[data-testid=collaboration-create-form]").SubmitAsync());
        cut.WaitForAssertion(() => Assert.Contains("Host-created thread", cut.Find("[data-testid=collaboration-thread-title]").TextContent));
        var owner = harness.Context.Services.GetRequiredService<CollaborationService>();
        var workspace = await owner.GetWorkspaceAsync();
        var id = Assert.Single(workspace.Threads).ThreadId;
        Assert.True(workspace.SelectedThread!.IsUnread);
        await cut.InvokeAsync(() => cut.Find("[data-testid=collaboration-mark-read]").ClickAsync(new MouseEventArgs()));
        Assert.False((await owner.GetWorkspaceAsync(id)).SelectedThread!.IsUnread);
        await cut.InvokeAsync(() => cut.Find("[data-testid=collaboration-reply-message]").Change("Persisted local reply"));
        await cut.InvokeAsync(() => cut.Find("[data-testid=collaboration-reply-form]").SubmitAsync());
        var detail = (await owner.GetWorkspaceAsync(id)).SelectedThread!;
        Assert.Equal(2, detail.Messages.Count);
        Assert.False(detail.IsUnread);
        await cut.InvokeAsync(() => cut.Find("[data-testid=collaboration-filter-unread]").ClickAsync(new MouseEventArgs()));
        Assert.Empty(cut.FindAll("[data-testid=collaboration-thread-title]"));
        await cut.InvokeAsync(() => cut.Find("[data-testid=collaboration-refresh]").ClickAsync(new MouseEventArgs()));
        Assert.Empty(cut.FindAll("[data-testid=collaboration-thread-title]"));
        await cut.InvokeAsync(() => cut.FindComponent<CollaborationWorkspaceSurface>().Instance.View.SetSectionAsync(CollaborationSection.Threads));
        Assert.Single(cut.FindAll("[data-testid=collaboration-thread-item]"));
    }

    [Fact]
    public async Task Real_host_same_target_parameters_preserve_validation_and_target_switch_discards_reply() {
        await using var harness = await ComponentTestHarness.CreateAsync();
        var owner = harness.Context.Services.GetRequiredService<CollaborationService>();
        var first = await owner.CreateThreadAsync(CollaborationService.CreateManualThreadRequest(new() { Subject = "First", MessageBody = "One" }));
        var second = await owner.CreateThreadAsync(CollaborationService.CreateManualThreadRequest(new() { Subject = "Second", MessageBody = "Two" }));
        var navigation = harness.Context.Services.GetRequiredService<NavigationManager>();
        navigation.NavigateTo($"/collaboration?threadId={first.Value:D}");
        var cut = harness.Context.Render<CollaborationHomePage>();
        cut.WaitForAssertion(() => Assert.Equal("First", cut.Find("[data-testid=collaboration-thread-title]").TextContent));
        var view = cut.FindComponent<CollaborationWorkspaceSurface>().Instance.View;
        var reply = view.Reply;
        await cut.InvokeAsync(() => cut.Find("[data-testid=collaboration-reply-form]").SubmitAsync());
        Assert.NotEmpty(reply.Context.GetValidationMessages());
        await cut.InvokeAsync(() => cut.Render());
        Assert.Same(reply, view.Reply);
        Assert.NotEmpty(reply.Context.GetValidationMessages());
        await cut.InvokeAsync(() => cut.Find("[data-testid=collaboration-reply-message]").Change("Unsent on First"));
        await cut.InvokeAsync(() => navigation.NavigateTo($"/collaboration?threadId={second.Value:D}"));
        cut.WaitForAssertion(() => Assert.Equal("Second", cut.Find("[data-testid=collaboration-thread-title]").TextContent));
        Assert.NotSame(reply, view.Reply);
        Assert.Empty(view.Reply.Model.MessageBody);
    }
}
