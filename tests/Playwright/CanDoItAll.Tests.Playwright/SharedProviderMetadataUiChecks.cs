using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Playwright;

namespace CanDoItAll.Tests.Playwright;

internal static class SharedProviderMetadataUiChecks {
    private static readonly string[] PriceFields = [
        "input", "cached", "cache-write", "output", "long-threshold",
        "long-input", "long-cached", "long-cache-write", "long-output"
    ];

    public static async Task ConfigureAsync(IPage page, string baseUrl, string providerName,
        string model, bool isPrivate, decimal inputRate, params string[] extraModels) {
        await OpenProviderAsync(page, baseUrl, providerName);
        await page.GetByTestId("provider-editor-tab-runtime").ClickAsync();
        await page.GetByTestId("providers-suggested-models").FillAsync(
            string.Join('\n', new[] { model }.Concat(extraModels)));
        await page.GetByTestId("provider-editor-tab-prices").ClickAsync();
        var rows = page.GetByTestId("provider-pricing-table").Locator("tbody tr");
        if (await rows.CountAsync() == 0) {
            await page.GetByTestId("provider-pricing-add-button").ClickAsync();
        }
        var privateCheckbox = page.GetByTestId("provider-private-input");
        if (await privateCheckbox.IsEnabledAsync()) {
            await privateCheckbox.SetCheckedAsync(isPrivate);
        }
        Assert.Equal(isPrivate, await privateCheckbox.IsCheckedAsync());
        await SetPriceAsync(page, 0, model, inputRate);
        foreach (var extraModel in extraModels) {
            var row = 0;
            var count = await rows.CountAsync();
            while (row < count && await page.GetByTestId($"provider-pricing-model-{row}").InputValueAsync() != extraModel) {
                row++;
            }
            if (row == count) {
                await page.GetByTestId("provider-pricing-add-button").ClickAsync();
            }
            await SetPriceAsync(page, row, extraModel, inputRate + 1m);
        }
        await page.GetByTestId("providers-save").ClickAsync();
        await page.GetByText("Provider profile saved.", new() { Exact = true }).WaitForAsync();
        await OpenProviderAsync(page, baseUrl, providerName);
        await page.GetByTestId("provider-editor-tab-prices").ClickAsync();
        await Assertions.Expect(page.GetByTestId("provider-private-input"))
            .ToBeCheckedAsync(new() { Checked = isPrivate });
    }

    public static async Task<IReadOnlyList<string>> AssertMirroredAsync(IPage sharedPage, IPage clientPage,
        string sharedUrl, string clientUrl, string providerName, string evidenceDirectory, string label) {
        var central = await ReadAsync(sharedPage, sharedUrl, providerName, sourceManaged: false);
        await ScreenshotAsync(sharedPage, evidenceDirectory, $"metadata-{label}-central.png");
        var imported = await ReadAsync(clientPage, clientUrl, providerName, sourceManaged: true);
        await ScreenshotAsync(clientPage, evidenceDirectory, $"metadata-{label}-client.png");
        Assert.Equal(central.DefaultModel, imported.DefaultModel);
        Assert.Equal(central.IsPrivate, imported.IsPrivate);
        Assert.Equal(central.Models.Order(), imported.Models.Order());
        Assert.Equal(central.Prices.Keys.Order(), imported.Prices.Keys.Order());
        foreach (var (model, prices) in central.Prices) {
            Assert.Equal(prices, imported.Prices[model]);
        }
        Assert.DoesNotContain("sp1.", imported.DefaultModel, StringComparison.Ordinal);
        Assert.DoesNotContain(imported.Prices.Keys, model => model.StartsWith("sp1.", StringComparison.Ordinal));
        Assert.False(await clientPage.Locator("#blazor-error-ui").IsVisibleAsync());
        await File.WriteAllTextAsync(Path.Combine(evidenceDirectory, $"metadata-{label}-parity.json"),
            JsonSerializer.Serialize(new { Source = central, Client = imported }, new JsonSerializerOptions { WriteIndented = true }));
        return central.Models;
    }

    public static async Task AssertAgentModelNamesAsync(IPage page, string clientUrl, string agentName,
        string defaultModel, string evidenceDirectory, IReadOnlyList<string> models, string selectedModel, string label) {
        await SharedProviderTwoInstanceUiAcceptanceTests.NavigateAsync(page, $"{clientUrl}/agents?tab=agents");
        var card = page.GetByTestId("agents-catalog-card-shell").Filter(new() { HasTextString = agentName });
        await card.First.DblClickAsync();
        var dialog = page.GetByTestId("agents-details-dialog").Last;
        await dialog.GetByRole(AriaRole.Tab, new() { Name = "Runtime", Exact = true }).ClickAsync();
        var selector = dialog.GetByTestId("agents-catalog-model-choice");
        await selector.SelectOptionAsync(new SelectOptionValue { Label = selectedModel });
        await Assertions.Expect(selector.Locator("option")).ToHaveCountAsync(models.Count);
        var expected = models.Where(model => model != defaultModel).Append($"Provider default ({defaultModel})");
        Assert.Equal(expected.Order(), (await selector.Locator("option").AllTextContentsAsync()).Order());
        Assert.Empty(await dialog.GetByTestId("agents-catalog-model-override").AllAsync());
        Assert.DoesNotContain("sp1.", await dialog.InnerTextAsync(), StringComparison.Ordinal);
        await selector.ClickAsync();
        await ScreenshotAsync(page, evidenceDirectory, $"metadata-agent-models-{label}-open.png");
        await selector.PressAsync("Escape");
        await selector.SelectOptionAsync(new SelectOptionValue { Label = selectedModel });
        await dialog.GetByTestId("agents-catalog-save").ClickAsync();
        await page.GetByText("Agent saved", new() { Exact = true }).WaitForAsync();
    }

    internal static async Task OpenProviderAsync(IPage page, string baseUrl, string providerName) {
        await SharedProviderTwoInstanceUiAcceptanceTests.NavigateAsync(page, $"{baseUrl}/agents?tab=providers");
        var provider = page.GetByTestId("providers-tree-provider")
            .Filter(new() { HasTextString = providerName }).First;
        await provider.WaitForAsync();
        await provider.ClickAsync();
        await Assertions.Expect(page.GetByTestId("providers-name-input")).ToHaveValueAsync(providerName);
    }

    public static async Task AssertSourceAgentChoicesAsync(IPage page, string baseUrl, string providerName,
        string defaultModel, IReadOnlyList<string> models, string evidenceDirectory, string label) {
        await SharedProviderTwoInstanceUiAcceptanceTests.NavigateAsync(page, $"{baseUrl}/agents?tab=agents");
        await page.GetByTestId("agents-catalog-new").ClickAsync();
        var dialog = page.GetByTestId("agents-details-dialog").Last;
        await dialog.GetByRole(AriaRole.Tab, new() { Name = "Runtime", Exact = true }).ClickAsync();
        await dialog.GetByTestId("agents-catalog-provider").SelectOptionAsync(new SelectOptionValue { Label = providerName });
        var selector = dialog.GetByTestId("agents-catalog-model-choice");
        await Assertions.Expect(selector.Locator("option")).ToHaveCountAsync(models.Count);
        Assert.Equal(models.Where(model => model != defaultModel).Append($"Provider default ({defaultModel})").Order(),
            (await selector.Locator("option").AllTextContentsAsync()).Order());
        await selector.ClickAsync();
        await ScreenshotAsync(page, evidenceDirectory, $"metadata-source-agent-{label}-open.png");
        await selector.PressAsync("Escape");
        await dialog.GetByRole(AriaRole.Button, new() { Name = "Close", Exact = true }).ClickAsync();
    }

    public static async Task ExerciseSimpleChatAsync(IPage page, string baseUrl, string providerName,
        string defaultModel, IReadOnlyList<string> models, string selectedModel, string evidenceDirectory, string label,
        string expectedResponse = "deterministic fixture response", string? prompt = null,
        Regex? responsePattern = null, bool importedProvider = true) {
        var definitionName = $"UI shared catalog {label}";
        await SharedProviderTwoInstanceUiAcceptanceTests.NavigateAsync(page,
            $"{baseUrl}/agents?tab=simple-chats&simpleChatView=definitions");
        var card = page.Locator("article[data-testid^='llm-chat-definition-']").Filter(new() { HasTextString = definitionName });
        if (await card.CountAsync() == 0) {
            await page.GetByTestId("llm-chat-definition-create").ClickAsync();
        } else {
            await card.GetByRole(AriaRole.Button, new() { Name = "Edit", Exact = true }).ClickAsync();
        }
        var dialog = page.GetByTestId("llm-chat-definition-editor-dialog");
        await dialog.GetByTestId("llm-chat-definition-name").FillAsync(definitionName);
        await dialog.GetByTestId("llm-chat-definition-tab-runtime").ClickAsync();
        await dialog.GetByTestId("llm-chat-definition-provider").SelectOptionAsync(new SelectOptionValue { Label = providerName });
        await dialog.GetByTestId("llm-chat-definition-system-prompt").FillAsync("Reply with a short confirmation.");
        var selector = dialog.GetByTestId("llm-chat-definition-model");
        await Assertions.Expect(selector.Locator("option")).ToHaveCountAsync(models.Count);
        Assert.Equal(models.Where(model => model != defaultModel).Append($"Provider default ({defaultModel})").Order(),
            (await selector.Locator("option").AllTextContentsAsync()).Order());
        var overrides = await dialog.GetByTestId("llm-chat-definition-model-override").AllAsync();
        if (importedProvider) {
            Assert.Empty(overrides);
        } else {
            Assert.Single(overrides);
        }
        await selector.ClickAsync();
        await ScreenshotAsync(page, evidenceDirectory, $"metadata-simple-chat-{label}-open.png");
        await selector.PressAsync("Escape");
        await selector.SelectOptionAsync(new SelectOptionValue { Label = selectedModel });
        await dialog.GetByTestId("llm-chat-definition-editor-save").ClickAsync();
        await dialog.WaitForAsync(new() { State = WaitForSelectorState.Detached });
        await card.GetByRole(AriaRole.Button, new() { Name = "Edit", Exact = true }).ClickAsync();
        await dialog.GetByTestId("llm-chat-definition-tab-runtime").ClickAsync();
        Assert.Equal(selectedModel, await dialog.GetByTestId("llm-chat-definition-model").Locator("option:checked").InnerTextAsync());
        var activate = dialog.GetByTestId("llm-chat-definition-status-active");
        if (await activate.CountAsync() > 0) {
            await activate.ClickAsync();
        } else {
            await dialog.GetByTestId("llm-chat-definition-editor-cancel").ClickAsync();
        }
        await dialog.WaitForAsync(new() { State = WaitForSelectorState.Detached });
        await SharedProviderTwoInstanceUiAcceptanceTests.NavigateAsync(page,
            $"{baseUrl}/agents?tab=simple-chats&simpleChatView=conversations");
        await page.GetByTestId("llm-chat-new").ClickAsync();
        var start = page.GetByTestId("llm-chat-start-dialog");
        await start.GetByTestId("llm-chat-start-definition-search").FillAsync(definitionName);
        await start.Locator("[data-testid^='llm-chat-start-definition-']")
            .Filter(new() { HasTextString = definitionName }).Last.ClickAsync();
        await start.GetByTestId("llm-chat-start-title").FillAsync($"Catalog validation {label} {DateTimeOffset.UtcNow:O}");
        await start.GetByTestId("llm-chat-start-confirm").ClickAsync();
        await start.WaitForAsync(new() { State = WaitForSelectorState.Detached });
        await page.GetByTestId("llm-chat-prompt").FillAsync(prompt ?? (
            expectedResponse == "deterministic fixture response"
                ? "Confirm this shared non-default model works."
                : $"Reply with exactly {expectedResponse} and nothing else."));
        await page.GetByTestId("llm-chat-send").ClickAsync();
        var reply = expectedResponse == "deterministic fixture response"
            ? page.GetByTestId("llm-chat-conversation-workspace").GetByText(expectedResponse, new() { Exact = true })
            : page.GetByTestId("llm-chat-conversation-workspace")
                .Locator("[data-testid='conversation-message'].justify-start .chat-markdown").Last;
        try {
            if (expectedResponse == "deterministic fixture response") {
                await reply.WaitForAsync(new() { Timeout = 120_000 });
            } else {
                await Assertions.Expect(page.GetByTestId("llm-chat-operation-status"))
                    .ToContainTextAsync("Completed", new() { Timeout = 120_000 });
                var actual = (await reply.InnerTextAsync()).Trim();
                await File.WriteAllTextAsync(Path.Combine(evidenceDirectory, $"metadata-simple-chat-{label}-answer.txt"), actual);
                Assert.Matches(responsePattern ?? new Regex($"^{Regex.Escape(expectedResponse)}[.!]?$"), actual);
            }
        } catch {
            await ScreenshotAsync(page, evidenceDirectory, $"metadata-simple-chat-{label}-failure.png");
            throw;
        }
        await ScreenshotAsync(page, evidenceDirectory, $"metadata-simple-chat-{label}-response.png");
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
        await page.GetByTestId("provider-editor-tab-runtime").ClickAsync();
        var modelText = await page.GetByTestId("providers-suggested-models").InputValueAsync();
        var sourceModels = modelText.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Append(defaultModel).Distinct(StringComparer.Ordinal).ToArray();
        if (!sourceManaged) {
            await page.GetByTestId("provider-editor-tab-sharing").ClickAsync();
            var preview = page.GetByText("Published capability preview", new() { Exact = true });
            await preview.WaitForAsync();
            var publishedModels = await preview.Locator("xpath=..").Locator("strong").AllTextContentsAsync();
            Assert.Equal(sourceModels.Order(), publishedModels.Order());
        }
        await page.GetByTestId("provider-editor-tab-prices").ClickAsync();
        var isPrivate = await page.GetByTestId("provider-private-input").IsCheckedAsync();
        var count = await page.Locator("[data-testid^='provider-pricing-row-']").CountAsync();
        Assert.InRange(count, 0, 256);
        var prices = new Dictionary<string, decimal?[]>(StringComparer.Ordinal);
        for (var row = 0; row < count; row++) {
            var model = await page.GetByTestId($"provider-pricing-model-{row}").InputValueAsync();
            if (!sourceManaged && !sourceModels.Contains(model, StringComparer.Ordinal)) {
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
            if (count > 0) {
                Assert.True(await page.GetByTestId("provider-pricing-input-0").IsDisabledAsync());
            }
            Assert.Empty(await page.GetByTestId("provider-pricing-reset-button").AllAsync());
            Assert.Empty(await page.GetByTestId("provider-pricing-add-button").AllAsync());
        }
        Assert.All(prices.Keys, model => Assert.Contains(model, sourceModels));
        return new(defaultModel, isPrivate, sourceModels, prices);
    }

    private static Task ScreenshotAsync(IPage page, string directory, string name) => page.ScreenshotAsync(new() {
        Path = Path.Combine(directory, name), FullPage = true
    });

    private sealed record ProviderUiMetadata(string DefaultModel, bool IsPrivate,
        IReadOnlyList<string> Models, IReadOnlyDictionary<string, decimal?[]> Prices);
}
