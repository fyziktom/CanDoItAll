using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text;
using CanDoItAll.Infrastructure.ControlPlane;
using CanDoItAll.Modules.Workspace.ApiAccess;
using CanDoItAll.SharedKernel;
using CanDoItAll.Web.Api;
using CanDoItAll.Web.Infrastructure;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using static CanDoItAll.Tests.Integration.Api.ApiUserSessionIntegrationTests;

namespace CanDoItAll.Tests.Integration.Api;

[Trait("Category", "HostPlatform")]
public sealed class UserUiPreferencesIntegrationTests {
    private const string PreferencesRoute = "/api/access/preferences";

    [Fact]
    public async Task Account_preferences_follow_the_account_across_clients_without_leaking_to_other_accounts() {
        var password = Password();
        await using var host = await CreateHostAsync(password);
        var admin = await LoginAsync(host.Client, "admin", password);
        SetToken(host.Client, admin.Token);
        var first = await CreateUserAsync(host.Client, "first", password, []);
        var second = await CreateUserAsync(host.Client, "second", password, []);
        var session = await LoginAsync(host.Client, first.UserName, password);
        SetToken(host.Client, session.Token);
        Assert.Equal(UserUiPreferences.Default, await ReadAsync(host.Client));
        Assert.Equal(new UserUiPreferences(UiThemeMode.Dark, true), await PatchAsync(host.Client, new(Theme: UiThemeMode.Dark)));
        using var otherBrowser = host.CreateClient();
        var otherSession = await LoginAsync(otherBrowser, first.UserName, password);
        SetToken(otherBrowser, otherSession.Token);
        Assert.Equal(new UserUiPreferences(UiThemeMode.Dark, true), await ReadAsync(otherBrowser));
        await PatchAsync(otherBrowser, new(MainMenuCollapsed: false));
        Assert.Equal(new UserUiPreferences(UiThemeMode.Dark, false), await ReadAsync(host.Client));
        SetToken(host.Client, (await LoginAsync(host.Client, second.UserName, password)).Token);
        Assert.Equal(UserUiPreferences.Default, await ReadAsync(host.Client));
        SetToken(host.Client, admin.Token);
        Assert.Equal(UserUiPreferences.Default, await ReadAsync(host.Client));
        await PatchAsync(host.Client, new(MainMenuCollapsed: false));
        Assert.Equal(new UserUiPreferences(UiThemeMode.Dark, false), await ReadAsync(otherBrowser));
    }

    [Fact]
    public async Task Renamed_account_retains_preferences_and_recreated_username_gets_new_defaults() {
        var password = Password();
        await using var host = await CreateHostAsync(password);
        var admin = await LoginAsync(host.Client, "admin", password);
        SetToken(host.Client, admin.Token);
        var account = await CreateUserAsync(host.Client, "original", password, []);
        using var browser = host.CreateClient();
        SetToken(browser, (await LoginAsync(browser, account.UserName, password)).Token);
        await PatchAsync(browser, new(Theme: UiThemeMode.Dark));
        using var renamed = await host.Client.PutAsJsonAsync($"/api/access/users/{account.Id}",
            new ApiUserUpdateRequest("renamed", account.DisplayName, true, [], account.Version));
        renamed.EnsureSuccessStatusCode();
        account = (await renamed.Content.ReadFromJsonAsync<ApiUserDetails>())!;
        using var retired = await browser.GetAsync(PreferencesRoute);
        Assert.Equal(HttpStatusCode.Unauthorized, retired.StatusCode);
        SetToken(browser, (await LoginAsync(browser, account.UserName, password)).Token);
        Assert.Equal(new UserUiPreferences(UiThemeMode.Dark, true), await ReadAsync(browser));
        using var deleted = await host.Client.DeleteAsync($"/api/access/users/{account.Id}?expectedVersion={account.Version}");
        Assert.Equal(HttpStatusCode.NoContent, deleted.StatusCode);
        var replacement = await CreateUserAsync(host.Client, "renamed", password, []);
        Assert.NotEqual(account.Id, replacement.Id);
        SetToken(browser, (await LoginAsync(browser, replacement.UserName, password)).Token);
        Assert.Equal(UserUiPreferences.Default, await ReadAsync(browser));
    }

    [Fact]
    public async Task Self_api_rejects_anonymous_machine_invalid_theme_and_caller_selected_owner() {
        var password = Password();
        await using var host = await CreateHostAsync(password);
        using var anonymous = await host.Client.GetAsync(PreferencesRoute);
        Assert.Equal(HttpStatusCode.Unauthorized, anonymous.StatusCode);
        var machine = host.App.Services.GetRequiredService<IApiTokenService>().IssueToken(new() { Scopes = [ApiAccessScopeNames.Api] });
        SetToken(host.Client, machine.Token);
        using var denied = await host.Client.GetAsync(PreferencesRoute);
        Assert.Equal(HttpStatusCode.Forbidden, denied.StatusCode);
        SetToken(host.Client, (await LoginAsync(host.Client, "admin", password)).Token);
        foreach (var json in new[] { "{\"theme\":999}", "{\"accountId\":\"other-account\",\"theme\":1}" }) {
            using var invalid = await host.Client.PatchAsync(PreferencesRoute, new StringContent(json, Encoding.UTF8, "application/json"));
            Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);
        }
        Assert.Equal(UserUiPreferences.Default, await ReadAsync(host.Client));
    }

    [Fact]
    public async Task Captured_authenticated_service_survives_missing_http_context_but_revalidates_identity_and_revocation() {
        var password = Password();
        UserUiPreferencesService? captured = null;
        ClaimsPrincipal? principal = null;
        await using var host = await ApiTestHost.CreateAsync(true, useInMemoryDatabase: true,
            apiConfiguration: Configuration(password), configureApplication: app => {
                app.MapGet("/test/preferences-owner", (HttpContext context, UserUiPreferencesService service) => {
                    captured = service;
                    principal = context.User;
                    return Results.NoContent();
                }).RequireAuthorization(ApiAuthorizationPolicies.UserSession);
            });
        SetToken(host.Client, (await LoginAsync(host.Client, "admin", password)).Token);
        using var capture = await host.Client.GetAsync("/test/preferences-owner");
        Assert.Equal(HttpStatusCode.NoContent, capture.StatusCode);
        Assert.Null(host.App.Services.GetRequiredService<IHttpContextAccessor>().HttpContext);
        Assert.Equal(UserUiPreferencesAccount.ConfiguredAdministrator, (await captured!.ReadAsync(principal!))!.Account);
        Assert.Null(await captured.ReadAsync(new ClaimsPrincipal(new ClaimsIdentity())));
        var synthetic = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier,
            LocalOperatorAuthenticationStateProvider.ActorId)], LocalOperatorAuthenticationStateProvider.AuthenticationType));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => captured.ReadAsync(synthetic));
        var forged = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, "forged")], "Bearer"));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => captured.ReadAsync(forged));
        using var logout = await host.Client.PostAsync("/api/access/logout", null);
        Assert.Equal(HttpStatusCode.NoContent, logout.StatusCode);
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => captured.UpdateAsync(principal!, new(Theme: UiThemeMode.Dark)));
        Assert.Equal(UserUiPreferences.Default, await host.App.Services.GetRequiredService<IUserUiPreferencesStore>()
            .ReadAsync(UserUiPreferencesAccount.ConfiguredAdministrator));
    }

    [Fact]
    public async Task Session_revoked_while_a_patch_is_pending_cannot_publish_account_preferences() {
        var password = Password();
        HeldPreferencesStore? held = null;
        await using var host = await CreateHostAsync(password, configureServices: services => {
            services.AddSingleton<IUserUiPreferencesStore>(provider => held = new(provider.GetRequiredService<FileUserUiPreferencesStore>()));
            services.AddSingleton<FileUserUiPreferencesStore>();
        });
        SetToken(host.Client, (await LoginAsync(host.Client, "admin", password)).Token);
        held = Assert.IsType<HeldPreferencesStore>(host.App.Services.GetRequiredService<IUserUiPreferencesStore>());
        var pending = host.Client.PatchAsJsonAsync(PreferencesRoute, new UserUiPreferencesPatch(Theme: UiThemeMode.Dark));
        try {
            await held!.Entered.Task.WaitAsync(TimeSpan.FromSeconds(10));
            using var logout = await host.Client.PostAsync("/api/access/logout", null);
            Assert.Equal(HttpStatusCode.NoContent, logout.StatusCode);
        } finally {
            held?.Release.TrySetResult();
        }
        using var rejected = await pending.WaitAsync(TimeSpan.FromSeconds(10));
        Assert.Equal(HttpStatusCode.Forbidden, rejected.StatusCode);
        Assert.Equal(UserUiPreferences.Default, await held!.ReadAsync(UserUiPreferencesAccount.ConfiguredAdministrator));
    }

    private static async Task<UserUiPreferences> ReadAsync(HttpClient client) {
        using var response = await client.GetAsync(PreferencesRoute);
        response.EnsureSuccessStatusCode();
        Assert.True(response.Headers.CacheControl?.NoStore);
        return (await response.Content.ReadFromJsonAsync<UserUiPreferences>())!;
    }

    private static async Task<UserUiPreferences> PatchAsync(HttpClient client, UserUiPreferencesPatch patch) {
        using var response = await client.PatchAsJsonAsync(PreferencesRoute, patch);
        response.EnsureSuccessStatusCode();
        Assert.True(response.Headers.CacheControl?.NoStore);
        return (await response.Content.ReadFromJsonAsync<UserUiPreferences>())!;
    }

    private static Dictionary<string, string?> Configuration(string password) => new() {
        ["Api:UserAuthentication:Enabled"] = "true",
        ["Api:UserAuthentication:AllowLoopbackHttp"] = "true",
        ["Api:AccessManagement:Enabled"] = "true",
        ["Api:BootstrapAdmin:PasswordHash"] = new ApiPasswordService().Hash(password)
    };

    private sealed class HeldPreferencesStore(IUserUiPreferencesStore inner) : IUserUiPreferencesStore {
        public TaskCompletionSource Entered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public Task<UserUiPreferences> ReadAsync(UserUiPreferencesAccount account, CancellationToken cancellationToken = default)
            => inner.ReadAsync(account, cancellationToken);

        public Task<UserUiPreferences> UpdateAsync(UserUiPreferencesAccount account, UserUiPreferencesPatch patch,
            Func<CancellationToken, Task> authorizeCommit, CancellationToken cancellationToken = default)
            => inner.UpdateAsync(account, patch, async token => {
                Entered.SetResult();
                await Release.Task.WaitAsync(token);
                await authorizeCommit(token);
            }, cancellationToken);
    }
}
