namespace CanDoItAll.AgentFramework.UiSandbox;

public enum SandboxSpecimen {
    Catalog,
    Capabilities
}

public static class SandboxSpecimens {
    public static SandboxSpecimen Parse(string? token) =>
        string.Equals(token?.Trim(), "capabilities", StringComparison.OrdinalIgnoreCase)
            ? SandboxSpecimen.Capabilities : SandboxSpecimen.Catalog;
}
