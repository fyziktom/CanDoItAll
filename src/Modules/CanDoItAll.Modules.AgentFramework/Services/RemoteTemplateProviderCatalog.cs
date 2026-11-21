using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.Providers;
using CanDoItAll.Modules.AgentFramework.ProviderManagement;
using CanDoItAll.SharedProviders.Abstractions;
using ProviderPurpose = CanDoItAll.AgentFramework.Models.ProviderProfilePurpose;

namespace CanDoItAll.Modules.AgentFramework;

public sealed record RemoteTemplateModelOption(RemoteTemplateModelChoice Choice, string ProviderName, string DisplayName,
    ProviderPurpose Purpose, bool FromSource, bool IsDefault, bool StructuredOutput, bool Vision, bool Tools) {
    public IReadOnlyList<AgentReasoningEffortLevel> AllowedThinkingEfforts { get; init; } = [];
    public bool HasModelPrice { get; init; }

    public bool Supports(RemoteTemplateModelRequirement requirement) =>
        Purpose == requirement.Purpose && (!requirement.StructuredOutput || StructuredOutput) &&
        (!requirement.Vision || Vision) && (!requirement.Tools || Tools) && (!requirement.RequiresModelPrice || HasModelPrice) &&
        (requirement.ThinkingEffort is not { } effort || AllowedThinkingEfforts.Contains(effort));
}

public sealed class RemoteTemplateProviderCatalog(IProviderProfileRegistry providers, IProviderProfileService features,
    ISharedProviderManagementService sharing, IProviderModelCapabilityResolver capabilities) {
    public async Task<RemoteTemplateModelRequirement> DescribeAsync(RemoteTemplateModelRequirement requirement,
        Guid? providerId, string? model, CancellationToken token) {
        if (providerId is not { } id || await providers.GetProviderAsync(id, token) is not { } profile) {
            return requirement with { ModelName = string.IsNullOrWhiteSpace(model) ? null : model };
        }
        var name = string.IsNullOrWhiteSpace(model) ? profile.DefaultModel : model;
        var state = await sharing.GetProfileSharingAsync(id, token);
        var routing = state.Publication is { IsPublished: true } publication && state.Eligibility is { IsEligible: true } eligibility &&
            eligibility.Models.Any(item => item.UpstreamModelId == name)
            ? SharedProviderRoutingModelIdCodec.Create(publication.PublicId, name).Value : null;
        return requirement with { ModelName = profile.GetModelDisplayName(name), PublishedModel = routing };
    }

    public async Task<IReadOnlyList<RemoteTemplateModelOption>> ListAsync(Guid sourceId, Guid publisherId, CancellationToken token, RemoteTemplateKind? kind = null) {
        var source = await sharing.FindSourceAsync(sourceId, token);
        var imports = source is { Source.IsEnabled: true } && source.Source.RemoteInstanceId?.Value == publisherId
            ? source.Imports.Where(item => item.IsEnabled && item.SelectionState == SharedProviderSelectionState.Selected &&
                item.AvailabilityState == SharedProviderAvailabilityState.Available).ToDictionary(item => item.ProviderProfileId) : [];
        var options = new List<RemoteTemplateModelOption>();
        foreach (var profile in await providers.ListProvidersAsync(token)) {
            if (!profile.IsEnabled || profile.IsSourceManaged && !imports.ContainsKey(profile.Id)) {
                continue;
            }
            var models = profile.IsSourceManaged ? imports[profile.Id].Models.Select(item => item.Id.Value) :
                profile.SuggestedModels.Prepend(profile.DefaultModel).Concat(profile.ModelCatalog.Select(item => item.Id));
            foreach (var model in models.Where(item => !string.IsNullOrWhiteSpace(item)).Distinct(StringComparer.Ordinal)) {
                if (kind == RemoteTemplateKind.Agent && !profile.IsSourceManaged && !ProviderModelSelectionPolicy.CanPinLocalAgentModel(profile, model)) {
                    continue;
                }
                var matrix = features.ResolveFeatureMatrixForModel(profile, model);
                var option = new RemoteTemplateModelOption(new(profile.Id, model), profile.Name, profile.GetModelDisplayName(model), profile.Purpose,
                    profile.IsSourceManaged, model == profile.DefaultModel, matrix.SupportsStructuredOutput, matrix.SupportsVision, matrix.SupportsTools) {
                    HasModelPrice = ProviderPricingDefaults.TryFindPrice(profile.ModelPrices, model, out _)
                };
                if (kind == RemoteTemplateKind.SimpleChat && profile.Purpose == ProviderPurpose.Chat &&
                    capabilities.ResolveThinkingEffort(profile, model) is { Status: AgentThinkingEffortSupportStatus.Supported } capability) {
                    option = option with { AllowedThinkingEfforts = capability.AllowedEfforts };
                }
                options.Add(option);
            }
        }
        return options.OrderByDescending(item => item.FromSource).ThenBy(item => item.ProviderName, StringComparer.OrdinalIgnoreCase)
            .ThenByDescending(item => item.IsDefault).ThenBy(item => item.DisplayName, StringComparer.OrdinalIgnoreCase).ToArray();
    }

    public static RemoteTemplateModelChoice? Preferred(RemoteTemplateModelRequirement requirement, IReadOnlyList<RemoteTemplateModelOption> options) {
        var candidates = options.Where(option => option.FromSource && option.Supports(requirement));
        var matches = requirement.PublishedModel is { Length: > 0 } route ? candidates.Where(option => option.Choice.Model == route) :
            string.IsNullOrWhiteSpace(requirement.ModelName) ? candidates.Where(option => option.IsDefault) :
            candidates.Where(option => string.Equals(option.DisplayName, requirement.ModelName, StringComparison.Ordinal));
        var unique = matches.Take(2).ToArray();
        return unique.Length == 1 ? unique[0].Choice : null;
    }
}
