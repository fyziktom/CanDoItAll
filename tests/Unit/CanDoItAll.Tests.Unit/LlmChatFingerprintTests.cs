using CanDoItAll.AgentFramework.Llm.Abstractions;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.Llm.SimpleChats.Common;

namespace CanDoItAll.Tests.Unit.LlmChats;

public sealed class LlmChatFingerprintTests
{
    [Fact]
    public void Request_fingerprint_includes_typed_attribution_scope_without_changing_unattributed_requests()
    {
        var conversationId = LlmChatConversationId.New();
        var settings = new LlmChatSettingsFingerprint(new string('a', 64));
        var projectId = Guid.NewGuid();

        var legacy = LlmChatFingerprints.CreateRequest(
            conversationId,
            3,
            "hello",
            settings);
        var explicitUnattributed = LlmChatFingerprints.CreateRequest(
            conversationId,
            3,
            "hello",
            settings,
            attributionScope: null);
        var attributed = LlmChatFingerprints.CreateRequest(
            conversationId,
            3,
            "hello",
            settings,
            WorkspaceScopeDescriptor.Project(projectId.ToString("D")));

        Assert.Equal(legacy, explicitUnattributed);
        Assert.NotEqual(legacy, attributed);
    }

    [Fact]
    public void Settings_fingerprint_distinguishes_provider_default_from_every_explicit_effort()
    {
        var providerId = Guid.NewGuid();
        var providerDefault = LlmChatFingerprints.CreateSettings(
            providerId,
            ProviderKind.OpenAi,
            "gpt-5",
            new LlmModelSettings());

        foreach (var effort in Enum.GetValues<AgentReasoningEffortLevel>())
        {
            var explicitEffort = LlmChatFingerprints.CreateSettings(
                providerId,
                ProviderKind.OpenAi,
                "gpt-5",
                new LlmModelSettings { ThinkingEffort = effort });

            Assert.NotEqual(providerDefault, explicitEffort);
        }
    }

    [Fact]
    public void Settings_fingerprint_is_stable_for_semantically_equivalent_parameter_json()
    {
        var providerId = Guid.NewGuid();
        var left = LlmChatFingerprints.CreateSettings(
            providerId,
            ProviderKind.OpenAi,
            "gpt-5",
            new LlmModelSettings(0.3, """{"maxOutputTokens":123,"metadata":{"b":2,"a":1}}"""));
        var right = LlmChatFingerprints.CreateSettings(
            providerId,
            ProviderKind.OpenAi,
            "gpt-5",
            new LlmModelSettings(0.3, """{"metadata":{"a":1,"b":2},"maxOutputTokens":123}"""));

        Assert.Equal(left, right);
    }

    [Fact]
    public void Settings_fingerprint_rejects_invalid_parameter_json()
    {
        Assert.Throws<ArgumentException>(() => LlmChatFingerprints.CreateSettings(
            Guid.NewGuid(),
            ProviderKind.OpenAi,
            "gpt-5",
            new LlmModelSettings(ModelParameterConfigurationJson: "not-json")));
    }
}
