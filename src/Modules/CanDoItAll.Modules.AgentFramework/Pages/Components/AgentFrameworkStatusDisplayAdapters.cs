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

public static class ProviderProfileDisplayAdapter {
    public static AgentFrameworkStatusBadge BuildEnabledBadge(ProviderProfile provider) {
        var badge = CanDoItAll.AgentFramework.Providers.UI.ProviderProfilePresentation.BuildEnabledBadge(provider);
        return new(badge.Text, badge.Tone);
    }
    public static string BuildStatusText(ProviderProfile provider) => CanDoItAll.AgentFramework.Providers.UI.ProviderProfilePresentation.BuildStatusText(provider);
    public static string BuildTreeTooltip(ProviderProfile provider) => CanDoItAll.AgentFramework.Providers.UI.ProviderProfilePresentation.BuildTreeTooltip(provider);
}
