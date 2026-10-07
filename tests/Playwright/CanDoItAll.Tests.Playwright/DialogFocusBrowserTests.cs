using System.Text.Json;
using Microsoft.Playwright;

namespace CanDoItAll.Tests.Playwright.Smoke;

public sealed class DialogFocusBrowserTests {
    [Fact]
    [Trait("Category", "Playwright")]
    [Trait("Category", "HostPlatform")]
    public async Task Dialog_script_preserves_disabled_opener_and_unrelated_focus_during_retirement() {
        await using var host = new DataSourcesBrowserHost();
        await host.StartAsync();
        using var playwright = await Microsoft.Playwright.Playwright.CreateAsync();
        await using var browser = await playwright.Chromium.LaunchAsync(new() { Headless = true });
        await using var context = await browser.NewContextAsync();
        var page = await context.NewPageAsync();
        var oracle = CrmHrBrowserOracle.Attach(page);
        await oracle.NavigateAsync(host.BaseUrl + "/settings");
        await page.GetByTestId("defaults-name").WaitForAsync();
        var resultJson = await page.EvaluateAsync<JsonElement>("""
            async () => {
                const dialogModule = await import('/_content/CanDoItAll.Components.BaseLib/Components/Modals/Dialog.razor.js');
                const frame = () => new Promise(resolve => requestAnimationFrame(resolve));
                const root = document.createElement('div');
                const opener = document.createElement('button');
                const first = document.createElement('dialog');
                const second = document.createElement('dialog');
                const firstInput = document.createElement('input');
                const secondInput = document.createElement('input');
                const ids = [crypto.randomUUID(), crypto.randomUUID()];
                const callback = { invokeMethodAsync: () => Promise.resolve() };
                first.append(firstInput);
                second.append(secondInput);
                root.append(opener, first, second);
                document.body.append(root);
                const originalOverflow = document.body.style.overflow;
                try {
                    opener.focus();
                    opener.disabled = true;
                    const lostFocusWhileDisabled = document.activeElement === document.body;
                    opener.disabled = false;
                    dialogModule.openDialog(first, ids[0], callback);
                    await frame();
                    dialogModule.closeDialog(ids[0]);
                    const openerRestored = document.activeElement === opener;
                    dialogModule.openDialog(first, ids[0], callback);
                    await frame();
                    dialogModule.openDialog(second, ids[1], callback);
                    await frame();
                    first.remove();
                    await frame();
                    const unrelatedPreserved = second.open && document.activeElement === secondInput
                        && document.body.style.overflow === 'hidden';
                    dialogModule.closeDialog(ids[1]);
                    root.append(first);
                    opener.focus();
                    dialogModule.openDialog(first, ids[0], callback);
                    dialogModule.closeDialog(ids[0]);
                    await frame();
                    return {
                        LostFocusWhileDisabled: lostFocusWhileDisabled,
                        OpenerRestored: openerRestored,
                        UnrelatedPreserved: unrelatedPreserved,
                        LateFrameIgnored: !first.open && document.activeElement === opener,
                        OverflowRestored: document.body.style.overflow === originalOverflow
                    };
                } finally {
                    dialogModule.closeDialog(ids[0]);
                    dialogModule.closeDialog(ids[1]);
                    root.remove();
                }
            }
            """);
        var result = resultJson.Deserialize<FocusResult>();
        Assert.NotNull(result);
        Assert.True(result.LostFocusWhileDisabled);
        Assert.True(result.OpenerRestored);
        Assert.True(result.UnrelatedPreserved);
        Assert.True(result.LateFrameIgnored);
        Assert.True(result.OverflowRestored);
    }

    private sealed record FocusResult(bool LostFocusWhileDisabled, bool OpenerRestored,
        bool UnrelatedPreserved, bool LateFrameIgnored, bool OverflowRestored);
}
