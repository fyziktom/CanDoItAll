using CanDoItAll.AgentFramework.Maf;
using CanDoItAll.AgentFramework.Models;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;

namespace CanDoItAll.Tests.Unit;

public sealed class MafRuntimeResponseAssemblerUsageTests
{
    [Fact]
    public void Provider_failure_diagnostic_uses_bounded_allowlisted_fields_only()
    {
        var secret = $"AccountKey={new string('s', 20_000)}";
        var exception = new MafProviderTransportException(
            Guid.Parse("11111111-1111-1111-1111-111111111111"),
            "unit-model",
            new HttpRequestException(
                secret,
                inner: null,
                System.Net.HttpStatusCode.BadGateway));

        var diagnostic = MafRuntimeResponseAssembler.BuildProviderFailureDiagnostic(exception);

        Assert.DoesNotContain(secret, diagnostic, StringComparison.Ordinal);
        Assert.DoesNotContain("AccountKey", diagnostic, StringComparison.Ordinal);
        Assert.Contains("HttpStatus=502", diagnostic, StringComparison.Ordinal);
        Assert.True(diagnostic.Length < 1024);
    }

    [Fact]
    public void Usage_observations_preserve_provider_request_boundaries_for_long_context_pricing()
    {
        var provider = CreateProvider();
        AgentResponseUpdate[] updates =
        [
            CreateUsageUpdate("response-a", inputTokens: 200_000),
            CreateUsageUpdate("response-b", inputTokens: 200_000)
        ];

        var observations = MafRuntimeResponseAssembler.CreateProviderUsageObservations(
            provider,
            OpenAiModelIds.Gpt56Terra,
            new TestAgentSession(),
            runtimeSessionKey: "runtime-session",
            updates,
            ProviderUsageSourcePhases.AgentRuntime,
            diagnostic: "test");
        var summary = ProviderPricingCalculator.SummarizeUsage(observations, [provider]);

        Assert.Collection(
            observations,
            first =>
            {
                Assert.Equal("response-a", first.ProviderResponseId);
                Assert.Equal(200_000, first.InputTokens);
            },
            second =>
            {
                Assert.Equal("response-b", second.ProviderResponseId);
                Assert.Equal(200_000, second.InputTokens);
            });
        Assert.Equal(400_000, summary.InputTokens);
        Assert.Equal(0.80m, summary.KnownCostUsd);
    }

    private static AgentResponseUpdate CreateUsageUpdate(string responseId, int inputTokens)
    {
        return new AgentResponseUpdate(
            ChatRole.Assistant,
            [
                new UsageContent(new UsageDetails
                {
                    InputTokenCount = inputTokens,
                    OutputTokenCount = 0,
                    TotalTokenCount = inputTokens
                })
            ])
        {
            ResponseId = responseId
        };
    }

    private static ProviderProfile CreateProvider()
    {
        return new ProviderProfile(
            Id: Guid.NewGuid(),
            Name: "OpenAI",
            Kind: ProviderKind.OpenAi,
            BaseUrl: "https://api.openai.com/v1",
            ApiKeyEnvironmentVariable: "OPENAI_API_KEY",
            DefaultModel: OpenAiModelIds.Gpt56Terra,
            Transport: ProviderTransportKind.Responses,
            IsEnabled: true,
            SupportsStreaming: true,
            SupportsTools: true,
            PreferFrameworkManagedChatHistory: false,
            SupportsBackgroundResponses: true,
            ConfigurationJson: "{}",
            Notes: string.Empty,
            HealthStatus: "ok",
            LastCheckedAtUtc: null,
            SuggestedModels: [])
        {
            ModelPrices = ProviderPricingDefaults.CreateDefaultPrices(
                ProviderKind.OpenAi,
                OpenAiModelIds.Gpt56Terra)
        };
    }

    private sealed class TestAgentSession : AgentSession;
}
