using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;

namespace CanDoItAll.Tests.Components.Shell;

public sealed class EmbeddedBrowserTests
{
    [Fact]
    public void Original_public_assembly_forwards_the_actual_neutral_renderer() {
        var forwarded = Type.GetType("CanDoItAll.AppComponents.EmbeddedBrowser, CanDoItAll.AppComponents", throwOnError: true);
        Assert.Same(typeof(EmbeddedBrowser), forwarded);
        Assert.Equal("CanDoItAll.AppComponents.EmbeddedBrowser", forwarded!.Assembly.GetName().Name);
        Assert.DoesNotContain(forwarded.Assembly.GetReferencedAssemblies(), reference =>
            reference.Name!.StartsWith("CanDoItAll.Modules.", StringComparison.Ordinal));
    }

    [Fact]
    public void Embeddable_source_renders_a_restricted_full_height_frame()
    {
        using var context = new BunitContext();
        var source = new Uri(new Uri(context.Services.GetRequiredService<NavigationManager>().BaseUri), "/_dev/runtime");

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

    [Theory]
    [InlineData("http://localhost:5149/", true)]
    [InlineData("http://127.0.0.1:5149/", true)]
    [InlineData("https://example.com/", false)]
    public void Only_a_local_application_on_another_origin_keeps_its_own_origin(string url, bool expectOwnOrigin)
    {
        using var context = new BunitContext();

        var cut = context.Render<EmbeddedBrowser>(parameters => parameters
            .Add(component => component.Source, new Uri(url))
            .Add(component => component.Title, "Local application"));

        var sandbox = cut.Find("iframe").GetAttribute("sandbox") ?? string.Empty;
        Assert.Contains("allow-scripts", sandbox, StringComparison.Ordinal);
        Assert.Equal(expectOwnOrigin, sandbox.Contains("allow-same-origin", StringComparison.Ordinal));
        Assert.DoesNotContain("allow-top-navigation", sandbox, StringComparison.Ordinal);
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
