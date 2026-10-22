using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;

namespace CanDoItAll.AgentFramework.Persistence;

internal sealed class FileProcessExecutionRunPageReader(FileSandboxWorkspaceStorageLayout layout, FileSandboxWorkspaceJsonStore jsonStore) {
    internal async Task<ProcessExecutionRunPage> ReadAsync(ProcessExecutionRunPageQuery query, CancellationToken cancellationToken) {
        var (status, index) = await new FileProcessExecutionIndex(layout, jsonStore).ReadAsync(cancellationToken).ConfigureAwait(false);
        if (status != ProcessExecutionRunPageStatus.Ready) {
            return ProcessExecutionRunPage.Pending(status);
        }
        if (index is null) {
            return new(ProcessExecutionRunPageStatus.Ready, [], query.AfterExecutionRunId, true);
        }
        var ids = new HashSet<Guid>();
        var selected = new PriorityQueue<Guid, Guid>(Comparer<Guid>.Create(static (left, right) => right.CompareTo(left)));
        foreach (var summary in index.Runs!) {
            cancellationToken.ThrowIfCancellationRequested();
            if (summary.ExecutionRunId == Guid.Empty || summary.ProcessRunId == Guid.Empty || !ids.Add(summary.ExecutionRunId)) {
                throw new InvalidDataException("The execution index has invalid or duplicate run identities.");
            }
            if (query.AfterExecutionRunId is { } after && summary.ExecutionRunId.CompareTo(after) <= 0) {
                continue;
            }
            if (summary.ProcessRunId is { } process && process != query.ProcessRunId) {
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
