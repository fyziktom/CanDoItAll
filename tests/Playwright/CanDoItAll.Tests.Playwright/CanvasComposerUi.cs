using System.Text.RegularExpressions;
using Microsoft.Playwright;

namespace CanDoItAll.Tests.Playwright;

internal static class CanvasComposerUi {
    internal static async Task ShowFieldAsync(ILocator composer, ILocator field) {
        await Assertions.Expect(composer).ToBeVisibleAsync();
        if (await field.IsVisibleAsync()) {
            return;
        }
        var disclosure = field.Locator("xpath=ancestor::details[1]");
        if (await disclosure.CountAsync() == 1) {
            await disclosure.Locator("summary").ClickAsync();
            await Assertions.Expect(field).ToBeVisibleAsync();
            return;
        }
        var sections = await composer.Locator(".cw-canvas-composer__section").CountAsync();
        var back = composer.GetByRole(AriaRole.Button, new() { Name = "Back", Exact = true });
        for (var index = 0; index < sections && await back.IsEnabledAsync(); index++) {
            await back.ClickAsync();
        }
        var status = composer.Locator(".cw-canvas-composer__step-status");
        for (var index = 0; index < sections && !await field.IsVisibleAsync(); index++) {
            var previous = await status.InnerTextAsync();
            await composer.GetByRole(AriaRole.Button, new() { NameRegex = new Regex("^(Next|Skip)$") }).ClickAsync();
            await Assertions.Expect(status).Not.ToHaveTextAsync(previous);
        }
        await Assertions.Expect(field).ToBeVisibleAsync();
    }
}
