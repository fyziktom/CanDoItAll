using CanDoItAll.RemoteInstances;

namespace CanDoItAll.AgentFramework.Models;

public static class AgentSpecialTags
{
    public const string Favorite = "favorite";

    public static bool IsFavorite(string tag)
        => string.Equals(tag, Favorite, StringComparison.OrdinalIgnoreCase);

    public static IReadOnlyList<string> NormalizeCustom(IEnumerable<string>? tags) => DefinitionSharingTags.Custom(tags)
        .Where(tag => !string.IsNullOrWhiteSpace(tag)).Select(tag => tag.Trim())
        .Distinct(StringComparer.OrdinalIgnoreCase).ToArray();

    public static IReadOnlyList<string> Visible(AgentDefinition agent) => DefinitionSharingTags.Project(
        NormalizeCustom(agent.Tags).Where(tag => !IsFavorite(tag)).OrderBy(tag => tag, StringComparer.OrdinalIgnoreCase),
        agent.ApiAvailable, agent.SharesRemoteTemplate);
}
