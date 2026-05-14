using CanDoItAll.Plugins.Abstractions;

namespace CanDoItAll.Modules.Plugins;

public sealed record PluginCapabilityGrantItem(
    PluginId PluginId,
    PluginCapabilityKind Capability,
    PluginHostToolRecipeId? RecipeId,
    PluginGrantScopeKind ScopeKind,
    string ScopeKey,
    PluginGrantState State,
    PluginGrantRiskKind RiskKind,
    string Reason,
    string UpdatedBy,
    DateTimeOffset? CreatedAtUtc,
    DateTimeOffset? UpdatedAtUtc,
    Guid? ConcurrencyToken);

public sealed record PluginGrantUpdateRequest(
    PluginCapabilityKind Capability,
    PluginGrantState State,
    string? RecipeId = null,
    PluginGrantScopeKind ScopeKind = PluginGrantScopeKind.Plugin,
    string ScopeKey = "",
    PluginGrantRiskKind RiskKind = PluginGrantRiskKind.Medium,
    string Reason = "");

public sealed record PluginConnectionItem(
    PluginConnectionId Id,
    PluginId PluginId,
    PluginConnectionKey ConnectionKey,
    string DisplayName,
    string SettingsJson,
    bool IsEnabled,
    string HealthStatus,
    string UpdatedBy,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset UpdatedAtUtc,
    Guid ConcurrencyToken);

public sealed record PluginConnectionSaveRequest(
    PluginConnectionId? Id,
    PluginConnectionKey ConnectionKey,
    string DisplayName,
    string SettingsJson,
    bool IsEnabled = true);

public sealed record PluginSettingsDetail(
    PluginCatalogItem CatalogItem,
    IReadOnlyList<PluginCapabilityGrantItem> Grants,
    IReadOnlyList<PluginConnectionItem> Connections,
    IReadOnlyList<PluginHostToolRecipeDescriptor> HostToolRecipes,
    IReadOnlyList<PluginConnectionDescriptor> ConnectionDescriptors,
    PluginOAuth2Descriptor? OAuth2);

public sealed record PluginHostToolRecipeDescriptor(
    PluginHostToolRecipeId RecipeId,
    string DisplayName,
    string Description,
    PluginGrantRiskKind RiskKind,
    bool MutatesHost);
