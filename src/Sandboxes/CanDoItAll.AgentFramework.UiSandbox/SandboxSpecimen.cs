namespace CanDoItAll.AgentFramework.UiSandbox;

public enum SandboxSpecimen {
    Catalog,
    Capabilities,
    Overview,
    Governance,
    Diagnostics,
    History
}

public static class SandboxSpecimens {
    public static SandboxSpecimen Parse(string? token) => token?.Trim().ToLowerInvariant() switch {
        "capabilities" => SandboxSpecimen.Capabilities,
        "overview" => SandboxSpecimen.Overview,
        "history" => SandboxSpecimen.History,
        "diagnostics" => SandboxSpecimen.Diagnostics,
        "governance" => SandboxSpecimen.Governance,
        _ => SandboxSpecimen.Catalog
    };
}
