using System.Net;
using Microsoft.Playwright;
using static CanDoItAll.Tests.Playwright.ApiAccessSettingsBrowserTests;

namespace CanDoItAll.Tests.Playwright;

public sealed class SwaggerAuthorizationBrowserTests {
    private const string SessionPath = "/api/access/me";

    [Fact]
    public async Task Anonymous_document_loads_and_authorize_sends_JWT_only_until_logout() {
        await using var host = new ApiAccessProductionHost();
        var password = ApiAccessProductionHost.Secret();
        await host.StartAsync(password, loopbackHttp: false);
        using var playwright = await Microsoft.Playwright.Playwright.CreateAsync();
        await using var browser = await playwright.Chromium.LaunchAsync(new() { Headless = true });
        await using var context = await browser.NewContextAsync(new() {
            IgnoreHTTPSErrors = true,
            ViewportSize = new() { Width = 1680, Height = 950 }
        });
        var page = await context.NewPageAsync();
        var document = await page.RunAndWaitForResponseAsync(
            () => page.GotoAsync(host.HttpsUrl + "/swagger/index.html"),
            response => response.Url.EndsWith("/swagger/v1/swagger.json", StringComparison.Ordinal));
        Assert.Equal((int)HttpStatusCode.OK, document.Status);
        var operation = page.Locator(".opblock-get").Filter(new() {
            Has = page.Locator($".opblock-summary-path[data-path='{SessionPath}']")
        });
        await operation.Locator(".opblock-summary-control").ClickAsync();
        await operation.Locator(".try-out__btn").ClickAsync();
        await ExecuteAsync(HttpStatusCode.Unauthorized);

        var session = await LoginAsync(host.Client, "admin", password);
        await page.Locator(".auth-wrapper button.authorize").ClickAsync();
        var dialog = page.Locator(".dialog-ux");
        await dialog.Locator("input[type='text']").FillAsync(session.Token);
        await dialog.GetByRole(AriaRole.Button, new() { Name = "Apply credentials", Exact = true }).ClickAsync();
        await dialog.Locator(".btn-done").ClickAsync();
        await ExecuteAsync(HttpStatusCode.OK);

        await page.Locator(".auth-wrapper button.authorize").ClickAsync();
        await dialog.GetByRole(AriaRole.Button, new() { Name = "Remove authorization", Exact = true }).ClickAsync();
        await dialog.Locator(".btn-done").ClickAsync();
        await ExecuteAsync(HttpStatusCode.Unauthorized);

        async Task ExecuteAsync(HttpStatusCode expectedStatus) {
            var response = await page.RunAndWaitForResponseAsync(
                () => operation.Locator("button.execute").ClickAsync(),
                response => response.Url == host.HttpsUrl + SessionPath && response.Request.Method == "GET");
            Assert.Equal((int)expectedStatus, response.Status);
            await Assertions.Expect(operation.Locator(".live-responses-table .response-col_status").Last)
                .ToContainTextAsync(((int)expectedStatus).ToString(System.Globalization.CultureInfo.InvariantCulture));
        }
    }
}
