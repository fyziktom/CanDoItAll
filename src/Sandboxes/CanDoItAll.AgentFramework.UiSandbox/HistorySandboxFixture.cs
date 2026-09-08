using System.Collections.Immutable;
using CanDoItAll.AgentFramework.ProviderHistory;
using CanDoItAll.AgentFramework.UI.History;

namespace CanDoItAll.AgentFramework.UiSandbox;

public static class HistorySandboxFixture {
    public static ImmutableArray<string> Scenarios { get; } = ["not-requested", "loading", "canceled", "failure", "empty", "partial", "paged", "metadata"];
    public static DateTimeOffset Now { get; } = new(2026, 9, 8, 12, 0, 0, TimeSpan.Zero);
    public const string SyntheticInput = "Synthetic authorized input <example>界</example>";
    public const string SyntheticResponse = "Synthetic authorized response";
    public static HistoryEntry Entry { get; } = new(new(Guid.Parse("29106dba-fdca-42a3-8cdb-430000000001")),
        new(Guid.Parse("29106dba-fdca-42a3-8cdb-430000000002"), Guid.Parse("29106dba-fdca-42a3-8cdb-430000000003"), "sandbox"),
        null, null, HistoryGranularity.ProviderCallAttempt, Now, HistoryTimeBasis.AttemptStarted, Now, Now,
        new(null, "Synthetic provider", "Fixture", new("Vendor/Exact Model"), new("Vendor/Exact Model")),
        HistoryOperation.CompleteChat, HistoryWorkload.Direct, HistoryOutcome.Succeeded,
        new(HistoryAuthenticationKind.TrustedLocalOperator), new(HistoryUsageState.Complete, 12, 8),
        new(HistoryPriceState.CalculatedAtExecution, 0.01m, "USD"), HistoryMetadataAuthority.Standalone,
        HistoryRetentionAuthority.HistoryPolicy, HistoryDetailState.Captured);
    public static HistoryDetail Content { get; } = new(Entry.Id, HistoryDetailState.Captured,
        new(SyntheticInput, System.Text.Encoding.UTF8.GetByteCount(SyntheticInput), System.Text.Encoding.UTF8.GetByteCount(SyntheticInput), HistoryDetailFlags.PriorContextNotCaptured),
        new(SyntheticResponse, SyntheticResponse.Length, SyntheticResponse.Length, HistoryDetailFlags.None), Now.AddDays(7));

    public static HistoryResultsPresentation Create(string? scenario) {
        var value = new HistoryResultsPresentation(HistorySearchPhase.Ready, new(new HistoryProviderScope.AllAuthorized(), Now.AddDays(-1), Now),
            false, null, [Entry], new(HistoryCoverageState.Current, Now), Now, 1, false, false, false);
        return scenario switch {
            "loading" => value with { Phase = HistorySearchPhase.Loading, Entries = [] },
            "canceled" => value with { Phase = HistorySearchPhase.Canceled, Entries = [] },
            "failure" => value with { Phase = HistorySearchPhase.Failed, Failure = HistoryFailure.Unavailable, Entries = [] },
            "empty" => value with { Entries = [] },
            "partial" => value with { Coverage = new(HistoryCoverageState.Partial, Now.AddHours(-1)) },
            "paged" => value with { PageNumber = 2, CanPrevious = true, CanNext = true, DraftChanged = true },
            "metadata" => value,
            _ => HistoryResultsPresentation.Initial
        };
    }
}
