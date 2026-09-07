namespace CanDoItAll.AgentFramework.UiSandbox;

public enum SandboxSpecimen {
    Catalog,
    Capabilities,
    Overview
}

public static class SandboxSpecimens {
    public static SandboxSpecimen Parse(string? token) => token?.Trim().ToLowerInvariant() switch {
        "capabilities" => SandboxSpecimen.Capabilities,
        "overview" => SandboxSpecimen.Overview,
        _ => SandboxSpecimen.Catalog
    };
}
