using System.Collections.Immutable;

namespace CanDoItAll.AgentFramework.UiSandbox;

public sealed record HistoryScenarioOption(HistoryScenario Value, string Token, string Text);

public static class HistoryScenarios {
    public static ImmutableArray<HistoryScenarioOption> Options { get; } = [
        new(HistoryScenario.Normal, "normal", "Representative requests"),
        new(HistoryScenario.Large, "large", "Large page and long content"),
        new(HistoryScenario.Empty, "empty", "Empty results"),
        new(HistoryScenario.Partial, "partial", "Incomplete coverage"),
        new(HistoryScenario.Failure, "failure", "Search unavailable"),
        new(HistoryScenario.Denied, "denied", "Search denied"),
        new(HistoryScenario.MetadataDenied, "metadata-denied", "Metadata denied"),
        new(HistoryScenario.ContentDenied, "content-denied", "Content denied after metadata"),
        new(HistoryScenario.Canonical, "canonical", "Exact canonical owners"),
        new(HistoryScenario.Expired, "expired", "Expired content"),
        new(HistoryScenario.Pending, "pending", "Pending canonical content"),
        new(HistoryScenario.Redacted, "redacted", "Redacted and truncated content"),
        new(HistoryScenario.Unavailable, "unavailable", "Content unavailable"),
        new(HistoryScenario.DelayedSearch, "delayed-search", "Controlled search completion"),
        new(HistoryScenario.DelayedMetadata, "delayed-metadata", "Controlled metadata completion"),
        new(HistoryScenario.DelayedContent, "delayed-content", "Controlled content completion"),
        new(HistoryScenario.TwoWorkspaces, "two-workspaces", "Two independent workspaces")
    ];

    public static HistoryScenario Resolve(string? token) => token switch {
        null or "not-requested" or "metadata" => HistoryScenario.Normal,
        _ => Options.FirstOrDefault(option => option.Token == token)?.Value
            ?? throw new ArgumentException("Unknown History sandbox scenario.", nameof(token))
    };
}
