using System.IO;
using Microsoft.Playwright;

namespace CanDoItAll.Tests.Playwright;

public sealed partial class AppSmokeTests
{
    [Fact]
    [Trait("Surface", "SharedCanvas")]
    public async Task Project_structure_toolbox_specific_entries_preselect_single_required_kind_inputs()
    {
        var repoRoot = GetRepoRoot();
        var artifactsDir = Path.Combine(repoRoot, "output", "playwright");
        Directory.CreateDirectory(artifactsDir);

        await using var context = await fixture.Browser.NewContextAsync(new BrowserNewContextOptions
        {
            IgnoreHTTPSErrors = true,
            ViewportSize = new ViewportSize
            {
                Width = 1700,
                Height = 1100
            }
        });
        var page = await context.NewPageAsync();

        var projectId = await CreateProjectAsync(page, "Playwright Structure Composer Defaults", "Validation");
        await page.WaitForSelectorAsync("text=Structure canvas");
        await EnsureStructureToolboxWindowExpandedAsync(page);
        await EnsureStructureToolboxGroupExpandedAsync(page, "people");

        await page.GetByTestId("project-structure-toolbox-add-participant-freelancer").ClickAsync();
        var composer = page.Locator(".cw-canvas-composer");
        await composer.WaitForAsync();

        var participantKindSelect = composer.Locator("select").First;
        Assert.Equal(
            "freelancer",
            await participantKindSelect.InputValueAsync());

        await composer.Locator("input[placeholder='Contract designer']").FillAsync("Autoselected Freelancer");
        await composer.Locator("input[placeholder='Specialist']").FillAsync("Canvas QA");
        await composer.Locator("textarea[placeholder='Availability or engagement notes']").FillAsync("Subtype-specific participant create should not require a redundant kind selection.");

        var addParticipantButton = page.GetByRole(AriaRole.Button, new() { Name = "Add participant", Exact = true });
        Assert.True(
            await addParticipantButton.IsEnabledAsync(),
            "Expected the freelancer composer to be submittable after the visible required fields are filled.");

        await addParticipantButton.ClickAsync();
        await page.WaitForSelectorAsync("text=Autoselected Freelancer");

        await EnsureStructureObjectIndexWindowExpandedAsync(page);
        await page.GetByTestId($"project-structure-outline-node-project-{projectId}").ClickAsync();
        await page.WaitForTimeoutAsync(200);
        await EnsureStructureToolboxWindowExpandedAsync(page);
        await EnsureStructureToolboxGroupExpandedAsync(page, "work");

        await page.GetByTestId("project-structure-toolbox-add-work-task").ClickAsync();
        await composer.WaitForAsync();

        var workItemKindSelect = composer.Locator("select").First;
        Assert.Equal(
            "task",
            await workItemKindSelect.InputValueAsync());

        await composer.Locator("input[placeholder='Implement export flow']").FillAsync("Autoselected Task");
        await composer.Locator("input[placeholder='Sprint or owner']").FillAsync("Canvas QA");
        await composer.Locator("textarea[placeholder='Definition of done or context']").FillAsync("Subtype-specific work-item create should not require a redundant kind selection.");

        var addWorkItemButton = page.GetByRole(AriaRole.Button, new() { Name = "Add work item", Exact = true });
        Assert.True(
            await addWorkItemButton.IsEnabledAsync(),
            "Expected the task composer to be submittable after the visible required fields are filled.");

        await CapturePrimaryWorkbenchShellAsync(page, Path.Combine(artifactsDir, "bundle-p3-01-structure-composer-defaults.png"));
        await addWorkItemButton.ClickAsync();
        await page.WaitForSelectorAsync("text=Autoselected Task");

        Assert.False(await page.Locator("#blazor-error-ui").IsVisibleAsync());
    }

    [Fact]
    [Trait("Surface", "SharedCanvas")]
    public async Task Project_structure_meeting_and_send_composers_keep_static_select_options()
    {
        await using var context = await fixture.Browser.NewContextAsync(new BrowserNewContextOptions
        {
            IgnoreHTTPSErrors = true,
            ViewportSize = new ViewportSize
            {
                Width = 1700,
                Height = 1100
            }
        });
        var page = await context.NewPageAsync();

        var projectId = await CreateProjectAsync(page, "Playwright Structure Static Select Options", "Validation");
        await page.WaitForSelectorAsync("text=Structure canvas");
        await EnsureStructureToolboxWindowExpandedAsync(page);
        await EnsureStructureToolboxGroupExpandedAsync(page, "meetings");

        await page.GetByTestId("project-structure-toolbox-add-meeting-online").ClickAsync();
        var composer = page.Locator(".cw-canvas-composer");
        await composer.WaitForAsync();

        var channelOptionCount = await ReadSelectableOptionCountAsync(composer.Locator("select").Nth(0));
        var repeatOptionCount = await ReadSelectableOptionCountAsync(composer.Locator("select").Nth(1));
        Assert.True(
            channelOptionCount >= 5,
            $"Expected the online meeting channel select to expose the shared channel options, but only found {channelOptionCount} selectable options.");
        Assert.True(
            repeatOptionCount >= 5,
            $"Expected the online meeting repeat select to expose the shared cadence options, but only found {repeatOptionCount} selectable options.");

        await page.Keyboard.PressAsync("Escape");
        await EnsureStructureObjectIndexWindowExpandedAsync(page);
        await page.GetByTestId($"project-structure-outline-node-project-{projectId}").ClickAsync();
        await page.WaitForTimeoutAsync(200);
        await EnsureStructureToolboxWindowExpandedAsync(page);
        await EnsureStructureToolboxGroupExpandedAsync(page, "work");

        await page.GetByTestId("project-structure-toolbox-add-work-send").ClickAsync();
        await composer.WaitForAsync();

        var sendKindOptionCount = await ReadSelectableOptionCountAsync(composer.Locator("select").Nth(1));
        var deliveryChannelOptionCount = await ReadSelectableOptionCountAsync(composer.Locator("select").Nth(2));
        Assert.True(
            sendKindOptionCount >= 6,
            $"Expected the send-intent select to expose the shared send kinds, but only found {sendKindOptionCount} selectable options.");
        Assert.True(
            deliveryChannelOptionCount >= 6,
            $"Expected the delivery-channel select to expose the shared message channels, but only found {deliveryChannelOptionCount} selectable options.");

        Assert.False(await page.Locator("#blazor-error-ui").IsVisibleAsync());
    }

    private static async Task EnsureStructureToolboxGroupExpandedAsync(IPage page, string groupKey)
    {
        var groupToggle = page.GetByTestId($"project-structure-toolbox-group-{groupKey}");
        var groupBody = page.GetByTestId($"project-structure-toolbox-group-body-{groupKey}");
        if (!await WaitForLocatorAsync(groupBody, 500))
        {
            await groupToggle.ClickAsync();
        }

        Assert.True(
            await WaitForLocatorAsync(groupBody, 2_000),
            $"Expected the project structure toolbox group '{groupKey}' to be expanded before interacting with its actions.");
    }

    private static Task<int> ReadSelectableOptionCountAsync(ILocator select)
        => select.EvaluateAsync<int>("element => Array.from(element.options).filter(option => option.value).length");
}
