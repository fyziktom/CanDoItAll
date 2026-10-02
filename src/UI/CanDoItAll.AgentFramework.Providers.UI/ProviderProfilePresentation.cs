using System.Globalization;
using CanDoItAll.AgentFramework.Models;

namespace CanDoItAll.AgentFramework.Providers.UI;

public static class ProviderProfilePresentation {
    public static (string Text, string Tone) BuildEnabledBadge(ProviderProfile provider) =>
        provider.IsEnabled ? ("Enabled", "success") : ("Disabled", "warning");

    public static string BuildStatusText(ProviderProfile provider) {
        var checkedAt = provider.LastCheckedAtUtc is { } time
            ? $" Last checked {time.UtcDateTime.ToString("yyyy-MM-dd HH:mm:ss 'UTC'", CultureInfo.InvariantCulture)}."
            : " Health has not been checked.";
        return $"{provider.Kind} / {provider.Transport} / {NormalizeHealthStatus(provider.HealthStatus)}.{checkedAt}";
    }

    public static string BuildTreeTooltip(ProviderProfile provider) =>
        $"{provider.Name}. {provider.Kind}, {provider.Transport}, {provider.DefaultModel}. {BuildEnabledBadge(provider).Text}. {NormalizeHealthStatus(provider.HealthStatus)}.";
    private static string NormalizeHealthStatus(string value) => string.IsNullOrWhiteSpace(value) ? "Not checked" : value.Trim();
}
