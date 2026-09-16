using System.Text.Json;
using System.Text.Json.Serialization;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.Tooling;
using CanDoItAll.Modules.AgentFramework;
using CanDoItAll.Modules.Prompts;
using Microsoft.Extensions.AI;

namespace CanDoItAll.Tests.Unit.AgentFramework;

[Collection(AppDbContextModelRegistryTestCollectionNames.Name)]
public sealed class PromptGalleryAgentRuntimeToolProviderTests
{
    private static readonly JsonSerializerOptions JsonOptions = CreateJsonOptions();

    [Theory]
    [InlineData(PromptGalleryToolPolicy.PromptGallerySearch, "archive")]
    [InlineData(PromptGalleryToolPolicy.PromptGallerySearch, "model")]
    [InlineData(PromptGalleryToolPolicy.PromptGallerySearch, "consumer")]
    [InlineData(PromptGalleryToolPolicy.PromptGalleryItemGet, "archive")]
    [InlineData(PromptGalleryToolPolicy.PromptGalleryItemGet, "model")]
    [InlineData(PromptGalleryToolPolicy.PromptGalleryItemGet, "consumer")]
    public async Task Saved_gallery_results_recheck_current_owner_eligibility_and_keep_the_original_immutable_content(
        string toolName, string revocation) {
        var gallery = PromptGalleryTestSupport.CreateService(PromptGalleryTestSupport.CreateFactory(
            $"{nameof(Saved_gallery_results_recheck_current_owner_eligibility_and_keep_the_original_immutable_content)}-{toolName}-{revocation}"));
        var saved = Require(await gallery.SaveDraftAsync(CreateDraft("Original immutable prompt.")));
        var version = Require(await gallery.CreateVersionAsync(saved.PromptArtifactId, new("Original version", saved.UpdatedAtUtc)));
        var provider = new PromptGalleryAgentRuntimeToolProvider(gallery, new PromptGalleryCompatibilityEvaluator());
        var context = CreateContext("gpt-5-mini");
        var tool = (await provider.CreateToolsAsync(context, default)).Single(item => item.Name == toolName);
        var metadata = provider.GetToolMetadata(context).Single(item => item.ToolName == toolName);
        object request = toolName == PromptGalleryToolPolicy.PromptGallerySearch
            ? new PromptGalleryAgentSearchInput(text: "Runtime prompt") : new PromptGalleryAgentItemInput(saved.PromptArtifactId);
        object result = toolName == PromptGalleryToolPolicy.PromptGallerySearch
            ? await InvokeAsync<PromptGalleryAgentSearchResult>(tool, request) : await InvokeAsync<PromptGalleryAgentItemResult>(tool, request);
        var disclosure = ManagedToolDisclosureTestData.Create(metadata, request, result);
        var authorize = metadata.AuthorizeResultDisclosureAsync!;
        Assert.NotNull(authorize);
        await using (var lease = await authorize(disclosure, default)) {
            Assert.Null(lease);
        }
        var current = Require(await gallery.GetItemAsync(saved.PromptArtifactId));
        if (revocation == "archive") {
            Assert.True((await gallery.ArchiveAsync(saved.PromptArtifactId, true)).IsSuccess);
        } else {
            var changed = CreateDraft("Human changed draft must never replace the cached body.", saved.PromptArtifactId, current.UpdatedAtUtc);
            changed = revocation == "model"
                ? changed with { SupportedModels = [new(ProviderKind.OpenAi.ToString(), "another-model")] }
                : changed with { SupportedConsumers = [PromptGalleryConsumer.Workflow] };
            Require(await gallery.SaveDraftAsync(changed));
        }
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => authorize(disclosure, default).AsTask());
        if (revocation == "archive") {
            Assert.True((await gallery.ArchiveAsync(saved.PromptArtifactId, false)).IsSuccess);
        } else {
            current = Require(await gallery.GetItemAsync(saved.PromptArtifactId));
            Require(await gallery.SaveDraftAsync(CreateDraft("Human changed draft remains untouched.", saved.PromptArtifactId, current.UpdatedAtUtc)));
        }
        var beforeObservation = Require(await gallery.GetItemAsync(saved.PromptArtifactId));
        await using (var lease = await authorize(disclosure, default)) {
            Assert.Null(lease);
        }
        var afterObservation = Require(await gallery.GetItemAsync(saved.PromptArtifactId));
        Assert.Equal(beforeObservation.UpdatedAtUtc, afterObservation.UpdatedAtUtc);
        Assert.Equal(beforeObservation.DraftContent, afterObservation.DraftContent);
        Assert.Single(afterObservation.Versions);
        if (result is PromptGalleryAgentItemResult item) {
            Assert.Equal(version.PromptVersionId, item.PromptVersionId);
            Assert.Equal("Original immutable prompt.", item.Content);
        }
    }

    [Fact]
    public async Task Saved_gallery_body_cannot_be_bound_to_another_item_or_changed_immutable_content() {
        var gallery = PromptGalleryTestSupport.CreateService(PromptGalleryTestSupport.CreateFactory(
            nameof(Saved_gallery_body_cannot_be_bound_to_another_item_or_changed_immutable_content)));
        var saved = Require(await gallery.SaveDraftAsync(CreateDraft("Original immutable prompt.")));
        Require(await gallery.CreateVersionAsync(saved.PromptArtifactId, new("Original version", saved.UpdatedAtUtc)));
        var provider = new PromptGalleryAgentRuntimeToolProvider(gallery, new PromptGalleryCompatibilityEvaluator());
        var context = CreateContext("gpt-5-mini");
        var tool = (await provider.CreateToolsAsync(context, default)).Single(item => item.Name == PromptGalleryToolPolicy.PromptGalleryItemGet);
        var metadata = provider.GetToolMetadata(context).Single(item => item.ToolName == tool.Name);
        var request = new PromptGalleryAgentItemInput(saved.PromptArtifactId);
        var result = await InvokeAsync<PromptGalleryAgentItemResult>(tool, request);
        var wrongItem = ManagedToolDisclosureTestData.Create(metadata, new PromptGalleryAgentItemInput(Guid.NewGuid()), result);
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => metadata.AuthorizeResultDisclosureAsync!(wrongItem, default).AsTask());
        var changedBody = ManagedToolDisclosureTestData.Create(metadata, request, result with { Content = "Substituted body" });
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => metadata.AuthorizeResultDisclosureAsync!(changedBody, default).AsTask());
    }

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
            tool => tool.Name == PromptGalleryToolPolicy.PromptGalleryItemGet);

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
            tool => tool.Name == PromptGalleryToolPolicy.PromptGalleryItemGet);
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
