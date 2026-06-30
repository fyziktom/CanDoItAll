using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Modules.AgentFramework.Pages.Components;

namespace CanDoItAll.Tests.Components;

public sealed class AgentFrameworkStatusDisplayAdapterTests
{
    [Fact]
    public void CapabilityProofDisplayAdapter_maps_canonical_statuses()
    {
        Assert.Equal(new AgentFrameworkStatusBadge("Verified", "success"), CapabilityProofDisplayAdapter.BuildBadge(CapabilityProofStatus.Verified));
        Assert.Equal(new AgentFrameworkStatusBadge("Pending review", "warning"), CapabilityProofDisplayAdapter.BuildBadge(CapabilityProofStatus.PendingReview));
        Assert.Equal(new AgentFrameworkStatusBadge("Failed", "danger"), CapabilityProofDisplayAdapter.BuildBadge(CapabilityProofStatus.Failed));
        Assert.Equal(new AgentFrameworkStatusBadge("Not run", "neutral"), CapabilityProofDisplayAdapter.BuildBadge(CapabilityProofStatus.NotRun));
    }

    [Fact]
    public void ProviderProfileDisplayAdapter_surfaces_enabled_and_health_state()
    {
        var provider = new ProviderProfile(
            Id: Guid.NewGuid(),
            Name: "Provider A",
            Kind: ProviderKind.OpenAi,
            BaseUrl: "https://api.example.test/v1",
            ApiKeyEnvironmentVariable: "TEST_API_KEY",
            DefaultModel: "model-a",
            Transport: ProviderTransportKind.Responses,
            IsEnabled: false,
            SupportsStreaming: true,
            SupportsTools: true,
            PreferFrameworkManagedChatHistory: false,
            SupportsBackgroundResponses: true,
            ConfigurationJson: "{}",
            Notes: string.Empty,
            HealthStatus: "Not checked",
            LastCheckedAtUtc: null,
            SuggestedModels: []);

        var badge = ProviderProfileDisplayAdapter.BuildEnabledBadge(provider);
        var status = ProviderProfileDisplayAdapter.BuildStatusText(provider);

        Assert.Equal(new AgentFrameworkStatusBadge("Disabled", "warning"), badge);
        Assert.Contains("OpenAi / Responses / Not checked", status, StringComparison.Ordinal);
        Assert.Contains("Health has not been checked", status, StringComparison.Ordinal);
    }
}
