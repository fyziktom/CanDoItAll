using Microsoft.Playwright;

namespace CanDoItAll.Tests.Playwright.Smoke;

[Collection(PlaywrightCollection.Name)]
public sealed class PromptGalleryBrowserTests
{
    private const string RailOverflowProbe = """
        () => {
            const rail = document.querySelector('[data-testid="prompt-gallery-filter-rail"]');
            const children = Array.from(rail.children);
            // Resolved row tracks count the grid rows; child tops differ within one row because items are centered.
            const rows = getComputedStyle(rail).gridTemplateRows.trim().split(/\s+/).filter(Boolean).length;
            const railBox = rail.getBoundingClientRect();
            const overflowing = children
                .filter(child => {
                    const box = child.getBoundingClientRect();
                    return box.right > railBox.right + 1 || child.scrollWidth > child.clientWidth + 1;
                })
                .map(child => child.getAttribute('data-testid') ?? child.tagName);
            return {
                template: getComputedStyle(rail).gridTemplateColumns,
                rows,
                railOverflow: rail.scrollWidth > rail.clientWidth + 1,
                overflowing
            };
        }
        """;

    private readonly PlaywrightAppFixture fixture;

    public PromptGalleryBrowserTests(PlaywrightAppFixture fixture)
    {
        this.fixture = fixture;
    }

    [Fact]
    public async Task Gallery_creates_saves_finalizes_and_reopens_an_item_through_the_real_host()
    {
        var artifactsDir = ArtifactsDirectory();
        await using var context = await NewContextAsync();
        var page = await context.NewPageAsync();
        var pageErrors = new List<string>();
        page.PageError += (_, message) => pageErrors.Add(message);
        var title = $"Browser gallery proof {Guid.NewGuid():N}";

        await OpenGalleryAsync(page, "/prompt-gallery");
        await page.GetByTestId("prompt-gallery-filter-rail").WaitForAsync();
        await page.ScreenshotAsync(new PageScreenshotOptions { Path = Path.Combine(artifactsDir, "01-gallery.png") });

        // A committed filter must survive the editor round trip instead of being reset by a list remount.
        await page.GetByTestId("prompt-gallery-status-filter").SelectOptionAsync("Draft");
        await page.GetByRole(AriaRole.Button, new() { Name = "New item", Exact = true }).ClickAsync();
        var editor = page.GetByTestId("prompt-gallery-item-editor");
        await editor.WaitForAsync();
        await page.GetByTestId("prompt-gallery-editor-save").ClickAsync();
        await page.GetByTestId("prompt-gallery-editor-validation").WaitForAsync();

        await page.GetByTestId("prompt-gallery-editor-title").FillAsync(title);
        await page.GetByTestId("prompt-gallery-editor-content").FillAsync("Summarize the supplied research in five bullets.");
        await page.GetByTestId("prompt-gallery-editor-save").ClickAsync();
        await page.GetByText("Prompt draft saved").First.WaitForAsync();
        await page.GetByTestId("prompt-gallery-editor-archive").WaitForAsync();
        await page.GetByText("Saved Gallery item").WaitForAsync();
        await page.ScreenshotAsync(new PageScreenshotOptions { Path = Path.Combine(artifactsDir, "02-draft-saved.png") });

        await page.GetByTestId("prompt-gallery-editor-finalize").ClickAsync();
        await page.GetByText("Final version created").First.WaitForAsync();
        await page.GetByTestId("prompt-gallery-editor-versions").WaitForAsync();
        await page.GetByText("Ready for reuse").First.WaitForAsync();
        await page.ScreenshotAsync(new PageScreenshotOptions { Path = Path.Combine(artifactsDir, "03-final-version.png") });

        await page.GetByTestId("prompt-gallery-editor-cancel").ClickAsync();
        await editor.WaitForAsync(new LocatorWaitForOptions { State = WaitForSelectorState.Detached });
        Assert.Equal("Draft", await page.GetByTestId("prompt-gallery-status-filter").InputValueAsync());
        await page.GetByTestId("prompt-gallery-status-filter").SelectOptionAsync("");
        await page.GetByTestId("prompt-gallery-search").FillAsync(title);
        // The debounced title search must have narrowed the list to this item before its badges are inspected;
        // the unfiltered page can already hold other finalized items.
        await Assertions.Expect(page.GetByTestId("prompt-gallery-edit")).ToHaveCountAsync(1);
        var row = page.GetByTestId("prompt-gallery-grid").GetByText(title, new() { Exact = true });
        await row.WaitForAsync();
        await page.GetByTestId("prompt-gallery-grid").GetByText("v1", new() { Exact = true }).WaitForAsync();
        await page.ScreenshotAsync(new PageScreenshotOptions { Path = Path.Combine(artifactsDir, "04-list-refreshed.png") });

        await page.GetByTestId("prompt-gallery-edit").First.ClickAsync();
        await editor.WaitForAsync();
        Assert.Equal(title, await page.GetByTestId("prompt-gallery-editor-title").InputValueAsync());
        var promptId = await page.EvaluateAsync<string>("() => document.querySelector('[data-testid=\"prompt-gallery-editor-versions\"]') ? 'present' : 'missing'");
        Assert.Equal("present", promptId);
        await page.GetByTestId("prompt-gallery-editor-cancel").ClickAsync();
        await editor.WaitForAsync(new LocatorWaitForOptions { State = WaitForSelectorState.Detached });

        Assert.False(await page.Locator("#blazor-error-ui").IsVisibleAsync());
        Assert.Empty(pageErrors);
    }

    [Fact]
    public async Task Missing_promptId_request_shows_a_failed_editor_and_a_valid_request_reopens_the_persisted_item()
    {
        var artifactsDir = ArtifactsDirectory();
        await using var context = await NewContextAsync();
        var page = await context.NewPageAsync();
        var pageErrors = new List<string>();
        page.PageError += (_, message) => pageErrors.Add(message);
        var title = $"Deep link proof {Guid.NewGuid():N}";

        // Persist an item through the real host first, so the valid request targets a genuine identity.
        await OpenGalleryAsync(page, "/prompt-gallery");
        var itemId = await CreateDraftAsync(page, title, "Reopen me through the promptId query.");

        var response = await page.GotoAsync($"{fixture.BaseUrl}/prompt-gallery?promptId={Guid.NewGuid():D}");
        Assert.NotNull(response);
        Assert.True(response!.Ok);
        await PlaywrightAppFixture.CompleteDatabaseStartupAsync(page);
        await page.GetByTestId("prompt-gallery-editor-retry").WaitForAsync();
        Assert.Equal(0, await page.GetByTestId("prompt-gallery-item-editor").CountAsync());
        await page.ScreenshotAsync(new PageScreenshotOptions { Path = Path.Combine(artifactsDir, "07-missing-promptid.png") });
        await page.GetByTestId("prompt-gallery-editor-close").ClickAsync();
        await page.GetByTestId("prompt-gallery-editor-retry").WaitForAsync(new LocatorWaitForOptions { State = WaitForSelectorState.Detached });
        await page.GetByTestId("prompt-gallery-search-list").WaitForAsync();

        // The same page instance must react to a changed promptId with the real item, not a new empty draft.
        response = await page.GotoAsync($"{fixture.BaseUrl}/prompt-gallery?promptId={itemId:D}");
        Assert.NotNull(response);
        Assert.True(response!.Ok);
        await PlaywrightAppFixture.CompleteDatabaseStartupAsync(page);
        var editor = page.GetByTestId("prompt-gallery-item-editor");
        await editor.WaitForAsync();
        await Assertions.Expect(page.GetByTestId("prompt-gallery-editor-title")).ToHaveValueAsync(title);
        await page.GetByText("Saved Gallery item").WaitForAsync();
        Assert.Equal(0, await page.GetByTestId("prompt-gallery-editor-retry").CountAsync());
        await page.ScreenshotAsync(new PageScreenshotOptions { Path = Path.Combine(artifactsDir, "08-valid-promptid.png") });
        await page.GetByTestId("prompt-gallery-editor-cancel").ClickAsync();
        await editor.WaitForAsync(new LocatorWaitForOptions { State = WaitForSelectorState.Detached });

        Assert.False(await page.Locator("#blazor-error-ui").IsVisibleAsync());
        Assert.Empty(pageErrors);
    }

    [Fact]
    public async Task Embedded_chat_picker_inserts_a_compatible_item_and_asks_before_inserting_a_model_restricted_item()
    {
        var artifactsDir = ArtifactsDirectory();
        await using var context = await NewContextAsync();
        var page = await context.NewPageAsync();
        var pageErrors = new List<string>();
        page.PageError += (_, message) => pageErrors.Add(message);
        var compatibleTitle = $"Chat-ready prompt {Guid.NewGuid():N}";
        var compatibleContent = $"Answer with the marker {Guid.NewGuid():N}.";
        var restrictedTitle = $"Model-restricted prompt {Guid.NewGuid():N}";
        var restrictedContent = $"Only for the restricted model {Guid.NewGuid():N}.";
        var workflowOnlyTitle = $"Workflow-only prompt {Guid.NewGuid():N}";

        await OpenGalleryAsync(page, "/prompt-gallery");
        await CreateDraftAsync(page, compatibleTitle, compatibleContent);
        await CreateDraftAsync(page, restrictedTitle, restrictedContent, async editor =>
        {
            // A supported model no chat agent uses makes the compatibility check raise the provider/model warning.
            await editor.GetByTestId("prompt-gallery-editor-provider-to-add").FillAsync("ZzzProvider");
            await editor.GetByTestId("prompt-gallery-editor-model-to-add").FillAsync("zzz-restricted-model");
            await editor.GetByTestId("prompt-gallery-editor-add-model").ClickAsync();
            await editor.GetByText("zzz-restricted-model", new() { Exact = true }).WaitForAsync();
        });
        await CreateDraftAsync(page, workflowOnlyTitle, "Never listed for a chat.", editor =>
            editor.GetByLabel("Support Workflow", new() { Exact = true }).CheckAsync());

        var response = await page.GotoAsync($"{fixture.BaseUrl}/agents?tab=chat");
        Assert.NotNull(response);
        Assert.True(response!.Ok, $"Expected /agents?tab=chat to return 2xx, got {(int)response.Status}.");
        await PlaywrightAppFixture.CompleteDatabaseStartupAsync(page);
        var chat = page.GetByTestId("agents-chat-panel");
        await chat.WaitForAsync();

        // The composer takes provider and model from the selected agent; the managed agents exist in every host.
        var switcher = page.GetByTestId("agent-switch-dialog");
        await OpenWithRetryAsync(chat.GetByTestId("agent-switch-button"), switcher, "agent switcher");
        var agentCards = switcher.GetByTestId("agent-switch-card");
        await agentCards.First.WaitForAsync();
        await agentCards.First.ClickAsync();
        await switcher.WaitForAsync(new LocatorWaitForOptions { State = WaitForSelectorState.Detached });

        var pickerButton = chat.GetByTestId("prompt-gallery-picker-button").First;
        await pickerButton.WaitForAsync();
        var picker = page.GetByTestId("prompt-gallery-picker-dialog");
        var compatibilityDialog = page.GetByTestId("prompt-gallery-chat-compatibility-dialog");
        var composerInput = page.GetByTestId("chat-prompt-input");

        // A compatible item is inserted straight into the real composer.
        await OpenWithRetryAsync(pickerButton, picker, "prompt gallery picker");
        await ShowAllPromptsAsync(picker);
        await picker.GetByTestId("prompt-gallery-search").FillAsync(compatibleTitle);
        await Assertions.Expect(picker.GetByTestId("prompt-gallery-select")).ToHaveCountAsync(1);
        await picker.GetByText(compatibleTitle, new() { Exact = true }).WaitForAsync();
        await page.ScreenshotAsync(new PageScreenshotOptions { Path = Path.Combine(artifactsDir, "09-embedded-picker.png") });
        await picker.GetByTestId("prompt-gallery-select").ClickAsync();
        await picker.WaitForAsync(new LocatorWaitForOptions { State = WaitForSelectorState.Detached });
        await Assertions.Expect(composerInput).ToHaveValueAsync(compatibleContent);
        Assert.Equal(0, await compatibilityDialog.CountAsync());
        await page.ScreenshotAsync(new PageScreenshotOptions { Path = Path.Combine(artifactsDir, "10-embedded-inserted.png") });

        // A workflow-only item is never offered to a chat: the picker's search is scoped to the chat consumer.
        await OpenWithRetryAsync(pickerButton, picker, "prompt gallery picker");
        await ShowAllPromptsAsync(picker);
        await picker.GetByTestId("prompt-gallery-search").FillAsync(workflowOnlyTitle);
        await picker.GetByText("No prompt items match these filters").WaitForAsync();
        Assert.Equal(0, await picker.GetByTestId("prompt-gallery-select").CountAsync());

        // A model-restricted item asks first; Cancel inserts nothing.
        await picker.GetByTestId("prompt-gallery-search").FillAsync(restrictedTitle);
        await Assertions.Expect(picker.GetByTestId("prompt-gallery-select")).ToHaveCountAsync(1);
        await picker.GetByText(restrictedTitle, new() { Exact = true }).WaitForAsync();
        await picker.GetByTestId("prompt-gallery-select").ClickAsync();
        await compatibilityDialog.WaitForAsync();
        await compatibilityDialog.GetByTestId("prompt-compatibility-insert").WaitForAsync();
        await compatibilityDialog.GetByTestId("prompt-compatibility-insert-suppress").WaitForAsync();
        await page.ScreenshotAsync(new PageScreenshotOptions { Path = Path.Combine(artifactsDir, "11-embedded-compatibility.png") });
        await compatibilityDialog.GetByTestId("prompt-compatibility-cancel").ClickAsync();
        await compatibilityDialog.WaitForAsync(new LocatorWaitForOptions { State = WaitForSelectorState.Detached });
        Assert.Equal(0, await picker.CountAsync());
        await Assertions.Expect(composerInput).ToHaveValueAsync(compatibleContent);

        // Explicit consent inserts the restricted item once, appended to the existing draft.
        await OpenWithRetryAsync(pickerButton, picker, "prompt gallery picker");
        await ShowAllPromptsAsync(picker);
        await picker.GetByTestId("prompt-gallery-search").FillAsync(restrictedTitle);
        await Assertions.Expect(picker.GetByTestId("prompt-gallery-select")).ToHaveCountAsync(1);
        await picker.GetByTestId("prompt-gallery-select").ClickAsync();
        await compatibilityDialog.WaitForAsync();
        await compatibilityDialog.GetByTestId("prompt-compatibility-insert").ClickAsync();
        await compatibilityDialog.WaitForAsync(new LocatorWaitForOptions { State = WaitForSelectorState.Detached });
        await Assertions.Expect(composerInput).ToHaveValueAsync(new System.Text.RegularExpressions.Regex(
            $"^{System.Text.RegularExpressions.Regex.Escape(compatibleContent)}\\s+{System.Text.RegularExpressions.Regex.Escape(restrictedContent)}$"));
        Assert.Equal(0, await picker.CountAsync());
        await page.ScreenshotAsync(new PageScreenshotOptions { Path = Path.Combine(artifactsDir, "12-embedded-consented.png") });

        Assert.False(await page.Locator("#blazor-error-ui").IsVisibleAsync());
        Assert.Empty(pageErrors);
    }

    [Fact]
    public async Task Filter_rail_stays_inside_its_container_at_desktop_and_constrained_widths()
    {
        var artifactsDir = ArtifactsDirectory();
        await using var context = await NewContextAsync();
        var page = await context.NewPageAsync();
        var pageErrors = new List<string>();
        page.PageError += (_, message) => pageErrors.Add(message);

        await OpenGalleryAsync(page, "/prompt-gallery");
        var rail = page.GetByTestId("prompt-gallery-filter-rail");
        await rail.WaitForAsync();
        await page.GetByTestId("prompt-gallery-archived-filter").WaitForAsync();

        var desktop = await ProbeRailAsync(page);
        await page.ScreenshotAsync(new PageScreenshotOptions { Path = Path.Combine(artifactsDir, "05-rail-1600.png") });
        Assert.False(desktop.RailOverflow, $"Desktop rail overflows: {desktop.Template}");
        Assert.Empty(desktop.Overflowing);
        Assert.Equal(1, desktop.Rows);

        // Below the xl breakpoint the rail wraps into an auto-fit grid instead of squeezing its controls.
        await page.SetViewportSizeAsync(1100, 900);
        await page.WaitForTimeoutAsync(300);
        var constrained = await ProbeRailAsync(page);
        await page.ScreenshotAsync(new PageScreenshotOptions { Path = Path.Combine(artifactsDir, "06-rail-1100.png") });
        Assert.False(constrained.RailOverflow, $"Constrained rail overflows: {constrained.Template}");
        Assert.Empty(constrained.Overflowing);
        Assert.True(constrained.Rows > 1, $"Expected the constrained rail to wrap, template: {constrained.Template}");

        Assert.False(await page.Locator("#blazor-error-ui").IsVisibleAsync());
        Assert.Empty(pageErrors);
    }

    private async Task OpenGalleryAsync(IPage page, string path)
    {
        var response = await page.GotoAsync($"{fixture.BaseUrl}{path}");
        Assert.NotNull(response);
        Assert.True(response!.Ok, $"Expected {path} to return 2xx, got {(int)response.Status}.");
        await PlaywrightAppFixture.CompleteDatabaseStartupAsync(page);
        await page.GetByTestId("prompt-gallery-search-list").WaitForAsync();
    }

    // Creates a draft through the real editor and returns the persisted identity read from the refreshed list.
    private static async Task<Guid> CreateDraftAsync(IPage page, string title, string content, Func<ILocator, Task>? configure = null)
    {
        await page.GetByRole(AriaRole.Button, new() { Name = "New item", Exact = true }).ClickAsync();
        var editor = page.GetByTestId("prompt-gallery-item-editor");
        await editor.WaitForAsync();
        await page.GetByTestId("prompt-gallery-editor-title").FillAsync(title);
        await page.GetByTestId("prompt-gallery-editor-content").FillAsync(content);
        if (configure is not null)
        {
            await configure(editor);
        }

        await page.GetByTestId("prompt-gallery-editor-save").ClickAsync();
        await page.GetByText("Prompt draft saved").First.WaitForAsync();
        await page.GetByTestId("prompt-gallery-editor-archive").WaitForAsync();
        await page.GetByTestId("prompt-gallery-editor-cancel").ClickAsync();
        await editor.WaitForAsync(new LocatorWaitForOptions { State = WaitForSelectorState.Detached });

        await page.GetByTestId("prompt-gallery-search").FillAsync(title);
        await page.GetByTestId("prompt-gallery-grid").GetByText(title, new() { Exact = true }).WaitForAsync();
        await Assertions.Expect(page.GetByTestId("prompt-gallery-edit")).ToHaveCountAsync(1);
        var itemId = await page.GetByTestId("prompt-gallery-edit").GetAttributeAsync("data-item-id");
        Assert.True(Guid.TryParse(itemId, out var parsed), $"The Details action carried no item identity: '{itemId}'.");
        await page.GetByTestId("prompt-gallery-search").FillAsync(string.Empty);
        return parsed;
    }

    // With a pinned agent model the picker starts on "Show actual chat model only"; the lane searches across all prompts.
    private static async Task ShowAllPromptsAsync(ILocator picker)
    {
        var actualModelFilter = picker.Locator(
            "input[data-testid='prompt-gallery-actual-model-filter'], [data-testid='prompt-gallery-actual-model-filter'] input").First;
        if (await actualModelFilter.CountAsync() > 0 && await actualModelFilter.IsCheckedAsync())
        {
            await actualModelFilter.UncheckAsync();
        }
    }

    // The chat shell re-renders right after the startup modal closes; a click that lands on a node replaced by that
    // render is lost by Blazor's event delegation, so dialogs on that page are opened with a bounded retry.
    private static async Task OpenWithRetryAsync(ILocator trigger, ILocator dialog, string description)
    {
        var timeoutAt = DateTimeOffset.UtcNow.AddSeconds(30);
        while (DateTimeOffset.UtcNow < timeoutAt)
        {
            await trigger.ClickAsync();
            try
            {
                await dialog.WaitForAsync(new LocatorWaitForOptions { Timeout = 3_000 });
                return;
            }
            catch (TimeoutException)
            {
                await Task.Delay(500);
            }
        }

        throw new TimeoutException($"The {description} did not open from the chat panel.");
    }

    private async Task<IBrowserContext> NewContextAsync()
        => await fixture.Browser.NewContextAsync(new BrowserNewContextOptions
        {
            ViewportSize = new ViewportSize
            {
                Width = 1600,
                Height = 1000
            }
        });

    private static string ArtifactsDirectory()
    {
        var artifactsDir = Path.Combine(PlaywrightTestHostPaths.RepositoryRoot, "output", "playwright", "prompt-gallery");
        Directory.CreateDirectory(artifactsDir);
        return artifactsDir;
    }

    private static async Task<RailProbe> ProbeRailAsync(IPage page)
    {
        var probe = await page.EvaluateAsync<System.Text.Json.JsonElement>(RailOverflowProbe);
        return new RailProbe(
            probe.GetProperty("template").GetString() ?? string.Empty,
            probe.GetProperty("rows").GetInt32(),
            probe.GetProperty("railOverflow").GetBoolean(),
            probe.GetProperty("overflowing").EnumerateArray().Select(item => item.GetString() ?? string.Empty).ToArray());
    }

    private sealed record RailProbe(string Template, int Rows, bool RailOverflow, string[] Overflowing);
}
