using System.Security.Claims;
using CanDoItAll.AppComponents;
using CanDoItAll.Components.BaseLib;
using CanDoItAll.SharedKernel;
using CanDoItAll.Web.Api;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.JSInterop;

namespace CanDoItAll.Web.Infrastructure;

public sealed class UserUiPreferencesState(
    AuthenticationStateProvider authentication,
    UserUiPreferencesService accounts,
    IJSRuntime js,
    ILogger<UserUiPreferencesState> logger) : IUserUiPreferencesState, IDisposable {
    private const string ApplyTheme = "CanDoItAll.appTheme.apply";
    private const string ReadBrowserPreferences = "CanDoItAll.appTheme.getPreferences";
    private const string UpdateBrowserPreferences = "CanDoItAll.appTheme.updatePreferences";
    private readonly CancellationTokenSource lifetime = new();
    private ClaimsPrincipal? principal;
    private long generation;
    private bool browserConnected;
    private bool disposed;

    public UserUiPreferences Current { get; private set; } = UserUiPreferences.Default;
    public bool IsInitialized { get; private set; }
    public bool IsReady { get; private set; }
    public bool IsBusy { get; private set; }
    public bool CanEdit => browserConnected && IsReady && !IsBusy && Failure == UserUiPreferencesFailure.None;
    public bool IsAnonymous => principal is not null && !IsAuthenticated(principal);
    public UserUiPreferencesFailure Failure { get; private set; }
    public string ThemeKey => Current.Theme == UiThemeMode.Dark ? CadThemes.Dark : CadThemes.Light;
    public event Action? Changed;

    public Task InitializePrerenderAsync(HttpContext context) => IsInitialized ? Task.CompletedTask :
        LoadAsync(() => Task.FromResult(new AuthenticationState(context.User)), context.Request.Cookies);

    public Task InitializeAsync(UserUiPreferences? anonymousBootstrap) => IsInitialized ? Task.CompletedTask :
        LoadAsync(authentication.GetAuthenticationStateAsync, anonymousBootstrap: anonymousBootstrap);

    public async Task ConnectBrowserAsync() {
        if (browserConnected || disposed) {
            return;
        }
        browserConnected = true;
        authentication.AuthenticationStateChanged += HandleAuthenticationChanged;
        await RetryAsync();
    }

    public Task RetryAsync() => LoadAsync(authentication.GetAuthenticationStateAsync);

    private void HandleAuthenticationChanged(Task<AuthenticationState> next) {
        _ = LoadAsync(() => next);
    }

    private async Task LoadAsync(Func<Task<AuthenticationState>> getAuthentication,
        IRequestCookieCollection? cookies = null, UserUiPreferences? anonymousBootstrap = null) {
        if (disposed) {
            return;
        }
        var operation = ++generation;
        IsInitialized = true;
        IsBusy = true;
        IsReady = false;
        Failure = UserUiPreferencesFailure.None;
        Changed?.Invoke();
        try {
            var nextPrincipal = (await getAuthentication().WaitAsync(lifetime.Token)).User;
            if (!IsCurrent(operation)) {
                return;
            }
            principal = nextPrincipal;
            var account = await accounts.ReadAsync(nextPrincipal, lifetime.Token);
            var preferences = account?.Preferences;
            if (account is null) {
                preferences = browserConnected
                    ? await js.InvokeAsync<UserUiPreferences>(ReadBrowserPreferences, lifetime.Token)
                        ?? throw new InvalidDataException("Browser UI preferences were not returned.")
                    : cookies is not null ? BrowserUiPreferences.Read(cookies) : anonymousBootstrap;
            }
            if (preferences is null || !IsCurrent(operation)) {
                return;
            }
            preferences.Validate();
            if (browserConnected) {
                await ApplyThemeAsync(preferences);
            }
            if (IsCurrent(operation)) {
                Current = preferences;
                IsReady = true;
            }
        } catch (OperationCanceledException) when (disposed) {
        } catch (Exception exception) {
            if (IsCurrent(operation)) {
                Failure = UserUiPreferencesFailure.Load;
                logger.LogWarning(exception, "Could not load personal UI preferences. Retry after the account or browser storage is available.");
            }
        } finally {
            if (IsCurrent(operation)) {
                IsBusy = false;
                Changed?.Invoke();
            }
        }
    }

    public async Task UpdateAsync(UserUiPreferencesPatch patch) {
        ArgumentNullException.ThrowIfNull(patch);
        patch.Validate();
        if (disposed || !browserConnected || !IsReady || IsBusy || Failure != UserUiPreferencesFailure.None || principal is null) {
            return;
        }
        var operation = generation;
        var owner = principal;
        IsBusy = true;
        Changed?.Invoke();
        try {
            var account = await accounts.UpdateAsync(owner, patch, lifetime.Token);
            if (!IsCurrent(operation)) {
                return;
            }
            var preferences = account?.Preferences ??
                await js.InvokeAsync<UserUiPreferences>(UpdateBrowserPreferences, lifetime.Token, patch);
            preferences.Validate();
            if (!IsCurrent(operation)) {
                return;
            }
            await ApplyThemeAsync(preferences);
            if (IsCurrent(operation)) {
                Current = preferences;
            }
        } catch (OperationCanceledException) when (disposed) {
        } catch (Exception exception) {
            if (IsCurrent(operation)) {
                Failure = UserUiPreferencesFailure.Save;
                logger.LogWarning(exception, "Could not save or apply personal UI preferences. Reload preferences before making another change.");
            }
        } finally {
            if (IsCurrent(operation)) {
                IsBusy = false;
                Changed?.Invoke();
            }
        }
    }

    private ValueTask ApplyThemeAsync(UserUiPreferences preferences) => js.InvokeVoidAsync(
        ApplyTheme, lifetime.Token, preferences.Theme == UiThemeMode.Dark ? CadThemes.Dark : CadThemes.Light);

    private bool IsCurrent(long operation) => !disposed && operation == generation;

    private static bool IsAuthenticated(ClaimsPrincipal value) => value.Identities.Any(identity => identity.IsAuthenticated);

    public void Dispose() {
        if (disposed) {
            return;
        }
        disposed = true;
        ++generation;
        authentication.AuthenticationStateChanged -= HandleAuthenticationChanged;
        lifetime.Cancel();
        lifetime.Dispose();
    }
}
