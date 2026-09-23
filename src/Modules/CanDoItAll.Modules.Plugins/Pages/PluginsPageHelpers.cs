using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Modules.Plugins;
using CanDoItAll.Plugins.Abstractions;
using CanDoItAll.SharedKernel.Configuration;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;

namespace CanDoItAll.Modules.Plugins.Pages;

internal static class PluginsPageHelpers
{
    public const string InstallAction = "install";
    public const string EnableAction = "enable";
    public const string DisableAction = "disable";
    public const string PackageInstallAction = "package-install";
    public const string PackageUploadAction = "package-upload";
    public const string RestartAction = "restart";

    public static bool ResolveCheckboxValue(ChangeEventArgs args)
        => args.Value is bool value && value;

    public static IReadOnlyList<ConfigurationValidationIssue> ResolveValidationIssues(
        PluginConnectionEditorState editor,
        ConfigurationFieldDescriptor field)
        => editor.Validation.Issues
            .Where(issue => string.Equals(issue.FieldKey, field.Key, StringComparison.OrdinalIgnoreCase))
            .ToArray();

    public static string ResolveFieldPlaceholder(
        PluginSettingsDetail settings,
        ConfigurationFieldDescriptor field,
        string callbackUri)
    {
        if (string.Equals(field.Key, PluginOAuthConnectionSettingKeys.ClientId, StringComparison.OrdinalIgnoreCase) &&
            !string.IsNullOrWhiteSpace(settings.OAuth2?.ClientId))
        {
            return "Using bundled client id";
        }

        return string.Equals(field.Key, PluginOAuthConnectionSettingKeys.RedirectUri, StringComparison.OrdinalIgnoreCase)
            ? callbackUri
            : string.Empty;
    }

    public static string ResolveInputType(ConfigurationFieldDescriptor field)
        => field.FieldType switch
        {
            ConfigurationFieldType.Number => "number",
            ConfigurationFieldType.Url => "url",
            _ => "text"
        };

    public static string BusyKey(PluginId pluginId, string action)
        => $"{pluginId.Value}:{action}";

    public static string PackageBusyKey(PluginPackageId packageId, string action)
        => $"package:{packageId.Value}:{action}";

    public static string PageBusyKey(string action)
        => $"page:{action}";

    public static string GrantBusyKey(PluginCapabilityGrantItem grant, string action)
        => $"{action}:{grant.Capability}:{grant.RecipeId?.Value ?? "capability"}";

    public static string OAuthBusyKey(PluginConnectionDescriptor descriptor, string action)
        => $"oauth-{action}:{descriptor.Key.Value}";

    public static string ConnectionSaveBusyKey(PluginConnectionDescriptor descriptor)
        => $"connection-save:{descriptor.Key.Value}";

    public static string ConnectionEditorKey(PluginId pluginId, PluginConnectionKey connectionKey)
        => $"{pluginId.Value}:{connectionKey.Value}";

    public static PluginConnectionItem? ResolveConnection(
        PluginSettingsDetail settings,
        PluginConnectionDescriptor descriptor)
        => settings.Connections
            .Where(connection => connection.ConnectionKey == descriptor.Key)
            .OrderByDescending(connection => connection.UpdatedAtUtc)
            .FirstOrDefault();

    public static bool HasGrantedCapability(
        PluginSettingsDetail settings,
        PluginCapabilityKind capability)
        => settings.Grants.Any(grant =>
            grant.Capability == capability &&
            grant.RecipeId is null &&
            grant.ScopeKind == PluginGrantScopeKind.Plugin &&
            string.IsNullOrWhiteSpace(grant.ScopeKey) &&
            grant.State == PluginGrantState.Granted);

    public static bool CanStartOAuth(
        PluginCatalogItem plugin,
        PluginSettingsDetail settings,
        PluginConnectionDescriptor descriptor,
        PluginConnectionEditorState editor,
        ConfigurationSchemaValidator validator)
        => plugin.IsEnabled &&
           HasGrantedCapability(settings, PluginCapabilityKind.OAuth2) &&
           !editor.IsDirty &&
           validator.Validate(descriptor.SettingsSchema, editor.State).Succeeded;

    public static string ResolveOAuthActionHint(
        PluginCatalogItem plugin,
        PluginSettingsDetail settings,
        PluginConnectionDescriptor descriptor,
        PluginConnectionEditorState editor,
        ConfigurationSchemaValidator validator)
    {
        if (!plugin.IsEnabled)
        {
            return "Install and enable the plugin before login.";
        }

        if (!HasGrantedCapability(settings, PluginCapabilityKind.OAuth2))
        {
            return "Grant OAuth2 capability before login.";
        }

        if (editor.IsDirty)
        {
            return "Save connection settings before login.";
        }

        var validation = validator.Validate(descriptor.SettingsSchema, editor.State);
        return validation.Succeeded
            ? string.Empty
            : string.Join(" ", validation.Issues.Select(issue => issue.Message));
    }

    public static IReadOnlyList<PluginCapabilityKind> ResolveDeclaredCapabilities(PluginCatalogItem plugin)
        => Enum.GetValues<PluginCapabilityKind>()
            .Where(capability => capability != PluginCapabilityKind.None && plugin.Capabilities.HasFlag(capability))
            .OrderBy(capability => capability.ToString(), StringComparer.OrdinalIgnoreCase)
            .ToArray();

    public static string ResolveGrantName(PluginCapabilityGrantItem grant)
        => grant.RecipeId is { } recipeId
            ? $"{grant.Capability}: {recipeId.Value}"
            : grant.Capability.ToString();

    public static string ResolveAvailabilityTone(PluginCatalogItem plugin)
        => plugin.Availability == PluginCatalogAvailabilityKind.Available
            ? "success"
            : "warning";

    public static string ResolveInstallationTone(PluginCatalogItem plugin)
        => plugin.InstallationState switch
        {
            PluginInstallationStateKind.InstalledEnabled => "success",
            PluginInstallationStateKind.InstalledDisabled => "warning",
            _ => "neutral"
        };

    public static string ResolvePackageTone(PluginPackageCatalogItem package)
        => package.IsInstalled ? "success" : "info";

    public static string ResolveGrantTone(PluginGrantState state)
        => state switch
        {
            PluginGrantState.Granted => "success",
            PluginGrantState.Denied or PluginGrantState.Revoked => "danger",
            PluginGrantState.Requested => "warning",
            _ => "neutral"
        };

    public static string ResolveRiskTone(PluginGrantRiskKind riskKind)
        => riskKind switch
        {
            PluginGrantRiskKind.High => "danger",
            PluginGrantRiskKind.Medium => "warning",
            _ => "info"
        };

    public static string ResolveOAuthTone(PluginOAuthConnectionStatusKind? status)
        => status switch
        {
            PluginOAuthConnectionStatusKind.Connected => "success",
            PluginOAuthConnectionStatusKind.ReconnectRequired => "warning",
            PluginOAuthConnectionStatusKind.Error => "danger",
            _ => "neutral"
        };

    public static string ResolveOAuthActionText(PluginOAuthConnectionStatusKind? status)
        => status == PluginOAuthConnectionStatusKind.ReconnectRequired ? "Reconnect" : "Login";

    public static string ResolveSettingsBadge(PluginSettingsDetail settings)
        => settings.ConnectionDescriptors
            .Count(descriptor => descriptor.SettingsSchema.Fields.Count > 0)
            .ToString();

    public static string ResolveExecutorBadge(PluginCatalogItem plugin)
        => plugin.Descriptor.WorkflowExecutors.Count.ToString();

    public static string ResolveExecutorApprovalTone(WorkflowExecutorApprovalRequirement requirement)
        => requirement switch
        {
            WorkflowExecutorApprovalRequirement.NotRequired => "success",
            WorkflowExecutorApprovalRequirement.RequiredForExternalEffect => "warning",
            WorkflowExecutorApprovalRequirement.AlwaysRequired => "danger",
            _ => "neutral"
        };

    public static string ResolveExecutorPolicySummary(PluginWorkflowExecutorDescriptor executor)
        => $"Timeout {executor.DefaultPolicy.TimeoutSeconds}s, retries {executor.DefaultPolicy.MaxRetryAttempts}";

    public static string ResolveExecutorSettingsSummary(PluginWorkflowExecutorDescriptor executor)
        => executor.SettingsSchema.Fields.Count == 0
            ? "No settings"
            : $"{executor.SettingsSchema.Fields.Count} setting(s)";

    public static string BuildPluginListTestId(PluginCatalogItem plugin)
        => $"plugins-list-item-{NormalizeTestId(plugin.PluginId.Value)}";

    public static string BuildExecutorRowTestId(PluginId pluginId, PluginWorkflowExecutorDescriptor executor)
        => $"plugin-executor-{NormalizeTestId(pluginId.Value)}-{NormalizeTestId(executor.ExecutorId.Value)}";

    public static string BuildPackageInstallTestId(PluginPackageCatalogItem package)
        => $"plugin-package-install-{NormalizeTestId(package.PackageId.Value)}";

    public static string BuildConnectionNameTestId(PluginId pluginId, PluginConnectionDescriptor descriptor)
        => $"plugin-connection-name-{NormalizeTestId(pluginId.Value)}-{NormalizeTestId(descriptor.Key.Value)}";

    public static string BuildConnectionEnabledTestId(PluginId pluginId, PluginConnectionDescriptor descriptor)
        => $"plugin-connection-enabled-{NormalizeTestId(pluginId.Value)}-{NormalizeTestId(descriptor.Key.Value)}";

    public static string BuildConnectionFieldTestId(
        PluginId pluginId,
        PluginConnectionDescriptor descriptor,
        ConfigurationFieldDescriptor field)
        => $"plugin-setting-{NormalizeTestId(pluginId.Value)}-{NormalizeTestId(descriptor.Key.Value)}-{field.Key}";

    public static string BuildConnectionSaveTestId(PluginId pluginId, PluginConnectionDescriptor descriptor)
        => $"plugin-connection-save-{NormalizeTestId(pluginId.Value)}-{NormalizeTestId(descriptor.Key.Value)}";

    public static string BuildOAuthLoginTestId(PluginId pluginId, PluginConnectionDescriptor descriptor)
        => $"plugin-oauth-login-{NormalizeTestId(pluginId.Value)}-{NormalizeTestId(descriptor.Key.Value)}";

    public static RenderFragment RenderPluginIcon(UiIconDescriptor? icon, string cssClass)
        => builder =>
        {
            var resolvedIcon = icon ?? UiIconDescriptor.Default;
            if (resolvedIcon.Kind is UiIconKind.StaticAsset or UiIconKind.PackageAsset && !string.IsNullOrWhiteSpace(resolvedIcon.Value))
            {
                builder.OpenElement(0, "img");
                builder.AddAttribute(1, "src", ResolveIconSource(resolvedIcon));
                builder.AddAttribute(2, "alt", ResolveIconLabel(resolvedIcon));
                builder.AddAttribute(3, "class", $"{cssClass} rounded object-contain");
                builder.CloseElement();
                return;
            }

            builder.OpenElement(4, "span");
            builder.AddAttribute(5, "class", $"cda-material-icon material-symbols-rounded inline-flex items-center justify-center text-[var(--cda-text-muted)] {cssClass}");
            builder.AddAttribute(6, "aria-hidden", "true");
            builder.AddContent(7, string.IsNullOrWhiteSpace(resolvedIcon.Value) ? UiIconDescriptor.Default.Value : resolvedIcon.Value);
            builder.CloseElement();
        };

    public static RenderFragment RenderLogList(IReadOnlyList<PluginLogItem> logs, string testIdPrefix)
        => builder =>
        {
            if (logs.Count == 0)
            {
                builder.OpenElement(0, "p");
                builder.AddAttribute(1, "class", "cda-body-muted");
                builder.AddAttribute(2, "data-testid", $"plugins-logs-{testIdPrefix}-empty");
                builder.AddContent(3, "No log records.");
                builder.CloseElement();
                return;
            }

            builder.OpenElement(4, "div");
            builder.AddAttribute(5, "class", "grid gap-2");
            foreach (var log in logs)
            {
                builder.OpenElement(6, "div");
                builder.AddAttribute(7, "class", "rounded-lg border border-[var(--cda-border-subtle)] bg-[var(--cda-surface-muted)] p-3");
                builder.AddAttribute(8, "data-testid", $"plugins-logs-{testIdPrefix}-row");
                builder.OpenElement(9, "div");
                builder.AddAttribute(10, "class", "mb-2 flex flex-wrap items-center gap-2 text-xs");
                builder.OpenElement(11, "span");
                builder.AddAttribute(12, "class", "font-semibold text-[var(--cda-text)]");
                builder.AddContent(13, log.OperationKind.ToString());
                builder.CloseElement();
                builder.OpenElement(14, "span");
                builder.AddAttribute(15, "class", "text-[var(--cda-text-muted)]");
                builder.AddContent(16, log.Severity.ToString());
                builder.CloseElement();
                builder.OpenElement(17, "span");
                builder.AddAttribute(18, "class", "text-[var(--cda-text-muted)]");
                builder.AddContent(19, log.CreatedAtUtc.ToLocalTime().ToString("g"));
                builder.CloseElement();
                builder.CloseElement();
                builder.OpenElement(20, "p");
                builder.AddAttribute(21, "class", "text-sm text-[var(--cda-text)]");
                builder.AddContent(22, log.Message);
                builder.CloseElement();
                if (log.PluginId is not null || log.PackageId is not null || log.WorkflowExecutorId is not null)
                {
                    builder.OpenElement(23, "p");
                    builder.AddAttribute(24, "class", "mt-2 break-all text-xs text-[var(--cda-text-muted)]");
                    builder.AddContent(25, string.Join(" | ", new[]
                    {
                        log.PluginId?.Value,
                        log.PackageId?.Value,
                        log.WorkflowExecutorId?.Value
                    }.Where(value => !string.IsNullOrWhiteSpace(value))));
                    builder.CloseElement();
                }

                builder.CloseElement();
            }

            builder.CloseElement();
        };

    public static string ResolveIconLabel(UiIconDescriptor? icon)
    {
        var resolvedIcon = icon ?? UiIconDescriptor.Default;
        if (!string.IsNullOrWhiteSpace(resolvedIcon.Label))
        {
            return resolvedIcon.Label;
        }

        return resolvedIcon.Kind switch
        {
            UiIconKind.MaterialIcon => resolvedIcon.Value,
            UiIconKind.PackageAsset => "Package icon",
            UiIconKind.StaticAsset => "Plugin icon",
            _ => "Plugin icon"
        };
    }

    private static string ResolveIconSource(UiIconDescriptor icon)
        => icon.Kind switch
        {
            UiIconKind.PackageAsset when !string.IsNullOrWhiteSpace(icon.PackageId) => $"/api/plugins/packages/{Uri.EscapeDataString(icon.PackageId)}/icon",
            _ => icon.Value
        };

    public static string NormalizeTestId(string value)
    {
        var chars = value
            .Trim()
            .Select(character => char.IsLetterOrDigit(character) ? char.ToLowerInvariant(character) : '-')
            .ToArray();
        var normalized = new string(chars);
        while (normalized.Contains("--", StringComparison.Ordinal))
        {
            normalized = normalized.Replace("--", "-", StringComparison.Ordinal);
        }

        return normalized.Trim('-');
    }
}
