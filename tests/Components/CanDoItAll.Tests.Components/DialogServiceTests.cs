using Bunit;
using CanDoItAll.Components.BaseLib;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;
using Microsoft.Extensions.DependencyInjection;

namespace CanDoItAll.Tests.Components;

public sealed class DialogServiceTests
{
    [Fact]
    public void AddCanDoItAllBaseLib_registers_overlay_services()
    {
        using var context = new TestContext();
        context.Services.AddCanDoItAllBaseLib();

        Assert.NotNull(context.Services.GetRequiredService<DialogService>());
        Assert.NotNull(context.Services.GetRequiredService<TooltipService>());
        Assert.NotNull(context.Services.GetRequiredService<NotificationService>());
    }

    [Fact]
    public async Task DialogHost_renders_fragment_dialog_and_returns_object_result()
    {
        using var context = CreateContext();
        var service = context.Services.GetRequiredService<DialogService>();
        var host = context.RenderComponent<DialogHost>();

        var resultTask = service.OpenAsync(
            "Confirm result",
            dialog => builder =>
            {
                builder.OpenElement(0, "button");
                builder.AddAttribute(1, "data-testid", "return-object");
                builder.AddAttribute(2, "onclick", EventCallback.Factory.Create(this, () => dialog.CloseAsync(new DialogReturn("approved", 7))));
                builder.AddContent(3, "Return object");
                builder.CloseElement();
            },
            new DialogOptions
            {
                TestId = "result-dialog",
                Subtitle = "Returns a typed object to the caller."
            });

        host.WaitForAssertion(() => Assert.Contains("Confirm result", host.Markup, StringComparison.Ordinal));

        host.Find("[data-testid='return-object']").Click();

        var result = await resultTask.WaitAsync(TimeSpan.FromSeconds(2));
        var typedResult = Assert.IsType<DialogReturn>(result);
        Assert.Equal("approved", typedResult.Status);
        Assert.Equal(7, typedResult.Count);
        Assert.Empty(service.Dialogs);
    }

    [Fact]
    public void DialogHost_maps_modal_size_and_backdrop_options()
    {
        using var context = CreateContext();
        var service = context.Services.GetRequiredService<DialogService>();
        var host = context.RenderComponent<DialogHost>();

        _ = service.OpenAsync(
            "Wide review",
            dialog => builder => builder.AddContent(0, "Wide content"),
            new DialogOptions
            {
                Size = ModalSize.Wide,
                CloseOnBackdrop = false,
                TestId = "wide-dialog"
            });

        host.WaitForAssertion(() =>
        {
            var dialog = host.Find("[data-testid='wide-dialog'] section");
            Assert.Contains("max-w-[min(88rem,100%)]", dialog.ClassList);
        });

        host.Find("[data-testid='wide-dialog'] > div").Click();

        Assert.Single(service.Dialogs);
    }

    [Fact]
    public async Task DialogHost_closes_topmost_dialog_from_service()
    {
        using var context = CreateContext();
        var service = context.Services.GetRequiredService<DialogService>();
        context.RenderComponent<DialogHost>();

        var first = service.OpenAsync("First", dialog => builder => builder.AddContent(0, "First content"));
        var second = service.OpenAsync("Second", dialog => builder => builder.AddContent(0, "Second content"));

        await service.CloseAsync("top-result");

        Assert.Single(service.Dialogs);
        Assert.Equal("top-result", await second.WaitAsync(TimeSpan.FromSeconds(2)));
        Assert.False(first.IsCompleted);
    }

    [Fact]
    public async Task DialogHost_cascades_dialog_reference_to_component_content()
    {
        using var context = CreateContext();
        var service = context.Services.GetRequiredService<DialogService>();
        var host = context.RenderComponent<DialogHost>();

        var resultTask = service.OpenAsync<DialogReferenceConsumer>(
            "Component dialog",
            options: new DialogOptions { TestId = "component-dialog" });

        host.WaitForAssertion(() => Assert.NotNull(host.Find("[data-testid='component-close']")));
        host.Find("[data-testid='component-close']").Click();

        Assert.Equal("component-result", await resultTask.WaitAsync(TimeSpan.FromSeconds(2)));
    }

    [Fact]
    public async Task DialogService_rejects_non_component_types()
    {
        using var context = CreateContext();
        var service = context.Services.GetRequiredService<DialogService>();

        await Assert.ThrowsAsync<ArgumentException>(() => service.OpenAsync("Invalid", typeof(string)));
    }

    private static TestContext CreateContext()
    {
        var context = new TestContext();
        context.Services.AddCanDoItAllBaseLib();
        return context;
    }

    private sealed record DialogReturn(string Status, int Count);

    private sealed class DialogReferenceConsumer : ComponentBase
    {
        [CascadingParameter]
        public DialogReference Dialog { get; set; } = default!;

        protected override void BuildRenderTree(RenderTreeBuilder builder)
        {
            builder.OpenElement(0, "button");
            builder.AddAttribute(1, "data-testid", "component-close");
            builder.AddAttribute(2, "onclick", EventCallback.Factory.Create(this, () => Dialog.CloseAsync("component-result")));
            builder.AddContent(3, "Close component dialog");
            builder.CloseElement();
        }
    }
}
