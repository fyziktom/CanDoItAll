using Bunit;
using CanDoItAll.Components.BaseLib;
using Microsoft.AspNetCore.Components.Web;

namespace CanDoItAll.Tests.Components;

public sealed class SummaryTileTests
{
    [Fact]
    public void Summary_tile_renders_inline_helper_text_by_default()
    {
        using var context = new TestContext();

        var cut = context.RenderComponent<SummaryTile>(parameters => parameters
            .Add(component => component.Label, "Definitions")
            .Add(component => component.Value, "8")
            .Add(component => component.HelperText, "Persisted process definitions."));

        Assert.Contains("Persisted process definitions.", cut.Markup);
        Assert.Empty(cut.FindAll("button[aria-label='Show help for Definitions']"));
    }

    [Fact]
    public void Summary_tile_renders_compact_help_toggle_when_tooltip_mode_is_enabled()
    {
        using var context = new TestContext();

        var cut = context.RenderComponent<SummaryTile>(parameters => parameters
            .Add(component => component.Label, "Definitions")
            .Add(component => component.Value, "8")
            .Add(component => component.HelperText, "Persisted process definitions.")
            .Add(component => component.ShowHelperTextAsTooltip, true));

        Assert.DoesNotContain("cda-summary-tile__helper", cut.Markup, StringComparison.Ordinal);
        Assert.NotNull(cut.Find("button[aria-label='Show help for Definitions']"));
    }

    [Fact]
    public void Summary_tile_tooltip_mode_opens_help_on_hover()
    {
        using var context = new TestContext();

        var cut = context.RenderComponent<SummaryTile>(parameters => parameters
            .Add(component => component.Label, "Definitions")
            .Add(component => component.Value, "8")
            .Add(component => component.HelperText, "Persisted process definitions.")
            .Add(component => component.ShowHelperTextAsTooltip, true));

        Assert.DoesNotContain("Persisted process definitions.", cut.Markup);

        cut.Find(".pf-help-popover")
            .TriggerEvent("onmouseenter", new MouseEventArgs());

        cut.WaitForAssertion(() => Assert.Contains("Persisted process definitions.", cut.Markup));
    }

    [Fact]
    public void Summary_tile_badge_mode_renders_compact_single_row_shell()
    {
        using var context = new TestContext();

        var cut = context.RenderComponent<SummaryTile>(parameters => parameters
            .Add(component => component.Label, "Definitions")
            .Add(component => component.Value, "8")
            .Add(component => component.HelperText, "Persisted process definitions.")
            .Add(component => component.ShowHelperTextAsTooltip, true)
            .Add(component => component.Badge, true));

        var tile = cut.Find(".cda-summary-tile");
        Assert.Contains("cda-summary-tile--badge", tile.ClassName, StringComparison.Ordinal);
        Assert.NotNull(cut.Find(".cda-summary-tile__row"));
        Assert.NotNull(cut.Find("button[aria-label='Show help for Definitions']"));
    }
}
