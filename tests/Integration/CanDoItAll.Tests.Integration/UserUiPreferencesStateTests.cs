using System.Security.Claims;
using CanDoItAll.AppComponents;
using CanDoItAll.Infrastructure.ControlPlane;
using CanDoItAll.Modules.Workspace.ApiAccess;
using CanDoItAll.SharedKernel;
using CanDoItAll.Web.Api;
using CanDoItAll.Web.Infrastructure;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.JSInterop;
using static CanDoItAll.Tests.Integration.Api.ApiUserSessionIntegrationTests;

namespace CanDoItAll.Tests.Integration.Api;

[Trait("Category", "HostPlatform")]
public sealed class UserUiPreferencesStateTests {
    [Fact]
    public async Task Anonymous_prerender_restores_cookies_and_connected_patches_preserve_other_fields() {
        await using var fixture = await Fixture.CreateAsync();
        var context = new DefaultHttpContext();
        context.Request.Headers.Cookie = $"{BrowserUiPreferences.ThemeCookieName}=dark; {BrowserUiPreferences.MainMenuCollapsedCookieName}=false";
        fixture.Js.BrowserPreferences = new(UiThemeMode.Dark, false);
        await fixture.State.InitializePrerenderAsync(context);
        Assert.Equal(fixture.Js.BrowserPreferences, fixture.State.Current);
        Assert.True(fixture.State.IsReady);
        Assert.False(((IUserUiPreferencesState)fixture.State).CanEdit);
        Assert.Empty(fixture.Js.AppliedThemes);
        await fixture.State.ConnectBrowserAsync();
        Assert.True(((IUserUiPreferencesState)fixture.State).CanEdit);
        await fixture.State.UpdateAsync(new(MainMenuCollapsed: true));
        Assert.Equal(new UserUiPreferences(UiThemeMode.Dark, true), fixture.Js.BrowserPreferences);
        Assert.Equal(fixture.Js.BrowserPreferences, fixture.State.Current);
        Assert.True(fixture.State.IsReady);
        Assert.Equal("dark", fixture.Js.AppliedThemes.Last());
        Assert.Equal(UserUiPreferences.Default, await fixture.Store.ReadAsync(UserUiPreferencesAccount.ConfiguredAdministrator));
    }

    [Fact]
    public async Task Failed_save_keeps_rendered_value_and_explicit_retry_recovers_from_saved_state() {
        await using var fixture = await Fixture.CreateAsync();
        await fixture.State.ConnectBrowserAsync();
        fixture.Js.Update = (_, _) => throw new JSException("The browser denied the preference write.");
        await fixture.State.UpdateAsync(new(Theme: UiThemeMode.Dark));
        Assert.Equal(UserUiPreferences.Default, fixture.State.Current);
        Assert.Equal(UserUiPreferencesFailure.Save, fixture.State.Failure);
        Assert.False(fixture.State.IsBusy);
        Assert.Equal("light", fixture.Js.AppliedThemes.Last());
        fixture.Js.Update = null;
        await fixture.State.RetryAsync();
        Assert.Equal(UserUiPreferencesFailure.None, fixture.State.Failure);
        await fixture.State.UpdateAsync(new(Theme: UiThemeMode.Dark));
        Assert.Equal(UiThemeMode.Dark, fixture.State.Current.Theme);
        Assert.Equal("dark", fixture.Js.AppliedThemes.Last());
    }

    [Fact]
    public async Task Refreshed_authenticated_state_loads_server_preferences_without_reading_anonymous_storage() {
        await using var fixture = await Fixture.CreateAsync();
        await fixture.Store.UpdateAsync(UserUiPreferencesAccount.ConfiguredAdministrator, new(UiThemeMode.Dark, false), Allow);
        fixture.SelectAccount();
        await fixture.State.InitializeAsync(UserUiPreferences.Default);
        await fixture.State.ConnectBrowserAsync();
        Assert.Equal(new UserUiPreferences(UiThemeMode.Dark, false), fixture.State.Current);
        Assert.Equal(0, fixture.Js.ReadCount);
        await fixture.State.UpdateAsync(new(MainMenuCollapsed: true));
        using var refreshed = fixture.NewState();
        await refreshed.InitializeAsync(UserUiPreferences.Default);
        await refreshed.ConnectBrowserAsync();
        Assert.Equal(new UserUiPreferences(UiThemeMode.Dark, true), refreshed.Current);
        Assert.Equal(UserUiPreferences.Default, fixture.Js.BrowserPreferences);
        Assert.Equal(0, fixture.Js.UpdateCount);
    }

    [Fact]
    public async Task Unsupported_authenticated_identity_reports_load_failure_without_browser_fallback() {
        await using var fixture = await Fixture.CreateAsync();
        fixture.Authentication.Set(new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, "unverified")], "Bearer")));
        await fixture.State.ConnectBrowserAsync();
        Assert.Equal(UserUiPreferencesFailure.Load, fixture.State.Failure);
        Assert.False(fixture.State.IsReady);
        Assert.Equal(0, fixture.Js.ReadCount);
        Assert.Empty(fixture.Js.AppliedThemes);
    }

    [Fact]
    public async Task Null_browser_response_is_an_explicit_load_failure() {
        await using var fixture = await Fixture.CreateAsync();
        fixture.Js.Read = _ => Task.FromResult<UserUiPreferences>(null!);
        await fixture.State.ConnectBrowserAsync();
        Assert.Equal(UserUiPreferencesFailure.Load, fixture.State.Failure);
        Assert.False(fixture.State.IsReady);
        Assert.False(fixture.State.IsBusy);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Anonymous_completion_after_account_change_cannot_replace_the_new_account_state(bool save) {
        await using var fixture = await Fixture.CreateAsync();
        var expected = new UserUiPreferences(UiThemeMode.Dark, false);
        await fixture.Store.UpdateAsync(UserUiPreferencesAccount.ConfiguredAdministrator, new(expected.Theme, expected.MainMenuCollapsed), Allow);
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource<UserUiPreferences>(TaskCreationOptions.RunContinuationsAsynchronously);
        if (save) {
            await fixture.State.ConnectBrowserAsync();
            fixture.Js.Update = (_, _) => {
                entered.SetResult();
                return release.Task;
            };
        } else {
            fixture.Js.Read = _ => {
                entered.SetResult();
                return release.Task;
            };
        }
        var pending = save ? fixture.State.UpdateAsync(new(MainMenuCollapsed: false)) : fixture.State.ConnectBrowserAsync();
        try {
            await entered.Task.WaitAsync(TimeSpan.FromSeconds(10));
            fixture.SelectAccount();
            await WaitForAsync(fixture.State, () => fixture.State.IsReady && fixture.State.Current == expected);
        } finally {
            release.TrySetResult(UserUiPreferences.Default);
            await pending.WaitAsync(TimeSpan.FromSeconds(10));
        }
        Assert.Equal(expected, fixture.State.Current);
        Assert.Equal("dark", fixture.Js.AppliedThemes.Last());
        Assert.Equal(UserUiPreferencesFailure.None, fixture.State.Failure);
    }

    [Fact]
    public async Task Late_apply_reply_cannot_replace_a_newer_account_after_its_synchronous_dom_update() {
        await using var fixture = await Fixture.CreateAsync();
        var expected = new UserUiPreferences(UiThemeMode.Dark, false);
        await fixture.Store.UpdateAsync(UserUiPreferencesAccount.ConfiguredAdministrator, new(expected.Theme, expected.MainMenuCollapsed), Allow);
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        fixture.Js.ApplyReply = () => {
            fixture.Js.ApplyReply = null;
            entered.SetResult();
            return release.Task;
        };
        var pending = fixture.State.ConnectBrowserAsync();
        try {
            await entered.Task.WaitAsync(TimeSpan.FromSeconds(10));
            Assert.Equal("light", fixture.Js.AppliedThemes.Last());
            fixture.SelectAccount();
            await WaitForAsync(fixture.State, () => fixture.State.Current == expected && fixture.State.IsReady);
            Assert.Equal("dark", fixture.Js.AppliedThemes.Last());
        } finally {
            release.TrySetResult();
            await pending.WaitAsync(TimeSpan.FromSeconds(10));
        }
        Assert.Equal(expected, fixture.State.Current);
        Assert.Equal("dark", fixture.Js.AppliedThemes.Last());
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Disposed_state_ignores_late_browser_read_or_write_completion(bool save) {
        await using var fixture = await Fixture.CreateAsync();
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource<UserUiPreferences>(TaskCreationOptions.RunContinuationsAsynchronously);
        if (save) {
            await fixture.State.ConnectBrowserAsync();
            fixture.Js.Update = (_, _) => {
                entered.SetResult();
                return release.Task;
            };
        } else {
            fixture.Js.Read = _ => {
                entered.SetResult();
                return release.Task;
            };
        }
        var appliedCount = fixture.Js.AppliedThemes.Count;
        var pending = save ? fixture.State.UpdateAsync(new(Theme: UiThemeMode.Dark)) : fixture.State.ConnectBrowserAsync();
        try {
            await entered.Task.WaitAsync(TimeSpan.FromSeconds(10));
            fixture.State.Dispose();
        } finally {
            release.TrySetResult(new(UiThemeMode.Dark, false));
            await pending.WaitAsync(TimeSpan.FromSeconds(10));
        }
        Assert.Equal(UserUiPreferences.Default, fixture.State.Current);
        Assert.Equal(appliedCount, fixture.Js.AppliedThemes.Count);
    }

    private static async Task WaitForAsync(UserUiPreferencesState state, Func<bool> condition) {
        var completed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        void Observe() {
            if (condition()) {
                completed.TrySetResult();
            }
        }
        state.Changed += Observe;
        try {
            Observe();
            await completed.Task.WaitAsync(TimeSpan.FromSeconds(10));
        } finally {
            state.Changed -= Observe;
        }
    }

    private static Task Allow(CancellationToken token) {
        token.ThrowIfCancellationRequested();
        return Task.CompletedTask;
    }

    private sealed class Fixture(ApiTestHost host, HttpContext accountContext) : IAsyncDisposable {
        private readonly MutableHttpContextAccessor http = new();
        public TestAuthentication Authentication { get; } = new();
        public TestJs Js { get; } = new();
        public IUserUiPreferencesStore Store { get; } = host.App.Services.GetRequiredService<IUserUiPreferencesStore>();
        public UserUiPreferencesState State { get; private set; } = null!;

        public UserUiPreferencesState NewState() => new(Authentication,
            new UserUiPreferencesService(http, host.App.Services.GetRequiredService<IApiTokenRegistry>(),
                host.App.Services.GetRequiredService<ApiSessionService>(), host.App.Services.GetRequiredService<IOptions<ApiAccessOptions>>(),
                host.App.Services.GetRequiredService<IClock>(), Store), Js, NullLogger<UserUiPreferencesState>.Instance);

        public void SelectAccount() {
            http.HttpContext = accountContext;
            Authentication.Set(accountContext.User);
        }

        public static async Task<Fixture> CreateAsync() {
            var password = Password();
            HttpContext? accountContext = null;
            var host = await ApiTestHost.CreateAsync(true, useInMemoryDatabase: true,
                apiConfiguration: new Dictionary<string, string?> {
                    ["Api:UserAuthentication:Enabled"] = "true",
                    ["Api:UserAuthentication:AllowLoopbackHttp"] = "true",
                    ["Api:BootstrapAdmin:PasswordHash"] = new ApiPasswordService().Hash(password)
                }, configureApplication: app => {
                    app.MapGet("/test/state-account", (HttpContext context) => {
                        accountContext = new DefaultHttpContext { User = context.User };
                        accountContext.Features.Set(context.Features.Get<ApiAuthenticationObservation>());
                        return Results.NoContent();
                    }).RequireAuthorization(ApiAuthorizationPolicies.UserSession);
                });
            try {
                SetToken(host.Client, (await LoginAsync(host.Client, "admin", password)).Token);
                using var captured = await host.Client.GetAsync("/test/state-account");
                captured.EnsureSuccessStatusCode();
                var fixture = new Fixture(host, accountContext!);
                fixture.State = fixture.NewState();
                return fixture;
            } catch {
                await host.DisposeAsync();
                throw;
            }
        }

        public async ValueTask DisposeAsync() {
            State.Dispose();
            await host.DisposeAsync();
        }
    }

    private sealed class MutableHttpContextAccessor : IHttpContextAccessor {
        public HttpContext? HttpContext { get; set; }
    }

    private sealed class TestAuthentication : AuthenticationStateProvider {
        private AuthenticationState current = new(new ClaimsPrincipal(new ClaimsIdentity()));
        public override Task<AuthenticationState> GetAuthenticationStateAsync() => Task.FromResult(current);
        public void Set(ClaimsPrincipal principal) {
            current = new(principal);
            NotifyAuthenticationStateChanged(Task.FromResult(current));
        }
    }

    private sealed class TestJs : IJSRuntime {
        public UserUiPreferences BrowserPreferences { get; set; } = UserUiPreferences.Default;
        public Func<CancellationToken, Task<UserUiPreferences>>? Read { get; set; }
        public Func<UserUiPreferencesPatch, CancellationToken, Task<UserUiPreferences>>? Update { get; set; }
        public Func<Task>? ApplyReply { get; set; }
        public List<string> AppliedThemes { get; } = [];
        public int ReadCount { get; private set; }
        public int UpdateCount { get; private set; }

        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, object?[]? args) => InvokeAsync<TValue>(identifier, default, args);

        public async ValueTask<TValue> InvokeAsync<TValue>(string identifier, CancellationToken cancellationToken, object?[]? args) {
            cancellationToken.ThrowIfCancellationRequested();
            object? result = null;
            switch (identifier) {
                case "CanDoItAll.appTheme.getPreferences":
                    ReadCount++;
                    result = Read is null ? BrowserPreferences : await Read(cancellationToken);
                    break;
                case "CanDoItAll.appTheme.updatePreferences":
                    UpdateCount++;
                    var patch = Assert.IsType<UserUiPreferencesPatch>(Assert.Single(args!));
                    BrowserPreferences = Update is null ? BrowserPreferences.Apply(patch) : await Update(patch, cancellationToken);
                    result = BrowserPreferences;
                    break;
                case "CanDoItAll.appTheme.apply":
                    AppliedThemes.Add(Assert.IsType<string>(Assert.Single(args!)));
                    if (ApplyReply is { } reply) {
                        await reply();
                    }
                    break;
                default:
                    throw new InvalidOperationException($"Unexpected preference browser operation: {identifier}");
            }
            return result is null ? default! : (TValue)result;
        }
    }
}
