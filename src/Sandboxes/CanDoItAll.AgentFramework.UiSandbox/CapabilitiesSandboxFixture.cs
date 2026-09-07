using System.Collections.Immutable;
using System.Text.Json;
using System.Text.Json.Serialization;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.UI.Capabilities;

namespace CanDoItAll.AgentFramework.UiSandbox;

public sealed record CapabilitiesSandboxPresentation(
    AgentCapabilitiesSnapshot Snapshot, AgentCapabilitiesLoadState LoadState);

public static class CapabilitiesSandboxFixture {
    public static Guid AlphaId { get; } = Guid.Parse("9ba4d415-cc9c-4e0e-b03a-cc0000000001");
    public static Guid BetaId { get; } = Guid.Parse("9ba4d415-cc9c-4e0e-b03a-cc0000000002");
    public static Guid MissingId { get; } = Guid.Parse("ffffffff-ffff-ffff-ffff-ffffffffffff");
    public static Guid AttemptId { get; } = Guid.Parse("9ba4d415-cc9c-4e0e-b03a-cc0000000091");
    public static Guid CuratorAttemptId { get; } = Guid.Parse("9ba4d415-cc9c-4e0e-b03a-cc0000000092");

    private static AgentCapabilitiesSnapshot Baseline { get; } = LoadBaseline();
    private static ImmutableArray<CapabilityCatalogItem> Cards { get; } = [
        Card("9ba4d415-cc9c-4e0e-b03a-cc0000000011", CapabilityKind.McpServer, "MCP inspection", CapabilityProofStatus.Verified, "connector"),
        Card("9ba4d415-cc9c-4e0e-b03a-cc0000000012", CapabilityKind.Skill, "Review skill", CapabilityProofStatus.NotRun, "review"),
        Card("9ba4d415-cc9c-4e0e-b03a-cc0000000013", CapabilityKind.Tool, "Workspace tool", CapabilityProofStatus.PendingReview, "review"),
        Card("9ba4d415-cc9c-4e0e-b03a-cc0000000014", CapabilityKind.Plugin, "Plugin inspection", CapabilityProofStatus.Failed, "connector"),
        Card("9ba4d415-cc9c-4e0e-b03a-cc0000000015", CapabilityKind.Rag, "Reference retrieval", CapabilityProofStatus.Verified, "knowledge"),
        Card("9ba4d415-cc9c-4e0e-b03a-cc0000000016", CapabilityKind.AiContext, "Context guidance", CapabilityProofStatus.NotRun, "knowledge"),
        Card("9ba4d415-cc9c-4e0e-b03a-cc0000000017", CapabilityKind.Memory, "Memory inspection", CapabilityProofStatus.PendingReview, "knowledge")
    ];

    public static CapabilitiesSandboxPresentation Create(CapabilitiesSandboxContext context) {
        var snapshot = context.Scenario == CapabilitiesSandboxScenario.Baseline ? Baseline : new AgentCapabilitiesSnapshot(
            [new(AlphaId, "Capabilities Alpha", "Rendering fixture", "Sample model", 1),
             new(BetaId, "Capabilities Beta", "Unassigned fixture", "", 0)],
            Cards, [Cards[0].Id], Baseline.Curator);
        if (context.AgentId == BetaId) {
            snapshot = snapshot with { SelectedCapabilityIds = [] };
        }
        if (context.Scenario == CapabilitiesSandboxScenario.NoAgents) {
            return new(AgentCapabilitiesSnapshot.Empty, AgentCapabilitiesLoadState.Ready);
        }
        if (context.Scenario == CapabilitiesSandboxScenario.Loading) {
            return new(snapshot, AgentCapabilitiesLoadState.Loading);
        }
        if (context.Scenario is CapabilitiesSandboxScenario.Failed or CapabilitiesSandboxScenario.MissingTarget ||
            !snapshot.Agents.Any(agent => agent.Id == context.AgentId)) {
            return new(snapshot with { LoadError = "The requested sample target could not be loaded. Retry keeps this identity." },
                AgentCapabilitiesLoadState.Failed);
        }

        var agentId = context.AgentId!.Value;
        snapshot = context.Scenario switch {
            CapabilitiesSandboxScenario.NoCapabilities => snapshot with { Capabilities = [], SelectedCapabilityIds = [] },
            CapabilitiesSandboxScenario.LongContent => snapshot with {
                Agents = snapshot.Agents.Select(agent => agent.Id == agentId ? agent with {
                    Name = "A deliberately long capability review agent name that wraps at the real header boundary"
                } : agent).ToImmutableArray(),
                Capabilities = Cards.Select(card => card with {
                    Name = card.Name + " with a deliberately long descriptive name for the real card",
                    Description = string.Join(" ", Enumerable.Repeat("Inspect wrapping and scroll behavior without external effects.", 4)),
                    EndpointOrPath = "render-only:" + new string('x', 140),
                    Tags = ["sample", "long-descriptive-tag-for-layout-inspection", "review"]
                }).ToImmutableArray()
            },
            CapabilitiesSandboxScenario.AssignmentPending => snapshot with {
                IsBusy = true, Operation = Notice(agentId, "Assignment pending; the authoritative before set remains visible.")
            },
            CapabilitiesSandboxScenario.AssignmentRejected => snapshot with {
                Operation = Notice(agentId, "Assignment rejected. Correct the selection and try again.")
            },
            CapabilitiesSandboxScenario.AssignmentConflict => snapshot with {
                Operation = Notice(agentId, "Assignment conflict. Read current state before continuing.", verify: true)
            },
            CapabilitiesSandboxScenario.CommittedWarning => snapshot with {
                IsBusy = true, SelectedCapabilityIds = [Cards[0].Id, Cards[1].Id],
                Operation = Notice(agentId, "Assignment committed; secondary refresh is pending.", reconcile: true)
            },
            CapabilitiesSandboxScenario.Unconfirmed => snapshot with {
                IsBusy = true, Operation = Notice(agentId, "Assignment outcome is unconfirmed. Verification performs reads only.", verify: true)
            },
            CapabilitiesSandboxScenario.ExactBefore => snapshot with {
                IsBusy = true, Operation = Notice(agentId, "The exact before state was observed. Deliberate retry is available.", retry: true)
            },
            CapabilitiesSandboxScenario.Intervening => snapshot with {
                IsBusy = true, Operation = Notice(agentId, "An intervening revision was observed. Adopt current state.", adopt: true)
            },
            CapabilitiesSandboxScenario.VerificationPending => snapshot with {
                IsBusy = true, Operation = Notice(agentId, "Diagnostic pending; previous proof remains authoritative.")
            },
            CapabilitiesSandboxScenario.VerificationSuperseded => snapshot with {
                Operation = Notice(agentId, "Verification was superseded; the previous proof was preserved.")
            },
            CapabilitiesSandboxScenario.VerificationRecovery => snapshot with {
                IsBusy = true, Operation = Notice(agentId, "A publication receipt is retained. Recover using canonical reads.", verify: true)
            },
            CapabilitiesSandboxScenario.DiagnosticAcknowledgement => snapshot with {
                IsBusy = true, Operation = Notice(agentId, "The diagnostic response is unknown and has no receipt. Acknowledge explicitly.", acknowledge: true)
            },
            CapabilitiesSandboxScenario.PreviewBusy => snapshot with { IsAccessPreviewBusy = true },
            CapabilitiesSandboxScenario.PreviewValid => snapshot with {
                Preview = new(true, 1, 0, [], [new("Sample policy", "The selected sample remains allowed.", "No repair is needed.")])
            },
            CapabilitiesSandboxScenario.PreviewInvalid => snapshot with {
                Preview = new(false, 0, 0, [new("Selector", "A selector value is required.", "Enter a sample value.")], [])
            },
            CapabilitiesSandboxScenario.CuratorUnavailable => snapshot with { Curator = snapshot.Curator with { CanLaunch = false } },
            CapabilitiesSandboxScenario.CuratorPending => snapshot with { CuratorLaunch = new(CuratorAttemptId, true, false, false) },
            CapabilitiesSandboxScenario.CuratorOpened or CapabilitiesSandboxScenario.CuratorAcknowledged =>
                snapshot with { CuratorLaunch = new(CuratorAttemptId, false, false, true) },
            CapabilitiesSandboxScenario.CuratorUnconfirmed => snapshot with {
                Curator = snapshot.Curator with { CanLaunch = false },
                CuratorLaunch = new(CuratorAttemptId, false, true, true)
            },
            _ => snapshot
        };
        return new(snapshot, AgentCapabilitiesLoadState.Ready);
    }

    private static AgentCapabilityOperationPresentation Notice(Guid agentId, string message,
        bool verify = false, bool reconcile = false, bool retry = false, bool adopt = false, bool acknowledge = false) =>
        new(agentId, AttemptId, message, verify, reconcile, retry, adopt, acknowledge);

    private static CapabilityCatalogItem Card(string id, CapabilityKind kind, string name, CapabilityProofStatus proof, string tag) =>
        new(Guid.Parse(id), kind, name, name, "Controlled rendering specimen. Actions only update sample state.",
            "render-only:" + kind, "{}", proof, "Sample proof", null, false) { Tags = ImmutableArray.Create("sample", tag) };

    private static AgentCapabilitiesSnapshot LoadBaseline() {
        using var stream = typeof(CapabilitiesSandboxFixture).Assembly.GetManifestResourceStream("CapabilitiesFixture.json")
            ?? throw new InvalidOperationException("The capabilities rendering fixture is missing.");
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        options.Converters.Add(new JsonStringEnumConverter<CapabilityKind>(allowIntegerValues: false));
        options.Converters.Add(new JsonStringEnumConverter<CapabilityProofStatus>(allowIntegerValues: false));
        var snapshot = JsonSerializer.Deserialize<AgentCapabilitiesSnapshot>(stream, options)
            ?? throw new InvalidOperationException("The capabilities rendering fixture is empty.");
        return snapshot with {
            Capabilities = snapshot.Capabilities.Select(card => card with { Tags = card.Tags.ToImmutableArray() }).ToImmutableArray()
        };
    }
}

