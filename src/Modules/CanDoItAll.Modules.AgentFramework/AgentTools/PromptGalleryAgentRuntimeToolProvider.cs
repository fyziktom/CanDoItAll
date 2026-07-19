using System.Text.Json.Serialization;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.Tooling;
using CanDoItAll.Modules.Prompts;
using CanDoItAll.SharedKernel;
using Microsoft.Extensions.AI;

namespace CanDoItAll.Modules.AgentFramework;

public sealed class PromptGalleryAgentRuntimeToolProvider(
    IPromptGalleryService promptGallery,
    PromptGalleryCompatibilityEvaluator compatibilityEvaluator) : IAgentRuntimeToolProvider
{
    public const string ProviderKey = "prompt-gallery.runtime-tools";

    private const int ProviderOrder = 935;

    public int Order => ProviderOrder;

    public AgentRuntimeToolProviderDescriptor Descriptor { get; } = new(
        ProviderKey,
        "Prompt Gallery tools",
        "Searches and retrieves canonical reusable prompts and prompt parts.",
        ["agent-framework", "prompt-gallery", "instructions"],
        [
            AgentRuntimeToolProviderPurpose.InteractiveChat,
            AgentRuntimeToolProviderPurpose.GovernedProcessAutomation,
            AgentRuntimeToolProviderPurpose.AutoApprovedNonInteractive,
            AgentRuntimeToolProviderPurpose.A2AEndpoint
        ]);

    public ValueTask<IReadOnlyList<AITool>> CreateToolsAsync(
        AgentRuntimeToolProviderContext context,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        cancellationToken.ThrowIfCancellationRequested();
        if (!context.Agent.Permissions.CanUseTools)
        {
            return ValueTask.FromResult<IReadOnlyList<AITool>>([]);
        }

        var provider = context.Provider.Kind.ToString();
        var model = string.IsNullOrWhiteSpace(context.Agent.Model)
            ? context.Provider.DefaultModel
            : context.Agent.Model;

        return ValueTask.FromResult<IReadOnlyList<AITool>>(
        [
            AIFunctionFactory.Create(
                (PromptGalleryAgentSearchInput request, CancellationToken token = default) =>
                    SearchAsync(request, provider, model, token),
                AgentToolInvocationPolicyMetadata.PromptGallerySearch,
                "Searches the canonical Prompt Gallery with bounded paging. Results contain metadata, not prompt bodies; retrieve one selected item with prompt_gallery_item_get."),
            AIFunctionFactory.Create(
                (PromptGalleryAgentItemInput request, CancellationToken token = default) =>
                    GetItemAsync(request, provider, model, token),
                AgentToolInvocationPolicyMetadata.PromptGalleryItemGet,
                "Retrieves one active, final Prompt Gallery item after enforcing compatibility with the current agent provider and model.")
        ]);
    }

    public IReadOnlyList<AgentRuntimeToolMetadata> GetToolMetadata(
        AgentRuntimeToolProviderContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        if (!context.Agent.Permissions.CanUseTools)
        {
            return [];
        }

        return
        [
            CreateMetadata(AgentToolInvocationPolicyMetadata.PromptGallerySearch),
            CreateMetadata(AgentToolInvocationPolicyMetadata.PromptGalleryItemGet)
        ];
    }

    private async Task<PromptGalleryAgentSearchResult> SearchAsync(
        PromptGalleryAgentSearchInput request,
        string provider,
        string model,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var page = await promptGallery.SearchAsync(
            new PromptGalleryQuery(
                request.Text,
                request.Tags,
                request.Kind,
                Status: PromptArtifactStatus.Final,
                IncludeArchived: false,
                provider,
                model,
                request.PageIndex,
                request.PageSize,
                PromptGalleryConsumer.AgentRuntime),
            cancellationToken);

        return new PromptGalleryAgentSearchResult(
            page.Items
                .Select(item => new PromptGalleryAgentSearchItem(
                    item.Id,
                    item.Title,
                    item.Summary,
                    item.Kind,
                    item.Tags,
                    item.SupportedModels,
                    item.Recommendations,
                    item.CurrentVersionNumber))
                .ToArray(),
            page.PageIndex,
            page.PageSize,
            page.TotalCount,
            page.TotalPages);
    }

    private async Task<PromptGalleryAgentItemResult> GetItemAsync(
        PromptGalleryAgentItemInput request,
        string provider,
        string model,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var itemResult = await promptGallery.GetItemAsync(request.PromptArtifactId, cancellationToken);
        var item = RequireValue(itemResult, "Prompt Gallery item");
        var compatibility = compatibilityEvaluator.Evaluate(
            item,
            new PromptGalleryConsumerContext(
                PromptGalleryConsumer.AgentRuntime,
                PromptGalleryCompatibilityPurpose.Execution,
                Provider: provider,
                Model: model,
                RequiresFinalVersion: true));
        if (!compatibility.CanUse)
        {
            throw new InvalidOperationException(
                string.Join(" ", compatibility.Issues.Select(issue => issue.Message)));
        }

        var versionResult = await promptGallery.GetVersionSnapshotAsync(
            item.Id,
            item.CurrentVersionNumber,
            cancellationToken);
        var version = RequireValue(versionResult, "Prompt Gallery version");
        if (version.Content.Length > PromptGalleryLimits.MaximumContentLength)
        {
            throw new InvalidOperationException(
                $"Prompt Gallery item '{item.Id}' exceeds the runtime tool limit of {PromptGalleryLimits.MaximumContentLength:N0} characters.");
        }

        return MapItem(item, version);
    }

    private static PromptGalleryAgentItemResult MapItem(
        PromptGalleryItemDetails item,
        PromptVersionSnapshot version)
        => new(
            item.Id,
            version.PromptVersionId,
            version.VersionNumber,
            version.Title,
            version.Summary,
            version.Kind,
            version.Content,
            item.Tags,
            item.TemplateTokens,
            item.SupportedModels,
            version.Recommendations,
            item.SupportedConsumers);

    private static T RequireValue<T>(Result<T> result, string resourceName)
    {
        if (result.IsSuccess && result.Value is not null)
        {
            return result.Value;
        }

        var detail = result.Errors.Count == 0
            ? "The operation did not return a value."
            : string.Join(" ", result.Errors.Select(error => $"{error.Code}: {error.Message}"));
        throw new InvalidOperationException($"{resourceName} could not be loaded. {detail}");
    }

    private static AgentRuntimeToolMetadata CreateMetadata(string toolName)
        => new(
            ProviderKey,
            toolName,
            AgentRuntimeToolOperationKind.Read,
            requiresApprovalByDefault: false,
            ["prompt-gallery", "instructions", "canonical-read"]);
}

public sealed record PromptGalleryAgentSearchInput
{
    [JsonConstructor]
    public PromptGalleryAgentSearchInput(
        string? text = null,
        IReadOnlyList<string>? tags = null,
        PromptGalleryItemKind? kind = null,
        int pageIndex = 0,
        int pageSize = 20)
    {
        var query = new PromptGalleryQuery(
            text,
            tags,
            kind,
            Status: null,
            IncludeArchived: false,
            Provider: null,
            Model: null,
            pageIndex,
            pageSize);
        query.Validate();

        Text = string.IsNullOrWhiteSpace(text) ? null : text.Trim();
        Tags = tags?
            .Select(tag => tag.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray() ?? [];
        Kind = kind;
        PageIndex = pageIndex;
        PageSize = pageSize;
    }

    public string? Text { get; }

    public IReadOnlyList<string> Tags { get; }

    public PromptGalleryItemKind? Kind { get; }

    public int PageIndex { get; }

    public int PageSize { get; }
}

public sealed record PromptGalleryAgentItemInput
{
    [JsonConstructor]
    public PromptGalleryAgentItemInput(Guid promptArtifactId)
    {
        if (promptArtifactId == Guid.Empty)
        {
            throw new ArgumentException("Prompt artifact id cannot be empty.", nameof(promptArtifactId));
        }

        PromptArtifactId = promptArtifactId;
    }

    public Guid PromptArtifactId { get; }
}

public sealed record PromptGalleryAgentSearchItem(
    Guid PromptArtifactId,
    string Title,
    string Summary,
    PromptGalleryItemKind Kind,
    IReadOnlyList<string> Tags,
    IReadOnlyList<PromptProviderModel> SupportedModels,
    PromptModelRecommendations Recommendations,
    int CurrentVersionNumber);

public sealed record PromptGalleryAgentSearchResult(
    IReadOnlyList<PromptGalleryAgentSearchItem> Items,
    int PageIndex,
    int PageSize,
    int TotalCount,
    int TotalPages);

public sealed record PromptGalleryAgentItemResult(
    Guid PromptArtifactId,
    Guid PromptVersionId,
    int VersionNumber,
    string Title,
    string Summary,
    PromptGalleryItemKind Kind,
    string Content,
    IReadOnlyList<string> Tags,
    IReadOnlyList<string> TemplateTokens,
    IReadOnlyList<PromptProviderModel> SupportedModels,
    PromptModelRecommendations Recommendations,
    IReadOnlyList<PromptGalleryConsumer> SupportedConsumers);
