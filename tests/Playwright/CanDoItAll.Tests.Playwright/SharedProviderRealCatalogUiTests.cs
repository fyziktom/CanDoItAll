using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Playwright;
using Xunit.Sdk;

namespace CanDoItAll.Tests.Playwright;

public sealed class SharedProviderRealCatalogUiTests {
    private const string OpenAiSecretName = "Real shared OpenAI API key";
    private const string SourceSecretName = "UI shared instance JWT";
    private const string SourceName = "UI shared instance";
    private const string ChatProvider = "UI Shared OpenAI Chat";
    private const string ImageProvider = "UI Shared OpenAI Image";
    private const string OllamaProvider = "UI Shared Ollama";

    [Fact]
    [Trait("Category", "ExternalRealSharedProviderUi")]
    public async Task Real_Ollama_inventory_replaces_fixture_data_and_mirrors_without_foreign_prices() {
        var sourceUrl = Required("CANDOITALL_REAL_SHARED_URL");
        var clientUrl = Required("CANDOITALL_REAL_CLIENT_URL");
        var evidence = Required("CANDOITALL_REAL_UI_EVIDENCE");
        var ollamaUrl = Required("CANDOITALL_REAL_OLLAMA_URL");
        using var http = new HttpClient();
        using var inventory = await http.GetFromJsonAsync<JsonDocument>($"{ollamaUrl}/api/tags");
        var models = inventory!.RootElement.GetProperty("models").EnumerateArray()
            .Select(model => model.GetProperty("name").GetString()!).Order(StringComparer.Ordinal).ToArray();
        Assert.Contains("gptoss20b64k:latest", models);
        Assert.Contains("gemma3:1b", models);
        Directory.CreateDirectory(evidence);
        await File.WriteAllTextAsync(Path.Combine(evidence, "ollama-upstream-models.json"), JsonSerializer.Serialize(models));
        using var playwright = await Microsoft.Playwright.Playwright.CreateAsync();
        await using var browser = await playwright.Chromium.LaunchAsync(new() { Headless = true, Channel = "chrome" });
        await using var sourceContext = await browser.NewContextAsync(new() { ViewportSize = new() { Width = 1920, Height = 1080 } });
        await using var clientContext = await browser.NewContextAsync(new() { ViewportSize = new() { Width = 1920, Height = 1080 } });
        var source = await sourceContext.NewPageAsync();
        var client = await clientContext.NewPageAsync();
        source.SetDefaultTimeout(45_000);
        client.SetDefaultTimeout(45_000);
        await SharedProviderMetadataUiChecks.OpenProviderAsync(source, sourceUrl, OllamaProvider);
        await source.GetByTestId("providers-kind-select").SelectOptionAsync("OpenAi");
        await Assertions.Expect(source.GetByTestId("providers-model-input")).ToHaveValueAsync("");
        await source.GetByTestId("providers-kind-select").SelectOptionAsync("Ollama");
        await source.GetByTestId("providers-base-url-input").FillAsync(ollamaUrl);
        await source.GetByTestId("providers-model-input").FillAsync("gptoss20b64k:latest");
        await source.GetByTestId("provider-editor-tab-prices").ClickAsync();
        await source.GetByTestId("provider-pricing-refresh-button").ClickAsync();
        await source.GetByText("Provider models loaded", new() { Exact = true }).WaitForAsync();
        Assert.Empty(await source.Locator("[data-testid^='provider-pricing-row-']").AllAsync());
        Assert.True(await source.GetByTestId("provider-private-input").IsCheckedAsync());
        await source.GetByTestId("provider-editor-tab-runtime").ClickAsync();
        var actual = (await source.GetByTestId("providers-suggested-models").InputValueAsync())
            .Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries);
        Assert.Equal(models, actual.Order(StringComparer.Ordinal));
        await source.GetByTestId("providers-save").ClickAsync();
        await source.GetByText("Provider profile saved.", new() { Exact = true }).WaitForAsync();
        await SynchronizeAsync(client, clientUrl);
        var mirrored = await SharedProviderMetadataUiChecks.AssertMirroredAsync(
            source, client, sourceUrl, clientUrl, OllamaProvider, evidence, "real-ollama");
        Assert.Equal(models, mirrored.Order(StringComparer.Ordinal));
        await SharedProviderMetadataUiChecks.AssertAgentModelNamesAsync(client, clientUrl,
            "UI Shared Ollama Agent", "gptoss20b64k:latest", evidence, mirrored, "gpt-oss:20b", "real-ollama");
    }

    [Fact]
    [Trait("Category", "ExternalRealSharedProviderUi")]
    public async Task Real_shared_providers_execute_simple_chats_agent_image_and_vision_through_UI() {
        var clientUrl = Required("CANDOITALL_REAL_CLIENT_URL");
        var evidence = Required("CANDOITALL_REAL_UI_EVIDENCE");
        var started = DateTimeOffset.UtcNow;
        Directory.CreateDirectory(evidence);
        await File.WriteAllTextAsync(Path.Combine(evidence, "execution-start.json"), JsonSerializer.Serialize(started));
        using var playwright = await Microsoft.Playwright.Playwright.CreateAsync();
        await using var browser = await playwright.Chromium.LaunchAsync(new() { Headless = true, Channel = "chrome" });
        await using var context = await browser.NewContextAsync(new() { ViewportSize = new() { Width = 1920, Height = 1080 } });
        var page = await context.NewPageAsync();
        page.SetDefaultTimeout(45_000);
        var token = await IssueTokenAsync(page, clientUrl,
            "api.llm-chats.read api.llm-chats.manage api.llm-chats.execute");
        var authority = new Uri(clientUrl).Authority;
        await context.RouteAsync("**/*", route => new Uri(route.Request.Url).Authority == authority
            ? route.ContinueAsync() : route.AbortAsync());
        await context.SetExtraHTTPHeadersAsync(new Dictionary<string, string> { ["Authorization"] = $"Bearer {token}" });
        var openAiModels = JsonSerializer.Deserialize<string[]>(await File.ReadAllTextAsync(Path.Combine(evidence, "upstream-models.json")))!;
        var ollamaModels = JsonSerializer.Deserialize<string[]>(await File.ReadAllTextAsync(Path.Combine(evidence, "ollama-upstream-models.json")))!;
        await SharedProviderMetadataUiChecks.ExerciseSimpleChatAsync(page, clientUrl, ChatProvider,
            "gpt-4.1-mini", openAiModels, "gpt-5.4-mini", evidence, "real-openai", "REAL_OPENAI_CHAT_OK");
        await SharedProviderMetadataUiChecks.ExerciseSimpleChatAsync(page, clientUrl, OllamaProvider,
            "gptoss20b64k:latest", ollamaModels, "gemma3:4b", evidence, "real-ollama", "4",
            "What is two plus two? Reply using only the number.", new Regex("^(4|four)[.!]?$", RegexOptions.IgnoreCase));
        await SharedProviderTwoInstanceUiAcceptanceTests.NavigateAsync(page, $"{clientUrl}/agents?tab=agents");
        await page.GetByTestId("agents-catalog-card-shell").Filter(new() { HasTextString = "UI Shared Multimedia Agent" }).First.DblClickAsync();
        var dialog = page.GetByTestId("agents-details-dialog").Last;
        await dialog.GetByRole(AriaRole.Tab, new() { Name = "Images", Exact = true }).ClickAsync();
        await dialog.GetByTestId("agents-catalog-image-generation-enabled").CheckAsync();
        await dialog.GetByTestId("agents-catalog-image-generation-provider").SelectOptionAsync(new SelectOptionValue { Label = ImageProvider });
        await dialog.GetByTestId("agents-catalog-image-model-choice").SelectOptionAsync(new SelectOptionValue { Label = "Provider default (gpt-image-1-mini)" });
        await dialog.GetByTestId("agents-catalog-save").ClickAsync();
        await page.GetByText("Agent saved", new() { Exact = true }).WaitForAsync();
        await SharedProviderTwoInstanceUiAcceptanceTests.NavigateAsync(page, $"{clientUrl}/agents?tab=chat");
        await SwitchAgentAsync(page, "UI Shared Ollama Agent");
        await SendNewChatAsync(page, "Reply with exactly REAL_OLLAMA_AGENT_OK and nothing else.");
        await page.GetByTestId("chat-workspace-panel").GetByText("REAL_OLLAMA_AGENT_OK", new() { Exact = true }).WaitForAsync(new() { Timeout = 180_000 });
        await WaitForCompletedAgentRunAsync(page);
        await page.ScreenshotAsync(new() { Path = Path.Combine(evidence, "real-ollama-agent-response.png"), FullPage = true });
        await SwitchAgentAsync(page, "UI Shared Multimedia Agent");
        await SendNewChatAsync(page, "Reply with exactly REAL_OPENAI_AGENT_OK and nothing else.");
        await page.GetByTestId("chat-workspace-panel").GetByText("REAL_OPENAI_AGENT_OK", new() { Exact = true }).WaitForAsync(new() { Timeout = 120_000 });
        await WaitForCompletedAgentRunAsync(page);
        await page.ScreenshotAsync(new() { Path = Path.Combine(evidence, "real-openai-agent-response.png"), FullPage = true });
        var outputPath = $"shared-provider-real-validation/lighthouse-{started.ToUnixTimeSeconds()}.png";
        await SendNewChatAsync(page, $"Call image_generation_create exactly once to create a simple blue geometric lighthouse. Use the configured gpt-image-1-mini model, quality low, size 1024x1024, one image. Save it to {outputPath}. After successful creation reply exactly REAL_IMAGE_CREATED_OK.");
        await page.GetByTestId("chat-approve-once-button").WaitForAsync(new() { Timeout = 90_000 });
        await page.ScreenshotAsync(new() { Path = Path.Combine(evidence, "real-image-approval.png"), FullPage = true });
        await page.GetByTestId("chat-approve-once-button").ClickAsync();
        try {
            await page.GetByTestId("chat-workspace-panel").GetByText("REAL_IMAGE_CREATED_OK", new() { Exact = true }).WaitForAsync(new() { Timeout = 180_000 });
            await WaitForCompletedAgentRunAsync(page);
        } catch {
            await page.ScreenshotAsync(new() { Path = Path.Combine(evidence, "real-image-failure.png") });
            await File.WriteAllTextAsync(Path.Combine(evidence, "real-image-failure.txt"),
                await page.GetByTestId("chat-workspace-panel").InnerTextAsync());
            throw;
        }
        await page.ScreenshotAsync(new() { Path = Path.Combine(evidence, "real-image-response.png"), FullPage = true });
        await File.WriteAllTextAsync(Path.Combine(evidence, "image-output-path.txt"), outputPath);
        await using var visualContext = await browser.NewContextAsync(new() { ViewportSize = new() { Width = 480, Height = 320 } });
        var visual = await visualContext.NewPageAsync();
        await visual.SetContentAsync("<html><body style='margin:0;background:white'><svg width='480' height='320'><circle cx='120' cy='160' r='70' fill='blue'/><rect x='280' y='90' width='140' height='140' fill='orange'/></svg></body></html>");
        var visionPath = Path.Combine(evidence, "vision-input.png");
        await visual.ScreenshotAsync(new() { Path = visionPath });
        await page.GetByRole(AriaRole.Button, new() { Name = "New thread", Exact = true }).First.ClickAsync();
        await page.GetByTestId("chat-image-attachment-input").SetInputFilesAsync(visionPath);
        await page.GetByText("1 staged", new() { Exact = true }).WaitForAsync();
        await page.GetByTestId("chat-prompt-input").FillAsync("Describe the two shapes and their colors in the attached image in one sentence.");
        await page.GetByTestId("chat-send-button").ClickAsync();
        var answer = page.GetByTestId("conversation-message").Filter(new() { HasTextRegex = new System.Text.RegularExpressions.Regex("blue.*circle|circle.*blue", System.Text.RegularExpressions.RegexOptions.IgnoreCase) }).Last;
        await answer.WaitForAsync(new() { Timeout = 120_000 });
        await Assertions.Expect(answer).ToContainTextAsync("orange", new() { IgnoreCase = true });
        await Assertions.Expect(answer).ToContainTextAsync("square", new() { IgnoreCase = true });
        await WaitForCompletedAgentRunAsync(page);
        await page.ScreenshotAsync(new() { Path = Path.Combine(evidence, "real-vision-response.png"), FullPage = true });
        await File.WriteAllTextAsync(Path.Combine(evidence, "execution-result.json"), JsonSerializer.Serialize(new {
            StartedAtUtc = started, FinishedAtUtc = DateTimeOffset.UtcNow,
            SimpleChats = new[] { "gpt-5.4-mini", "gemma3:4b" }, Agents = new[] { "gpt-5.4-mini", "gpt-oss:20b" },
            ImageModel = "gpt-image-1-mini", ImagePath = outputPath, VisionAnswer = await answer.InnerTextAsync()
        }));
    }

    private static Task WaitForCompletedAgentRunAsync(IPage page) =>
        Assertions.Expect(page.GetByTestId("agent-execution-activity-phase"))
            .ToHaveTextAsync("Completed", new() { Timeout = 30_000 });

    private static async Task SwitchAgentAsync(IPage page, string name) {
        await page.GetByTestId("agent-switch-button").ClickAsync();
        var dialog = page.GetByTestId("agent-switch-dialog");
        await page.GetByTestId("agent-switch-search").FillAsync(name);
        await dialog.GetByTestId("agent-switch-card-shell").Filter(new() { HasTextString = name }).GetByTestId("agent-switch-card").ClickAsync();
        await dialog.WaitForAsync(new() { State = WaitForSelectorState.Detached });
        await Assertions.Expect(page.GetByTestId("agent-thread-selected-agent")).ToContainTextAsync(name);
    }

    private static async Task SendNewChatAsync(IPage page, string prompt) {
        await page.GetByRole(AriaRole.Button, new() { Name = "New thread", Exact = true }).First.ClickAsync();
        await page.GetByTestId("chat-prompt-input").FillAsync(prompt);
        await page.GetByTestId("chat-send-button").ClickAsync();
    }

    [Fact]
    [Trait("Category", "ExternalRealSharedProviderUi")]
    public async Task Real_OpenAi_inventory_is_configured_in_UI_and_mirrored_without_aliases_or_fake_prices() {
        var sourceUrl = Required("CANDOITALL_REAL_SHARED_URL");
        var clientUrl = Required("CANDOITALL_REAL_CLIENT_URL");
        var evidence = Required("CANDOITALL_REAL_UI_EVIDENCE");
        var apiKey = Required("OPENAI_API_KEY");
        Directory.CreateDirectory(evidence);
        using var http = new HttpClient();
        http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
        using var inventory = await http.GetFromJsonAsync<JsonDocument>("https://api.openai.com/v1/models");
        var realModels = inventory!.RootElement.GetProperty("data").EnumerateArray()
            .Select(model => model.GetProperty("id").GetString()!).Order(StringComparer.Ordinal).ToArray();
        Assert.Contains("gpt-4.1-mini", realModels);
        Assert.Contains("gpt-image-1-mini", realModels);
        await File.WriteAllTextAsync(Path.Combine(evidence, "upstream-models.json"), JsonSerializer.Serialize(realModels));

        using var playwright = await Microsoft.Playwright.Playwright.CreateAsync();
        await using var browser = await playwright.Chromium.LaunchAsync(new() { Headless = true, Channel = "chrome" });
        await using var sourceContext = await browser.NewContextAsync(new() { ViewportSize = new() { Width = 1920, Height = 1080 } });
        await using var clientContext = await browser.NewContextAsync(new() { ViewportSize = new() { Width = 1920, Height = 1080 } });
        var source = await sourceContext.NewPageAsync();
        var client = await clientContext.NewPageAsync();
        source.SetDefaultTimeout(45_000);
        client.SetDefaultTimeout(45_000);
        await SaveSecretSafelyAsync(source, sourceUrl, OpenAiSecretName, apiKey);
        await ConfigureChatAsync(source, sourceUrl, realModels);
        var imageModels = realModels.Where(model => model.StartsWith("gpt-image-", StringComparison.Ordinal)).ToArray();
        await ConfigureImagesAsync(source, sourceUrl, imageModels);
        var token = await IssueTokenAsync(source, sourceUrl,
            "api.shared-providers.catalog.read api.shared-providers.invoke");
        await SaveSecretSafelyAsync(client, clientUrl, SourceSecretName, token);
        await SynchronizeAsync(client, clientUrl);
        var mirroredChat = await SharedProviderMetadataUiChecks.AssertMirroredAsync(
            source, client, sourceUrl, clientUrl, ChatProvider, evidence, "real-openai");
        Assert.Equal(realModels, mirroredChat.Order(StringComparer.Ordinal));
        var mirroredImages = await SharedProviderMetadataUiChecks.AssertMirroredAsync(
            source, client, sourceUrl, clientUrl, ImageProvider, evidence, "real-images");
        Assert.Equal(imageModels, mirroredImages.Order(StringComparer.Ordinal));
        await SharedProviderMetadataUiChecks.AssertAgentModelNamesAsync(client, clientUrl,
            "UI Shared Multimedia Agent", "gpt-4.1-mini", evidence, mirroredChat, "gpt-5.4-mini", "real-openai");
        await File.WriteAllTextAsync(Path.Combine(evidence, "catalog-result.json"), JsonSerializer.Serialize(new {
            ChatModels = mirroredChat, ImageModels = mirroredImages,
            Source = sourceUrl, Client = clientUrl, ProviderDefault = "gpt-4.1-mini",
            SelectedNondefaultAgentModel = "gpt-5.4-mini", PricesAndPrivateFlagMatch = true,
            ConfiguredThroughUi = true
        }));
    }

    private static async Task ConfigureChatAsync(IPage page, string url, IReadOnlyList<string> realModels) {
        await SharedProviderMetadataUiChecks.OpenProviderAsync(page, url, ChatProvider);
        await page.GetByTestId("providers-base-url-input").FillAsync("https://api.openai.com/v1");
        await page.GetByTestId("providers-model-input").FillAsync("gpt-4.1-mini");
        await page.GetByTestId("providers-api-key-input").SelectOptionAsync(new SelectOptionValue { Label = OpenAiSecretName });
        await page.GetByTestId("provider-editor-tab-prices").ClickAsync();
        await page.GetByTestId("provider-pricing-refresh-button").ClickAsync();
        await page.GetByText("Provider models loaded", new() { Exact = true }).WaitForAsync();
        await page.GetByTestId("provider-editor-tab-runtime").ClickAsync();
        var loaded = (await page.GetByTestId("providers-suggested-models").InputValueAsync())
            .Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries);
        Assert.Equal(realModels, loaded.Order(StringComparer.Ordinal));
        await page.GetByTestId("providers-save").ClickAsync();
        await page.GetByText("Provider profile saved.", new() { Exact = true }).WaitForAsync();
    }

    private static async Task ConfigureImagesAsync(IPage page, string url, IReadOnlyList<string> imageModels) {
        await SharedProviderMetadataUiChecks.OpenProviderAsync(page, url, ImageProvider);
        await page.GetByTestId("providers-base-url-input").FillAsync("https://api.openai.com/v1");
        await page.GetByTestId("providers-model-input").FillAsync("gpt-image-1-mini");
        await page.GetByTestId("providers-api-key-input").SelectOptionAsync(new SelectOptionValue { Label = OpenAiSecretName });
        await page.GetByTestId("provider-editor-tab-runtime").ClickAsync();
        await page.GetByTestId("providers-suggested-models").FillAsync(string.Join('\n', imageModels));
        await page.GetByTestId("provider-editor-tab-prices").ClickAsync();
        var pricedRows = page.Locator("[data-testid^='provider-pricing-row-']");
        while (await pricedRows.CountAsync() > 0) {
            await pricedRows.First.GetByRole(AriaRole.Button, new() { Name = "Remove", Exact = true }).ClickAsync();
        }
        await page.GetByTestId("providers-save").ClickAsync();
        await page.GetByText("Provider profile saved.", new() { Exact = true }).WaitForAsync();
    }

    private static async Task<string> IssueTokenAsync(IPage page, string url, string scopes) {
        await SharedProviderTwoInstanceUiAcceptanceTests.NavigateAsync(page, $"{url}/settings?tab=api-access");
        await Field(page, "Subject").FillAsync("real-provider-validation-client");
        await Field(page, "Display name").FillAsync("Real provider validation");
        await Field(page, "Lifetime minutes").FillAsync("240");
        await Field(page, "Scopes").FillAsync(scopes);
        await page.GetByRole(AriaRole.Button, new() { Name = "Create token", Exact = true }).ClickAsync();
        var output = page.Locator("textarea[readonly]");
        await output.WaitForAsync();
        var token = await output.InputValueAsync();
        Assert.True(token.StartsWith("eyJ", StringComparison.Ordinal), "The UI did not issue a JWT.");
        return token;
    }

    private static async Task SaveSecretSafelyAsync(IPage page, string url, string name, string value) {
        try {
            await SharedProviderTwoInstanceUiAcceptanceTests.CreateSecretAsync(page, url, name, value);
        } catch {
            throw new XunitException($"UI secret entry failed for '{name}'. Credential details were suppressed.");
        }
    }

    private static async Task SynchronizeAsync(IPage page, string url) {
        await SharedProviderTwoInstanceUiAcceptanceTests.NavigateAsync(page, $"{url}/agents?tab=providers");
        await page.GetByTestId("providers-tree-provider").First.WaitForAsync();
        await page.GetByTestId("provider-editor-tab-sharing").ClickAsync();
        var source = page.GetByTestId("shared-provider-source-card").Filter(new() { HasTextString = SourceName });
        await source.GetByTestId("shared-provider-source-discover").ClickAsync();
        var dialog = page.GetByTestId("shared-provider-catalog-dialog");
        await dialog.WaitForAsync();
        var selections = dialog.GetByTestId("shared-provider-catalog-selection");
        for (var index = 0; index < await selections.CountAsync(); index++) {
            await selections.Nth(index).SetCheckedAsync(true);
        }
        await page.GetByTestId("shared-provider-catalog-apply").ClickAsync();
        await dialog.WaitForAsync(new() { State = WaitForSelectorState.Detached });
        await page.GetByText("Shared providers imported", new() { Exact = true }).WaitForAsync();
    }

    private static ILocator Field(IPage page, string label) => page.Locator("label.cda-field-label")
        .Filter(new() { HasTextString = label }).First.Locator("xpath=following-sibling::*[1]");

    private static string Required(string name) => Environment.GetEnvironmentVariable(name) is { Length: > 0 } value
        ? value : throw SkipException.ForSkip($"Set {name} to run the explicitly configured real-provider UI check.");
}
