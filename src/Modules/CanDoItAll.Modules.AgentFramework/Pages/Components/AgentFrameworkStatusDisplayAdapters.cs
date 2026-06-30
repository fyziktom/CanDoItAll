using CanDoItAll.AgentFramework.Models;

namespace CanDoItAll.Modules.AgentFramework.Pages.Components;

public sealed record AgentFrameworkStatusBadge(string Text, string Tone);

public static class CapabilityProofDisplayAdapter
{
    public static AgentFrameworkStatusBadge BuildBadge(CapabilityProofStatus status)
    {
        return new AgentFrameworkStatusBadge(
            ResolveLabel(status),
            ResolveTone(status));
    }

    public static string ResolveLabel(CapabilityProofStatus status)
    {
        return status switch
        {
            CapabilityProofStatus.Verified => "Verified",
            CapabilityProofStatus.PendingReview => "Pending review",
            CapabilityProofStatus.Failed => "Failed",
            CapabilityProofStatus.NotRun => "Not run",
            _ => status.ToString()
        };
    }

    public static string ResolveTone(CapabilityProofStatus status)
    {
        return status switch
        {
            CapabilityProofStatus.Verified => "success",
            CapabilityProofStatus.PendingReview => "warning",
            CapabilityProofStatus.Failed => "danger",
            CapabilityProofStatus.NotRun => "neutral",
            _ => "neutral"
        };
    }
}

public static class ProviderProfileDisplayAdapter
{
    public static AgentFrameworkStatusBadge BuildEnabledBadge(ProviderProfile provider)
    {
        ArgumentNullException.ThrowIfNull(provider);

        return provider.IsEnabled
            ? new AgentFrameworkStatusBadge("Enabled", "success")
            : new AgentFrameworkStatusBadge("Disabled", "warning");
    }

    public static string BuildStatusText(ProviderProfile provider)
    {
        ArgumentNullException.ThrowIfNull(provider);

        var checkedAt = provider.LastCheckedAtUtc.HasValue
            ? $" Last checked {provider.LastCheckedAtUtc.Value.LocalDateTime:g}."
            : " Health has not been checked.";
        return $"{provider.Kind} / {provider.Transport} / {NormalizeHealthStatus(provider.HealthStatus)}.{checkedAt}";
    }

    public static string BuildTreeTooltip(ProviderProfile provider)
    {
        ArgumentNullException.ThrowIfNull(provider);

        var enabled = BuildEnabledBadge(provider).Text;
        return $"{provider.Name}. {provider.Kind}, {provider.Transport}, {provider.DefaultModel}. {enabled}. {NormalizeHealthStatus(provider.HealthStatus)}.";
    }

    private static string NormalizeHealthStatus(string value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? "Not checked"
            : value.Trim();
    }
}
