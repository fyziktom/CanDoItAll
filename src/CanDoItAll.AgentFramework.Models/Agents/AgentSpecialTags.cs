namespace CanDoItAll.AgentFramework.Models;

public static class AgentSpecialTags
{
    public const string Favorite = "favorite";

    public static bool IsFavorite(string tag)
        => string.Equals(tag, Favorite, StringComparison.OrdinalIgnoreCase);
}
