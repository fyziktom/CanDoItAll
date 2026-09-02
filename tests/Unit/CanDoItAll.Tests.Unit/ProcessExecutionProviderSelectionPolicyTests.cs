using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Core.Execution;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Modules.Processes;

namespace CanDoItAll.Tests.Unit.Processes;

/// <summary>
/// Direct unit coverage for <see cref="ProcessExecutionProviderSelectionPolicy"/> (SB13), instantiating it
/// directly rather than through <c>AgentFrameworkWorkspaceExecutionService</c> (which now only holds the generic
/// iteration; the governed-process string/type logic moved here verbatim). Ported from the deleted
/// <c>AgentFrameworkWorkspaceExecutionService.ShouldOverrideProviderForGovernedProcessStep</c> /
/// <c>OrderGovernedProcessProviderOverrideCandidates</c> coverage.
/// </summary>
public sealed class ProcessExecutionProviderSelectionPolicyTests
{
    [Fact]
    public void ShouldOverrideConfiguredProvider_is_true_for_a_governed_process_step_whose_provider_lacks_structured_output()
    {
        var policy = new ProcessExecutionProviderSelectionPolicy(new ProviderProfileService());
        var configuredProvider = CreateProvider(ProviderKind.ComfyUi, ProviderTransportKind.ChatCompletions);
        var request = new AgentExecutionProviderSelectionRequest(
            "process-step",
            IsGovernedProcessStep: true,
            typeof(ProcessStepOutcomeResult),
            AgentStructuredOutputContracts.ProcessStepOutcomeResultKey,
            configuredProvider);

        Assert.True(policy.ShouldOverrideConfiguredProvider(request));
    }

    [Fact]
    public void ShouldOverrideConfiguredProvider_is_false_when_the_configured_provider_already_supports_structured_output()
    {
        var policy = new ProcessExecutionProviderSelectionPolicy(new ProviderProfileService());
        var configuredProvider = CreateProvider(ProviderKind.OpenAi, ProviderTransportKind.Responses);
        var request = new AgentExecutionProviderSelectionRequest(
            "process-step",
            IsGovernedProcessStep: true,
            typeof(ProcessStepOutcomeResult),
            AgentStructuredOutputContracts.ProcessStepOutcomeResultKey,
            configuredProvider);

        Assert.False(policy.ShouldOverrideConfiguredProvider(request));
    }

    [Fact]
    public void ShouldOverrideConfiguredProvider_is_false_when_the_step_is_not_governed()
    {
        var policy = new ProcessExecutionProviderSelectionPolicy(new ProviderProfileService());
        var configuredProvider = CreateProvider(ProviderKind.Ollama, ProviderTransportKind.ChatCompletions);
        var request = new AgentExecutionProviderSelectionRequest(
            "manual",
            IsGovernedProcessStep: false,
            typeof(ProcessStepOutcomeResult),
            AgentStructuredOutputContracts.ProcessStepOutcomeResultKey,
            configuredProvider);

        Assert.False(policy.ShouldOverrideConfiguredProvider(request));
    }

    [Fact]
    public void ShouldOverrideConfiguredProvider_is_false_for_a_different_structured_output_type()
    {
        var policy = new ProcessExecutionProviderSelectionPolicy(new ProviderProfileService());
        var configuredProvider = CreateProvider(ProviderKind.Ollama, ProviderTransportKind.ChatCompletions);
        var request = new AgentExecutionProviderSelectionRequest(
            "process-step",
            IsGovernedProcessStep: true,
            typeof(CodeReviewResult),
            AgentStructuredOutputContracts.CodeReviewResultKey,
            configuredProvider);

        Assert.False(policy.ShouldOverrideConfiguredProvider(request));
    }

    [Fact]
    public void SelectOverrideCandidates_prefers_responses_provider_matching_the_configured_family()
    {
        var policy = new ProcessExecutionProviderSelectionPolicy(new ProviderProfileService());
        var responsesProvider = CreateProvider(
            ProviderKind.OpenAi,
            ProviderTransportKind.Responses,
            preferFrameworkManagedHistory: false) with
        {
            Id = Guid.Parse("c1c103db-707e-3f52-8809-8d804fc171d1"),
            Name = ManagedSeedProviderFallbacks.OpenAiDefaultProviderName,
            ConfigurationJson = "{\"history\":\"service-managed\"}"
        };
        var chatCompletionsProvider = CreateProvider(
            ProviderKind.OpenAi,
            ProviderTransportKind.ChatCompletions,
            preferFrameworkManagedHistory: true) with
        {
            Id = Guid.Parse("036b360a-e3f4-8350-97ca-f88de60ba2bb"),
            Name = ManagedSeedProviderFallbacks.OpenAiChatCompletionsProviderName,
            ConfigurationJson = "{\"history\":\"framework-managed\",\"timeoutSeconds\":600}"
        };
        var request = new AgentExecutionProviderSelectionRequest(
            "process-step",
            IsGovernedProcessStep: true,
            typeof(ProcessStepOutcomeResult),
            AgentStructuredOutputContracts.ProcessStepOutcomeResultKey,
            responsesProvider);

        var orderedProviders = policy.SelectOverrideCandidates(
            request,
            [responsesProvider, chatCompletionsProvider]);

        Assert.Equal(responsesProvider.Id, orderedProviders[0].Id);
    }

    [Fact]
    public void SelectOverrideCandidates_excludes_disabled_and_scenario_harness_providers()
    {
        var policy = new ProcessExecutionProviderSelectionPolicy(new ProviderProfileService());
        var configuredProvider = CreateProvider(ProviderKind.Ollama, ProviderTransportKind.ChatCompletions);
        var disabledProvider = CreateProvider(ProviderKind.OpenAi, ProviderTransportKind.Responses) with
        {
            Id = Guid.NewGuid(),
            IsEnabled = false
        };
        var scenarioHarnessProvider = CreateProvider(ProviderKind.OpenAi, ProviderTransportKind.Responses) with
        {
            Id = Guid.NewGuid(),
            Name = "Scenario Harness Provider"
        };
        var eligibleProvider = CreateProvider(ProviderKind.OpenAi, ProviderTransportKind.Responses) with
        {
            Id = Guid.NewGuid()
        };
        var request = new AgentExecutionProviderSelectionRequest(
            "process-step",
            IsGovernedProcessStep: true,
            typeof(ProcessStepOutcomeResult),
            AgentStructuredOutputContracts.ProcessStepOutcomeResultKey,
            configuredProvider);

        var candidates = policy.SelectOverrideCandidates(
            request,
            [disabledProvider, scenarioHarnessProvider, eligibleProvider]);

        var candidate = Assert.Single(candidates);
        Assert.Equal(eligibleProvider.Id, candidate.Id);
    }

    private static ProviderProfile CreateProvider(
        ProviderKind kind,
        ProviderTransportKind transport,
        bool preferFrameworkManagedHistory = false)
    {
        return new ProviderProfile(
            Guid.NewGuid(),
            kind.ToString(),
            kind,
            "https://example.invalid/v1",
            "PROVIDER_API_KEY",
            "default-model",
            transport,
            IsEnabled: true,
            SupportsStreaming: true,
            SupportsTools: true,
            preferFrameworkManagedHistory,
            SupportsBackgroundResponses: true,
            ConfigurationJson: "{}",
            Notes: string.Empty,
            HealthStatus: string.Empty,
            LastCheckedAtUtc: null,
            SuggestedModels: []);
    }
}
