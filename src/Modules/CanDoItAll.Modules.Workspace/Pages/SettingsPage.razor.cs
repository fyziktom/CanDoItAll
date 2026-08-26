using CanDoItAll.Components.BaseLib;
using CanDoItAll.Modules.Security;
using CanDoItAll.Modules.Workspace.ApiAccess;
using CanDoItAll.Modules.Workspace.Pages.Components;
using Microsoft.AspNetCore.Components;

namespace CanDoItAll.Modules.Workspace.Pages;

public partial class SettingsPage
{
    private const string FilesSettingsTabKey = "files";

    [SupplyParameterFromQuery(Name = "tab")]
    public string? RequestedTab { get; set; }

    [Inject]
    public NotificationService NotificationService { get; set; } = default!;

    [Inject]
    public IApiTokenService ApiTokenService { get; set; } = default!;

    private WorkspaceSettingsModel settingsModel = new();
    private SecretEditorModel secretModel = NewSecret();
    private ApiTokenIssueRequest apiTokenModel = NewApiToken();
    private ApiAccessStatus? apiStatus;
    private ApiTokenIssueResult? issuedApiToken;
    private IReadOnlyList<WorkspaceProviderOption> providers = [];
    private IReadOnlyList<SecretListItem> secrets = [];
    private string settingsTab = "workspace";
    private string secretSearch = string.Empty;
    private string apiScopesText = "api";

    private IReadOnlyList<SecondaryTabItem> SettingsTabs =>
    [
        new("workspace", "Workspace"),
        new("data-sources", "Data Sources"),
        new("storage", "Storage"),
        new(FilesSettingsTabKey, "Files"),
        new("secrets", "Secrets", secrets.Count.ToString()),
        new("providers", "Providers", providers.Count.ToString()),
        new("api-access", "API Access", apiStatus?.AuthorizationEnabled == true ? "JWT" : "Open")
    ];

    private IReadOnlyList<SecretListItem> FilteredSecrets => secrets
        .Where(secret =>
            string.IsNullOrWhiteSpace(secretSearch) ||
            secret.Name.Contains(secretSearch, StringComparison.OrdinalIgnoreCase) ||
            secret.Scope.Contains(secretSearch, StringComparison.OrdinalIgnoreCase))
        .OrderBy(secret => secret.Name)
        .ToList();

    protected override async Task OnInitializedAsync()
    {
        await LoadAsync();
    }

    protected override void OnParametersSet()
    {
        ApplyRequestedTab();
    }

    private async Task LoadAsync()
    {
        settingsModel = await WorkspaceService.GetSettingsAsync();
        providers = await ProviderCatalog.ListAsync();
        secrets = await WorkspaceService.ListSecretsAsync();
        apiStatus = ApiTokenService.GetStatus();
        apiTokenModel = NewApiToken(apiStatus?.DefaultTokenLifetimeMinutes);
        ApplyRequestedTab();
    }

    private async Task SaveSettingsAsync()
    {
        try
        {
            await WorkspaceService.SaveSettingsAsync(settingsModel);
            providers = await ProviderCatalog.ListAsync();
            NotificationService.Success("Workspace defaults saved", "Workspace defaults saved.");
        }
        catch (Exception exception)
        {
            NotificationService.Error("Workspace defaults save failed", exception.Message);
        }
    }

    private async Task SaveSecretAsync()
    {
        try
        {
            var result = await SecretService.SaveAsync(secretModel);
            if (!result.IsSuccess)
            {
                NotificationService.Warning("Secret was not saved", string.Join(" ", result.Errors.Select(error => error.Message)));
                return;
            }

            secrets = await WorkspaceService.ListSecretsAsync();
            await ResetSecretAsync();
            NotificationService.Success("Secret saved", "Secret saved.");
        }
        catch (Exception exception)
        {
            NotificationService.Error("Secret save failed", exception.Message);
        }
    }

    private async Task EditSecretAsync(Guid id)
    {
        var model = await SecretService.GetAsync(id);
        if (model is not null)
        {
            secretModel = model;
        }
    }

    private async Task DeleteSecretAsync()
    {
        if (!secretModel.Id.HasValue)
        {
            return;
        }

        try
        {
            await SecretService.DeleteAsync(secretModel.Id.Value);
            secrets = await WorkspaceService.ListSecretsAsync();
            await ResetSecretAsync();
            NotificationService.Success("Secret deleted", "Secret deleted.");
        }
        catch (Exception exception)
        {
            NotificationService.Error("Secret delete failed", exception.Message);
        }
    }

    private Task ResetSecretAsync()
    {
        secretModel = NewSecret();
        return Task.CompletedTask;
    }

    private Task HandleSettingsTabChanged(string key)
    {
        if (string.Equals(key, "providers", StringComparison.Ordinal))
        {
            Navigation.NavigateTo("/agents?tab=providers", replace: true);
            return Task.CompletedTask;
        }

        settingsTab = key;
        Navigation.NavigateTo(BuildSettingsRoute(key), replace: true);
        return Task.CompletedTask;
    }

    private Task IssueApiTokenAsync()
    {
        try
        {
            apiTokenModel.Scopes = ParseApiScopes(apiScopesText);
            issuedApiToken = ApiTokenService.IssueToken(apiTokenModel);
            NotificationService.Success("API token created", $"Token expires at {FormatTimestamp(issuedApiToken.ExpiresAtUtc)}.");
        }
        catch (Exception exception)
        {
            issuedApiToken = null;
            NotificationService.Error("API token failed", exception.Message);
        }

        return Task.CompletedTask;
    }

    private void ApplyRequestedTab()
    {
        if (string.Equals(RequestedTab, "providers", StringComparison.Ordinal))
        {
            settingsTab = "workspace";
            Navigation.NavigateTo("/agents?tab=providers", replace: true);
            return;
        }

        if (IsValidSettingsTab(RequestedTab))
        {
            settingsTab = RequestedTab!;
            return;
        }

        if (!IsValidSettingsTab(settingsTab))
        {
            settingsTab = "workspace";
        }
    }

    private static bool IsValidSettingsTab(string? key)
    {
        return key is "workspace" or "data-sources" or "storage" or FilesSettingsTabKey or "secrets" or "providers" or "api-access";
    }

    private static string BuildSettingsRoute(string key)
    {
        return string.Equals(key, "workspace", StringComparison.Ordinal)
            ? "/settings"
            : $"/settings?tab={Uri.EscapeDataString(key)}";
    }

    private void ResetSecretSearch()
    {
        secretSearch = string.Empty;
    }

    private static string FormatTimestamp(DateTimeOffset timestamp)
    {
        return timestamp.LocalDateTime.ToString("g");
    }

    private string FormatApiAudience()
    {
        if (apiStatus is null)
        {
            return "API status is not loaded.";
        }

        return apiStatus.AuthorizationEnabled
            ? $"Issuer: {apiStatus.Issuer} / Audience: {apiStatus.Audience}"
            : "Bearer tokens are not required.";
    }

    private static List<string> ParseApiScopes(string value)
    {
        var scopes = value
            .Split([' ', ',', ';', '\r', '\n', '\t'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(item => item, StringComparer.OrdinalIgnoreCase)
            .ToList();

        return scopes.Count == 0 ? ["api"] : scopes;
    }

    private static SecretEditorModel NewSecret() => new()
    {
        Kind = SecretKind.ApiKey,
        Scope = "workspace"
    };

    private static ApiTokenIssueRequest NewApiToken(int? lifetimeMinutes = null) => new()
    {
        Subject = "api-client",
        DisplayName = "API client",
        LifetimeMinutes = lifetimeMinutes,
        Scopes = ["api"]
    };

}
