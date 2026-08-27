using System.Globalization;
using Microsoft.Playwright;

namespace CanDoItAll.Tests.Playwright;

internal static class SharedProviderMetadataUiChecks {
    private static readonly string[] PriceFields = [
        "input", "cached", "cache-write", "output", "long-threshold",
        "long-input", "long-cached", "long-cache-write", "long-output"
    ];

    public static async Task ConfigureAsync(IPage page, string baseUrl, string providerName,
        string model, bool isPrivate, decimal inputRate, string? extraModel = null) {
        await OpenProviderAsync(page, baseUrl, providerName);
        await page.GetByTestId("provider-editor-tab-runtime").ClickAsync();
        await page.GetByTestId("providers-suggested-models").FillAsync(
            extraModel is null ? model : $"{model}\n{extraModel}");
        await page.GetByTestId("provider-editor-tab-prices").ClickAsync();
        var rows = page.GetByTestId("provider-pricing-table").Locator("tbody tr");
        while (await rows.CountAsync() > 1) {
            await rows.Last.GetByRole(AriaRole.Button, new() { Name = "Remove", Exact = true }).ClickAsync();
        }
        if (await rows.CountAsync() == 0) {
            await page.GetByTestId("provider-pricing-add-button").ClickAsync();
        }
        var privateCheckbox = page.GetByTestId("provider-private-input");
        if (await privateCheckbox.IsEnabledAsync()) {
            await privateCheckbox.SetCheckedAsync(isPrivate);
        }
        Assert.Equal(isPrivate, await privateCheckbox.IsCheckedAsync());
        await SetPriceAsync(page, 0, model, inputRate);
        if (extraModel is not null) {
            await page.GetByTestId("provider-pricing-add-button").ClickAsync();
            await SetPriceAsync(page, 1, extraModel, inputRate + 1m);
        }
        await page.GetByTestId("providers-save").ClickAsync();
        await page.GetByText("Provider profile saved.", new() { Exact = true }).WaitForAsync();
        await OpenProviderAsync(page, baseUrl, providerName);
        await page.GetByTestId("provider-editor-tab-prices").ClickAsync();
        await Assertions.Expect(page.GetByTestId("provider-private-input"))
            .ToBeCheckedAsync(new() { Checked = isPrivate });
    }

    public static async Task AssertMirroredAsync(IPage sharedPage, IPage clientPage,
        string sharedUrl, string clientUrl, string providerName, string evidenceDirectory, string label) {
        var central = await ReadAsync(sharedPage, sharedUrl, providerName, sourceManaged: false);
        await ScreenshotAsync(sharedPage, evidenceDirectory, $"metadata-{label}-central.png");
        var imported = await ReadAsync(clientPage, clientUrl, providerName, sourceManaged: true);
        await ScreenshotAsync(clientPage, evidenceDirectory, $"metadata-{label}-client.png");
        Assert.Equal(central.DefaultModel, imported.DefaultModel);
        Assert.Equal(central.IsPrivate, imported.IsPrivate);
        Assert.Equal(central.Prices.Keys.Order(), imported.Prices.Keys.Order());
        foreach (var (model, prices) in central.Prices) {
            Assert.Equal(prices, imported.Prices[model]);
        }
        Assert.DoesNotContain("sp1.", imported.DefaultModel, StringComparison.Ordinal);
        Assert.DoesNotContain(imported.Prices.Keys, model => model.StartsWith("sp1.", StringComparison.Ordinal));
        Assert.False(await clientPage.Locator("#blazor-error-ui").IsVisibleAsync());
    }

    public static async Task AssertAgentModelNamesAsync(IPage page, string clientUrl, string agentName,
        string defaultModel, string evidenceDirectory, string? extraModel = null) {
        await SharedProviderTwoInstanceUiAcceptanceTests.NavigateAsync(page, $"{clientUrl}/agents?tab=agents");
        var card = page.GetByTestId("agents-catalog-card-shell").Filter(new() { HasTextString = agentName });
        await card.First.DblClickAsync();
        var dialog = page.GetByTestId("agents-details-dialog").Last;
        await dialog.GetByRole(AriaRole.Tab, new() { Name = "Runtime", Exact = true }).ClickAsync();
        var selector = dialog.GetByTestId("agents-catalog-model-choice");
        var expected = extraModel is null
            ? new[] { $"Provider default ({defaultModel})" }
            : new[] { $"Provider default ({defaultModel})", extraModel };
        await Assertions.Expect(selector.Locator("option")).ToHaveTextAsync(expected);
        Assert.Empty(await dialog.GetByTestId("agents-catalog-model-override").AllAsync());
        Assert.DoesNotContain("sp1.", await dialog.InnerTextAsync(), StringComparison.Ordinal);
        await selector.ClickAsync();
        await ScreenshotAsync(page, evidenceDirectory, extraModel is null
            ? "metadata-agent-models-resynced.png" : "metadata-agent-models-open.png");
        await selector.PressAsync("Escape");
        await dialog.GetByRole(AriaRole.Button, new() { Name = "Close", Exact = true }).ClickAsync();
    }

    private static async Task OpenProviderAsync(IPage page, string baseUrl, string providerName) {
        await SharedProviderTwoInstanceUiAcceptanceTests.NavigateAsync(page, $"{baseUrl}/agents?tab=providers");
        var provider = page.GetByTestId("providers-tree-provider")
            .Filter(new() { HasTextString = providerName }).First;
        await provider.WaitForAsync();
        await provider.ClickAsync();
        await Assertions.Expect(page.GetByTestId("providers-name-input")).ToHaveValueAsync(providerName);
    }

    private static async Task SetPriceAsync(IPage page, int row, string model, decimal inputRate) {
        await page.GetByTestId($"provider-pricing-model-{row}").FillAsync(model);
        decimal?[] values = [inputRate, 0m, 0.35m, 4.56m, 12345, inputRate * 2, 0m, 0.70m, 9.12m];
        for (var index = 0; index < PriceFields.Length; index++) {
            await page.GetByTestId($"provider-pricing-{PriceFields[index]}-{row}")
                .FillAsync(values[index]?.ToString(CultureInfo.InvariantCulture) ?? string.Empty);
        }
    }

    private static async Task<ProviderUiMetadata> ReadAsync(IPage page, string baseUrl,
        string providerName, bool sourceManaged) {
        await OpenProviderAsync(page, baseUrl, providerName);
        var defaultModel = await page.GetByTestId("providers-model-input").InputValueAsync();
        var publishedModels = new HashSet<string>(StringComparer.Ordinal);
        if (!sourceManaged) {
            await page.GetByTestId("provider-editor-tab-sharing").ClickAsync();
            var preview = page.GetByText("Published capability preview", new() { Exact = true });
            await preview.WaitForAsync();
            publishedModels.UnionWith(await preview.Locator("xpath=..").Locator("strong").AllTextContentsAsync());
            Assert.NotEmpty(publishedModels);
        }
        await page.GetByTestId("provider-editor-tab-prices").ClickAsync();
        var isPrivate = await page.GetByTestId("provider-private-input").IsCheckedAsync();
        var count = await page.GetByTestId("provider-pricing-table").Locator("tbody tr").CountAsync();
        Assert.InRange(count, 1, 256);
        var prices = new Dictionary<string, decimal?[]>(StringComparer.Ordinal);
        for (var row = 0; row < count; row++) {
            var model = await page.GetByTestId($"provider-pricing-model-{row}").InputValueAsync();
            if (!sourceManaged && !publishedModels.Contains(model)) {
                continue;
            }
            var values = new decimal?[PriceFields.Length];
            for (var index = 0; index < PriceFields.Length; index++) {
                var value = await page.GetByTestId($"provider-pricing-{PriceFields[index]}-{row}").InputValueAsync();
                values[index] = value.Length == 0 ? null : decimal.Parse(value, CultureInfo.InvariantCulture);
            }
            prices.Add(model, values);
        }
        if (sourceManaged) {
            Assert.True(await page.GetByTestId("provider-pricing-input-0").IsDisabledAsync());
            Assert.Empty(await page.GetByTestId("provider-pricing-reset-button").AllAsync());
            Assert.Empty(await page.GetByTestId("provider-pricing-add-button").AllAsync());
        } else {
            Assert.Equal(publishedModels.Order(), prices.Keys.Order());
        }
        return new(defaultModel, isPrivate, prices);
    }

    private static Task ScreenshotAsync(IPage page, string directory, string name) => page.ScreenshotAsync(new() {
        Path = Path.Combine(directory, name), FullPage = true
    });

    private sealed record ProviderUiMetadata(string DefaultModel, bool IsPrivate,
        IReadOnlyDictionary<string, decimal?[]> Prices);
}
