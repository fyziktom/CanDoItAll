namespace CanDoItAll.Workbench.Content.UI.Analysis;

public sealed record ContentProgressRow(string NodeId, string Title, string KindLabel, string Status,
    string ProgressLabel, int Depth, string StartLabel, string EndLabel);

public sealed record ContentProgressSummary(Guid OpeningId, string Title, IReadOnlyList<ContentProgressRow> Rows,
    IReadOnlyList<string> Statuses, int Completed, int Active, int Blocked, int Review, int Undated,
    bool IsBusy = false, bool RequiresObservation = false, string Message = "");

public sealed record ContentSummaryActions(Func<string, string, Task> ChangeStatus,
    Func<Task> ExportWorkbook, Func<Task> ExportGantt, Func<Task> Close);

public enum ContentTranscriptAction { Summarize, FindMyTasks, FindOthersDeliveries }
public enum ContentConfirmationPhase { Loading, Ready, Submitting, ObservationRequired, Completed }
public sealed record ContentProviderOption(Guid Id, string Name);
public sealed record ContentTranscriptConfirmation(Guid OpeningId, string Title, ContentTranscriptAction Action,
    IReadOnlyList<ContentProviderOption> Providers, Guid? SelectedProviderId, string LastProviderName,
    ContentConfirmationPhase Phase, string Message = "");
public sealed record ContentTranscriptActions(Func<Guid?, Task> SelectProvider, Func<Task> Submit, Func<Task> Close);

public sealed record ContentLegacyMermaid(string Title, string Source, string DiagramKind, bool CanEdit) {
    public Guid OpeningId { get; init; }
}
