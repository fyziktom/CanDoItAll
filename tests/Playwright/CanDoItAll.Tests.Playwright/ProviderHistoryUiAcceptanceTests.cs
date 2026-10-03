using System.Runtime.ExceptionServices;
using System.Text.RegularExpressions;
using CanDoItAll.SharedProviders.Abstractions;
using Microsoft.Playwright;
using Xunit.Sdk;

namespace CanDoItAll.Tests.Playwright;

public sealed class ProviderHistoryUiAcceptanceTests {
    private const string SharedUrlVariable = "CANDOITALL_SHARED_UI_SHARED_URL";
    private const string ClientUrlVariable = "CANDOITALL_SHARED_UI_CLIENT_URL";
    private const string EvidenceDirectoryVariable = "CANDOITALL_SHARED_UI_EVIDENCE_DIRECTORY";
    private readonly string SourceSecretName = "UI shared instance JWT";
    private readonly string SourceName = "UI shared instance";
    private readonly string ProviderName = "UI Shared Ollama";
    private readonly AcceptanceSettings? fixtureSettings;
    private const string AcceptanceAgentName = "Provider history acceptance agent";
    private const string TokenNamePrefix = "History acceptance";
    private const string CredentialScopes =
        "api.shared-providers.catalog.read api.shared-providers.invoke";
    private const string Subject = "provider-history-acceptance-client";
    private static readonly string[] LegacySecretNames =
        ["History source A a5416ad7", "History source B a5416ad7"];

    public ProviderHistoryUiAcceptanceTests() { }

    internal ProviderHistoryUiAcceptanceTests(SharedProviderNativeDefaultsUiTests.Settings settings,
        string sourceSecretName, string sourceName, string providerName) {
        fixtureSettings = new(settings.Central, settings.Clients[0], settings.Evidence);
        SourceSecretName = sourceSecretName;
        SourceName = sourceName;
        ProviderName = providerName;
    }

    [Fact]
    [Trait("Category", "ExternalSharedProviderUi")]
    public async Task Provider_and_global_history_are_lazy_and_filter_the_same_attempts() {
        var settings = fixtureSettings ?? LoadSettings();
        Directory.CreateDirectory(settings.EvidenceDirectory);
        var suffix = Guid.NewGuid().ToString("N")[..8];
        var subject = $"{Subject}-{suffix}";
        var tokenNames = new[] { $"History acceptance A {suffix}", $"History acceptance B {suffix}" };
        var issued = new List<IssuedCredential>();
        string? originalSecretValue = null;
        Exception? failure = null;
        var cleanupFailures = new List<Exception>();

        using var playwright = await Microsoft.Playwright.Playwright.CreateAsync();
        await using var browser = await playwright.Chromium.LaunchAsync(new() { Headless = true, Channel = "chrome" });
        await using var sourceContext = await browser.NewContextAsync(ContextOptions());
        await using var clientContext = await browser.NewContextAsync(ContextOptions());
        var browserErrors = new List<string>();
        var requestFailures = new List<string>();
        sourceContext.Page += (_, page) => Observe(page);
        clientContext.Page += (_, page) => Observe(page);
        var source = await sourceContext.NewPageAsync();
        var client = await clientContext.NewPageAsync();
        source.SetDefaultTimeout(30_000);
        client.SetDefaultTimeout(30_000);

        try {
            await DeleteAgentAsync(client, settings.ClientUrl, AcceptanceAgentName);
            foreach (var name in LegacySecretNames) {
                await DeleteSecretAsync(client, settings.ClientUrl, name);
            }
            await DeleteCredentialsByPrefixAsync(source, settings.SharedUrl, TokenNamePrefix);
            originalSecretValue = await ReadSecretValueAsync(client, settings.ClientUrl, SourceSecretName);
            foreach (var name in tokenNames) {
                var credential = await IssueCredentialAsync(source, settings.SharedUrl, name, subject);
                await ValidateCredentialAsync(sourceContext.APIRequest, settings.SharedUrl, credential.Token);
                issued.Add(credential);
            }
            await SharedProviderTwoInstanceUiAcceptanceTests.CreateSecretAsync(
                client, settings.ClientUrl, SourceSecretName, issued[0].Token);
            await TestSourceAsync(client, settings.ClientUrl);
            await CreateAgentAsync(client, settings.ClientUrl, AcceptanceAgentName);
            await InvokeAgentAsync(client, settings.ClientUrl, AcceptanceAgentName,
                ["SHARED_HISTORY_KEY_A", "SHARED_HISTORY_ATTEMPT_2"]);
            await ProviderHistoryAcceptanceRelayClient.InvokeAsync(
                sourceContext.APIRequest, settings.SharedUrl, issued[1].Token, ProviderName);
            var expectedKeyLabels = issued.Select(item => $"Key {item.Id:N}"[..12]).ToArray();
            await NavigateAsync(source, $"{settings.SharedUrl}/agents?tab=request-history");
            var publisherResults = source.GetByTestId("history-results");
            for (var attempt = 0; attempt < 60; attempt++) {
                await source.GetByTestId("history-search").ClickAsync();
                await publisherResults.WaitForAsync();
                var text = await publisherResults.InnerTextAsync();
                if (expectedKeyLabels.All(label => text.Contains(label, StringComparison.OrdinalIgnoreCase))
                    && Regex.Matches(text, Regex.Escape(subject), RegexOptions.IgnoreCase).Count >= 3) {
                    break;
                }
                await Task.Delay(500);
            }
            await Assertions.Expect(publisherResults).ToContainTextAsync(expectedKeyLabels[1]);
            await ScreenshotAsync(client, settings, "5212-shared-agent-success.png");

            var clientGlobal = await SearchGlobalAsync(client, settings.ClientUrl, ProviderName);
            var clientProvider = await SearchProviderAsync(client, settings.ClientUrl, ProviderName);
            Assert.Equal(clientGlobal.Take(2).Order(), clientProvider.Take(2).Order());
            await ScreenshotAsync(client, settings, "5212-provider-history.png");

            var publisherGlobal = await SearchGlobalAsync(source, settings.SharedUrl, ProviderName, issued[0].Id);
            var publisherProvider = await SearchProviderAsync(source, settings.SharedUrl, ProviderName, issued[0].Id);
            Assert.Equal(publisherGlobal.Order(), publisherProvider.Order());
            var publisherText = await source.GetByTestId("history-results").InnerTextAsync();
            Assert.Contains(expectedKeyLabels[0], publisherText, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain(expectedKeyLabels[1], publisherText, StringComparison.OrdinalIgnoreCase);
            Assert.Equal(publisherProvider.Count, Regex.Matches(publisherText, Regex.Escape(subject), RegexOptions.IgnoreCase).Count);
            await ScreenshotAsync(source, settings, "5210-agent-key-provider-history.png");

            var relayGlobal = await SearchGlobalAsync(source, settings.SharedUrl, ProviderName, issued[1].Id, 1);
            var relayProvider = await SearchProviderAsync(source, settings.SharedUrl, ProviderName, issued[1].Id, 1);
            Assert.Equal(relayGlobal, relayProvider);
            Assert.Single(relayProvider);
            Assert.Empty(relayProvider.Intersect(publisherProvider));
            var relayText = await source.GetByTestId("history-results").InnerTextAsync();
            Assert.Contains(expectedKeyLabels[1], relayText, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain(expectedKeyLabels[0], relayText, StringComparison.OrdinalIgnoreCase);
            Assert.Single(Regex.Matches(relayText, Regex.Escape(subject), RegexOptions.IgnoreCase));
            await ScreenshotAsync(source, settings, "5210-relay-key-provider-history.png");
            await AssertPagingAndDeniedAuthorityAsync(browser, source, settings, issued, publisherProvider, relayProvider, Observe);
            await File.WriteAllTextAsync(Path.Combine(settings.EvidenceDirectory, "history-attempt-identities.json"),
                System.Text.Json.JsonSerializer.Serialize(new {
                    ClientGlobal = clientGlobal,
                    ClientProvider = clientProvider,
                    PublisherGlobal = publisherGlobal,
                    PublisherProvider = publisherProvider,
                    RelayGlobal = relayGlobal,
                    RelayProvider = relayProvider
                }));
            await DeleteCredentialsByPrefixAsync(source, settings.SharedUrl, tokenNames[0]);
            var revoked = await sourceContext.APIRequest.GetAsync(settings.SharedUrl + SharedProviderRoutes.Catalog,
                new() { Headers = new Dictionary<string, string> { ["Authorization"] = $"Bearer {issued[0].Token}" } });
            Assert.Equal(401, revoked.Status);
            await SharedProviderTwoInstanceUiAcceptanceTests.CreateSecretAsync(client, settings.ClientUrl, SourceSecretName, issued[1].Token);
            await TestSourceAsync(client, settings.ClientUrl);
            await InvokeAgentAsync(client, settings.ClientUrl, AcceptanceAgentName, ["SHARED_HISTORY_ROTATED_KEY_B"]);
            var rotated = await SearchProviderAsync(source, settings.SharedUrl, ProviderName, issued[1].Id);
            Assert.Contains(relayProvider[0], rotated);
            Assert.NotEmpty(rotated.Except(relayProvider));
            await ScreenshotAsync(source, settings, "history-rotated-credential.png");
            await File.WriteAllTextAsync(Path.Combine(settings.EvidenceDirectory, "history-credential-rotation.json"),
                System.Text.Json.JsonSerializer.Serialize(new {
                    RevokedCredentialId = issued[0].Id, ReplacementCredentialId = issued[1].Id,
                    RevokedStatus = revoked.Status, RotatedAttemptIds = rotated, NativeTurnCompleted = true
                }));
        } catch (Exception exception) {
            failure = exception;
            try {
                await ScreenshotAsync(client, settings, "5212-shared-agent-failure.png");
            } catch {
            }
        }

        var cleanupClient = await clientContext.NewPageAsync();
        var cleanupSource = await sourceContext.NewPageAsync();
        cleanupClient.SetDefaultTimeout(30_000);
        cleanupSource.SetDefaultTimeout(30_000);
        if (originalSecretValue is not null) {
            await CaptureCleanupAsync(
                () => SharedProviderTwoInstanceUiAcceptanceTests.CreateSecretAsync(
                    cleanupClient, settings.ClientUrl, SourceSecretName, originalSecretValue),
                cleanupFailures);
        }
        await CaptureCleanupAsync(
            () => DeleteAgentAsync(cleanupClient, settings.ClientUrl, AcceptanceAgentName),
            cleanupFailures);
        foreach (var name in LegacySecretNames) {
            await CaptureCleanupAsync(
                () => DeleteSecretAsync(cleanupClient, settings.ClientUrl, name),
                cleanupFailures);
        }
        await CaptureCleanupAsync(
            () => DeleteCredentialsByPrefixAsync(cleanupSource, settings.SharedUrl, TokenNamePrefix),
            cleanupFailures);
        await File.WriteAllTextAsync(Path.Combine(settings.EvidenceDirectory, "history-browser-checks.json"),
            System.Text.Json.JsonSerializer.Serialize(new { BrowserErrors = browserErrors, RequestFailures = requestFailures }));
        await CaptureCleanupAsync(() => {
            Assert.Empty(browserErrors);
            Assert.Empty(requestFailures);
            return Task.CompletedTask;
        }, cleanupFailures);

        if (failure is not null && cleanupFailures.Count > 0) {
            throw new AggregateException(
                "Acceptance and cleanup both failed.",
                new[] { failure }.Concat(cleanupFailures));
        }
        if (cleanupFailures.Count > 0) {
            throw new AggregateException("Acceptance cleanup failed.", cleanupFailures);
        }
        if (failure is not null) {
            ExceptionDispatchInfo.Capture(failure).Throw();
        }

        void Observe(IPage page) {
            page.PageError += (_, error) => browserErrors.Add(error);
            page.Console += (_, message) => {
                if (message.Type == "error") {
                    browserErrors.Add(message.Text);
                }
            };
            page.RequestFailed += (_, request) => requestFailures.Add(new Uri(request.Url).AbsolutePath + ": " + request.Failure);
        }
    }

    private static async Task<IssuedCredential> IssueCredentialAsync(IPage page, string baseUrl, string displayName, string subject) {
        await NavigateAsync(page, $"{baseUrl}/settings?tab=api-access");
        await page.GetByRole(AriaRole.Textbox, new() { Name = "Token subject", Exact = true }).FillAsync(subject);
        await page.GetByRole(AriaRole.Textbox, new() { Name = "Token display name", Exact = true }).FillAsync(displayName);
        await page.GetByRole(AriaRole.Textbox, new() { Name = "Token lifetime minutes", Exact = true }).FillAsync("30");
        await page.GetByTestId("api-token-scopes").FillAsync(CredentialScopes);
        await page.GetByRole(AriaRole.Button, new() { Name = "Create token", Exact = true }).ClickAsync();
        var field = page.GetByTestId("api-issued-token");
        await field.WaitForAsync();
        await Assertions.Expect(field).ToHaveValueAsync(new Regex("^eyJ"));
        var token = await field.InputValueAsync();
        Assert.StartsWith("eyJ", token, StringComparison.Ordinal);
        var id = ProviderHistoryAcceptanceRelayClient.ReadCredentialId(token, CredentialScopes);
        await field.EvaluateAsync("element => { element.value = '[redacted after capture]'; }");
        return new(id, token);
    }

    private static async Task ValidateCredentialAsync(
        IAPIRequestContext request,
        string baseUrl,
        string token) {
        var response = await request.GetAsync(
            $"{baseUrl}{SharedProviderRoutes.Catalog}",
            new APIRequestContextOptions {
                Headers = new Dictionary<string, string> {
                    ["Authorization"] = $"Bearer {token}"
                }
            });
        Assert.Equal(200, response.Status);
    }

    private static async Task<string> ReadSecretValueAsync(IPage page, string baseUrl, string name) {
        await NavigateAsync(page, $"{baseUrl}/settings?tab=secrets");
        var secret = page.GetByText(name, new() { Exact = true }).First;
        await secret.ClickAsync();
        await Assertions.Expect(FieldByLabel(page, "Name")).ToHaveValueAsync(name);
        var value = await page.GetByTestId("settings-secret-value").InputValueAsync();
        Assert.False(string.IsNullOrWhiteSpace(value), $"Secret '{name}' did not contain a value.");
        return value;
    }

    private async Task TestSourceAsync(IPage page, string baseUrl) {
        await NavigateAsync(page, $"{baseUrl}/agents?tab=providers");
        await page.GetByTestId("agents-provider-profiles-panel").WaitForAsync();
        await page.GetByTestId("providers-connections").ClickAsync();
        var connections = page.GetByTestId("shared-provider-connections-dialog");
        await connections.WaitForAsync();
        var source = connections.GetByTestId("shared-provider-source-card")
            .Filter(new() { HasTextString = SourceName });
        await source.GetByTestId("shared-provider-source-test").ClickAsync();
        await page.GetByText("Source connection passed", new() { Exact = true })
            .WaitForAsync(new() { Timeout = 60_000 });
    }

    private async Task CreateAgentAsync(IPage page, string baseUrl, string agentName) {
        await OpenAgentCatalogAsync(page, baseUrl);
        await page.GetByTestId("agents-catalog-new").ClickAsync();
        var dialog = page.GetByTestId("agents-details-dialog");
        await dialog.WaitForAsync();
        await dialog.GetByTestId("agents-catalog-name").FillAsync(agentName);
        await dialog.GetByTestId("agents-catalog-role").FillAsync("Shared provider history acceptance agent");
        await dialog.GetByTestId("agents-catalog-summary").FillAsync("Exercises shared-provider request history.");
        await dialog.GetByTestId("agents-catalog-instructions").FillAsync(
            "Return plain text only. Never call tools or inspect any workspace.");
        await dialog.GetByRole(AriaRole.Tab, new() { Name = "Runtime", Exact = true }).ClickAsync();
        await FieldByLabel(dialog, "Status").SelectOptionAsync("Active");
        await SelectOptionContainingAsync(dialog.GetByTestId("agents-catalog-provider"), ProviderName);
        var modelChoice = dialog.GetByTestId("agents-catalog-model-choice");
        await modelChoice.SelectOptionAsync(new SelectOptionValue { Index = 0 });
        Assert.StartsWith("Provider default", await modelChoice.Locator("option:checked").InnerTextAsync());
        var approval = dialog.GetByTestId("agents-catalog-require-external-approval");
        if (await approval.IsCheckedAsync()) {
            await approval.UncheckAsync();
        }
        await dialog.GetByTestId("agents-catalog-save").ClickAsync();
        await page.GetByText("Agent saved", new() { Exact = true }).WaitForAsync();
    }

    private static async Task DeleteAgentAsync(IPage page, string baseUrl, string agentName) {
        await OpenAgentCatalogAsync(page, baseUrl);
        var card = page.GetByTestId("agents-catalog-card-shell")
            .Filter(new() { HasTextString = agentName })
            .First;
        if (await card.CountAsync() == 0) {
            return;
        }
        await card.DblClickAsync();
        var dialog = page.GetByTestId("agents-details-dialog").Last;
        await dialog.WaitForAsync();
        await dialog.GetByTestId("agents-catalog-delete").ClickAsync();
        var confirmation = page.GetByTestId("agents-catalog-delete-confirmation");
        await confirmation.WaitForAsync();
        await page.GetByTestId("agents-catalog-delete-confirm").ClickAsync();
        await confirmation.WaitForAsync(new() { State = WaitForSelectorState.Detached });
        await page.GetByText("Agent deleted", new() { Exact = true }).WaitForAsync();
    }

    private static async Task OpenAgentCatalogAsync(IPage page, string baseUrl) {
        await NavigateAsync(page, $"{baseUrl}/agents?tab=agents");
        await page.Locator(".agent-catalog-panel").GetByText(new Regex(@"^\d+ of \d+ agent\(s\)$")).WaitForAsync();
    }

    private static async Task SelectOptionContainingAsync(ILocator select, string expectedText) {
        foreach (var option in await select.Locator("option").AllAsync()) {
            if (!(await option.InnerTextAsync()).Contains(expectedText, StringComparison.Ordinal)) {
                continue;
            }
            var value = await option.GetAttributeAsync("value");
            Assert.False(string.IsNullOrWhiteSpace(value));
            await select.SelectOptionAsync(value);
            return;
        }
        throw new InvalidOperationException($"No option contained '{expectedText}'.");
    }

    private static async Task InvokeAgentAsync(IPage page, string baseUrl, string agentName, IReadOnlyList<string> markers) {
        await NavigateAsync(page, $"{baseUrl}/agents?tab=chat");
        await page.GetByTestId("agents-chat-panel").WaitForAsync();
        await page.GetByTestId("agent-switch-button").ClickAsync();
        var switcher = page.GetByTestId("agent-switch-dialog");
        await switcher.WaitForAsync();
        await switcher.GetByTestId("agent-switch-search").FillAsync(agentName);
        var card = switcher.GetByTestId("agent-switch-card-shell").Filter(new() { HasTextString = agentName });
        await card.GetByTestId("agent-switch-card").ClickAsync();
        await switcher.WaitForAsync(new() { State = WaitForSelectorState.Detached });
        await Assertions.Expect(page.GetByTestId("agent-thread-selected-agent")).ToContainTextAsync(agentName);
        var chat = page.GetByTestId("agents-chat-panel");
        var newThread = chat.GetByRole(AriaRole.Button, new() { Name = "New thread", Exact = true }).First;
        await Assertions.Expect(newThread).ToBeEnabledAsync();
        await newThread.ClickAsync();
        await page.GetByText("New thread created.", new() { Exact = true }).WaitForAsync();
        var responses = page.GetByTestId("chat-workspace-panel")
            .Locator("[data-testid='conversation-message'].justify-start .chat-markdown");
        foreach (var marker in markers) {
            var responseCount = await responses.CountAsync();
            await page.GetByTestId("chat-prompt-input").FillAsync($"Do not call tools. Reply with exactly {marker} and nothing else.");
            await page.GetByTestId("chat-send-button").ClickAsync();
            await Assertions.Expect(page.GetByTestId("agent-execution-activity-phase")).Not.ToHaveTextAsync("Completed", new() { Timeout = 10_000 });
            await Assertions.Expect(page.GetByTestId("agent-execution-activity-phase")).ToHaveTextAsync("Completed", new() { Timeout = 90_000 });
            await Assertions.Expect(responses).ToHaveCountAsync(responseCount + 1, new() { Timeout = 30_000 });
            Assert.False(string.IsNullOrWhiteSpace(await responses.Last.InnerTextAsync()));
        }
    }

    private static async Task<IReadOnlyList<Guid>> SearchGlobalAsync(IPage page, string baseUrl, string providerName, Guid? credentialId = null, int minimumAttempts = 2) {
        await SharedProviderMetadataUiChecks.OpenProviderAsync(page, baseUrl, providerName);
        var treeId = await page.GetByTestId("providers-tree-provider").Filter(new() { HasTextString = providerName }).First.GetAttributeAsync("id");
        var match = Regex.Match(treeId!, "[0-9a-f]{32}$", RegexOptions.CultureInvariant);
        Assert.True(match.Success, "The selected native provider must expose its exact tree identity.");
        await NavigateAsync(page, $"{baseUrl}/agents?tab=request-history");
        return await SearchAndReadIdsAsync(page, providerName, credentialId, minimumAttempts, Guid.ParseExact(match.Value, "N"));
    }

    private static async Task<IReadOnlyList<Guid>> SearchProviderAsync(IPage page, string baseUrl, string providerName, Guid? credentialId = null, int minimumAttempts = 2) {
        await SharedProviderMetadataUiChecks.OpenProviderAsync(page, baseUrl, providerName);
        await page.GetByTestId("provider-editor-tab-history").ClickAsync();
        return await SearchAndReadIdsAsync(page, providerName, credentialId, minimumAttempts);
    }

    private static async Task<IReadOnlyList<Guid>> SearchAndReadIdsAsync(IPage page, string providerName, Guid? credentialId, int minimumAttempts, Guid? providerId = null) {
        var panel = page.GetByTestId("provider-request-history");
        await panel.WaitForAsync();
        await panel.GetByText("History not requested", new() { Exact = true }).WaitForAsync();
        Assert.Equal(0, await panel.GetByTestId("history-results").CountAsync());
        if (providerId.HasValue) {
            await panel.GetByTestId("history-provider").FillAsync(providerId.Value.ToString("D"));
        }
        await panel.GetByTestId("history-more-filters").ClickAsync();
        await panel.GetByTestId("history-page-size").FillAsync("20");
        await panel.GetByTestId("history-page-size").PressAsync("Tab");
        if (credentialId.HasValue) {
            await panel.GetByTestId("history-credential").FillAsync(credentialId.Value.ToString("D"));
        }
        await panel.GetByTestId("history-search").ClickAsync();
        var results = panel.GetByTestId("history-results");
        await results.WaitForAsync();
        Assert.Contains(providerName, await results.InnerTextAsync(), StringComparison.Ordinal);
        var details = results.GetByTestId("history-details");
        var available = await details.CountAsync();
        Assert.InRange(available, minimumAttempts, 20);
        var readCount = credentialId.HasValue ? available : minimumAttempts;
        return await ReadVisibleIdsAsync(page, readCount);
    }

    private static async Task<IReadOnlyList<Guid>> ReadVisibleIdsAsync(IPage page, int readCount) {
        var details = page.GetByTestId("history-results").GetByTestId("history-details");
        var ids = new List<Guid>();
        for (var index = 0; index < readCount; index++) {
            await details.Nth(index).ClickAsync();
            var dialog = page.GetByTestId("history-detail-dialog");
            await dialog.GetByText("Entry / provider", new() { Exact = true }).WaitForAsync();
            var text = await dialog.InnerTextAsync();
            Assert.Contains("Content loads on request in a separate read-only dialog.", text, StringComparison.Ordinal);
            await Assertions.Expect(page.GetByTestId("history-content-dialog")).ToHaveCountAsync(0);
            await Assertions.Expect(page.GetByTestId("history-content-text")).ToHaveCountAsync(0);
            var identity = await dialog.GetByText("Entry / provider", new() { Exact = true }).Locator("xpath=following-sibling::*[1]").InnerTextAsync();
            var match = Regex.Match(identity, @"^[0-9a-fA-F-]{36}");
            Assert.True(match.Success, "The history detail dialog did not expose the entry identity.");
            ids.Add(Guid.Parse(match.Value));
            await dialog.GetByTestId("history-detail-close").ClickAsync();
            await dialog.WaitForAsync(new() { State = WaitForSelectorState.Detached });
        }
        return ids;
    }

    private static async Task AssertPagingAndDeniedAuthorityAsync(IBrowser browser, IPage source, AcceptanceSettings settings,
        IReadOnlyList<IssuedCredential> credentials, IReadOnlyList<Guid> agentIds, IReadOnlyList<Guid> relayIds, Action<IPage> observe) {
        if (!await source.GetByTestId("history-page-size").IsVisibleAsync()) {
            await source.GetByTestId("history-more-filters").ClickAsync();
        }
        await source.GetByTestId("history-page-size").FillAsync("1");
        await source.GetByTestId("history-page-size").PressAsync("Tab");
        await source.GetByTestId("history-credential").FillAsync(credentials[0].Id.ToString("D"));
        await source.GetByTestId("history-search").ClickAsync();
        await Assertions.Expect(source.GetByTestId("history-details")).ToHaveCountAsync(1);
        var pagedIds = new List<Guid>();
        for (var index = 0; index < agentIds.Count; index++) {
            await Assertions.Expect(source.GetByTestId("history-results")).ToContainTextAsync($"Page {index + 1} · 1 rows.");
            pagedIds.Add(Assert.Single(await ReadVisibleIdsAsync(source, 1)));
            if (index + 1 < agentIds.Count) {
                await source.GetByTestId("history-next").ClickAsync();
            }
        }
        Assert.Equal(agentIds.Order(), pagedIds.Order());
        Assert.Equal(pagedIds.Count, pagedIds.Distinct().Count());
        await source.GetByTestId("history-credential").FillAsync(credentials[1].Id.ToString("D"));
        await source.GetByTestId("history-search").ClickAsync();
        await Assertions.Expect(source.GetByTestId("history-previous")).ToBeDisabledAsync();
        Assert.Equal(relayIds, await ReadVisibleIdsAsync(source, 1));
        await ScreenshotAsync(source, settings, "history-pagination-credential-change.png");
        await using var deniedContext = await browser.NewContextAsync(ContextOptions());
        await deniedContext.SetExtraHTTPHeadersAsync(new Dictionary<string, string> { ["Authorization"] = $"Bearer {credentials[0].Token}" });
        var denied = await deniedContext.NewPageAsync();
        observe(denied);
        await NavigateAsync(denied, settings.SharedUrl + "/agents?tab=request-history");
        await denied.GetByTestId("history-search").ClickAsync();
        await Assertions.Expect(denied.GetByTestId("history-error"))
            .ToHaveTextAsync("Denied: Access denied. This evidence is not available to the current identity.");
        Assert.Empty(await denied.GetByTestId("history-details").AllAsync());
        Assert.Empty(await denied.GetByTestId("history-content-text").AllAsync());
        await ScreenshotAsync(denied, settings, "history-authority-denied.png");
    }

    private static async Task DeleteSecretAsync(IPage page, string baseUrl, string name) {
        await NavigateAsync(page, $"{baseUrl}/settings?tab=secrets");
        var secret = page.GetByText(name, new() { Exact = true }).First;
        if (await secret.CountAsync() == 0) {
            return;
        }
        await secret.ClickAsync();
        await Assertions.Expect(FieldByLabel(page, "Name")).ToHaveValueAsync(name);
        await page.GetByRole(AriaRole.Button, new() { Name = "Delete", Exact = true }).ClickAsync();
        await page.GetByTestId("settings-operation").First.GetByText("DeleteSecret: Committed", new() { Exact = true }).WaitForAsync();
    }

    private static async Task DeleteCredentialsByPrefixAsync(IPage page, string baseUrl, string displayNamePrefix) {
        const int cleanupLimit = 25;
        for (var deleted = 0; deleted < cleanupLimit; deleted++) {
            await NavigateAsync(page, $"{baseUrl}/settings?tab=api-access");
            await page.GetByTestId("api-tokens-open").ClickAsync();
            var dialog = page.GetByTestId("api-tokens-dialog");
            await dialog.WaitForAsync();
            await dialog.GetByTestId("api-tokens-search").FillAsync(displayNamePrefix);
            var search = dialog.GetByTestId("api-tokens-search-submit");
            await search.ClickAsync();
            await Assertions.Expect(search).ToBeEnabledAsync();
            await page.WaitForTimeoutAsync(500);
            var row = dialog.Locator("tbody tr")
                .Filter(new() { HasTextString = displayNamePrefix })
                .First;
            if (await row.CountAsync() == 0) {
                return;
            }
            var delete = row.GetByTestId("api-token-delete");
            await Assertions.Expect(delete).ToBeEnabledAsync();
            await delete.ClickAsync();
            var confirmation = page.GetByTestId("api-token-confirmation");
            await confirmation.WaitForAsync();
            await page.GetByTestId("api-token-confirm").ClickAsync();
            await confirmation.WaitForAsync(new() { State = WaitForSelectorState.Detached });
        }
        throw new InvalidOperationException(
            $"More than {cleanupLimit} token records matched '{displayNamePrefix}'.");
    }

    private static async Task CaptureCleanupAsync(
        Func<Task> cleanup,
        ICollection<Exception> failures) {
        try {
            await cleanup();
        } catch (Exception exception) {
            failures.Add(exception);
        }
    }

    private static Task NavigateAsync(IPage page, string url)
        => SharedProviderTwoInstanceUiAcceptanceTests.NavigateAsync(page, url);

    private static ILocator FieldByLabel(IPage page, string label) => FieldByLabel(page.Locator("body"), label);

    private static ILocator FieldByLabel(ILocator root, string label) => root.Locator("label.cda-field-label")
        .Filter(new() { HasTextString = label }).First.Locator("xpath=following-sibling::*[1]");

    private static async Task ScreenshotAsync(IPage page, AcceptanceSettings settings, string name) =>
        await page.ScreenshotAsync(new() { Path = Path.Combine(settings.EvidenceDirectory, name), FullPage = true });

    private static BrowserNewContextOptions ContextOptions() => new() {
        ViewportSize = new() { Width = 1920, Height = 1080 }
    };

    private static AcceptanceSettings LoadSettings() {
        var shared = Environment.GetEnvironmentVariable(SharedUrlVariable);
        var client = Environment.GetEnvironmentVariable(ClientUrlVariable);
        var evidence = Environment.GetEnvironmentVariable(EvidenceDirectoryVariable);
        if (string.IsNullOrWhiteSpace(shared) || string.IsNullOrWhiteSpace(client) || string.IsNullOrWhiteSpace(evidence)) {
            throw SkipException.ForSkip($"Set {SharedUrlVariable}, {ClientUrlVariable}, and {EvidenceDirectoryVariable}.");
        }
        return new(shared.TrimEnd('/'), client.TrimEnd('/'), Path.GetFullPath(evidence));
    }

    private sealed record IssuedCredential(Guid Id, string Token);
    private sealed record AcceptanceSettings(string SharedUrl, string ClientUrl, string EvidenceDirectory);
}


