using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Components.BaseLib;
using Microsoft.AspNetCore.Components;

namespace CanDoItAll.Modules.AgentFramework.Pages.Components;

public partial class AgentProviderProfilesPanel
{
    [Inject]
    public IAgentFrameworkWorkspaceService WorkspaceService { get; set; } = default!;

    [Inject]
    public NotificationService NotificationService { get; set; } = default!;

    private readonly HashSet<string> expandedProviderTreeNodeIds = [];
    private readonly HashSet<string> knownProviderTagNodeIds = [];
    private IReadOnlyList<ProviderProfile> providers = [];
    private ProviderProfileEditorModel providerModel = CreateNewProviderEditor();
    private IReadOnlyList<string> providerTagValues = [];
    private string providerSearch = string.Empty;
    private string suggestedModelsText = string.Empty;
    private bool isLoading;
    private bool isBusy;

    private IReadOnlyList<ProviderProfile> FilteredProviders => providers
        .Where(MatchesProviderSearch)
        .OrderBy(provider => provider.Name, StringComparer.OrdinalIgnoreCase)
        .ToList();

    private IReadOnlyList<TreeViewNode> ProviderTreeNodes
        => ProviderProfileTreeNodeBuilder.Build(FilteredProviders, providerModel.Id, expandedProviderTreeNodeIds);

    private IReadOnlyList<string> AvailableProviderTags => providers
        .SelectMany(provider => provider.Tags)
        .Where(item => !string.IsNullOrWhiteSpace(item))
        .Distinct(StringComparer.OrdinalIgnoreCase)
        .OrderBy(item => item, StringComparer.OrdinalIgnoreCase)
        .ToList();

    protected override async Task OnInitializedAsync()
    {
        await LoadAsync();
    }

    private async Task LoadAsync()
    {
        isLoading = true;
        try
        {
            providers = await WorkspaceService.ListProvidersAsync();
            RefreshProviderTreeExpansionDefaults();

            if (providerModel.Id.HasValue &&
                providers.Any(item => item.Id == providerModel.Id.Value))
            {
                await EditProviderAsync(providerModel.Id.Value);
            }
            else if (providers.Count > 0)
            {
                await EditProviderAsync(providers[0].Id);
            }
        }
        catch (Exception exception)
        {
            NotificationService.Error("Provider catalog failed", exception.Message);
        }
        finally
        {
            isLoading = false;
        }
    }

    private Task HandleProviderTreeSelectAsync(string nodeId)
    {
        if (ProviderProfileTreeNodeBuilder.TryReadProviderId(nodeId, out var providerId) &&
            providers.Any(item => item.Id == providerId))
        {
            return EditProviderAsync(providerId);
        }

        return Task.CompletedTask;
    }

    private Task HandleProviderTreeToggleAsync(string nodeId)
    {
        if (!expandedProviderTreeNodeIds.Add(nodeId))
        {
            expandedProviderTreeNodeIds.Remove(nodeId);
        }

        return Task.CompletedTask;
    }

    private async Task EditProviderAsync(Guid providerId)
    {
        providerModel = await WorkspaceService.GetProviderEditorAsync(providerId);
        SyncProviderEditorText();
    }

    private async Task SaveProviderAsync()
    {
        isBusy = true;
        try
        {
            providerModel.SuggestedModels = ParseLines(suggestedModelsText).ToList();
            providerModel.Tags = providerTagValues.ToList();
            var providerId = await WorkspaceService.SaveProviderAsync(providerModel);
            providers = await WorkspaceService.ListProvidersAsync();
            RefreshProviderTreeExpansionDefaults();
            await EditProviderAsync(providerId);
            NotificationService.Success("Provider saved", "Provider profile saved.");
        }
        catch (Exception exception)
        {
            NotificationService.Error("Provider save failed", exception.Message);
        }
        finally
        {
            isBusy = false;
        }
    }

    private async Task TestProviderAsync(Guid providerId)
    {
        isBusy = true;
        try
        {
            var result = await WorkspaceService.TestProviderAsync(providerId);
            providers = await WorkspaceService.ListProvidersAsync();
            await EditProviderAsync(providerId);
            if (result.Success)
            {
                NotificationService.Success("Provider health check passed", result.Summary);
            }
            else
            {
                NotificationService.Warning("Provider health check failed", result.Summary);
            }
        }
        catch (Exception exception)
        {
            NotificationService.Error("Provider health check failed", exception.Message);
        }
        finally
        {
            isBusy = false;
        }
    }

    private async Task DeleteProviderAsync(Guid providerId)
    {
        isBusy = true;
        try
        {
            await WorkspaceService.DeleteProviderAsync(providerId);
            providers = await WorkspaceService.ListProvidersAsync();
            RefreshProviderTreeExpansionDefaults();
            if (providers.Count > 0)
            {
                await EditProviderAsync(providers[0].Id);
            }
            else
            {
                await ResetProviderAsync();
            }

            NotificationService.Success("Provider deleted", "Provider profile deleted.");
        }
        catch (Exception exception)
        {
            NotificationService.Error("Provider delete failed", exception.Message);
        }
        finally
        {
            isBusy = false;
        }
    }

    private Task ResetProviderAsync()
    {
        providerModel = CreateNewProviderEditor();
        SyncProviderEditorText();
        return Task.CompletedTask;
    }

    private Task HandleProviderTagsChangedAsync(IReadOnlyList<string> value)
    {
        providerTagValues = value;
        providerModel.Tags = value.ToList();
        return Task.CompletedTask;
    }

    private void ResetProviderSearch()
    {
        providerSearch = string.Empty;
    }

    private void RefreshProviderTreeExpansionDefaults()
    {
        var validTagNodeIds = ProviderProfileTreeNodeBuilder.BuildTagNodeIds(FilteredProviders).ToHashSet(StringComparer.OrdinalIgnoreCase);
        expandedProviderTreeNodeIds.RemoveWhere(nodeId => !validTagNodeIds.Contains(nodeId));
        knownProviderTagNodeIds.RemoveWhere(nodeId => !validTagNodeIds.Contains(nodeId));
        foreach (var tagNodeId in validTagNodeIds)
        {
            if (knownProviderTagNodeIds.Add(tagNodeId))
            {
                expandedProviderTreeNodeIds.Add(tagNodeId);
            }
        }
    }

    private bool MatchesProviderSearch(ProviderProfile provider)
    {
        if (string.IsNullOrWhiteSpace(providerSearch))
        {
            return true;
        }

        return provider.Name.Contains(providerSearch, StringComparison.OrdinalIgnoreCase) ||
               provider.Kind.ToString().Contains(providerSearch, StringComparison.OrdinalIgnoreCase) ||
               provider.Transport.ToString().Contains(providerSearch, StringComparison.OrdinalIgnoreCase) ||
               provider.DefaultModel.Contains(providerSearch, StringComparison.OrdinalIgnoreCase) ||
               provider.BaseUrl.Contains(providerSearch, StringComparison.OrdinalIgnoreCase) ||
               provider.Tags.Any(tag => tag.Contains(providerSearch, StringComparison.OrdinalIgnoreCase));
    }

    private void SyncProviderEditorText()
    {
        providerTagValues = providerModel.Tags
            .Where(item => !string.IsNullOrWhiteSpace(item))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(item => item, StringComparer.OrdinalIgnoreCase)
            .ToList();
        suggestedModelsText = string.Join(Environment.NewLine, providerModel.SuggestedModels);
    }

    private string ResolveSelectedProviderStatus()
    {
        if (!providerModel.Id.HasValue)
        {
            return "Draft provider profile.";
        }

        var provider = providers.FirstOrDefault(item => item.Id == providerModel.Id.Value);
        if (provider is null)
        {
            return "Provider is not loaded in the current catalog snapshot.";
        }

        return ProviderProfileDisplayAdapter.BuildStatusText(provider);
    }

    private static IReadOnlyList<string> ParseLines(string value)
    {
        return value
            .Split(['\r', '\n', ',', ';'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(item => !string.IsNullOrWhiteSpace(item))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static ProviderProfileEditorModel CreateNewProviderEditor()
    {
        return new ProviderProfileEditorModel
        {
            Name = "New OpenAI provider",
            Kind = ProviderKind.OpenAi,
            BaseUrl = ManagedSeedProviderFallbacks.OpenAiBaseUrl,
            ApiKeyEnvironmentVariable = "OPENAI_API_KEY",
            DefaultModel = ManagedSeedProviderFallbacks.OpenAiDefaultModel,
            Transport = ProviderTransportKind.Responses,
            Purpose = ProviderProfilePurpose.Chat,
            IsEnabled = true,
            SupportsStreaming = true,
            SupportsTools = true,
            SupportsBackgroundResponses = true,
            PreferFrameworkManagedChatHistory = false,
            ConfigurationJson = "{}",
            SuggestedModels = [ManagedSeedProviderFallbacks.OpenAiDefaultModel, "gpt-5.4", "gpt-4.1-mini"],
            Tags = ["openai", "cloud", "chat", "responses"]
        };
    }
}
