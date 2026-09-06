namespace CanDoItAll.Modules.AgentFramework.ProviderManagement;

public static class SharedProviderLocalAliasPolicy {
    public static string Normalize(string alias) {
        ArgumentException.ThrowIfNullOrWhiteSpace(alias);
        var normalized = alias.Trim();
        if (normalized.Length > 200 || normalized.Any(char.IsControl)) {
            throw new ArgumentException("The local provider alias must contain at most 200 visible characters.", nameof(alias));
        }
        return normalized;
    }
}
