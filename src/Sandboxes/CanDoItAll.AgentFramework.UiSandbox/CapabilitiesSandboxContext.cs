using System.Collections.Immutable;
using CanDoItAll.AgentFramework.UI.Capabilities;

namespace CanDoItAll.AgentFramework.UiSandbox;

public enum CapabilitiesSandboxScenario {
    Baseline,
    Loading,
    Failed,
    MissingTarget,
    NoAgents,
    NoCapabilities,
    Selected,
    KindsAndProof,
    LongContent,
    AssignmentPending,
    AssignmentRejected,
    AssignmentConflict,
    CommittedWarning,
    Unconfirmed,
    ExactBefore,
    Intervening,
    VerificationPending,
    VerificationSuperseded,
    VerificationRecovery,
    DiagnosticAcknowledgement,
    PreviewBusy,
    PreviewValid,
    PreviewInvalid,
    CuratorAvailable,
    CuratorUnavailable,
    CuratorPending,
    CuratorOpened,
    CuratorUnconfirmed,
    CuratorAcknowledged
}

public sealed record CapabilitiesSandboxScenarioDefinition(
    CapabilitiesSandboxScenario Scenario, string Token, string Label);

public sealed record CapabilitiesSandboxContext(
    CapabilitiesSandboxScenario Scenario = CapabilitiesSandboxScenario.Baseline,
    CatalogSandboxLayout Layout = CatalogSandboxLayout.Matched,
    Guid? AgentId = null) {
    public static ImmutableArray<CapabilitiesSandboxScenarioDefinition> Scenarios { get; } = [
        new(CapabilitiesSandboxScenario.Baseline, "baseline", "Production baseline"),
        new(CapabilitiesSandboxScenario.Loading, "loading", "Loading"),
        new(CapabilitiesSandboxScenario.Failed, "failed", "Failed read"),
        new(CapabilitiesSandboxScenario.MissingTarget, "missing-target", "Missing target"),
        new(CapabilitiesSandboxScenario.NoAgents, "no-agents", "No agents"),
        new(CapabilitiesSandboxScenario.NoCapabilities, "no-capabilities", "No capabilities"),
        new(CapabilitiesSandboxScenario.Selected, "selected", "Selected agent"),
        new(CapabilitiesSandboxScenario.KindsAndProof, "kinds-and-proof", "Kinds and proof"),
        new(CapabilitiesSandboxScenario.LongContent, "long-content", "Long content"),
        new(CapabilitiesSandboxScenario.AssignmentPending, "assignment-pending", "Assignment pending"),
        new(CapabilitiesSandboxScenario.AssignmentRejected, "assignment-rejected", "Assignment rejected"),
        new(CapabilitiesSandboxScenario.AssignmentConflict, "assignment-conflict", "Assignment conflict"),
        new(CapabilitiesSandboxScenario.CommittedWarning, "committed-warning", "Committed warning"),
        new(CapabilitiesSandboxScenario.Unconfirmed, "unconfirmed", "Unknown assignment"),
        new(CapabilitiesSandboxScenario.ExactBefore, "exact-before", "Exact before / deliberate retry"),
        new(CapabilitiesSandboxScenario.Intervening, "intervening", "Intervening / adopt"),
        new(CapabilitiesSandboxScenario.VerificationPending, "verification-pending", "Verification pending"),
        new(CapabilitiesSandboxScenario.VerificationSuperseded, "verification-superseded", "Verification superseded"),
        new(CapabilitiesSandboxScenario.VerificationRecovery, "verification-recovery", "Receipt-backed recovery"),
        new(CapabilitiesSandboxScenario.DiagnosticAcknowledgement, "diagnostic-acknowledgement", "Receipt-less acknowledgement"),
        new(CapabilitiesSandboxScenario.PreviewBusy, "preview-busy", "Preview busy"),
        new(CapabilitiesSandboxScenario.PreviewValid, "preview-valid", "Preview valid"),
        new(CapabilitiesSandboxScenario.PreviewInvalid, "preview-invalid", "Preview invalid"),
        new(CapabilitiesSandboxScenario.CuratorAvailable, "curator-available", "Curator available"),
        new(CapabilitiesSandboxScenario.CuratorUnavailable, "curator-unavailable", "Curator unavailable"),
        new(CapabilitiesSandboxScenario.CuratorPending, "curator-pending", "Curator pending"),
        new(CapabilitiesSandboxScenario.CuratorOpened, "curator-opened", "Curator opened"),
        new(CapabilitiesSandboxScenario.CuratorUnconfirmed, "curator-unconfirmed", "Curator unknown"),
        new(CapabilitiesSandboxScenario.CuratorAcknowledged, "curator-acknowledged", "Curator acknowledged")
    ];

    public AgentCapabilitiesSelection Selection => new(AgentId);

    public static CapabilitiesSandboxContext Parse(string? scenario, string? layout, string? agentId) {
        var definition = Scenarios.FirstOrDefault(item =>
            string.Equals(item.Token, scenario?.Trim(), StringComparison.OrdinalIgnoreCase));
        var selectedScenario = definition?.Scenario ?? CapabilitiesSandboxScenario.Baseline;
        Guid? target = string.IsNullOrWhiteSpace(agentId) ? CapabilitiesSandboxFixture.AlphaId
            : Guid.TryParse(agentId, out var id) ? id : Guid.Empty;
        if (selectedScenario == CapabilitiesSandboxScenario.MissingTarget && string.IsNullOrWhiteSpace(agentId)) {
            target = CapabilitiesSandboxFixture.MissingId;
        }
        if (selectedScenario == CapabilitiesSandboxScenario.NoAgents) {
            target = null;
        }
        return new(selectedScenario,
            string.Equals(layout?.Trim(), "flexible", StringComparison.OrdinalIgnoreCase)
                ? CatalogSandboxLayout.Flexible : CatalogSandboxLayout.Matched, target);
    }

    public IReadOnlyDictionary<string, object?> ToQuery() => new Dictionary<string, object?> {
        ["specimen"] = "capabilities",
        ["scenario"] = Scenarios.Single(item => item.Scenario == Scenario).Token,
        ["layout"] = Layout == CatalogSandboxLayout.Matched ? "matched" : "flexible",
        ["agentId"] = AgentId,
        ["teamId"] = null
    };
}
