using CanDoItAll.Components.CanvasLib;

namespace CanDoItAll.Tests.Components;

public sealed class CanvasThemeTokenPackTests
{
    [Fact]
    public void Default_pack_exports_the_current_workbench_css_variables()
    {
        var pack = CanvasThemeTokenPack.Default;
        var variables = pack.ToCssVariables();

        Assert.Equal("canvas-sunrise", pack.ThemeKey);
        Assert.Equal("28px", variables["--cw-stage-radius"]);
        Assert.Equal("#8b5cf6", variables["--cw-accent-purple-start"]);
        Assert.Equal("#0f172a", variables["--cw-text"]);
    }

    [Fact]
    public void Default_pack_builds_inline_style_preview_data()
    {
        var pack = CanvasThemeTokenPack.Default;

        Assert.Contains("--cw-stage-radius:28px;", pack.ToInlineStyle(), StringComparison.Ordinal);
        Assert.Contains(pack.BuildPreviewSwatches(), swatch => swatch.Label == "Backdrop start");
        Assert.Contains(pack.BuildMetrics(), metric => metric.Contains("Stage radius", StringComparison.Ordinal));
    }
}


