using CanDoItAll.AgentFramework.Maf;
using CanDoItAll.AgentFramework.Models;
using Microsoft.Extensions.AI;

namespace CanDoItAll.Tests.Unit;

public sealed class MafProviderRequestCompatibilityEvidenceFactoryTests
{
    [Fact]
    public void Create_records_adjustment_from_the_final_function_tool_manifest()
    {
        var provider = CreateProvider(ProviderTransportKind.ChatCompletions);
        var function = AIFunctionFactory.Create(
            () => "ok",
            "project_structure_read",
            "Read the canonical project structure.");

        var evidence = MafProviderRequestCompatibilityEvidenceFactory.Create(
            provider,
            OpenAiModelIds.Gpt56Luna,
            "gpt-5.6-luna-2026-08-01",
            [function],
            AgentReasoningEffortLevel.Medium);

        Assert.Equal(ProviderRequestCompatibilityEvidence.CurrentSchemaVersion, evidence.SchemaVersion);
        Assert.Equal(provider.Id, evidence.ProviderProfileId);
        Assert.Equal(ProviderTransportKind.ChatCompletions, evidence.Transport);
        Assert.Equal(OpenAiModelIds.Gpt56Luna, evidence.RequestedModel);
        Assert.Equal("gpt-5.6-luna-2026-08-01", evidence.EffectiveModel);
        Assert.Equal(ProviderInvocationFeatures.FunctionTools, evidence.InvocationFeatures);
        Assert.Equal(AgentReasoningEffortLevel.Medium, evidence.RequestedEffort);
        Assert.Equal(AgentReasoningEffortLevel.None, evidence.EffectiveEffort);
        Assert.Equal(ProviderRequestCompatibilityDisposition.Adjusted, evidence.Disposition);
        Assert.Equal(
            ProviderModelParameterAdjustment.ReasoningDisabledForFunctionTools,
            evidence.Adjustment);
        var progressMessage = MafProviderRequestCompatibilityEvidenceFactory
            .CreateAdjustmentProgressMessage(evidence);
        Assert.Contains("before provider dispatch", progressMessage, StringComparison.Ordinal);
        Assert.Contains("Medium -> None", progressMessage, StringComparison.Ordinal);
        Assert.Contains(
            nameof(ProviderModelParameterAdjustment.ReasoningDisabledForFunctionTools),
            progressMessage,
            StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(ProviderTransportKind.Responses, true)]
    [InlineData(ProviderTransportKind.ChatCompletions, false)]
    public void Create_preserves_unaffected_request_shapes(
        ProviderTransportKind transport,
        bool includeFunctionTool)
    {
        var provider = CreateProvider(transport);
        IReadOnlyList<AITool> tools = includeFunctionTool
            ?
            [
                AIFunctionFactory.Create(
                    () => "ok",
                    "project_structure_read",
                    "Read the canonical project structure.")
            ]
            : [];

        var evidence = MafProviderRequestCompatibilityEvidenceFactory.Create(
            provider,
            OpenAiModelIds.Gpt56Luna,
            OpenAiModelIds.Gpt56Luna,
            tools,
            AgentReasoningEffortLevel.Medium);

        Assert.Equal(AgentReasoningEffortLevel.Medium, evidence.EffectiveEffort);
        Assert.Equal(ProviderRequestCompatibilityDisposition.Preserved, evidence.Disposition);
        Assert.Equal(ProviderModelParameterAdjustment.None, evidence.Adjustment);
        Assert.Null(
            MafProviderRequestCompatibilityEvidenceFactory.CreateAdjustmentProgressMessage(
                evidence));
    }

    private static ProviderProfile CreateProvider(ProviderTransportKind transport)
    {
        return new ProviderProfile(
            Id: Guid.NewGuid(),
            Name: "OpenAI compatibility evidence test",
            Kind: ProviderKind.OpenAi,
            BaseUrl: "https://api.openai.com/v1",
            ApiKeyEnvironmentVariable: "TEST_OPENAI_API_KEY",
            DefaultModel: OpenAiModelIds.Gpt56Luna,
            Transport: transport,
            IsEnabled: true,
            SupportsStreaming: true,
            SupportsTools: true,
            PreferFrameworkManagedChatHistory: true,
            SupportsBackgroundResponses: false,
            ConfigurationJson: "{}",
            Notes: string.Empty,
            HealthStatus: "ok",
            LastCheckedAtUtc: null,
            SuggestedModels: []);
    }
}
