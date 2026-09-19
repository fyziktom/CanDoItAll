using System.Text.Json.Serialization;

namespace CanDoItAll.Plugins.Abstractions;

/// <summary>
/// Identifier of a host tool recipe, a reviewed host command that a plugin may run, for example
/// <c>docker.list-containers</c>. On the wire it is a JSON string of letters, digits, <c>.</c>, <c>-</c> and
/// <c>_</c>, lower-cased by the server.
/// </summary>
[JsonConverter(typeof(PluginHostToolRecipeIdJsonConverter))]
public readonly record struct PluginHostToolRecipeId
{
    public PluginHostToolRecipeId(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("Plugin host-tool recipe id cannot be empty.", nameof(value));
        }

        Value = PluginId.NormalizeIdentifier(value, nameof(value));
    }

    public string Value { get; }

    public override string ToString() => Value;
}

public static class PluginHostToolRecipeIds
{
    public static PluginHostToolRecipeId DockerListContainers { get; } = new("docker.list-containers");

    public static PluginHostToolRecipeId DockerPullImage { get; } = new("docker.pull-image");

    public static PluginHostToolRecipeId DockerStartContainer { get; } = new("docker.start-container");

    public static PluginHostToolRecipeId DockerReadLogs { get; } = new("docker.read-logs");

    public static PluginHostToolRecipeId PowerShellReviewedScript { get; } = new("powershell.reviewed-script");
}

/// <summary>
/// Decision state of a plugin capability grant, as a JSON integer: 0 Requested (not decided yet; reported for
/// declared capabilities without a saved decision), 1 Granted (allowed), 2 Denied (refused), 3 Revoked (withdrawn after
/// being granted), 4 Unavailable (the saved state could not be read). Only Granted allows the capability.
/// </summary>
public enum PluginGrantState
{
    Requested,
    Granted,
    Denied,
    Revoked,
    Unavailable
}

/// <summary>
/// Scope of a plugin capability grant, as a JSON integer: 0 Plugin (the whole plugin; the only scope the runtime
/// evaluates), 1 Connection, 2 Workflow. Grants with scope 1 or 2 are stored and listed but not currently evaluated.
/// </summary>
public enum PluginGrantScopeKind
{
    Plugin,
    Connection,
    Workflow
}

/// <summary>
/// Risk rating of a plugin capability grant, as a JSON integer: 0 Low, 1 Medium, 2 High. Informational: it does not
/// change how the grant is evaluated.
/// </summary>
public enum PluginGrantRiskKind
{
    Low,
    Medium,
    High
}

public enum PluginGrantDecisionKind
{
    Allowed,
    PluginNotInstalled,
    PluginDisabled,
    CapabilityNotDeclared,
    GrantMissing,
    GrantDenied,
    GrantRevoked,
    RecipeGrantMissing,
    RecipeGrantDenied,
    RecipeGrantRevoked,
    ConnectionMissing,
    RecipeUnavailable,
    PolicyRejected
}

public sealed record PluginGrantDecision(
    PluginId PluginId,
    PluginCapabilityKind Capability,
    PluginHostToolRecipeId? RecipeId,
    PluginGrantDecisionKind Kind,
    bool Allowed,
    string Message)
{
    public static PluginGrantDecision Allow(
        PluginId pluginId,
        PluginCapabilityKind capability,
        PluginHostToolRecipeId? recipeId = null)
        => new(pluginId, capability, recipeId, PluginGrantDecisionKind.Allowed, true, "Plugin capability is granted.");

    public static PluginGrantDecision Deny(
        PluginId pluginId,
        PluginCapabilityKind capability,
        PluginGrantDecisionKind kind,
        string message,
        PluginHostToolRecipeId? recipeId = null)
        => new(pluginId, capability, recipeId, kind, false, message);
}
