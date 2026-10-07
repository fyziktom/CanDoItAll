using Microsoft.Playwright;

namespace CanDoItAll.Tests.Playwright;

[Trait("Category", "Playwright")]
[Trait("Category", "HostPlatform")]
public sealed class ApiAccessSandboxBrowserTests {
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Independent_sandbox_uses_real_forms_dialogs_retirement_and_assets(bool published) {
        await using var host = new ApiAccessSandboxHost();
        await host.StartAsync(published);
        using var playwright = await Microsoft.Playwright.Playwright.CreateAsync();
        await using var browser = await playwright.Chromium.LaunchAsync(new() { Headless = true });
        await using var context = await browser.NewContextAsync(new() { ViewportSize = new() { Width = 1600, Height = 1000 }, Permissions = ["clipboard-read", "clipboard-write"] });
        var page = await context.NewPageAsync();
        var errors = new List<string>();
        var assets = new HashSet<string>(StringComparer.Ordinal);
        var unexpectedOrigins = new HashSet<string>(StringComparer.Ordinal);
        page.Request += (_, request) => {
            if (Uri.TryCreate(request.Url, UriKind.Absolute, out var uri) && uri.Scheme is "http" or "https" &&
                !uri.GetLeftPart(UriPartial.Authority).Equals(host.BaseUrl, StringComparison.Ordinal)) {
                unexpectedOrigins.Add(uri.GetLeftPart(UriPartial.Authority));
            }
        };
        page.PageError += (_, _) => errors.Add("Page error");
        page.Response += (_, response) => {
            if (response.Url.Contains("_content/", StringComparison.Ordinal) || response.Url.EndsWith(".css", StringComparison.Ordinal) || response.Url.Contains(".woff", StringComparison.Ordinal)) {
                if (response.Status >= 400) {
                    errors.Add($"Asset HTTP {response.Status}");
                } else {
                    assets.Add(response.Url);
                }
            }
        };
        await page.GotoAsync(host.BaseUrl);
        await page.GetByTestId("api-users-panel").WaitForAsync();
        await Assertions.Expect(page.GetByTestId("api-users-page")).ToContainTextAsync("3 users");
        await Assertions.Expect(page.GetByTestId("api-tokens-dialog")).ToHaveCountAsync(0);
        await Shot("normal");
        await page.GetByTestId("api-token-scopes").FillAsync("fixture.read, incomplete");
        await page.GetByTestId("api-scopes-open").ClickAsync();
        await Assertions.Expect(page.GetByTestId("api-scope-option")).ToHaveCountAsync(3);
        await Shot("scopes");
        await page.Keyboard.PressAsync("Escape");
        await Assertions.Expect(page.GetByTestId("api-scopes-dialog")).ToHaveCountAsync(0);
        await Assertions.Expect(page.GetByTestId("api-token-scopes")).ToHaveValueAsync("fixture.read, incomplete");
        await page.GetByTestId("api-token-scopes").FillAsync("fixture.read");
        await page.Locator("#api-token-lifetime").FillAsync("1.");
        await page.GetByTestId("api-token-create").ClickAsync();
        try {
            await Assertions.Expect(page.Locator(".validation-message")).ToContainTextAsync("whole number");
        } catch (PlaywrightException) {
            throw new InvalidOperationException($"Invalid lifetime did not render its error. Raw lifetime: {await page.Locator("#api-token-lifetime").InputValueAsync()}; effects: {await page.GetByTestId("scenario-effects").TextContentAsync()}; disclosure count: {await page.GetByTestId("api-issued-token").CountAsync()}; remaining scopes dialogs: {await page.GetByTestId("api-scopes-dialog").CountAsync()}");
        }
        await Assertions.Expect(page.GetByTestId("scenario-effects")).ToContainTextAsync("Current writes: 0");
        await page.Locator("#api-token-lifetime").FillAsync("17");
        await page.GetByTestId("api-token-subject").FillAsync("captured-subject");
        await Hold("Access");
        await page.GetByTestId("api-token-create").ClickAsync();
        await Assertions.Expect(page.GetByTestId("api-token-create")).ToBeDisabledAsync();
        var subject = page.GetByTestId("api-token-subject");
        await subject.FillAsync("later-subject");
        await subject.EvaluateAsync("element => element.setSelectionRange(5,5)");
        await page.Keyboard.PressAsync("Alt+Shift+R");
        await page.GetByTestId("api-issued-token").WaitForAsync();
        await Assertions.Expect(subject).ToBeFocusedAsync();
        Assert.Equal(5, await subject.EvaluateAsync<int>("element => element.selectionStart"));
        var disclosure = await page.GetByTestId("api-issued-token").InputValueAsync();
        Assert.True(disclosure.StartsWith("NOT-A-CREDENTIAL-", StringComparison.Ordinal), "The sandbox must only disclose nonusable fixture values.");
        await page.GetByTestId("api-token-copy").ClickAsync();
        await Assertions.Expect(page.GetByTestId("api-token-copy")).ToHaveAttributeAsync("data-copy-state", "copied");
        Assert.True(await page.EvaluateAsync<string>("navigator.clipboard.readText()") == disclosure, "Copy did not return this fixture's one-time value.");
        await page.GetByTestId("api-token-dismiss").ClickAsync();
        await Assertions.Expect(page.GetByTestId("api-issued-token")).ToHaveCountAsync(0);
        await page.GetByTestId("api-tokens-open").ClickAsync();
        await page.GetByTestId("api-tokens-search").FillAsync("captured-subject");
        await page.GetByTestId("api-tokens-search").PressAsync("Enter");
        await Assertions.Expect(page.GetByTestId("api-tokens-page")).ToContainTextAsync("1 token(s)");
        await page.GetByTestId("api-token-revoke").ClickAsync();
        await Assertions.Expect(page.GetByTestId("api-token-confirmation")).ToBeVisibleAsync();
        await Shot("token-confirmation");
        await page.Keyboard.PressAsync("Escape");
        await Assertions.Expect(page.GetByTestId("api-token-confirmation")).ToHaveCountAsync(0);
        await Assertions.Expect(page.GetByTestId("api-tokens-dialog")).ToBeVisibleAsync();
        await page.GetByTestId("api-token-revoke").ClickAsync();
        await page.GetByTestId("api-token-confirm").ClickAsync();
        await Assertions.Expect(page.GetByTestId("api-tokens-table")).ToContainTextAsync("Revoked");
        await page.GetByTestId("api-token-delete").ClickAsync();
        await page.GetByTestId("api-token-confirm").ClickAsync();
        await Assertions.Expect(page.GetByTestId("api-tokens-page")).ToContainTextAsync("0 token(s)");
        await page.GetByTestId("api-tokens-dialog").Locator("footer").GetByRole(AriaRole.Button, new() { Name = "Close", Exact = true }).ClickAsync();

        await Hold("CreateAccount");
        await page.GetByTestId("api-user-create").ClickAsync();
        await page.GetByTestId("api-user-name").FillAsync("created-fixture");
        await page.GetByTestId("api-user-display-name").FillAsync("Captured name");
        await page.GetByTestId("api-user-password").FillAsync("fixture-password-only");
        await page.GetByTestId("api-user-scopes").ClickAsync();
        await Assertions.Expect(page.GetByTestId("api-scope-option")).ToHaveCountAsync(2);
        await page.GetByTestId("api-scopes-confirm").ClickAsync();
        await page.GetByTestId("api-user-save").ClickAsync();
        await Assertions.Expect(page.GetByTestId("api-user-save")).ToBeDisabledAsync();
        await page.GetByTestId("api-user-display-name").FillAsync("Later name");
        await page.Keyboard.PressAsync("Alt+Shift+R");
        await Assertions.Expect(page.GetByTestId("api-user-identity")).ToContainTextAsync("Version 1");
        await Assertions.Expect(page.GetByTestId("api-user-display-name")).ToHaveValueAsync("Later name");
        await Assertions.Expect(page.GetByTestId("api-user-password")).ToHaveCountAsync(0);
        await Shot("account-editor");
        await page.GetByTestId("api-user-save").ClickAsync();
        await Assertions.Expect(page.GetByTestId("api-user-dialog")).ToHaveCountAsync(0);
        await Assertions.Expect(page.GetByTestId("api-users-table")).ToContainTextAsync("Later name");

        await Reset("MultiplePages");
        await Assertions.Expect(page.GetByTestId("api-users-page")).ToContainTextAsync("61 users");
        await page.GetByRole(AriaRole.Button, new() { Name = "Next", Exact = true }).ClickAsync();
        await Assertions.Expect(page.GetByTestId("api-users-page")).ToContainTextAsync("Page 2");
        await page.GetByTestId("api-tokens-open").ClickAsync();
        await page.GetByRole(AriaRole.Button, new() { Name = "Next token page", Exact = true }).ClickAsync();
        await Assertions.Expect(page.GetByTestId("api-tokens-page")).ToContainTextAsync("Page 2");
        await Shot("token-page-two");
        await page.GetByTestId("api-tokens-dialog").Locator("footer").GetByRole(AriaRole.Button, new() { Name = "Close", Exact = true }).ClickAsync();

        await Reset("Empty");
        await page.GetByLabel("Operation", new() { Exact = true }).SelectOptionAsync("IssueToken");
        await page.GetByLabel("Next outcome", new() { Exact = true }).SelectOptionAsync("UnknownAfterCommit");
        await page.GetByTestId("scenario-fault").ClickAsync();
        await page.GetByTestId("api-token-create").ClickAsync();
        await Assertions.Expect(page.GetByTestId("api-token-outcome")).ToContainTextAsync("Candidate identity");
        await page.GetByRole(AriaRole.Button, new() { Name = "Observe exact identity", Exact = true }).ClickAsync();
        await Assertions.Expect(page.GetByTestId("api-token-create")).ToBeDisabledAsync();
        await Assertions.Expect(page.GetByTestId("api-issued-token")).ToHaveCountAsync(0);
        await Shot("unknown-outcome");
        await Reset("Empty");
        await Hold("IssueToken");
        await page.GetByTestId("api-token-create").ClickAsync();
        await Assertions.Expect(page.GetByTestId("scenario-effects")).ToContainTextAsync("Pending writes: 1");
        await page.GetByTestId("scenario-retire").ClickAsync();
        await Reset("Empty");
        await page.GetByTestId("scenario-release").ClickAsync();
        await Assertions.Expect(page.GetByTestId("scenario-effects")).ToContainTextAsync("Current writes: 0");
        await Assertions.Expect(page.GetByTestId("scenario-effects")).ToContainTextAsync("Retired writes: 7");
        await Assertions.Expect(page.GetByTestId("api-issued-token")).ToHaveCountAsync(0);
        foreach (var scenario in new[] { "StatusUnavailable", "AccessUnavailable", "Denied", "AuthorizationOff", "MissingSigningKey", "ReadUnavailable" }) {
            await Reset(scenario);
            await Assertions.Expect(page.GetByTestId("api-access-retry")).ToBeVisibleAsync();
            Assert.Equal(0, await page.GetByTestId("api-issued-token").CountAsync());
        }
        await Reset("Representative");
        await Assertions.Expect(page.GetByTestId("api-users-panel")).ToBeVisibleAsync();
        Assert.Contains(assets, url => url.Contains("_content/CanDoItAll.Components.BaseLib", StringComparison.Ordinal));
        Assert.Contains(assets, url => url.Contains(".woff", StringComparison.Ordinal));
        Assert.Empty(errors);
        Assert.Empty(unexpectedOrigins);

        async Task Hold(string operation) {
            await page.GetByLabel("Operation", new() { Exact = true }).SelectOptionAsync(operation);
            await page.GetByTestId("scenario-hold").ClickAsync();
        }
        async Task Reset(string scenario) {
            await page.GetByLabel("Scenario", new() { Exact = true }).SelectOptionAsync(scenario);
            await page.GetByTestId("scenario-reset").ClickAsync();
        }
        async Task Shot(string name) {
            Assert.True(await page.EvaluateAsync<bool>("document.documentElement.scrollWidth <= window.innerWidth"), "The desktop surface overflowed horizontally.");
            var directory = Path.Combine(PlaywrightTestHostPaths.RepositoryRoot, "output", "playwright", "workspace-api-access-ui");
            Directory.CreateDirectory(directory);
            await page.ScreenshotAsync(new() { Path = Path.Combine(directory, $"{(published ? "published" : "sandbox")}-{name}.png") });
        }
    }
}
