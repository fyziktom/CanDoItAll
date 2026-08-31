using System.Net;
using System.Text.Json;
using Microsoft.Playwright;
using Xunit.Sdk;

namespace CanDoItAll.Tests.Playwright;

public sealed class LocalSimpleChatBrowserAccessTests {
    [Theory]
    [Trait("Category", "ExternalRealSharedProviderUi")]
    [InlineData(false, "UI Shared OpenAI Chat", "gpt-5.4-mini", "LOCAL_OPENAI_OK")]
    [InlineData(false, "UI Shared Ollama", "gemma3:4b", "4")]
    [InlineData(true, "UI Shared OpenAI Chat", "gpt-5.4-mini", "LOCAL_SOURCE_OK")]
    public async Task LOCAL_UI_ACCESS_plain_browser_creates_and_runs_chat_while_API_stays_protected(
        bool source, string providerName, string model, string answer) {
        var url = Required(source ? "CANDOITALL_REAL_SHARED_URL" : "CANDOITALL_REAL_CLIENT_URL");
        var evidence = Required("CANDOITALL_REAL_UI_EVIDENCE");
        var label = $"local-{(source ? "source" : "client")}-{answer.ToLowerInvariant()}";
        Directory.CreateDirectory(evidence);
        using var playwright = await Microsoft.Playwright.Playwright.CreateAsync();
        await using var browser = await playwright.Chromium.LaunchAsync(new() { Headless = true, Channel = "chrome" });
        await using var context = await browser.NewContextAsync(new() {
            ViewportSize = new() { Width = 1920, Height = 1080 }
        });
        Assert.Empty(await context.CookiesAsync());
        var page = await context.NewPageAsync();
        page.SetDefaultTimeout(45_000);
        var authorizedRequests = 0;
        page.Request += (_, request) => {
            if (request.Headers.ContainsKey("authorization")) {
                Interlocked.Increment(ref authorizedRequests);
            }
        };
        await SharedProviderMetadataUiChecks.OpenProviderAsync(page, url, providerName);
        var defaultModel = await page.GetByTestId("providers-model-input").InputValueAsync();
        await page.GetByTestId("provider-editor-tab-runtime").ClickAsync();
        var models = (await page.GetByTestId("providers-suggested-models").InputValueAsync())
            .Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Append(defaultModel).Distinct(StringComparer.Ordinal).ToArray();
        Assert.Contains(model, models);
        await SharedProviderMetadataUiChecks.ExerciseSimpleChatAsync(page, url, providerName,
            defaultModel, models, model, evidence, label, answer,
            answer == "4" ? "What is two plus two? Reply using only the number." : null,
            importedProvider: !source);
        var conversationUrl = page.Url;
        await SharedProviderTwoInstanceUiAcceptanceTests.NavigateAsync(page, conversationUrl);
        await Assertions.Expect(page.GetByTestId("llm-chat-conversation-workspace")
            .Locator("[data-testid='conversation-message'].justify-start .chat-markdown").Last)
            .ToContainTextAsync(answer);
        await page.ScreenshotAsync(new() { Path = Path.Combine(evidence, $"{label}-reloaded.png"), FullPage = true });
        Assert.Equal(0, Volatile.Read(ref authorizedRequests));

        using var http = new HttpClient();
        http.DefaultRequestHeaders.Add("X-Forwarded-For", "127.0.0.1");
        using var api = await http.GetAsync($"{url}/api/llm-chats");
        using var files = await http.GetAsync($"{url}/authorized-files/content");
        Assert.Equal(HttpStatusCode.Unauthorized, api.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, files.StatusCode);
        await File.WriteAllTextAsync(Path.Combine(evidence, $"{label}-result.json"), JsonSerializer.Serialize(new {
            Invariants = new[] { "LOCAL-UI-ACCESS", "API-BOUNDARY" }, Url = url, Provider = providerName,
            Model = model, ExpectedAnswer = answer, ConversationUrl = conversationUrl,
            BrowserAuthorizationRequests = authorizedRequests, ApiStatus = (int)api.StatusCode,
            AuthorizedFileStatus = (int)files.StatusCode, CompletedAtUtc = DateTimeOffset.UtcNow
        }));
    }

    private static string Required(string name) => Environment.GetEnvironmentVariable(name) is { Length: > 0 } value
        ? value : throw SkipException.ForSkip($"Set {name} to run the explicitly configured real-provider UI check.");
}
