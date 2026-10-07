using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Plugins.Abstractions;

namespace CanDoItAll.Modules.Plugins;

/// <summary>
/// A capability grant of a plugin: the saved decision whether the plugin may use one capability, or a synthesized
/// undecided entry for a declared capability without a decision.
/// </summary>
/// <param name="PluginId">Identifier of the plugin.</param>
/// <param name="Capability">
/// The capability, as a JSON integer (a single flag): 1 WorkflowExecutor, 2 SettingsRenderer, 4 SecretReference,
/// 8 WorkspaceFiles, 16 Storage, 32 ProjectStructure, 64 HttpClient, 128 OAuth2, 256 ExecutionEvents, 512 HostCommand.
/// </param>
/// <param name="RecipeId">
/// Host tool recipe that the grant covers, for HostCommand (512) grants of a single recipe; null for a
/// capability-level grant.
/// </param>
/// <param name="ScopeKind">
/// Scope of the grant, as a JSON integer: 0 Plugin (evaluated), 1 Connection, 2 Workflow (stored, not evaluated).
/// </param>
/// <param name="ScopeKey">Key within the scope; empty for plugin-scope grants.</param>
/// <param name="State">
/// Decision, as a JSON integer: 0 Requested (undecided), 1 Granted, 2 Denied, 3 Revoked, 4 Unavailable (unreadable
/// saved state). Only 1 allows the capability.
/// </param>
/// <param name="RiskKind">Risk rating, as a JSON integer: 0 Low, 1 Medium, 2 High. Informational.</param>
/// <param name="Reason">Reason recorded with the decision.</param>
/// <param name="UpdatedBy">Actor that made the decision, for example <c>api</c>; empty for an undecided entry.</param>
/// <param name="CreatedAtUtc">When the decision was first saved; null for an undecided entry.</param>
/// <param name="UpdatedAtUtc">When the decision last changed; null for an undecided entry.</param>
/// <param name="ConcurrencyToken">
/// Version stamp that changes with every save; null for an undecided entry. The API does not use it for concurrency
/// checks.
/// </param>
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

/// <summary>
/// Body of <c>PUT /api/plugins/{pluginId}/grants</c>: one grant decision. The grant is identified by
/// <c>capability</c>, <c>recipeId</c>, <c>scopeKind</c> and <c>scopeKey</c>; saving creates it or replaces the
/// decision of the existing grant with the same identity.
/// </summary>
/// <param name="Capability">
/// The capability, as a JSON integer; send a single flag: 1 WorkflowExecutor, 2 SettingsRenderer, 4 SecretReference,
/// 8 WorkspaceFiles, 16 Storage, 32 ProjectStructure, 64 HttpClient, 128 OAuth2, 256 ExecutionEvents, 512 HostCommand.
/// A combined value is stored but never matches at runtime.
/// </param>
/// <param name="State">
/// Decision, as a JSON integer: 1 Granted, 2 Denied or 3 Revoked. 0 Requested and 4 Unavailable are rejected.
/// </param>
/// <param name="RecipeId">
/// Host tool recipe identifier, for example <c>docker.list-containers</c>, to decide one recipe of HostCommand
/// (512); a recipe also needs the capability-level HostCommand grant. Omitted or null for a capability-level grant.
/// </param>
/// <param name="ScopeKind">
/// Scope, as a JSON integer: 0 Plugin (default; the only scope evaluated at runtime), 1 Connection, 2 Workflow.
/// </param>
/// <param name="ScopeKey">Key within the scope; leave it empty (the default) for plugin-scope grants.</param>
/// <param name="RiskKind">Risk rating to record, as a JSON integer: 0 Low, 1 Medium (default), 2 High.</param>
/// <param name="Reason">Reason for the decision; optional.</param>
public sealed record PluginGrantUpdateRequest(
    PluginCapabilityKind Capability,
    PluginGrantState State,
    string? RecipeId = null,
    PluginGrantScopeKind ScopeKind = PluginGrantScopeKind.Plugin,
    string ScopeKey = "",
    PluginGrantRiskKind RiskKind = PluginGrantRiskKind.Medium,
    string Reason = "");

/// <summary>
/// A saved connection of a plugin: which connection kind it uses and its settings. OAuth tokens are not part of it.
/// </summary>
/// <param name="Id">Identifier of the connection.</param>
/// <param name="PluginId">Identifier of the plugin.</param>
/// <param name="ConnectionKey">Key of the connection kind, for example <c>gmail</c>.</param>
/// <param name="DisplayName">Display name of the connection.</param>
/// <param name="SettingsJson">
/// Connection settings as JSON text (a string that contains a JSON object), returned exactly as saved; for example
/// OAuth connections read <c>clientId</c> and <c>redirectUri</c> from it.
/// </param>
/// <param name="IsEnabled">
/// False excludes the connection from automatic selection by workflows and from token use.
/// </param>
/// <param name="HealthStatus">
/// Health text of the connection; <c>Not checked</c> by default, and the plugin API does not update it.
/// </param>
/// <param name="UpdatedBy">Actor of the last change, for example <c>api</c>.</param>
/// <param name="CreatedAtUtc">When the connection was created.</param>
/// <param name="UpdatedAtUtc">When the connection last changed.</param>
/// <param name="ConcurrencyToken">
/// Version stamp that changes with every save. The API does not use it for concurrency checks.
/// </param>
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

/// <summary>
/// Body of <c>POST /api/plugins/{pluginId}/connections</c>: creates a connection or replaces the settings of an
/// existing one.
/// </summary>
/// <param name="Id">
/// Identifier of the connection to update; null creates a new connection. An identifier that no connection of this
/// plugin has creates a connection with that identifier.
/// </param>
/// <param name="ConnectionKey">
/// Key of the connection kind, for example <c>gmail</c>. Used only when the connection is created; an update keeps the
/// saved key.
/// </param>
/// <param name="DisplayName">Display name; required, must not be blank.</param>
/// <param name="SettingsJson">
/// Settings as JSON text: a string that contains a JSON object such as <c>{"clientId":"example-client"}</c>, not a
/// nested object. It is stored as sent (surrounding whitespace removed; blank means <c>{}</c>) and not checked against
/// the connection kind's settings form. Do not put secrets in it: it is returned unredacted.
/// </param>
/// <param name="IsEnabled">True (default) makes the connection usable; false disables it.</param>
public sealed record PluginConnectionSaveRequest(
    PluginConnectionId? Id,
    PluginConnectionKey ConnectionKey,
    string DisplayName,
    string SettingsJson,
    bool IsEnabled = true);

/// <summary>
/// Settings view of one plugin, returned by <c>GET /api/plugins/{pluginId}/settings</c>.
/// </summary>
/// <param name="CatalogItem">The plugin's catalog entry, as in <c>GET /api/plugins/catalog</c>.</param>
/// <param name="Grants">
/// Effective grants: one entry per declared capability and host tool recipe (undecided entries are synthesized with
/// state 0 Requested), followed by any other saved grants.
/// </param>
/// <param name="Connections">Saved connections, ordered by connection key and then display name.</param>
/// <param name="HostToolRecipes">Host tool recipes that the plugin can request; empty for most plugins.</param>
/// <param name="ConnectionDescriptors">Connection kinds that the plugin declares.</param>
/// <param name="OAuth2">OAuth 2.0 configuration of the plugin; null when it uses no OAuth connection.</param>
public sealed record PluginSettingsDetail(
    PluginCatalogItem CatalogItem,
    IReadOnlyList<PluginCapabilityGrantItem> Grants,
    IReadOnlyList<PluginConnectionItem> Connections,
    IReadOnlyList<PluginHostToolRecipeDescriptor> HostToolRecipes,
    IReadOnlyList<PluginConnectionDescriptor> ConnectionDescriptors,
    PluginOAuth2Descriptor? OAuth2);

/// <summary>
/// A host tool recipe: a reviewed host command that a plugin with the HostCommand capability may run once it is
/// granted.
/// </summary>
/// <param name="RecipeId">Identifier of the recipe, for example <c>docker.list-containers</c>.</param>
/// <param name="DisplayName">Display name of the recipe.</param>
/// <param name="Description">What the recipe does.</param>
/// <param name="RiskKind">Risk rating, as a JSON integer: 0 Low, 1 Medium, 2 High.</param>
/// <param name="MutatesHost">True when the recipe changes the host, for example by starting a container.</param>
public sealed record PluginHostToolRecipeDescriptor(
    PluginHostToolRecipeId RecipeId,
    string DisplayName,
    string Description,
    PluginGrantRiskKind RiskKind,
    bool MutatesHost);

/// <summary>
/// Stream of a plugin log entry, as a JSON integer: 0 Installation (package and plugin lifecycle), 1 Runtime
/// (workflow executor runs and plugin events).
/// </summary>
public enum PluginLogStreamKind
{
    Installation,
    Runtime
}

/// <summary>
/// Operation recorded by a plugin log entry, as a JSON integer: 0 PackageUpload, 1 PackageValidation,
/// 2 PackageInstall, 3 PluginInstall, 4 PluginEnable, 5 PluginDisable, 6 RestartRequired, 7 RuntimeActivation,
/// 8 ExecutorStarted, 9 ExecutorCompleted, 10 ExecutorFailed, 11 PluginEvent.
/// </summary>
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

/// <summary>
/// Severity of a plugin log entry, as a JSON integer: 0 Information, 1 Warning, 2 Error.
/// </summary>
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

/// <summary>
/// One entry of the plugin installation and runtime log, returned by <c>GET /api/plugins/logs</c>.
/// </summary>
/// <param name="Id">Identifier of the log entry.</param>
/// <param name="StreamKind">Stream, as a JSON integer: 0 Installation, 1 Runtime.</param>
/// <param name="OperationKind">
/// Recorded operation, as a JSON integer: 0 PackageUpload, 1 PackageValidation, 2 PackageInstall, 3 PluginInstall,
/// 4 PluginEnable, 5 PluginDisable, 6 RestartRequired, 7 RuntimeActivation, 8 ExecutorStarted, 9 ExecutorCompleted,
/// 10 ExecutorFailed, 11 PluginEvent.
/// </param>
/// <param name="Severity">Severity, as a JSON integer: 0 Information, 1 Warning, 2 Error.</param>
/// <param name="Status">
/// Short status text, for example <c>Installed</c>, <c>Failed</c>, <c>Enabled</c> or <c>Completed</c>; at most 80
/// characters.
/// </param>
/// <param name="Message">Log message with secrets redacted; at most 1,200 characters.</param>
/// <param name="DetailsJson">
/// Details as JSON text (a string) with secrets replaced by <c>[REDACTED]</c>; at most 8,000 characters, and text cut
/// at that limit ends with <c>...[TRUNCATED]</c> and may not parse as JSON.
/// </param>
/// <param name="PluginId">Identifier of the plugin concerned; null when the entry concerns none.</param>
/// <param name="PackageId">Identifier of the package concerned; null when the entry concerns none.</param>
/// <param name="WorkflowExecutorId">
/// Identifier of the workflow executor concerned, for example <c>gmail.messages-by-label</c>; null when none.
/// </param>
/// <param name="CorrelationId">Correlation identifier of the operation that wrote the entry, for log searches.</param>
/// <param name="CreatedAtUtc">When the entry was written.</param>
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
