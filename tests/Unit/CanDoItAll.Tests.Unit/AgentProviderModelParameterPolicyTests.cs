using System.Text.Json;
using CanDoItAll.AgentFramework.Models;

namespace CanDoItAll.Tests.Unit;

public sealed class AgentProviderModelParameterPolicyTests
{
    [Theory]
    [InlineData("gpt-5")]
    [InlineData("gpt-5.4-mini")]
    [InlineData("gpt-5.2")]
    [InlineData("o1-mini")]
    [InlineData("o3")]
    [InlineData("o4-mini")]
    public void Openai_reasoning_models_omit_temperature(string model)
    {
        var shouldOmit = AgentProviderModelParameterPolicy.ShouldOmitTemperature(ProviderKind.OpenAi, model);

        Assert.True(shouldOmit);
    }

    [Theory]
    [InlineData("gpt-4o-mini")]
    [InlineData("gpt-4.1")]
    [InlineData("qwen3.5:9b")]
    public void Non_reasoning_models_keep_temperature_unless_forced(string model)
    {
        var shouldOmit = AgentProviderModelParameterPolicy.ShouldOmitTemperature(ProviderKind.OpenAi, model);

        Assert.False(shouldOmit);
    }

    [Fact]
    public void Forced_omission_applies_to_non_openai_providers()
    {
        var shouldOmit = AgentProviderModelParameterPolicy.ShouldOmitTemperature(
            ProviderKind.Ollama,
            "qwen3.5:9b",
            forceOmitTemperature: true);

        Assert.True(shouldOmit);
    }

    [Fact]
    public void Openai_reasoning_model_uses_agent_reasoning_effort_over_provider()
    {
        var effort = AgentProviderModelParameterPolicy.ResolveReasoningEffort(
            ProviderKind.OpenAi,
            ProviderTransportKind.Responses,
            "gpt-5.4",
            "{\"reasoningEffort\":\"low\"}",
            "{\"reasoningEffort\":\"medium\"}");

        Assert.Equal(AgentReasoningEffortLevel.Medium, effort);
    }

    [Fact]
    public void Openai_reasoning_model_reads_nested_provider_reasoning_effort()
    {
        var effort = AgentProviderModelParameterPolicy.ResolveReasoningEffort(
            ProviderKind.OpenAi,
            ProviderTransportKind.Responses,
            "gpt-5.4",
            "{\"modelParameters\":{\"reasoningEffort\":\"xhigh\"}}",
            string.Empty);

        Assert.Equal(AgentReasoningEffortLevel.ExtraHigh, effort);
    }

    [Fact]
    public void Openai_non_reasoning_model_rejects_agent_override()
    {
        var exception = Assert.Throws<InvalidOperationException>(() =>
            AgentProviderModelParameterPolicy.ResolveReasoningEffort(
                ProviderKind.OpenAi,
                ProviderTransportKind.Responses,
                "gpt-4.1",
                "{\"reasoningEffort\":\"medium\"}",
                "{\"reasoningEffort\":\"high\"}"));

        Assert.Contains("agent thinking-effort override", exception.Message, StringComparison.Ordinal);
        Assert.Contains("gpt-4.1", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Openai_non_reasoning_model_ignores_provider_default()
    {
        var effort = AgentProviderModelParameterPolicy.ResolveReasoningEffort(
            ProviderKind.OpenAi,
            ProviderTransportKind.Responses,
            "gpt-4.1",
            "{\"reasoningEffort\":\"medium\"}",
            string.Empty);

        Assert.Null(effort);
    }

    [Fact]
    public void Invalid_reasoning_effort_fails_explicitly()
    {
        var exception = Assert.Throws<InvalidOperationException>(() =>
            AgentProviderModelParameterPolicy.ResolveReasoningEffort(
                ProviderKind.OpenAi,
                ProviderTransportKind.Responses,
                "gpt-5.4",
                "{\"reasoningEffort\":\"aggressive\"}",
                string.Empty));

        Assert.Contains("Unsupported thinking effort", exception.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(ProviderTransportKind.Responses)]
    [InlineData(ProviderTransportKind.ChatCompletions)]
    public void Openai_supported_transports_apply_reasoning_effort(ProviderTransportKind transport)
    {
        var effort = AgentProviderModelParameterPolicy.ResolveReasoningEffort(
            ProviderKind.OpenAi,
            transport,
            OpenAiModelIds.Gpt56Sol,
            "{\"reasoningEffort\":\"medium\"}",
            string.Empty);

        Assert.True(AgentProviderModelParameterPolicy.CanApplyReasoningEffort(
            ProviderKind.OpenAi,
            transport,
            OpenAiModelIds.Gpt56Sol));
        Assert.Equal(AgentReasoningEffortLevel.Medium, effort);
    }

    [Theory]
    [InlineData("gpt-5.4", AgentThinkingEffortSupportStatus.Supported)]
    [InlineData("gpt-4.1", AgentThinkingEffortSupportStatus.Unsupported)]
    [InlineData("custom-deployment-west", AgentThinkingEffortSupportStatus.Unknown)]
    public void Openai_model_capability_distinguishes_supported_unsupported_and_unknown(
        string model,
        AgentThinkingEffortSupportStatus expectedStatus)
    {
        var capability = AgentThinkingEffortPolicy.ResolveDefinedCapability(
            ProviderKind.OpenAi,
            ProviderTransportKind.Responses,
            model);

        Assert.Equal(expectedStatus, capability.Status);
        Assert.Equal(AgentThinkingEffortCapabilitySource.Defined, capability.Source);
        Assert.Equal(
            expectedStatus == AgentThinkingEffortSupportStatus.Supported,
            capability.AllowedEfforts.Count > 0);
    }

    [Fact]
    public void Openai_model_matrix_exposes_only_documented_effort_levels()
    {
        Assert.Equal(
            [
                AgentReasoningEffortLevel.None,
                AgentReasoningEffortLevel.Low,
                AgentReasoningEffortLevel.Medium,
                AgentReasoningEffortLevel.High,
                AgentReasoningEffortLevel.ExtraHigh,
                AgentReasoningEffortLevel.Max
            ],
            ResolveOpenAiEfforts(OpenAiModelIds.Gpt56Sol));
        Assert.Equal(
            [
                AgentReasoningEffortLevel.None,
                AgentReasoningEffortLevel.Low,
                AgentReasoningEffortLevel.Medium,
                AgentReasoningEffortLevel.High,
                AgentReasoningEffortLevel.ExtraHigh
            ],
            ResolveOpenAiEfforts("gpt-5.4-mini"));
        Assert.Equal(
            [
                AgentReasoningEffortLevel.None,
                AgentReasoningEffortLevel.Low,
                AgentReasoningEffortLevel.Medium,
                AgentReasoningEffortLevel.High
            ],
            ResolveOpenAiEfforts("gpt-5.1"));
        Assert.Equal(
            [
                AgentReasoningEffortLevel.Minimal,
                AgentReasoningEffortLevel.Low,
                AgentReasoningEffortLevel.Medium,
                AgentReasoningEffortLevel.High
            ],
            ResolveOpenAiEfforts("gpt-5-2025-08-07"));
        Assert.Equal(
            [
                AgentReasoningEffortLevel.Low,
                AgentReasoningEffortLevel.Medium,
                AgentReasoningEffortLevel.High,
                AgentReasoningEffortLevel.ExtraHigh
            ],
            ResolveOpenAiEfforts("gpt-5.3-codex"));
    }

    [Fact]
    public void Openai_registry_rows_are_unique_complete_and_internally_valid()
    {
        var definitions = OpenAiThinkingEffortModelRegistry.All;
        Assert.Equal(
            definitions.Count,
            definitions.Select(definition => definition.Model).Distinct(StringComparer.OrdinalIgnoreCase).Count());
        Assert.All(definitions, definition =>
        {
            Assert.NotEmpty(definition.AllowedTransports);
            if (definition.Status == AgentThinkingEffortSupportStatus.Supported)
            {
                Assert.NotEmpty(definition.AllowedEfforts);
                return;
            }

            Assert.Equal(AgentThinkingEffortSupportStatus.Unsupported, definition.Status);
            Assert.Empty(definition.AllowedEfforts);
        });

        AssertRegistryGroup(
            [
                OpenAiModelIds.Gpt56Sol,
                OpenAiModelIds.Gpt56Terra,
                OpenAiModelIds.Gpt56Luna,
                OpenAiModelIds.Gpt56
            ],
            [
                AgentReasoningEffortLevel.None,
                AgentReasoningEffortLevel.Low,
                AgentReasoningEffortLevel.Medium,
                AgentReasoningEffortLevel.High,
                AgentReasoningEffortLevel.ExtraHigh,
                AgentReasoningEffortLevel.Max
            ]);
        AssertRegistryGroup(
            ["gpt-5.5", "gpt-5.4-mini", "gpt-5.4-nano", "gpt-5.4", "gpt-5.2"],
            [
                AgentReasoningEffortLevel.None,
                AgentReasoningEffortLevel.Low,
                AgentReasoningEffortLevel.Medium,
                AgentReasoningEffortLevel.High,
                AgentReasoningEffortLevel.ExtraHigh
            ]);
        AssertRegistryGroup(
            ["gpt-5.1"],
            [
                AgentReasoningEffortLevel.None,
                AgentReasoningEffortLevel.Low,
                AgentReasoningEffortLevel.Medium,
                AgentReasoningEffortLevel.High
            ]);
        AssertRegistryGroup(
            ["gpt-5-mini", "gpt-5-nano", "gpt-5"],
            [
                AgentReasoningEffortLevel.Minimal,
                AgentReasoningEffortLevel.Low,
                AgentReasoningEffortLevel.Medium,
                AgentReasoningEffortLevel.High
            ]);
        AssertRegistryGroup(
            ["gpt-5.3-codex"],
            [
                AgentReasoningEffortLevel.Low,
                AgentReasoningEffortLevel.Medium,
                AgentReasoningEffortLevel.High,
                AgentReasoningEffortLevel.ExtraHigh
            ]);
        AssertRegistryGroup(
            ["o1-mini", "o1-preview", "o1", "o3-mini", "o3", "o4-mini"],
            [
                AgentReasoningEffortLevel.Low,
                AgentReasoningEffortLevel.Medium,
                AgentReasoningEffortLevel.High
            ]);
        AssertRegistryGroup(
            ["gpt-5.5-pro", "gpt-5.4-pro", "gpt-5.2-pro"],
            [
                AgentReasoningEffortLevel.Medium,
                AgentReasoningEffortLevel.High,
                AgentReasoningEffortLevel.ExtraHigh
            ],
            responsesOnly: true);
        AssertRegistryGroup(
            ["gpt-5-pro"],
            [AgentReasoningEffortLevel.High],
            responsesOnly: true);

        var unsupportedModels = definitions
            .Where(definition => definition.Status == AgentThinkingEffortSupportStatus.Unsupported)
            .Select(definition => definition.Model)
            .ToList();
        Assert.Equal(
            ["gpt-4.1", "gpt-4.1-mini", "gpt-4.1-nano", "gpt-4o", "gpt-4o-mini"],
            unsupportedModels);
    }

    [Fact]
    public void Openai_pro_model_requires_responses_transport_and_uses_its_narrow_level_set()
    {
        var responsesCapability = AgentThinkingEffortPolicy.ResolveDefinedCapability(
            ProviderKind.OpenAi,
            ProviderTransportKind.Responses,
            "gpt-5.4-pro");
        var chatCapability = AgentThinkingEffortPolicy.ResolveDefinedCapability(
            ProviderKind.OpenAi,
            ProviderTransportKind.ChatCompletions,
            "gpt-5.4-pro");

        Assert.Equal(AgentThinkingEffortSupportStatus.Supported, responsesCapability.Status);
        Assert.Equal(
            [
                AgentReasoningEffortLevel.Medium,
                AgentReasoningEffortLevel.High,
                AgentReasoningEffortLevel.ExtraHigh
            ],
            responsesCapability.AllowedEfforts);
        Assert.Equal(AgentThinkingEffortSupportStatus.Unsupported, chatCapability.Status);
        Assert.Contains("Responses transport", chatCapability.Summary, StringComparison.Ordinal);
    }

    [Fact]
    public void Openai_unknown_variant_is_not_misclassified_by_a_broad_prefix()
    {
        var capability = AgentThinkingEffortPolicy.ResolveDefinedCapability(
            ProviderKind.OpenAi,
            ProviderTransportKind.Responses,
            "gpt-5.4-custom");

        Assert.Equal(AgentThinkingEffortSupportStatus.Unknown, capability.Status);
        Assert.Empty(capability.AllowedEfforts);
    }

    [Theory]
    [InlineData("gpt-5.4-2026-03-05", AgentThinkingEffortSupportStatus.Supported)]
    [InlineData("gpt-4.1-2025-04-14", AgentThinkingEffortSupportStatus.Unsupported)]
    [InlineData("gpt-5.4-2026", AgentThinkingEffortSupportStatus.Unknown)]
    [InlineData("gpt-5.4-2026----", AgentThinkingEffortSupportStatus.Unknown)]
    [InlineData("gpt-5.4-custom", AgentThinkingEffortSupportStatus.Unknown)]
    public void Openai_registry_matches_only_exact_models_or_strict_date_snapshots(
        string model,
        AgentThinkingEffortSupportStatus expectedStatus)
    {
        var capability = AgentThinkingEffortPolicy.ResolveDefinedCapability(
            ProviderKind.OpenAi,
            ProviderTransportKind.Responses,
            model);

        Assert.Equal(expectedStatus, capability.Status);
    }

    [Fact]
    public void Azure_deployment_requires_exact_provider_scoped_capability_metadata()
    {
        const string deployment = "gpt-5.4";
        var withoutMetadata = CreateProvider(ProviderKind.AzureOpenAi, deployment);
        var withMetadata = withoutMetadata with
        {
            ModelThinkingEffortCapabilities =
            [
                new ProviderModelThinkingEffortCapability(
                    deployment,
                    AgentThinkingEffortSupportStatus.Supported,
                    AgentThinkingEffortCapabilitySource.Defined,
                    [AgentReasoningEffortLevel.Low, AgentReasoningEffortLevel.High],
                    Summary: "Azure deployment capability supplied by provider metadata.")
            ]
        };

        var unknownCapability = AgentThinkingEffortPolicy.ResolveCapability(
            withoutMetadata,
            deployment);
        var metadataCapability = AgentThinkingEffortPolicy.ResolveCapability(
            withMetadata,
            deployment);
        var otherDeploymentCapability = AgentThinkingEffortPolicy.ResolveCapability(
            withMetadata,
            "other-deployment");

        Assert.Equal(AgentThinkingEffortSupportStatus.Unknown, unknownCapability.Status);
        Assert.Contains("provider-scoped", unknownCapability.Summary, StringComparison.Ordinal);
        Assert.Equal(AgentThinkingEffortSupportStatus.Supported, metadataCapability.Status);
        Assert.Equal(
            [AgentReasoningEffortLevel.Low, AgentReasoningEffortLevel.High],
            metadataCapability.AllowedEfforts);
        Assert.Equal(AgentThinkingEffortSupportStatus.Unknown, otherDeploymentCapability.Status);
    }

    [Fact]
    public void Effort_enum_preserves_existing_values_and_capability_normalization_uses_semantic_order()
    {
        Assert.Equal(0, (int)AgentReasoningEffortLevel.None);
        Assert.Equal(1, (int)AgentReasoningEffortLevel.Low);
        Assert.Equal(2, (int)AgentReasoningEffortLevel.Medium);
        Assert.Equal(3, (int)AgentReasoningEffortLevel.High);
        Assert.Equal(4, (int)AgentReasoningEffortLevel.ExtraHigh);
        Assert.Equal(5, (int)AgentReasoningEffortLevel.Max);
        Assert.Equal(6, (int)AgentReasoningEffortLevel.Minimal);

        var capability = AgentThinkingEffortPolicy.NormalizeCapability(
            new ProviderModelThinkingEffortCapability(
                "custom",
                AgentThinkingEffortSupportStatus.Supported,
                AgentThinkingEffortCapabilitySource.Defined,
                [
                    AgentReasoningEffortLevel.High,
                    AgentReasoningEffortLevel.Minimal,
                    AgentReasoningEffortLevel.None,
                    AgentReasoningEffortLevel.Low
                ]));

        Assert.Equal(
            [
                AgentReasoningEffortLevel.None,
                AgentReasoningEffortLevel.Minimal,
                AgentReasoningEffortLevel.Low,
                AgentReasoningEffortLevel.High
            ],
            capability.AllowedEfforts);
    }

    [Fact]
    public void Max_reasoning_effort_is_parsed_and_formatted_for_openai_models()
    {
        var capability = AgentThinkingEffortPolicy.ResolveDefinedCapability(
            ProviderKind.OpenAi,
            ProviderTransportKind.Responses,
            OpenAiModelIds.Gpt56Sol);
        var effort = AgentProviderModelParameterPolicy.ResolveReasoningEffort(
            ProviderKind.OpenAi,
            ProviderTransportKind.Responses,
            OpenAiModelIds.Gpt56Sol,
            "{\"reasoningEffort\":\"max\"}",
            string.Empty);
        var parsedEffort = Assert.IsType<AgentReasoningEffortLevel>(effort);

        Assert.Equal(AgentReasoningEffortLevel.Max, parsedEffort);
        Assert.Contains(AgentReasoningEffortLevel.Max, capability.AllowedEfforts);
        Assert.Equal("max", AgentProviderModelParameterPolicy.FormatReasoningEffort(parsedEffort));
    }

    [Theory]
    [InlineData("gpt-5.4")]
    [InlineData("gpt-5.5")]
    [InlineData("o3")]
    public void Max_reasoning_effort_fails_before_calling_unsupported_openai_models(string model)
    {
        var exception = Assert.Throws<InvalidOperationException>(() =>
            AgentProviderModelParameterPolicy.ResolveReasoningEffort(
                ProviderKind.OpenAi,
                ProviderTransportKind.Responses,
                model,
                "{\"reasoningEffort\":\"max\"}",
                string.Empty));

        Assert.Contains("not supported", exception.Message, StringComparison.Ordinal);
        Assert.Contains("Allowed values", exception.Message, StringComparison.Ordinal);
        Assert.Contains(model, exception.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("qwen3.5:2b", false)]
    [InlineData("gemma4:12b", false)]
    [InlineData("deepseek-r1:14b", false)]
    [InlineData("gpt-oss:20b", true)]
    [InlineData("gptoss32k:latest", true)]
    public void Ollama_defined_capability_exposes_only_the_models_native_control_shape(
        string model,
        bool usesLevels)
    {
        var capability = AgentThinkingEffortPolicy.ResolveDefinedCapability(
            ProviderKind.Ollama,
            ProviderTransportKind.ChatCompletions,
            model);
        IReadOnlyList<AgentReasoningEffortLevel> expectedEfforts = usesLevels
            ?
            [
                AgentReasoningEffortLevel.Low,
                AgentReasoningEffortLevel.Medium,
                AgentReasoningEffortLevel.High
            ]
            :
            [
                AgentReasoningEffortLevel.None,
                AgentReasoningEffortLevel.Medium
            ];

        Assert.Equal(AgentThinkingEffortSupportStatus.Supported, capability.Status);
        Assert.Equal(
            usesLevels
                ? AgentThinkingEffortControlMode.EffortLevels
                : AgentThinkingEffortControlMode.BooleanToggle,
            capability.ControlMode);
        Assert.Equal(expectedEfforts, capability.AllowedEfforts);
    }

    [Theory]
    [InlineData("custom-thinking:latest", "custom", AgentThinkingEffortSupportStatus.Supported, true)]
    [InlineData("gpt-oss:20b", "gptoss", AgentThinkingEffortSupportStatus.Unsupported, false)]
    [InlineData("custom-unknown", "custom", AgentThinkingEffortSupportStatus.Unknown, false)]
    public void Ollama_discovered_capability_overrides_defined_model_family_fallback(
        string model,
        string family,
        AgentThinkingEffortSupportStatus discoveredStatus,
        bool expectsAllowedEfforts)
    {
        var discoveredCapability = AgentThinkingEffortPolicy.CreateDiscoveredCapability(
            model,
            family,
            discoveredStatus);
        var provider = CreateProvider(ProviderKind.Ollama, model) with
        {
            ModelThinkingEffortCapabilities = [discoveredCapability]
        };

        var capability = AgentThinkingEffortPolicy.ResolveCapability(provider, model);

        Assert.Equal(discoveredStatus, capability.Status);
        Assert.Equal(AgentThinkingEffortCapabilitySource.Discovered, capability.Source);
        Assert.Equal(family, capability.ModelFamily);
        Assert.Equal(expectsAllowedEfforts, capability.AllowedEfforts.Count > 0);
        if (discoveredStatus == AgentThinkingEffortSupportStatus.Supported)
        {
            Assert.Equal(
                [AgentReasoningEffortLevel.None, AgentReasoningEffortLevel.Medium],
                capability.AllowedEfforts);
        }

        Assert.DoesNotContain(AgentReasoningEffortLevel.ExtraHigh, capability.AllowedEfforts);
    }

    [Theory]
    [InlineData("gpt-oss:20b", "gptoss")]
    [InlineData("gptoss32k:latest", "gptoss")]
    public void Ollama_discovered_gptoss_capability_exposes_levels_without_disable(
        string model,
        string family)
    {
        var capability = AgentThinkingEffortPolicy.CreateDiscoveredCapability(
            model,
            family,
            AgentThinkingEffortSupportStatus.Supported);

        Assert.Equal(
            [
                AgentReasoningEffortLevel.Low,
                AgentReasoningEffortLevel.Medium,
                AgentReasoningEffortLevel.High
            ],
            capability.AllowedEfforts);
        Assert.Equal(AgentThinkingEffortControlMode.EffortLevels, capability.ControlMode);
        Assert.DoesNotContain(AgentReasoningEffortLevel.None, capability.AllowedEfforts);
        Assert.DoesNotContain(AgentReasoningEffortLevel.Max, capability.AllowedEfforts);
        Assert.Contains("cannot be disabled", capability.Summary, StringComparison.Ordinal);
    }

    [Fact]
    public void Ollama_unknown_discovery_does_not_eclipse_a_known_family_definition()
    {
        const string knownModel = "deepseek-r1:14b";
        var provider = CreateProvider(ProviderKind.Ollama, knownModel) with
        {
            ModelThinkingEffortCapabilities =
            [
                AgentThinkingEffortPolicy.CreateDiscoveredCapability(
                    knownModel,
                    "qwen2",
                    AgentThinkingEffortSupportStatus.Unknown),
                AgentThinkingEffortPolicy.CreateDiscoveredCapability(
                    "custom-unknown",
                    "custom",
                    AgentThinkingEffortSupportStatus.Unknown)
            ]
        };

        var knownCapability = AgentThinkingEffortPolicy.ResolveCapability(provider, knownModel);
        var customCapability = AgentThinkingEffortPolicy.ResolveCapability(provider, "custom-unknown");

        Assert.Equal(AgentThinkingEffortSupportStatus.Supported, knownCapability.Status);
        Assert.Equal(AgentThinkingEffortCapabilitySource.Defined, knownCapability.Source);
        Assert.Equal(AgentThinkingEffortControlMode.BooleanToggle, knownCapability.ControlMode);
        Assert.Equal(AgentThinkingEffortSupportStatus.Unknown, customCapability.Status);
        Assert.Equal(AgentThinkingEffortCapabilitySource.Discovered, customCapability.Source);
    }

    [Fact]
    public void Override_support_query_uses_the_same_provider_model_capability_as_save_and_ui()
    {
        var supportedProvider = CreateProvider(ProviderKind.OpenAi, OpenAiModelIds.Gpt56Sol);
        var unsupportedProvider = CreateProvider(ProviderKind.OpenAi, "gpt-4.1");
        var ollamaProvider = CreateProvider(ProviderKind.Ollama, "qwen3.5:2b");

        Assert.True(AgentThinkingEffortPolicy.IsOverrideSupported(
            supportedProvider,
            OpenAiModelIds.Gpt56Sol,
            AgentReasoningEffortLevel.Max));
        Assert.False(AgentThinkingEffortPolicy.IsOverrideSupported(
            unsupportedProvider,
            "gpt-4.1",
            AgentReasoningEffortLevel.Medium));
        Assert.False(AgentThinkingEffortPolicy.IsOverrideSupported(
            ollamaProvider,
            "qwen3.5:2b",
            AgentReasoningEffortLevel.ExtraHigh));
    }

    [Theory]
    [InlineData("{\"reasoningEffort\":\"high\"}", AgentReasoningEffortLevel.High)]
    [InlineData("{\"reasoningEffort\":\"minimal\"}", AgentReasoningEffortLevel.Minimal)]
    [InlineData("{\"modelParameters\":{\"reasoningEffort\":\"low\"}}", AgentReasoningEffortLevel.Low)]
    [InlineData("{\"think\":true}", AgentReasoningEffortLevel.Medium)]
    [InlineData("{\"modelParameters\":{\"think\":false}}", AgentReasoningEffortLevel.None)]
    public void Thinking_effort_reader_supports_legacy_root_nested_and_ollama_aliases(
        string configurationJson,
        AgentReasoningEffortLevel expectedEffort)
    {
        var effort = AgentThinkingEffortPolicy.ReadConfiguredEffort(configurationJson, "agent");

        Assert.Equal(expectedEffort, effort);
    }

    [Fact]
    public void Thinking_effort_writer_canonicalizes_and_resets_without_losing_unrelated_configuration()
    {
        const string configurationJson =
            """
            {
              "reasoningEffort": "high",
              "think": true,
              "keepRoot": "value",
              "modelParameters": {
                "reasoningEffort": "low",
                "think": false,
                "numPredict": 64
              }
            }
            """;

        var canonicalJson = AgentThinkingEffortPolicy.WriteAgentOverride(
            configurationJson,
            AgentReasoningEffortLevel.Medium);
        using var canonicalDocument = JsonDocument.Parse(canonicalJson);
        var canonicalRoot = canonicalDocument.RootElement;
        var canonicalModelParameters = canonicalRoot.GetProperty("modelParameters");

        Assert.Equal("value", canonicalRoot.GetProperty("keepRoot").GetString());
        Assert.False(canonicalRoot.TryGetProperty("reasoningEffort", out _));
        Assert.False(canonicalRoot.TryGetProperty("think", out _));
        Assert.Equal("medium", canonicalModelParameters.GetProperty("reasoningEffort").GetString());
        Assert.Equal(64, canonicalModelParameters.GetProperty("numPredict").GetInt32());
        Assert.False(canonicalModelParameters.TryGetProperty("think", out _));

        var resetJson = AgentThinkingEffortPolicy.WriteAgentOverride(canonicalJson, null);
        using var resetDocument = JsonDocument.Parse(resetJson);
        var resetRoot = resetDocument.RootElement;
        var resetModelParameters = resetRoot.GetProperty("modelParameters");

        Assert.Equal("value", resetRoot.GetProperty("keepRoot").GetString());
        Assert.False(resetRoot.TryGetProperty("reasoningEffort", out _));
        Assert.False(resetRoot.TryGetProperty("think", out _));
        Assert.Equal(64, resetModelParameters.GetProperty("numPredict").GetInt32());
        Assert.False(resetModelParameters.TryGetProperty("reasoningEffort", out _));
        Assert.False(resetModelParameters.TryGetProperty("think", out _));
    }

    [Fact]
    public void Provider_default_writer_uses_the_same_canonical_nested_contract()
    {
        const string configurationJson =
            """{"reasoningEffort":"low","keep":"value","modelParameters":{"think":true,"maxOutputTokens":128}}""";

        var canonicalJson = AgentThinkingEffortPolicy.WriteProviderDefault(
            configurationJson,
            AgentReasoningEffortLevel.High);

        using var document = JsonDocument.Parse(canonicalJson);
        var root = document.RootElement;
        var modelParameters = root.GetProperty("modelParameters");
        Assert.Equal("value", root.GetProperty("keep").GetString());
        Assert.False(root.TryGetProperty("reasoningEffort", out _));
        Assert.False(root.TryGetProperty("think", out _));
        Assert.Equal("high", modelParameters.GetProperty("reasoningEffort").GetString());
        Assert.Equal(128, modelParameters.GetProperty("maxOutputTokens").GetInt32());
        Assert.False(modelParameters.TryGetProperty("think", out _));
    }

    [Fact]
    public void Max_output_tokens_prefers_agent_configuration_over_provider_configuration()
    {
        var maxOutputTokens = AgentProviderModelParameterPolicy.ResolveMaxOutputTokens(
            ProviderKind.OpenAi,
            OpenAiModelIds.Gpt56Luna,
            "{\"modelParameters\":{\"maxOutputTokens\":300}}",
            "{\"maxOutputTokens\":120}");

        Assert.Equal(120, maxOutputTokens);
    }

    [Fact]
    public void Gpt5_max_output_tokens_accepts_128k_and_rejects_larger_values()
    {
        var maxOutputTokens = AgentProviderModelParameterPolicy.ResolveMaxOutputTokens(
            ProviderKind.OpenAi,
            OpenAiModelIds.Gpt56Luna,
            "{\"modelParameters\":{\"maxOutputTokens\":128000}}",
            string.Empty);

        var exception = Assert.Throws<InvalidOperationException>(() =>
            AgentProviderModelParameterPolicy.ResolveMaxOutputTokens(
                ProviderKind.OpenAi,
                OpenAiModelIds.Gpt56Luna,
                "{\"modelParameters\":{\"maxOutputTokens\":128001}}",
                string.Empty));

        Assert.Equal(128_000, maxOutputTokens);
        Assert.Contains("between 1 and 128000", exception.Message);
    }

    [Theory]
    [InlineData("gpt-4.1")]
    [InlineData("gpt-4o")]
    public void Older_openai_models_keep_the_conservative_output_token_limit(string model)
    {
        var exception = Assert.Throws<InvalidOperationException>(() =>
            AgentProviderModelParameterPolicy.ResolveMaxOutputTokens(
                ProviderKind.OpenAi,
                model,
                "{\"modelParameters\":{\"maxOutputTokens\":8193}}",
                string.Empty));

        Assert.Contains("between 1 and 8192", exception.Message);
    }

    [Fact]
    public void Ollama_num_predict_is_treated_as_max_output_tokens()
    {
        var maxOutputTokens = AgentProviderModelParameterPolicy.ResolveMaxOutputTokens(
            ProviderKind.Ollama,
            "{\"modelParameters\":{\"numPredict\":160}}",
            string.Empty);

        Assert.Equal(160, maxOutputTokens);
    }

    [Fact]
    public void Ollama_legacy_think_false_maps_to_explicit_none()
    {
        var provider = CreateProvider(ProviderKind.Ollama, "qwen3.5:2b");

        var effort = AgentThinkingEffortPolicy.ResolveEffectiveEffort(
            provider,
            provider.DefaultModel,
            "{\"modelParameters\":{\"think\":false}}");

        Assert.Equal(AgentReasoningEffortLevel.None, effort);
    }

    [Theory]
    [InlineData("{\"think\":true}", "{}")]
    [InlineData("{}", "{\"modelParameters\":{\"think\":false}}")]
    public void Openai_runtime_ignores_legacy_ollama_think_aliases(
        string providerConfigurationJson,
        string agentConfigurationJson)
    {
        var provider = CreateProvider(ProviderKind.OpenAi, "gpt-5.4-mini") with
        {
            ConfigurationJson = providerConfigurationJson
        };

        var effort = AgentThinkingEffortPolicy.ResolveEffectiveEffort(
            provider,
            provider.DefaultModel,
            agentConfigurationJson);

        Assert.Null(effort);
    }

    [Fact]
    public void Ollama_defaults_bound_generation_and_inherit_thinking_effort()
    {
        var maxOutputTokens = AgentProviderModelParameterPolicy.ResolveOllamaMaxOutputTokensOrDefault(
            "{}",
            string.Empty);
        var provider = CreateProvider(ProviderKind.Ollama, "qwen3.5:2b");
        var effort = AgentThinkingEffortPolicy.ResolveEffectiveEffort(
            provider,
            "qwen3.5:2b",
            "{}");

        Assert.Equal(AgentProviderModelParameterPolicy.DefaultOllamaMaxOutputTokens, maxOutputTokens);
        Assert.Null(effort);
    }

    [Fact]
    public void Invalid_max_output_tokens_fails_explicitly()
    {
        var exception = Assert.Throws<InvalidOperationException>(() =>
            AgentProviderModelParameterPolicy.ResolveMaxOutputTokens(
                ProviderKind.Ollama,
                "{\"modelParameters\":{\"num_predict\":0}}",
                string.Empty));

        Assert.Contains("must be between", exception.Message);
    }

    private static ProviderProfile CreateProvider(ProviderKind kind, string defaultModel)
    {
        return new ProviderProfile(
            Guid.NewGuid(),
            $"{kind} provider",
            kind,
            "http://provider.test",
            string.Empty,
            defaultModel,
            ProviderTransportKind.ChatCompletions,
            IsEnabled: true,
            SupportsStreaming: false,
            SupportsTools: false,
            PreferFrameworkManagedChatHistory: true,
            SupportsBackgroundResponses: false,
            ConfigurationJson: "{}",
            Notes: string.Empty,
            HealthStatus: "Not checked",
            LastCheckedAtUtc: null,
            SuggestedModels: [defaultModel]);
    }

    private static IReadOnlyList<AgentReasoningEffortLevel> ResolveOpenAiEfforts(string model)
    {
        return AgentThinkingEffortPolicy.ResolveDefinedCapability(
            ProviderKind.OpenAi,
            ProviderTransportKind.Responses,
            model).AllowedEfforts;
    }

    private static void AssertRegistryGroup(
        IReadOnlyList<string> models,
        IReadOnlyList<AgentReasoningEffortLevel> expectedEfforts,
        bool responsesOnly = false)
    {
        foreach (var model in models)
        {
            var definition = Assert.Single(
                OpenAiThinkingEffortModelRegistry.All,
                item => string.Equals(item.Model, model, StringComparison.OrdinalIgnoreCase));
            Assert.Equal(AgentThinkingEffortSupportStatus.Supported, definition.Status);
            Assert.Equal(expectedEfforts, definition.AllowedEfforts);
            IReadOnlyList<ProviderTransportKind> expectedTransports = responsesOnly
                ? [ProviderTransportKind.Responses]
                : [ProviderTransportKind.Responses, ProviderTransportKind.ChatCompletions];
            Assert.Equal(
                expectedTransports,
                definition.AllowedTransports);
        }
    }
}
