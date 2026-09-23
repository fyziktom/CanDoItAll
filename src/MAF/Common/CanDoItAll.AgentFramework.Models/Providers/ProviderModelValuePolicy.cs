namespace CanDoItAll.AgentFramework.Models;

public static class ProviderModelValuePolicy {
    public static string Normalize(string? value) => value?.Trim() ?? string.Empty;
}
