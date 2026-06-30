using CanDoItAll.AgentFramework.Models;
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

public enum PluginLogStreamKind
{
    Installation,
    Runtime
}

public enum PluginLogOperationKind
{
    PackageUpload,
    PackageValidation,
    PackageInstall,
    PluginInstall,
    PluginEnable,
    PluginDisable,
    RestartRequired,
    RuntimeActivation,
    ExecutorStarted,
    ExecutorCompleted,
    ExecutorFailed,
    PluginEvent
}

public enum PluginLogSeverity
{
    Information,
    Warning,
    Error
}

public sealed record PluginLogWriteRequest(
    PluginLogStreamKind StreamKind,
    PluginLogOperationKind OperationKind,
    PluginLogSeverity Severity,
    string Status,
    string Message,
    string DetailsJson = "{}",
    PluginId? PluginId = null,
    PluginPackageId? PackageId = null,
    WorkflowExecutorId? WorkflowExecutorId = null,
    string CorrelationId = "");

public sealed record PluginLogQuery(
    PluginLogStreamKind? StreamKind = null,
    PluginId? PluginId = null,
    PluginPackageId? PackageId = null,
    PluginLogSeverity? MinimumSeverity = null,
    int Take = 100);

public sealed record PluginLogItem(
    Guid Id,
    PluginLogStreamKind StreamKind,
    PluginLogOperationKind OperationKind,
    PluginLogSeverity Severity,
    string Status,
    string Message,
    string DetailsJson,
    PluginId? PluginId,
    PluginPackageId? PackageId,
    WorkflowExecutorId? WorkflowExecutorId,
    string CorrelationId,
    DateTimeOffset CreatedAtUtc);
