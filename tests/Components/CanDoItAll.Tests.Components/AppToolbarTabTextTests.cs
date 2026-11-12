using Bunit;
using CanDoItAll.AppComponents;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;

namespace CanDoItAll.Tests.Components;

public sealed class AppToolbarTabTextTests
{
    [Fact]
    public void Disposing_the_only_registered_tab_text_clears_it()
    {
        using var context = CreateContext();
        var toolbarState = context.Services.GetRequiredService<AppToolbarState>();

        var cut = context.Render<AppToolbarTabText>(parameters => parameters
            .Add(p => p.Text, "Overview"));

        Assert.Equal("Overview", toolbarState.TabText);

        cut.Instance.Dispose();

        Assert.Null(toolbarState.TabText);
    }

    [Fact]
    public void Disposing_a_superseded_tab_text_registration_does_not_clear_the_newer_value()
    {
        using var context = CreateContext();
        var toolbarState = context.Services.GetRequiredService<AppToolbarState>();

        var first = context.Render<AppToolbarTabText>(parameters => parameters
            .Add(p => p.Text, "Overview"));
        var second = context.Render<AppToolbarTabText>(parameters => parameters
            .Add(p => p.Text, "Workforce"));

        Assert.Equal("Workforce", toolbarState.TabText);

        first.Instance.Dispose();

        Assert.Equal("Workforce", toolbarState.TabText);

        second.Instance.Dispose();

        Assert.Null(toolbarState.TabText);
    }

    private static BunitContext CreateContext()
    {
        var context = new BunitContext();
        context.Services.AddScoped<AppToolbarState>();
        return context;
    }
}
