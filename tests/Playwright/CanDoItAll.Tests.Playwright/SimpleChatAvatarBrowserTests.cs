using Microsoft.Playwright;
using Xunit.Sdk;

namespace CanDoItAll.Tests.Playwright;

public sealed class SimpleChatAvatarBrowserTests {
    [Theory]
    [Trait("Category", "ExternalRealSharedProviderUi")]
    [InlineData("CANDOITALL_REAL_SHARED_URL", "source")]
    [InlineData("CANDOITALL_REAL_CLIENT_URL", "client")]
    public async Task Avatar_matches_catalog_editor_picker_and_persists_selection_and_reset(string urlVariable, string label) {
        var url = Required(urlVariable);
        var evidence = Required("CANDOITALL_AVATAR_UI_EVIDENCE");
        Directory.CreateDirectory(evidence);
        using var playwright = await Microsoft.Playwright.Playwright.CreateAsync();
        await using var browser = await playwright.Chromium.LaunchAsync(new() { Headless = true, Channel = "chrome" });
        await using var context = await browser.NewContextAsync(new() { ViewportSize = new() { Width = 1920, Height = 1080 } });
        var page = await context.NewPageAsync();
        page.SetDefaultTimeout(45_000);
        await page.GotoAsync(url, new() { WaitUntil = WaitUntilState.NetworkIdle });
        await page.GetByTestId("database-startup-modal").WaitForAsync();
        await page.GetByTestId("database-startup-continue").ClickAsync();
        await page.GetByTestId("database-startup-modal").WaitForAsync(new() { State = WaitForSelectorState.Detached });
        await NavigateToDefinitionsAsync(page, url, evidence, label);
        var firstCardId = await page.Locator("article[data-testid^='llm-chat-definition-']").First.GetAttributeAsync("data-testid");
        var firstCard = page.GetByTestId(firstCardId!);
        var originalAvatar = await firstCard.Locator("img").GetAttributeAsync("src");
        await firstCard.GetByRole(AriaRole.Button, new() { Name = "Edit", Exact = true }).ClickAsync();
        await AssertPreviewsAsync(page, originalAvatar!);
        await page.ScreenshotAsync(new() { Path = Path.Combine(evidence, $"{label}-existing-picker.png") });
        await page.GetByTestId("llm-chat-definition-avatar-close").ClickAsync();
        await page.GetByTestId("llm-chat-definition-editor-dialog").GetByRole(AriaRole.Button, new() { Name = "Cancel", Exact = true }).ClickAsync();

        var name = $"Avatar verification {label} {Guid.NewGuid():N}";
        await page.GetByTestId("llm-chat-definition-create").ClickAsync();
        await page.GetByTestId("llm-chat-definition-name").FillAsync(name);
        await page.GetByTestId("llm-chat-definition-avatar-open").ClickAsync();
        var selectedAvatar = await page.GetByTestId("llm-chat-definition-avatar-option-2").Locator("img").GetAttributeAsync("src");
        await page.GetByTestId("llm-chat-definition-avatar-option-2").ClickAsync();
        await page.GetByTestId("llm-chat-definition-avatar-close").ClickAsync();
        await page.GetByTestId("llm-chat-definition-tab-runtime").ClickAsync();
        await page.GetByTestId("llm-chat-definition-provider").SelectOptionAsync(new SelectOptionValue { Label = "UI Shared OpenAI Chat" });
        await page.GetByTestId("llm-chat-definition-editor-save").ClickAsync();
        await page.GetByTestId("llm-chat-definition-editor-dialog").WaitForAsync(new() { State = WaitForSelectorState.Detached });
        var card = page.Locator("article[data-testid^='llm-chat-definition-']").Filter(new() { HasTextString = name });
        await Assertions.Expect(card.Locator("img")).ToHaveAttributeAsync("src", selectedAvatar!);
        await NavigateToDefinitionsAsync(page, url, evidence, label);
        await card.GetByRole(AriaRole.Button, new() { Name = "Edit", Exact = true }).ClickAsync();
        await AssertPreviewsAsync(page, selectedAvatar!);
        await page.GetByTestId("llm-chat-definition-avatar-close").ClickAsync();
        await page.ScreenshotAsync(new() { Path = Path.Combine(evidence, $"{label}-selected-editor.png") });
        await page.GetByTestId("llm-chat-definition-avatar-clear").ClickAsync();
        await Assertions.Expect(page.GetByTestId("llm-chat-definition-avatar-summary")).ToContainTextAsync("Default generated avatar");
        var defaultAvatar = await page.GetByTestId("llm-chat-definition-avatar-summary").Locator("img").GetAttributeAsync("src");
        await page.GetByTestId("llm-chat-definition-editor-save").ClickAsync();
        await page.GetByTestId("llm-chat-definition-editor-dialog").WaitForAsync(new() { State = WaitForSelectorState.Detached });
        await NavigateToDefinitionsAsync(page, url, evidence, label);
        await Assertions.Expect(card.Locator("img")).ToHaveAttributeAsync("src", defaultAvatar!);
        await page.ScreenshotAsync(new() { Path = Path.Combine(evidence, $"{label}-catalog-reset.png") });
        await card.GetByRole(AriaRole.Button, new() { Name = "Edit", Exact = true }).ClickAsync();
        await AssertPreviewsAsync(page, defaultAvatar!);
        Assert.False(await page.Locator("#blazor-error-ui").IsVisibleAsync());
    }

    private static async Task NavigateToDefinitionsAsync(IPage page, string url, string evidence, string label) {
        var response = await page.GotoAsync($"{url}/agents?tab=simple-chats&simpleChatView=definitions",
            new() { WaitUntil = WaitUntilState.NetworkIdle });
        Assert.NotNull(response);
        Assert.True(response.Ok);
        await page.GetByRole(AriaRole.Tablist, new() { Name = "Open workspace tabs", Exact = true }).WaitForAsync();
        await page.ScreenshotAsync(new() { Path = Path.Combine(evidence, $"{label}-latest-load.png") });
        await page.GetByTestId("llm-chat-definition-create").WaitForAsync();
    }

    private static async Task AssertPreviewsAsync(IPage page, string expectedUrl) {
        await page.GetByTestId("llm-chat-definition-editor-dialog").WaitForAsync();
        await Assertions.Expect(page.GetByTestId("llm-chat-definition-avatar-summary").Locator("img"))
            .ToHaveAttributeAsync("src", expectedUrl);
        await page.GetByTestId("llm-chat-definition-avatar-open").ClickAsync();
        await Assertions.Expect(page.GetByTestId("llm-chat-definition-avatar-dialog").Locator("img").First)
            .ToHaveAttributeAsync("src", expectedUrl);
    }

    private static string Required(string name) => Environment.GetEnvironmentVariable(name) is { Length: > 0 } value
        ? value : throw SkipException.ForSkip($"Set {name} for the explicit live UI check.");
}
