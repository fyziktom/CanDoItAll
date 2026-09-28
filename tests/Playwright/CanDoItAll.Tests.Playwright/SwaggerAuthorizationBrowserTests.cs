using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using CanDoItAll.Modules.Workspace.ApiAccess;
using Microsoft.Playwright;
using static CanDoItAll.Tests.Playwright.ApiAccessSettingsBrowserTests;

namespace CanDoItAll.Tests.Playwright;

public sealed class SwaggerAuthorizationBrowserTests {
    private const string SessionPath = "/api/access/me";

    [Fact]
    public async Task Http_swagger_redirects_and_user_login_authorizes_requests_only_until_logout() {
        await using var host = new ApiAccessProductionHost();
        var password = ApiAccessProductionHost.Secret();
        await host.StartAsync(password, loopbackHttp: false);
        var userPassword = ApiAccessProductionHost.Secret();
        host.ProtectSecrets(userPassword);
        var administrator = await LoginAsync(host.Client, "admin", password);
        host.Client.DefaultRequestHeaders.Authorization = new("Bearer", administrator.Token);
        using var created = await host.Client.PostAsJsonAsync("/api/access/users", new ApiUserCreateRequest(
            "swagger-user", "Swagger user", userPassword, true, [ApiAccessScopeNames.ReadWorkflows]));
        Assert.Equal(HttpStatusCode.OK, created.StatusCode);
        var user = (await created.Content.ReadFromJsonAsync<ApiUserDetails>())!;
        host.Client.DefaultRequestHeaders.Authorization = null;
        using var playwright = await Microsoft.Playwright.Playwright.CreateAsync();
        await using var browser = await playwright.Chromium.LaunchAsync(new() { Headless = true });
        await using var context = await browser.NewContextAsync(new() {
            IgnoreHTTPSErrors = true,
            ViewportSize = new() { Width = 1680, Height = 950 }
        });
        var page = await context.NewPageAsync();
        var document = await page.RunAndWaitForResponseAsync(
            () => page.GotoAsync(host.HttpUrl + "/swagger/index.html"),
            response => response.Url.EndsWith("/swagger/v1/swagger.json", StringComparison.Ordinal));
        Assert.Equal((int)HttpStatusCode.OK, document.Status);
        await Assertions.Expect(page).ToHaveURLAsync(host.HttpsUrl + "/swagger/index.html");
        var operation = page.Locator(".opblock-get").Filter(new() {
            Has = page.Locator($".opblock-summary-path[data-path='{SessionPath}']")
        });
        await operation.Locator(".opblock-summary-control").ClickAsync();
        await operation.Locator(".try-out__btn").ClickAsync();
        await ExecuteAsync(HttpStatusCode.Unauthorized);

        const string loginPath = "/api/access/login";
        var loginOperation = page.Locator(".opblock-post").Filter(new() {
            Has = page.Locator($".opblock-summary-path[data-path='{loginPath}']")
        });
        await loginOperation.Locator(".opblock-summary-control").ClickAsync();
        await loginOperation.Locator(".try-out__btn").ClickAsync();
        var jsonOptions = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        await loginOperation.Locator("textarea.body-param__text").FillAsync(
            JsonSerializer.Serialize(new ApiLoginRequest(user.UserName, userPassword), jsonOptions));
        var loginResponse = await page.RunAndWaitForResponseAsync(
            () => loginOperation.Locator("button.execute").ClickAsync(),
            response => response.Url == host.HttpsUrl + loginPath && response.Request.Method == "POST");
        Assert.Equal((int)HttpStatusCode.OK, loginResponse.Status);
        var session = JsonSerializer.Deserialize<ApiLoginResult>(await loginResponse.BodyAsync(), jsonOptions)!;
        host.ProtectSecrets(session.Token);
        Assert.Equal(user.Id, session.UserId);
        Assert.False(session.IsAdministrator);
        await AssertStatusAsync(host.Client, session.Token, "/api/workflows/templates", HttpStatusCode.OK);
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
