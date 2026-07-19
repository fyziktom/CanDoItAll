using System.Text.Json;
using System.Text.Json.Serialization;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.Tooling;
using CanDoItAll.Modules.AgentFramework;
using CanDoItAll.Modules.Prompts;
using Microsoft.Extensions.AI;

namespace CanDoItAll.Tests.Unit;

[Collection(AppDbContextModelRegistryTestCollectionNames.Name)]
public sealed class PromptGalleryAgentRuntimeToolProviderTests
{
    private static readonly JsonSerializerOptions JsonOptions = CreateJsonOptions();

    [Fact]
    public async Task Item_tool_returns_immutable_current_version_and_enforces_runtime_model()
    {
        var factory = PromptGalleryTestSupport.CreateFactory(nameof(Item_tool_returns_immutable_current_version_and_enforces_runtime_model));
        var gallery = PromptGalleryTestSupport.CreateService(factory);
        var saveReceipt = Require(await gallery.SaveDraftAsync(CreateDraft("Immutable version content.")));
        var promptId = saveReceipt.PromptArtifactId;
        var version = Require(await gallery.CreateVersionAsync(
            promptId,
            new PromptVersionCreateRequest(
                "Runtime tool proof",
                saveReceipt.UpdatedAtUtc)));
        var finalized = Require(await gallery.GetItemAsync(promptId));
        _ = Require(await gallery.SaveDraftAsync(CreateDraft(
            "Mutable draft must not leak.",
            promptId,
            finalized.UpdatedAtUtc)));

        var toolProvider = new PromptGalleryAgentRuntimeToolProvider(
            gallery,
            new PromptGalleryCompatibilityEvaluator());
        var compatibleContext = CreateContext("gpt-5-mini");
        var tools = await toolProvider.CreateToolsAsync(compatibleContext, CancellationToken.None);
        var itemTool = Assert.Single(
            tools,
            tool => tool.Name == AgentToolInvocationPolicyMetadata.PromptGalleryItemGet);

        var result = await InvokeAsync<PromptGalleryAgentItemResult>(
            itemTool,
            new PromptGalleryAgentItemInput(promptId));

        Assert.Equal(promptId, result.PromptArtifactId);
        Assert.Equal(version.PromptVersionId, result.PromptVersionId);
        Assert.Equal("Immutable version content.", result.Content);
        Assert.DoesNotContain("Mutable draft", result.Content, StringComparison.Ordinal);

        var incompatibleTools = await toolProvider.CreateToolsAsync(
            CreateContext("gpt-incompatible"),
            CancellationToken.None);
        var incompatibleItemTool = Assert.Single(
            incompatibleTools,
            tool => tool.Name == AgentToolInvocationPolicyMetadata.PromptGalleryItemGet);
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            InvokeAsync<PromptGalleryAgentItemResult>(
                incompatibleItemTool,
                new PromptGalleryAgentItemInput(promptId)));
        Assert.Contains("not declared as supported", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    private static PromptGalleryDraft CreateDraft(
        string content,
        Guid? id = null,
        DateTimeOffset? expectedUpdatedAtUtc = null)
        => new(
            id,
            ProjectId: null,
            CollectionId: null,
            "Runtime prompt",
            "Runtime tool immutable version proof.",
            PromptGalleryItemKind.FullPrompt,
            "agent-runtime",
            content,
            Tags: ["runtime"],
            SupportedModels: [new PromptProviderModel(ProviderKind.OpenAi.ToString(), "gpt-5-mini")],
            SupportedConsumers: [PromptGalleryConsumer.AgentRuntime],
            Recommendations: new PromptModelRecommendations(0.2, 800),
            ExpectedUpdatedAtUtc: expectedUpdatedAtUtc);

    private static AgentRuntimeToolProviderContext CreateContext(string model)
    {
        var provider = new ProviderProfile(
            Guid.NewGuid(),
            "OpenAI chat",
            ProviderKind.OpenAi,
            "https://api.openai.com",
            "OPENAI_API_KEY",
            "gpt-5-mini",
            ProviderTransportKind.ChatCompletions,
            IsEnabled: true,
            SupportsStreaming: true,
            SupportsTools: true,
            PreferFrameworkManagedChatHistory: true,
            SupportsBackgroundResponses: false,
            ConfigurationJson: string.Empty,
            Notes: string.Empty,
            HealthStatus: string.Empty,
            LastCheckedAtUtc: null,
            SuggestedModels: ["gpt-5-mini"],
            ProviderProfilePurpose.Chat);
        var now = DateTimeOffset.UnixEpoch;
        var agent = new AgentDefinition(
            Guid.NewGuid(),
            "Prompt Gallery agent",
            "Prompt operator",
            "Exercises Prompt Gallery tools.",
            "Use the Gallery version exactly.",
            AgentLifecycleStatus.Active,
            provider.Id,
            model,
            AgentWorkloadKind.General,
            AgentChatHistoryMode.ProviderDefault,
            0.2,
            RequirePerServiceCallChatHistoryPersistence: false,
            EnableBackgroundResponses: false,
            "{}",
            IsTemplate: false,
            TemplateKey: string.Empty,
            AgentPermissionsPolicy.Default,
            Capabilities: [],
            Tags: [],
            now,
            now);
        return new AgentRuntimeToolProviderContext(
            agent,
            provider,
            Capabilities: [],
            SuppressApprovalRequirements: false,
            Purpose: AgentRuntimeToolProviderPurpose.InteractiveChat,
            RuntimeSessionKey: "prompt-gallery-runtime-test",
            ContextIntent: AgentRuntimeContextIntent.Empty,
            Tags: new Dictionary<string, string>());
    }

    private static async Task<TResult> InvokeAsync<TResult>(AITool tool, object request)
    {
        var function = Assert.IsAssignableFrom<AIFunction>(tool);
        var rawResult = await function.InvokeAsync(new AIFunctionArguments
        {
            ["request"] = request
        });
        return rawResult switch
        {
            TResult result => result,
            JsonElement element => JsonSerializer.Deserialize<TResult>(element.GetRawText(), JsonOptions)
                ?? throw new InvalidOperationException("Prompt Gallery runtime tool returned null JSON."),
            _ => throw new InvalidOperationException(
                $"Unexpected Prompt Gallery runtime tool result type '{rawResult?.GetType().FullName ?? "<null>"}'.")
        };
    }

    private static T Require<T>(CanDoItAll.SharedKernel.Result<T> result)
        => result.IsSuccess && result.Value is not null
            ? result.Value
            : throw new InvalidOperationException(string.Join(" ", result.Errors.Select(error => error.Message)));

    private static JsonSerializerOptions CreateJsonOptions()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        options.Converters.Add(new JsonStringEnumConverter());
        return options;
    }
}
