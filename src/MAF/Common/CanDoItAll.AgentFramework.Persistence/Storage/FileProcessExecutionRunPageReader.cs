using System.Text.Json.Serialization;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;

namespace CanDoItAll.AgentFramework.Persistence;

internal sealed class FileProcessExecutionRunPageReader(FileSandboxWorkspaceStorageLayout layout, FileSandboxWorkspaceJsonStore jsonStore) {
    internal async Task<ProcessExecutionRunPage> ReadAsync(ProcessExecutionRunPageQuery query, CancellationToken cancellationToken) {
        var execution = await jsonStore.ReadJsonAsync<ProcessSelectionExecutionIndex>(layout.ExecutionIndexPath, cancellationToken).ConfigureAwait(false);
        if (execution is null) {
            return File.Exists(layout.CatalogPath) || File.Exists(layout.LegacyExecutionPath) || layout.ExecutionStorageExists()
                ? ProcessExecutionRunPage.Pending(ProcessExecutionRunPageStatus.LayoutUnavailable)
                : new(ProcessExecutionRunPageStatus.Ready, [], query.AfterExecutionRunId, true);
        }
        if (string.IsNullOrWhiteSpace(execution.Version) || execution.Revision < 0 || execution.RunCount < 0 || execution.SessionCount < 0) {
            throw new InvalidDataException("The canonical execution index has invalid version, revision or counts.");
        }
        var index = await jsonStore.ReadJsonAsync<ProcessSelectionIndex>(layout.ExecutionChatIndexPath, cancellationToken).ConfigureAwait(false);
        if (index is null || index.Version != execution.Version || index.Revision != execution.Revision ||
                index.RunSummaries is null || index.SessionSummaries is null ||
                index.RunSummaries.Count != execution.RunCount || index.SessionSummaries.Count != execution.SessionCount ||
                index.ReportingProjectionVersion < 0 || index.ReportingProjectionVersion > WorkspaceChatProjectionBuilder.CurrentReportingProjectionVersion) {
            return ProcessExecutionRunPage.Pending(ProcessExecutionRunPageStatus.IndexUnavailable);
        }
        var ids = new HashSet<Guid>();
        var selected = new PriorityQueue<Guid, Guid>(Comparer<Guid>.Create(static (left, right) => right.CompareTo(left)));
        foreach (var summary in index.RunSummaries) {
            cancellationToken.ThrowIfCancellationRequested();
            if (summary is null || summary.ExecutionRunId == Guid.Empty || !ids.Add(summary.ExecutionRunId) || summary.ReportingProjectionVersion < 0) {
                throw new InvalidDataException("The execution index has invalid or duplicate run identities.");
            }
            if (summary.ReportingProjectionVersion > WorkspaceChatProjectionBuilder.CurrentReportingProjectionVersion) {
                return ProcessExecutionRunPage.Pending(ProcessExecutionRunPageStatus.IndexUnavailable);
            }
            if (query.AfterExecutionRunId is { } after && summary.ExecutionRunId.CompareTo(after) <= 0) {
                continue;
            }
            if (summary.ProcessRunId is not null && summary.ReportingProjectionVersion == WorkspaceChatProjectionBuilder.CurrentReportingProjectionVersion &&
                    (!Guid.TryParse(summary.ProcessRunId, out var process) || process != query.ProcessRunId)) {
                continue;
            }
            selected.Enqueue(summary.ExecutionRunId, summary.ExecutionRunId);
            if (selected.Count > query.Take + 1) {
                selected.Dequeue();
            }
        }
        var exhausted = selected.Count <= query.Take;
        if (!exhausted) {
            selected.Dequeue();
        }
        var pageIds = selected.UnorderedItems.Select(item => item.Element).Order().ToArray();
        var runs = await jsonStore.ReadJsonBatchAsync<ExecutionRunRecord>(pageIds.Select(layout.RunPath).ToArray(), cancellationToken).ConfigureAwait(false);
        for (var position = 0; position < pageIds.Length; position++) {
            if (runs[position] is not { } run || run.Id != pageIds[position]) {
                throw new InvalidDataException("The selected execution index entry has no matching original run record.");
            }
        }
        return new(ProcessExecutionRunPageStatus.Ready, runs.Select(run => run!).ToArray(),
            pageIds.Length == 0 ? query.AfterExecutionRunId : pageIds[^1], exhausted);
    }
}

internal sealed record ProcessSelectionExecutionIndex([property: JsonRequired] string Version, [property: JsonRequired] long Revision,
    [property: JsonRequired] int SessionCount, [property: JsonRequired] int RunCount);
internal sealed record ProcessSelectionIndex([property: JsonRequired] string Version, [property: JsonRequired] long Revision,
    [property: JsonRequired] IReadOnlyList<ProcessSelectionSession>? SessionSummaries,
    [property: JsonRequired] IReadOnlyList<ProcessSelectionSummary>? RunSummaries,
    int ReportingProjectionVersion = 0);
internal sealed record ProcessSelectionSession(Guid Id);
internal sealed record ProcessSelectionSummary([property: JsonRequired] Guid ExecutionRunId, string? ProcessRunId, int ReportingProjectionVersion = 0);
