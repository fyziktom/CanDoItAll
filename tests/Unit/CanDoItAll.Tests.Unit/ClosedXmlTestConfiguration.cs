using System.Runtime.CompilerServices;
using ClosedXML.Excel;
using ClosedXML.Graphics;

namespace CanDoItAll.Tests.Unit;

internal static class ClosedXmlTestConfiguration
{
    private const string FallbackFontResourceName =
        "ClosedXML.Graphics.Fonts.CarlitoBare-Regular.ttf";

    [ModuleInitializer]
    internal static void ConfigureGraphicEngine()
    {
        using var fallbackFont = typeof(DefaultGraphicEngine).Assembly.GetManifestResourceStream(
            FallbackFontResourceName) ?? throw new InvalidOperationException(
            $"ClosedXML fallback font resource '{FallbackFontResourceName}' was not found.");
        LoadOptions.DefaultGraphicEngine = DefaultGraphicEngine.CreateOnlyWithFonts(fallbackFont);
    }
}
