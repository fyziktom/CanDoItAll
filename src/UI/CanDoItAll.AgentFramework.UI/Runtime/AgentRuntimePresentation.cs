using System.Collections.Immutable;
using CanDoItAll.AgentFramework.UI.Governance;

namespace CanDoItAll.AgentFramework.UI.Runtime;

public sealed record AgentRequestCompatibilityPresentation(
    string Transport, string Disposition, string RequestedEffort, string EffectiveEffort,
    string RequestedModel, string EffectiveModel, string Adjustment);

public sealed record AgentRuntimeDetailsState(
    Guid? RunId, string Title, string State, string Tone, string? Outcome, string UpdatedAtUtc,
    string EntryProvider, string? FailureProvider, AgentRequestCompatibilityPresentation? Compatibility,
    ImmutableArray<GovernanceEntry> Timeline, ExecutionMetricsPresentation Metrics);

public sealed record AgentExecutionStepPresentation(
    Guid Id, Guid RunId, string State, string Tone, string Phase, string CreatedAtUtc, string Message);

public sealed record AgentExecutionLogState(
    string Title, string Metadata, string? Provider, string State, string Tone, string Duration,
    Guid? HighlightEntryId, ImmutableArray<AgentExecutionStepPresentation> Entries);
