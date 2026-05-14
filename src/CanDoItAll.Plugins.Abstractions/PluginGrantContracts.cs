using System.Text.Json.Serialization;

namespace CanDoItAll.Plugins.Abstractions;

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

public enum PluginGrantState
{
    Requested,
    Granted,
    Denied,
    Revoked,
    Unavailable
}

public enum PluginGrantScopeKind
{
    Plugin,
    Connection,
    Workflow
}

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
