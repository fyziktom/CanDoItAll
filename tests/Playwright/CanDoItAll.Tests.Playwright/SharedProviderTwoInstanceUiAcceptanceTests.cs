using Microsoft.Playwright;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Xunit.Sdk;

namespace CanDoItAll.Tests.Playwright;

public sealed class SharedProviderTwoInstanceUiAcceptanceTests
{
    private const string SharedUrlEnvironmentVariable = "CANDOITALL_SHARED_UI_SHARED_URL";
    private const string ClientUrlEnvironmentVariable = "CANDOITALL_SHARED_UI_CLIENT_URL";
    private const string UpstreamTokenFileEnvironmentVariable = "CANDOITALL_SHARED_UI_UPSTREAM_TOKEN_FILE";
    private const string EvidenceDirectoryEnvironmentVariable = "CANDOITALL_SHARED_UI_EVIDENCE_DIRECTORY";
    private const string VisionImageEnvironmentVariable = "CANDOITALL_SHARED_UI_VISION_IMAGE";
    private const string UpstreamSecretName = "UI acceptance upstream token";
    private const string SourceSecretName = "UI shared instance JWT";
    private const string SourceName = "UI shared instance";
    private const string OpenAiChatProviderName = "UI Shared OpenAI Chat";
    private const string OpenAiImageProviderName = "UI Shared OpenAI Image";
    private const string OllamaProviderName = "UI Shared Ollama";
    private const string OllamaAgentName = "UI Shared Ollama Agent";
    private const string MultimediaAgentName = "UI Shared Multimedia Agent";

    [Fact]
    [Trait("Category", "ExternalSharedProviderUi")]
    public async Task Provider_empty_client_imports_shared_providers_and_runs_chat_image_and_vision()
    {
        var settings = LoadSettings();
        Directory.CreateDirectory(settings.EvidenceDirectory);
        var browserErrors = new List<string>();

        using var playwright = await Microsoft.Playwright.Playwright.CreateAsync();
        await using var browser = await playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions
        {
            Headless = true
        });
        var contextOptions = new BrowserNewContextOptions
        {
            ViewportSize = new ViewportSize
            {
                Width = 1920,
                Height = 1080
            }
        };
        await using var sharedContext = await browser.NewContextAsync(contextOptions);
        await using var clientContext = await browser.NewContextAsync(contextOptions);
        var sharedPage = await sharedContext.NewPageAsync();
        var clientPage = await clientContext.NewPageAsync();
        ConfigurePage(sharedPage, browserErrors);
        ConfigurePage(clientPage, browserErrors);

        var sourceToken = await ConfigureSharedInstanceAsync(sharedPage, settings);
        await VerifySharedRelayContractAsync(settings, sourceToken);
        await ConfigureClientInstanceAsync(clientPage, settings, sourceToken);
        await ExerciseClientAgentsAsync(clientPage, settings);

        Assert.False(await sharedPage.Locator("#blazor-error-ui").IsVisibleAsync());
        Assert.False(await clientPage.Locator("#blazor-error-ui").IsVisibleAsync());
        Assert.Empty(browserErrors);
    }

    private static async Task<string> ConfigureSharedInstanceAsync(IPage page, AcceptanceSettings settings)
    {
        await NavigateAsync(page, $"{settings.SharedUrl}/settings?tab=api-access");
        await FieldByLabel(page, "Subject").FillAsync("shared-providers-ui-client");
        await FieldByLabel(page, "Display name").FillAsync("Shared provider desktop client");
        await FieldByLabel(page, "Lifetime minutes").FillAsync("120");
        await FieldByLabel(page, "Scopes").FillAsync(
            "api.shared-providers.catalog.read api.shared-providers.invoke");
        await page.GetByRole(AriaRole.Button, new() { Name = "Create token", Exact = true }).ClickAsync();
        var tokenField = page.Locator("textarea[readonly]");
        await tokenField.WaitForAsync();
        var sourceToken = await tokenField.InputValueAsync();
        Assert.StartsWith("eyJ", sourceToken, StringComparison.Ordinal);
        await tokenField.EvaluateAsync(
            "element => { element.value = '[redacted after capture]'; }");
        await ScreenshotAsync(page, settings, "01-shared-api-token-issued.png");

        var upstreamToken = (await File.ReadAllTextAsync(settings.UpstreamTokenFile)).TrimEnd('\r', '\n');
        Assert.NotEmpty(upstreamToken);
        await CreateSecretAsync(page, settings.SharedUrl, UpstreamSecretName, upstreamToken);
        await DeleteProvidersNamedAsync(page, settings.SharedUrl, "New OpenAI provider");
        await CreateAndPublishProviderAsync(
            page,
            settings,
            OpenAiChatProviderName,
            "e2e-duplicate-model",
            "OpenAi",
            "Chat",
            "http://candoitall-spui-upstream:8080/v1",
            UpstreamSecretName,
            "ChatCompletions");
        await CreateAndPublishProviderAsync(
            page,
            settings,
            OpenAiImageProviderName,
            "e2e-openai-image",
            "OpenAi",
            "ImageGeneration",
            "http://candoitall-spui-upstream:8080/v1",
            UpstreamSecretName,
            "Responses");
        await CreateAndPublishProviderAsync(
            page,
            settings,
            OllamaProviderName,
            "e2e-ollama",
            "Ollama",
            "Chat",
            "http://candoitall-spui-upstream:8080",
            UpstreamSecretName,
            "ChatCompletions");
        await ScreenshotAsync(page, settings, "02-shared-three-providers-published.png");
        return sourceToken;
    }

    private static async Task VerifySharedRelayContractAsync(
        AcceptanceSettings settings,
        string sourceToken)
    {
        using var client = new HttpClient
        {
            BaseAddress = new Uri(settings.SharedUrl, UriKind.Absolute)
        };
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", sourceToken);

        using var catalogResponse = await client.GetAsync(
            "/api/shared-providers/v1/catalog");
        var catalogJson = await catalogResponse.Content.ReadAsStringAsync();
        Assert.True(
            catalogResponse.IsSuccessStatusCode,
            $"Shared-provider catalog preflight returned HTTP {(int)catalogResponse.StatusCode}: {catalogJson}");

        using var catalog = JsonDocument.Parse(catalogJson);
        var publication = catalog.RootElement
            .GetProperty("providers")
            .EnumerateArray()
            .Single(item => string.Equals(
                item.GetProperty("displayName").GetString(),
                OllamaProviderName,
                StringComparison.Ordinal));
        var model = publication.GetProperty("defaultModelId").GetString();
        Assert.False(string.IsNullOrWhiteSpace(model));

        using var relayResponse = await client.PostAsJsonAsync(
            "/api/shared-providers/openai/v1/chat/completions",
            new
            {
                model,
                messages = new[]
                {
                    new
                    {
                        role = "user",
                        content = "Verify the shared-provider relay contract."
                    }
                },
                stream = false
            });
        var relayJson = await relayResponse.Content.ReadAsStringAsync();
        Assert.True(
            relayResponse.IsSuccessStatusCode,
            $"Shared-provider inference preflight returned HTTP {(int)relayResponse.StatusCode}: {relayJson}");
        Assert.Contains(
            "deterministic fixture response",
            relayJson,
            StringComparison.Ordinal);

        var streamingResponse = await client.PostAsJsonAsync(
            "/api/shared-providers/openai/v1/chat/completions",
            new
            {
                model,
                messages = new[]
                {
                    new
                    {
                        role = "user",
                        content = "Verify the shared-provider streaming relay contract."
                    }
                },
                stream = true,
                stream_options = new
                {
                    include_usage = true
                }
            });
        var streamingRelayBody = await streamingResponse.Content.ReadAsStringAsync();
        Assert.True(
            streamingResponse.IsSuccessStatusCode,
            $"Shared-provider streaming inference preflight returned HTTP {(int)streamingResponse.StatusCode}: {streamingRelayBody}");
    }

    private static async Task ConfigureClientInstanceAsync(
        IPage page,
        AcceptanceSettings settings,
        string sourceToken)
    {
        await CreateSecretAsync(page, settings.ClientUrl, SourceSecretName, sourceToken);
        await NavigateAsync(page, $"{settings.ClientUrl}/agents?tab=providers");
        await page.GetByTestId("agents-provider-profiles-panel").WaitForAsync();
        await page.GetByTestId("providers-tree-provider").First.WaitForAsync();
        await page.GetByTestId("provider-editor-tab-sharing").ClickAsync();
        await page.GetByTestId("shared-provider-source-add").WaitForAsync();
        var existingSource = page.GetByTestId("shared-provider-source-card")
            .Filter(new LocatorFilterOptions { HasTextString = SourceName });
        if (await existingSource.CountAsync() > 0)
        {
            await existingSource.GetByTestId("shared-provider-source-test").ClickAsync();
            await page.GetByText("Source connection passed", new() { Exact = true }).WaitForAsync();
            await existingSource.GetByTestId("shared-provider-source-discover").ClickAsync();
            var existingCatalogDialog = page.GetByTestId("shared-provider-catalog-dialog");
            await existingCatalogDialog.WaitForAsync();
            var existingSelections = existingCatalogDialog.GetByTestId("shared-provider-catalog-selection");
            Assert.Equal(3, await existingSelections.CountAsync());
            for (var index = 0; index < await existingSelections.CountAsync(); index++)
            {
                Assert.True(await existingSelections.Nth(index).IsCheckedAsync());
            }

            await page.GetByTestId("shared-provider-catalog-apply").ClickAsync();
            await existingCatalogDialog.WaitForAsync(new LocatorWaitForOptions { State = WaitForSelectorState.Detached });
            await page.GetByText("Shared providers imported", new() { Exact = true }).WaitForAsync();
            await AssertProviderVisibleAsync(page, OpenAiChatProviderName);
            await AssertProviderVisibleAsync(page, OpenAiImageProviderName);
            await AssertProviderVisibleAsync(page, OllamaProviderName);
            await ScreenshotAsync(page, settings, "04-client-three-shared-providers-imported.png");
            await CreateAgentAsync(page, settings, OllamaAgentName, OllamaProviderName, null);
            await CreateAgentAsync(page, settings, MultimediaAgentName, OpenAiChatProviderName, OpenAiImageProviderName);
            await ScreenshotAsync(page, settings, "05-client-agents-created-from-shared-providers.png");
            return;
        }

        await DeleteAllPersistedProvidersAsync(page);
        await page.GetByTestId("providers-new").ClickAsync();
        await page.GetByRole(AriaRole.Heading, new() { Name = "New provider profile", Exact = true }).WaitForAsync();
        await page.GetByTestId("provider-editor-tab-sharing").ClickAsync();
        await page.GetByTestId("shared-provider-source-add").WaitForAsync();
        Assert.Contains(
            "No provider selected",
            await page.GetByTestId("shared-provider-management").InnerTextAsync(),
            StringComparison.OrdinalIgnoreCase);
        await ScreenshotAsync(page, settings, "03-client-empty-provider-catalog-source-controls.png");

        await NavigateAsync(page, $"{settings.ClientUrl}/agents?tab=providers");
        await page.GetByTestId("provider-editor-tab-sharing").ClickAsync();
        await page.GetByTestId("shared-provider-source-add").ClickAsync();
        var sourceDialog = page.GetByTestId("shared-provider-source-dialog");
        await sourceDialog.WaitForAsync();
        await page.GetByTestId("shared-provider-source-name").FillAsync(SourceName);
        await page.GetByTestId("shared-provider-source-uri").FillAsync("http://candoitall-spui-shared:8080/");
        await page.GetByTestId("shared-provider-source-secret").SelectOptionAsync(
            new SelectOptionValue { Label = SourceSecretName });
        await sourceDialog.GetByText("Allow HTTP on a private network", new() { Exact = true }).ClickAsync();
        await page.GetByTestId("shared-provider-source-save").ClickAsync();
        await sourceDialog.WaitForAsync(new LocatorWaitForOptions { State = WaitForSelectorState.Detached });

        var sourceCard = page.GetByTestId("shared-provider-source-card").Filter(new() { HasTextString = SourceName });
        await sourceCard.WaitForAsync();
        await sourceCard.GetByTestId("shared-provider-source-test").ClickAsync();
        await page.GetByText("Source connection passed", new() { Exact = true }).WaitForAsync();
        await sourceCard.GetByTestId("shared-provider-source-discover").ClickAsync();
        var catalogDialog = page.GetByTestId("shared-provider-catalog-dialog");
        await catalogDialog.WaitForAsync();
        var selections = catalogDialog.GetByTestId("shared-provider-catalog-selection");
        Assert.Equal(3, await selections.CountAsync());
        for (var index = 0; index < await selections.CountAsync(); index++)
        {
            await selections.Nth(index).CheckAsync();
        }

        await page.GetByTestId("shared-provider-catalog-apply").ClickAsync();
        await catalogDialog.WaitForAsync(new LocatorWaitForOptions { State = WaitForSelectorState.Detached });
        await page.GetByText("Shared providers imported", new() { Exact = true }).WaitForAsync();
        await page.GetByText("4 profile(s)", new() { Exact = true }).WaitForAsync();
        await AssertProviderVisibleAsync(page, OpenAiChatProviderName);
        await AssertProviderVisibleAsync(page, OpenAiImageProviderName);
        await AssertProviderVisibleAsync(page, OllamaProviderName);
        await ScreenshotAsync(page, settings, "04-client-three-shared-providers-imported.png");

        await CreateAgentAsync(page, settings, OllamaAgentName, OllamaProviderName, null);
        await CreateAgentAsync(page, settings, MultimediaAgentName, OpenAiChatProviderName, OpenAiImageProviderName);
        await ScreenshotAsync(page, settings, "05-client-agents-created-from-shared-providers.png");
    }

    private static async Task ExerciseClientAgentsAsync(IPage page, AcceptanceSettings settings)
    {
        await NavigateAsync(page, $"{settings.ClientUrl}/agents?tab=chat");
        await page.GetByTestId("agents-chat-panel").WaitForAsync();
        var chatWorkspace = page.GetByTestId("chat-workspace-panel");

        await SwitchAgentAsync(page, OllamaAgentName);
        await EnsureNewThreadAsync(page);
        await SendPromptAsync(page, "Reply with a short confirmation from the shared Ollama provider.");
        await chatWorkspace.GetByText("deterministic fixture response", new() { Exact = true }).WaitForAsync(
            new LocatorWaitForOptions { Timeout = 60_000 });
        await ScreenshotAsync(page, settings, "06-client-ollama-shared-chat.png");

        await SwitchAgentAsync(page, MultimediaAgentName);
        await EnsureNewThreadAsync(page);
        await page.GetByTestId("chat-approve-once-button").WaitForAsync(
            new LocatorWaitForOptions
            {
                State = WaitForSelectorState.Detached,
                Timeout = 30_000
            });
        await SendPromptAsync(page, "Create an image of a blue geometric lighthouse for this acceptance test.");
        await chatWorkspace.GetByText("image_generation_create", new() { Exact = true }).WaitForAsync();
        var approveOnceButton = page.GetByTestId("chat-approve-once-button");
        await approveOnceButton.WaitForAsync(new LocatorWaitForOptions { Timeout = 30_000 });
        await ScreenshotAsync(page, settings, "07a-client-shared-image-generation-approval.png");
        await approveOnceButton.ClickAsync();
        await approveOnceButton.WaitForAsync(
            new LocatorWaitForOptions
            {
                State = WaitForSelectorState.Detached,
                Timeout = 30_000
            });
        await chatWorkspace.GetByText("deterministic fixture response", new() { Exact = false }).Last.WaitForAsync(
            new LocatorWaitForOptions { Timeout = 90_000 });
        await chatWorkspace.GetByText("shared-provider-ui/generated.png", new() { Exact = false }).First.WaitForAsync(
            new LocatorWaitForOptions { Timeout = 30_000 });
        await ScreenshotAsync(page, settings, "07-client-shared-image-generation.png");

        await EnsureNewThreadAsync(page);
        await page.GetByTestId("chat-image-attachment-input").SetInputFilesAsync(settings.VisionImagePath);
        await page.GetByText("1 staged", new() { Exact = true }).WaitForAsync();
        await SendPromptAsync(page, "Analyze the attached image and confirm that you received visual input.");
        await chatWorkspace.GetByText("deterministic fixture analyzed the attached image", new() { Exact = true }).WaitForAsync(
            new LocatorWaitForOptions { Timeout = 90_000 });
        await ScreenshotAsync(page, settings, "08-client-shared-image-analysis.png");
    }

    private static async Task CreateAndPublishProviderAsync(
        IPage page,
        AcceptanceSettings settings,
        string name,
        string model,
        string kind,
        string purpose,
        string baseUrl,
        string secretName,
        string transport)
    {
        await NavigateAsync(page, $"{settings.SharedUrl}/agents?tab=providers");
        await page.GetByTestId("providers-tree-provider").First.WaitForAsync();
        var existingProvider = page.GetByTestId("providers-tree-provider")
            .Filter(new LocatorFilterOptions { HasTextString = name })
            .First;
        if (await existingProvider.CountAsync() > 0)
        {
            await existingProvider.ClickAsync();
            await page.GetByTestId("provider-editor-tab-sharing").ClickAsync();
            var publicationStatus = page.GetByTestId("shared-provider-publication-status");
            await publicationStatus.WaitForAsync();
            if ((await publicationStatus.InnerTextAsync()).Contains("Published", StringComparison.Ordinal))
            {
                return;
            }

            await page.GetByTestId("provider-editor-tab-runtime").ClickAsync();
            await page.GetByRole(AriaRole.Button, new() { Name = "Health", Exact = true }).ClickAsync();
            await page.GetByText("Provider health check passed", new() { Exact = true }).WaitForAsync(
                new LocatorWaitForOptions { Timeout = 60_000 });
            await page.GetByTestId("provider-editor-tab-sharing").ClickAsync();
            await page.GetByTestId("shared-provider-publish").ClickAsync();
            await ExpectTextAsync(publicationStatus, "Published");
            return;
        }

        await page.GetByTestId("providers-new").ClickAsync();
        await page.GetByRole(AriaRole.Heading, new() { Name = "New provider profile", Exact = true }).WaitForAsync();
        await page.GetByTestId("providers-name-input").FillAsync(name);
        await page.GetByTestId("providers-model-input").FillAsync(model);
        await page.GetByTestId("providers-kind-select").SelectOptionAsync(kind);
        await page.GetByTestId("providers-purpose-select").SelectOptionAsync(purpose);
        await page.GetByTestId("providers-base-url-input").FillAsync(baseUrl);
        await page.GetByTestId("providers-api-key-input").SelectOptionAsync(
            new SelectOptionValue { Label = secretName });
        await page.GetByTestId("provider-editor-tab-runtime").ClickAsync();
        await page.GetByTestId("providers-transport-select").SelectOptionAsync(transport);
        await page.GetByTestId("providers-suggested-models").FillAsync(model);
        await page.GetByTestId("providers-save").ClickAsync();
        try
        {
            await AssertProviderVisibleAsync(page, name);
        }
        catch (TimeoutException)
        {
            await ScreenshotAsync(page, settings, "failure-provider-save.png");
            throw new XunitException(
                $"Provider '{name}' was not saved through the UI. Visible page text:{Environment.NewLine}{await page.Locator("body").InnerTextAsync()}");
        }

        await page.GetByRole(AriaRole.Button, new() { Name = "Health", Exact = true }).ClickAsync();
        await page.GetByText("Provider health check passed", new() { Exact = true }).WaitForAsync(
            new LocatorWaitForOptions { Timeout = 60_000 });
        await page.GetByTestId("provider-editor-tab-sharing").ClickAsync();
        await page.GetByTestId("shared-provider-publish").ClickAsync();
        await ExpectTextAsync(page.GetByTestId("shared-provider-publication-status"), "Published");
    }

    private static async Task CreateSecretAsync(IPage page, string baseUrl, string name, string value)
    {
        await NavigateAsync(page, $"{baseUrl}/settings?tab=secrets");
        var existingSecret = page.GetByText(name, new() { Exact = true }).First;
        if (await existingSecret.CountAsync() > 0)
        {
            await SelectSecretForEditingAsync(page, existingSecret, name);
            await page.GetByTestId("settings-secret-value").FillAsync(value);
            await page.GetByRole(AriaRole.Button, new() { Name = "Save secret", Exact = true }).ClickAsync();
            await page.GetByText("Secret saved", new() { Exact = true }).WaitForAsync();
            await AssertSecretValueRoundTripsThroughUiAsync(page, name, value);
            return;
        }

        await page.GetByRole(AriaRole.Button, new() { Name = "New secret", Exact = true }).ClickAsync();
        await FieldByLabel(page, "Name").FillAsync(name);
        await FieldByLabel(page, "Kind").SelectOptionAsync("ApiKey");
        await FieldByLabel(page, "Scope").FillAsync("workspace");
        await page.GetByTestId("settings-secret-value").FillAsync(value);
        await page.GetByRole(AriaRole.Button, new() { Name = "Save secret", Exact = true }).ClickAsync();
        await page.GetByText(name, new() { Exact = true }).First.WaitForAsync();
        await AssertSecretValueRoundTripsThroughUiAsync(page, name, value);
    }

    private static async Task AssertSecretValueRoundTripsThroughUiAsync(
        IPage page,
        string name,
        string expectedValue)
    {
        var secret = page.GetByText(name, new() { Exact = true }).First;
        await SelectSecretForEditingAsync(page, secret, name);
        var actualValue = await page.GetByTestId("settings-secret-value").InputValueAsync();
        var expectedHash = SHA256.HashData(Encoding.UTF8.GetBytes(expectedValue));
        var actualHash = SHA256.HashData(Encoding.UTF8.GetBytes(actualValue));
        Assert.True(
            CryptographicOperations.FixedTimeEquals(expectedHash, actualHash),
            $"Secret '{name}' did not round-trip through the UI with the submitted value.");
    }

    private static async Task SelectSecretForEditingAsync(
        IPage page,
        ILocator secret,
        string expectedName)
    {
        await secret.ClickAsync();
        var nameField = FieldByLabel(page, "Name");
        var timeoutAt = DateTimeOffset.UtcNow.AddSeconds(30);
        while (DateTimeOffset.UtcNow < timeoutAt)
        {
            if (string.Equals(
                await nameField.InputValueAsync(),
                expectedName,
                StringComparison.Ordinal))
            {
                return;
            }

            await Task.Delay(100);
        }

        throw new TimeoutException($"Timed out waiting for secret '{expectedName}' to load for editing.");
    }

    private static async Task DeleteProvidersNamedAsync(IPage page, string baseUrl, string providerName)
    {
        await NavigateAsync(page, $"{baseUrl}/agents?tab=providers");
        await page.GetByTestId("providers-tree-provider").First.WaitForAsync();
        var provider = page.GetByTestId("providers-tree-provider")
            .Filter(new LocatorFilterOptions { HasTextString = providerName })
            .First;
        while (await provider.CountAsync() > 0)
        {
            var selectedProviderRowId = await provider.GetAttributeAsync("id");
            Assert.False(string.IsNullOrWhiteSpace(selectedProviderRowId));
            await provider.ClickAsync();
            await page.GetByRole(AriaRole.Button, new() { Name = "Delete", Exact = true }).ClickAsync();
            await page.GetByText("Provider deleted", new() { Exact = true }).WaitForAsync();
            await page.Locator($"[id='{selectedProviderRowId}']")
                .WaitForAsync(new LocatorWaitForOptions { State = WaitForSelectorState.Detached });
        }
    }

    private static async Task DeleteAllPersistedProvidersAsync(IPage page)
    {
        for (var attempt = 0; attempt < 20; attempt++)
        {
            var profileCount = page.GetByText(
                new System.Text.RegularExpressions.Regex(@"^\d+ profile\(s\)$"))
                .First;
            var profileCountText = await profileCount.InnerTextAsync();
            var separatorIndex = profileCountText.IndexOf(' ', StringComparison.Ordinal);
            Assert.True(separatorIndex > 0);
            var remainingProfiles = int.Parse(profileCountText[..separatorIndex]);
            if (remainingProfiles == 0)
            {
                break;
            }

            if (remainingProfiles == 1 &&
                await page.GetByRole(AriaRole.Heading, new() { Name = "Remote Ollama", Exact = true }).CountAsync() > 0)
            {
                break;
            }

            var deleteButton = page.GetByRole(AriaRole.Button, new() { Name = "Delete", Exact = true });
            if (await deleteButton.CountAsync() == 0 || !await deleteButton.IsVisibleAsync())
            {
                break;
            }

            await deleteButton.EvaluateAsync("button => button.click()");
            await page.GetByText($"{remainingProfiles - 1} profile(s)", new() { Exact = true }).WaitForAsync();
        }

        await page.GetByText("1 profile(s)", new() { Exact = true }).WaitForAsync();
        await page.GetByRole(AriaRole.Heading, new() { Name = "Remote Ollama", Exact = true }).WaitForAsync();
    }

    private static async Task CreateAgentAsync(
        IPage page,
        AcceptanceSettings settings,
        string agentName,
        string runtimeProviderName,
        string? imageProviderName)
    {
        await NavigateAsync(page, $"{settings.ClientUrl}/agents?tab=agents");
        var existingAgentCard = page.GetByTestId("agents-catalog-card-shell")
            .Filter(new LocatorFilterOptions { HasTextString = agentName })
            .First;
        if (await existingAgentCard.CountAsync() > 0)
        {
            await existingAgentCard.DblClickAsync();
            var existingDialog = page.GetByTestId("agents-details-dialog").Last;
            await existingDialog.WaitForAsync();
            await ConfigureAgentRuntimeAsync(existingDialog, runtimeProviderName, imageProviderName);
            await existingDialog.GetByTestId("agents-catalog-save").ClickAsync();
            await page.GetByText("Agent saved", new() { Exact = true }).WaitForAsync();
            await NavigateAsync(page, $"{settings.ClientUrl}/agents?tab=agents");
            await page.GetByText(agentName, new() { Exact = true }).First.WaitForAsync();
            return;
        }

        await page.GetByTestId("agents-catalog-new").ClickAsync();
        var dialog = page.GetByTestId("agents-details-dialog");
        await dialog.WaitForAsync();
        await page.GetByTestId("agents-catalog-name").FillAsync(agentName);
        await page.GetByTestId("agents-catalog-role").FillAsync("Shared provider acceptance agent");
        await page.GetByTestId("agents-catalog-summary").FillAsync("Exercises remote shared-provider inference.");
        await page.GetByTestId("agents-catalog-instructions").FillAsync(
            "Use the configured provider. When asked to create an image, call image_generation_create exactly once.");

        await ConfigureAgentRuntimeAsync(dialog, runtimeProviderName, imageProviderName);

        await page.GetByTestId("agents-catalog-save").ClickAsync();
        await page.GetByText("Agent saved", new() { Exact = true }).WaitForAsync();
        await NavigateAsync(page, $"{settings.ClientUrl}/agents?tab=agents");
        await page.GetByText(agentName, new() { Exact = true }).First.WaitForAsync();
    }

    private static async Task ConfigureAgentRuntimeAsync(
        ILocator dialog,
        string runtimeProviderName,
        string? imageProviderName)
    {
        await dialog.GetByRole(AriaRole.Tab, new() { Name = "Runtime", Exact = true }).ClickAsync();
        await FieldByLabel(dialog, "Status").SelectOptionAsync("Active");
        await SelectOptionContainingAsync(dialog.GetByTestId("agents-catalog-provider"), runtimeProviderName);
        var approvalCheckbox = dialog.GetByTestId("agents-catalog-require-external-approval");
        if (await approvalCheckbox.IsCheckedAsync())
        {
            await approvalCheckbox.UncheckAsync();
        }

        await dialog.GetByRole(AriaRole.Tab, new() { Name = "Images", Exact = true }).ClickAsync();
        var imageGenerationCheckbox = dialog.GetByTestId("agents-catalog-image-generation-enabled");
        if (imageProviderName is not null)
        {
            await imageGenerationCheckbox.CheckAsync();
            await SelectOptionContainingAsync(
                dialog.GetByTestId("agents-catalog-image-generation-provider"),
                imageProviderName);
            return;
        }

        if (await imageGenerationCheckbox.IsCheckedAsync())
        {
            await imageGenerationCheckbox.UncheckAsync();
        }
    }

    private static async Task SwitchAgentAsync(IPage page, string agentName)
    {
        await page.GetByTestId("agent-switch-button").ClickAsync();
        var dialog = page.GetByTestId("agent-switch-dialog");
        await dialog.WaitForAsync();
        await page.GetByTestId("agent-switch-search").FillAsync(agentName);
        var card = page.GetByTestId("agent-switch-card-shell").Filter(new() { HasTextString = agentName });
        await card.GetByTestId("agent-switch-card").ClickAsync();
        await dialog.WaitForAsync(new LocatorWaitForOptions { State = WaitForSelectorState.Detached });
        await ExpectTextAsync(page.GetByTestId("agent-thread-selected-agent"), agentName);
    }

    private static async Task EnsureNewThreadAsync(IPage page)
    {
        await page.GetByRole(AriaRole.Button, new() { Name = "New thread", Exact = true }).First.ClickAsync();
        await page.GetByTestId("chat-prompt-input").WaitForAsync();
    }

    private static async Task SendPromptAsync(IPage page, string prompt)
    {
        await page.GetByTestId("chat-prompt-input").FillAsync(prompt);
        await page.GetByTestId("chat-send-button").ClickAsync();
    }

    private static async Task AssertProviderVisibleAsync(IPage page, string providerName)
    {
        await page.GetByTestId("providers-tree-provider")
            .Filter(new LocatorFilterOptions { HasTextString = providerName })
            .First
            .WaitForAsync();
    }

    private static async Task SelectOptionContainingAsync(ILocator select, string expectedText)
    {
        var option = select.Locator("option").Filter(new LocatorFilterOptions { HasTextString = expectedText }).First;
        await option.WaitForAsync(new LocatorWaitForOptions { State = WaitForSelectorState.Attached });
        var value = await option.GetAttributeAsync("value");
        Assert.False(string.IsNullOrWhiteSpace(value));
        await select.SelectOptionAsync(value);
    }

    private static async Task NavigateAsync(IPage page, string url)
    {
        var response = await page.GotoAsync(url, new PageGotoOptions { WaitUntil = WaitUntilState.DOMContentLoaded });
        Assert.NotNull(response);
        Assert.True(response.Ok, $"Navigation to '{url}' returned HTTP {response.Status}.");
        await DismissStartupModalIfPresentAsync(page);
    }

    private static async Task DismissStartupModalIfPresentAsync(IPage page)
    {
        var dialog = page.GetByTestId("database-startup-modal");
        try
        {
            await dialog.WaitForAsync(new LocatorWaitForOptions { Timeout = 1_500 });
        }
        catch (TimeoutException)
        {
            return;
        }

        await page.GetByTestId("database-startup-continue").ClickAsync();
        await dialog.WaitForAsync(new LocatorWaitForOptions { State = WaitForSelectorState.Detached });
    }

    private static ILocator FieldByLabel(IPage page, string label)
        => page.Locator("label.cda-field-label")
            .Filter(new LocatorFilterOptions { HasTextString = label })
            .First
            .Locator("xpath=following-sibling::*[1]");

    private static ILocator FieldByLabel(ILocator root, string label)
        => root.Locator("label.cda-field-label")
            .Filter(new LocatorFilterOptions { HasTextString = label })
            .First
            .Locator("xpath=following-sibling::*[1]");

    private static async Task ExpectTextAsync(ILocator locator, string expected)
    {
        var timeoutAt = DateTimeOffset.UtcNow.AddSeconds(30);
        while (DateTimeOffset.UtcNow < timeoutAt)
        {
            if ((await locator.InnerTextAsync()).Contains(expected, StringComparison.Ordinal))
            {
                return;
            }

            await Task.Delay(200);
        }

        throw new TimeoutException($"Timed out waiting for text '{expected}'.");
    }

    private static async Task ScreenshotAsync(IPage page, AcceptanceSettings settings, string fileName)
    {
        await page.ScreenshotAsync(new PageScreenshotOptions
        {
            Path = Path.Combine(settings.EvidenceDirectory, fileName),
            FullPage = true
        });
    }

    private static void ConfigurePage(IPage page, ICollection<string> browserErrors)
    {
        page.SetDefaultTimeout(30_000);
        page.PageError += (_, error) => browserErrors.Add(error);
    }

    private static AcceptanceSettings LoadSettings()
    {
        var sharedUrl = Environment.GetEnvironmentVariable(SharedUrlEnvironmentVariable);
        var clientUrl = Environment.GetEnvironmentVariable(ClientUrlEnvironmentVariable);
        var upstreamTokenFile = Environment.GetEnvironmentVariable(UpstreamTokenFileEnvironmentVariable);
        var evidenceDirectory = Environment.GetEnvironmentVariable(EvidenceDirectoryEnvironmentVariable);
        var visionImagePath = Environment.GetEnvironmentVariable(VisionImageEnvironmentVariable);
        if (string.IsNullOrWhiteSpace(sharedUrl) ||
            string.IsNullOrWhiteSpace(clientUrl) ||
            string.IsNullOrWhiteSpace(upstreamTokenFile) ||
            string.IsNullOrWhiteSpace(evidenceDirectory) ||
            string.IsNullOrWhiteSpace(visionImagePath))
        {
            throw SkipException.ForSkip(
                $"Set {SharedUrlEnvironmentVariable}, {ClientUrlEnvironmentVariable}, " +
                $"{UpstreamTokenFileEnvironmentVariable}, {EvidenceDirectoryEnvironmentVariable}, and " +
                $"{VisionImageEnvironmentVariable} to run the external two-instance UI acceptance test.");
        }

        if (!File.Exists(upstreamTokenFile))
        {
            throw new FileNotFoundException("The upstream token input file was not found.", upstreamTokenFile);
        }

        if (!File.Exists(visionImagePath))
        {
            throw new FileNotFoundException("The vision test image was not found.", visionImagePath);
        }

        return new AcceptanceSettings(
            sharedUrl.TrimEnd('/'),
            clientUrl.TrimEnd('/'),
            Path.GetFullPath(upstreamTokenFile),
            Path.GetFullPath(evidenceDirectory),
            Path.GetFullPath(visionImagePath));
    }

    private sealed record AcceptanceSettings(
        string SharedUrl,
        string ClientUrl,
        string UpstreamTokenFile,
        string EvidenceDirectory,
        string VisionImagePath);
}
