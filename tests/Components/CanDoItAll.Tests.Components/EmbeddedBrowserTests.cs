using Bunit;

namespace CanDoItAll.Tests.Components;

public sealed class EmbeddedBrowserTests
{
    [Fact]
    public void Embeddable_source_renders_a_restricted_full_height_frame()
    {
        using var context = new BunitContext();
        var source = new Uri("http://127.0.0.1:5032/_dev/runtime");

        var cut = context.Render<EmbeddedBrowser>(parameters => parameters
            .Add(component => component.Source, source)
            .Add(component => component.Title, "Runtime health")
            .Add(component => component.DataTestId, "runtime-browser"));

        var host = cut.Find("[data-testid='runtime-browser']");
        Assert.Equal("true", host.GetAttribute("data-embeddable"));
        Assert.Contains("height:100%", host.GetAttribute("style"), StringComparison.Ordinal);
        Assert.Contains("min-height:0", host.GetAttribute("style"), StringComparison.Ordinal);

        var frame = cut.Find("iframe");
        Assert.Equal(source.AbsoluteUri, frame.GetAttribute("src"));
        var sandbox = frame.GetAttribute("sandbox");
        Assert.Equal("allow-forms allow-popups allow-scripts", sandbox);
        Assert.DoesNotContain("allow-same-origin", sandbox, StringComparison.Ordinal);
        Assert.DoesNotContain("allow-top-navigation", sandbox, StringComparison.Ordinal);
        Assert.Equal("Runtime health", frame.GetAttribute("title"));
    }

    [Fact]
    public void Blocked_source_renders_an_explicit_native_browser_link()
    {
        using var context = new BunitContext();
        var source = new Uri("https://google.com/");

        var cut = context.Render<EmbeddedBrowser>(parameters => parameters
            .Add(component => component.Source, source)
            .Add(component => component.Title, "Google")
            .Add(component => component.CanEmbed, false)
            .Add(component => component.EmbedUnavailableReason, "Google blocks embedded browsing."));

        Assert.Empty(cut.FindAll("iframe"));
        Assert.Contains("Google blocks embedded browsing.", cut.Markup, StringComparison.Ordinal);

        var externalLink = cut.Find("[data-testid='embedded-browser-open-browser']");
        Assert.Equal("a", externalLink.LocalName);
        Assert.Equal(source.AbsoluteUri, externalLink.GetAttribute("href"));
        Assert.Equal("_blank", externalLink.GetAttribute("target"));
        Assert.Equal("noopener noreferrer", externalLink.GetAttribute("rel"));
    }
}
