using CanDoItAll.Workbench.Content.UI.Analysis;
using CanDoItAll.Workbench.Content.UI.Generation;

namespace CanDoItAll.Workbench.Content.UiSandbox;

public enum ContentAnalysisSpecimenKind { Summary, Transcript, LegacyMermaid }
public enum ContentScenarioState { Representative, Empty, Large, Loading, Failed, Pending, Partial, Unconfirmed }

internal static class ContentAnalysisFixtures {
    internal static readonly Guid ProviderId = Guid.Parse("665cd33e-dee4-4f18-8491-ebd0e511081c");
    internal static readonly ContentImageProvider ImageProvider = new(ProviderId, "Shared studio", "Studio default",
        [new("opaque-route-default", "Studio default"), new("opaque-route-alternate", "Studio alternate")], false);
    internal static readonly string[] Statuses = ["Open", "Active", "Blocked", "Review", "Done", "Cancelled"];
    internal static ContentProgressSummary Summary(string title, ContentScenarioState state) {
        var count = state == ContentScenarioState.Empty ? 0 : state == ContentScenarioState.Large ? 128 : 8;
        var rows = Enumerable.Range(0, count).Select(index => new ContentProgressRow($"scenario:{index}",
            index == 0 ? title : $"Accepted content row {index}", index % 2 == 0 ? "Note" : "Task", Statuses[index % Statuses.Length],
            index % 2 == 0 ? "50%" : "Undated", index % 3, index % 2 == 0 ? "7 Oct 2026 09:00" : "Undated",
            index % 2 == 0 ? "7 Oct 2026 11:00" : "")).ToArray();
        return new(Guid.NewGuid(), title, rows, Statuses, count / 6, count / 6, count / 6, count / 6, count / 2,
            state is ContentScenarioState.Loading or ContentScenarioState.Pending, state is ContentScenarioState.Partial or ContentScenarioState.Unconfirmed,
            state switch {
                ContentScenarioState.Failed => "The original action was refused. The accepted source is unchanged.",
                ContentScenarioState.Partial => "The exported asset was accepted; its view could not be refreshed. Observe the original asset.",
                ContentScenarioState.Unconfirmed => "The write acknowledgement is unknown. Observe the original action before repeating it.",
                _ => string.Empty
            });
    }
}
