using System.Text.Json.Serialization;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;

namespace CanDoItAll.AgentFramework.Persistence;

internal sealed class FileProcessExecutionIndex(FileSandboxWorkspaceStorageLayout layout, FileSandboxWorkspaceJsonStore jsonStore) {
    internal const int SchemaVersion = 2;

    internal async Task<(ProcessExecutionRunPageStatus Status, ProcessExecutionSelectionIndex? Index)> ReadAsync(CancellationToken cancellationToken) {
        var canonical = await ReadCanonicalAsync(cancellationToken).ConfigureAwait(false);
        if (canonical is null) {
            return (MissingCanonicalStatus(), null);
        }
        var index = await jsonStore.ReadJsonAsync<ProcessExecutionSelectionIndex>(layout.ExecutionProcessIndexPath, cancellationToken).ConfigureAwait(false);
        if (index is null || index.SchemaVersion is not (1 or SchemaVersion) || !index.SupportedProjection || index.Version != canonical.Version ||
                index.Revision != (index.SchemaVersion == 1 ? canonical.Revision : canonical.ProcessSelectionRevision ?? canonical.Revision) ||
                index.RunCount != canonical.RunCount || index.SessionCount != canonical.SessionCount ||
                index.Runs is null || index.NonProcessRunCount < 0 || (long)index.NonProcessRunCount + index.Runs.Count != index.RunCount) {
            return (ProcessExecutionRunPageStatus.IndexUnavailable, null);
        }
        return (ProcessExecutionRunPageStatus.Ready, index);
    }

    internal async Task<ProcessExecutionRunPageStatus> PrepareAsync(CancellationToken cancellationToken) {
        var canonical = await ReadCanonicalAsync(cancellationToken).ConfigureAwait(false);
        if (canonical is null) {
            return MissingCanonicalStatus();
        }
        var source = await jsonStore.ReadJsonAsync<ProcessSelectionSourceIndex>(layout.ExecutionChatIndexPath, cancellationToken).ConfigureAwait(false);
        if (source is null || source.Version != canonical.Version || source.Revision != canonical.Revision ||
                source.ProcessSelectionRevision != canonical.ProcessSelectionRevision ||
                source.RunSummaries is null || source.SessionSummaries is null ||
                source.RunSummaries.Count != canonical.RunCount || source.SessionSummaries.Count != canonical.SessionCount) {
            return ProcessExecutionRunPageStatus.IndexUnavailable;
        }
        var index = Create(source.Version, source.ProcessSelectionRevision ?? source.Revision, source.SessionSummaries.Count,
            source.RunSummaries, source.ReportingProjectionVersion, cancellationToken);
        if (!index.SupportedProjection) {
            return ProcessExecutionRunPageStatus.IndexUnavailable;
        }
        await WriteAsync(index, cancellationToken).ConfigureAwait(false);
        return ProcessExecutionRunPageStatus.Ready;
    }

    internal async Task PublishAsync(ExecutionChatIndex target, CancellationToken cancellationToken, bool onlyIfChanged = false) {
        RequireValidRevision(target.Revision, target.ProcessSelectionRevision);
        var index = Create(target.Version, target.ProcessSelectionRevision ?? target.Revision, target.SessionSummaries.Count,
            target.RunSummaries.Select(summary => summary is not null
                ? new ProcessSelectionSourceRun(summary.ExecutionRunId, summary.ProcessRunId, summary.ReportingProjectionVersion)
                : throw new InvalidDataException("The execution projection contains an absent run summary.")),
            target.ReportingProjectionVersion, cancellationToken);
        if (onlyIfChanged) {
            await jsonStore.WriteJsonIfChangedAsync(layout.ExecutionChatIndexPath, target, cancellationToken).ConfigureAwait(false);
        } else {
            await jsonStore.WriteJsonAtomicallyAsync(layout.ExecutionChatIndexPath, target, cancellationToken).ConfigureAwait(false);
        }
        await WriteAsync(index, cancellationToken).ConfigureAwait(false);
    }

    private Task WriteAsync(ProcessExecutionSelectionIndex index, CancellationToken cancellationToken)
        => jsonStore.WriteJsonIfChangedAsync(layout.ExecutionProcessIndexPath, index, cancellationToken, compact: true);

    internal static long NextSelectionRevision(long currentRevision, long? selectionRevision,
        string? previousProcessRunId, string? targetProcessRunId) {
        RequireValidRevision(currentRevision, selectionRevision);
        var nextRevision = checked(currentRevision + 1);
        return selectionRevision is null || !string.Equals(previousProcessRunId, targetProcessRunId, StringComparison.Ordinal)
            ? nextRevision : selectionRevision.Value;
    }

    internal static void RequireValidRevision(long revision, long? selectionRevision) {
        if (revision < 0 || selectionRevision is < 0 || selectionRevision > revision) {
            throw new InvalidDataException("The execution index has invalid ordinary or Process selection revisions.");
        }
    }

    private async Task<ProcessSelectionCanonicalIndex?> ReadCanonicalAsync(CancellationToken cancellationToken) {
        var index = await jsonStore.ReadJsonAsync<ProcessSelectionCanonicalIndex>(layout.ExecutionIndexPath, cancellationToken).ConfigureAwait(false);
        if (index is not null && (string.IsNullOrWhiteSpace(index.Version) || index.Revision < 0 || index.RunCount < 0 || index.SessionCount < 0)) {
            throw new InvalidDataException("The canonical execution index has invalid version, revision or counts.");
        }
        if (index is not null) {
            RequireValidRevision(index.Revision, index.ProcessSelectionRevision);
        }
        return index;
    }

    private ProcessExecutionRunPageStatus MissingCanonicalStatus()
        => File.Exists(layout.CatalogPath) || File.Exists(layout.LegacyExecutionPath) || layout.ExecutionStorageExists() ||
            File.Exists(layout.ExecutionProcessIndexPath) || File.Exists(layout.ExecutionChatIndexPath)
            ? ProcessExecutionRunPageStatus.LayoutUnavailable : ProcessExecutionRunPageStatus.Ready;

    private static ProcessExecutionSelectionIndex Create(string version, long revision, int sessionCount,
        IEnumerable<ProcessSelectionSourceRun> summaries, int reportingVersion, CancellationToken cancellationToken) {
        if (string.IsNullOrWhiteSpace(version) || revision < 0 || sessionCount < 0 || reportingVersion < 0) {
            throw new InvalidDataException("The source execution projection has invalid version, revision or counts.");
        }
        var ids = new HashSet<Guid>();
        var runs = new List<ProcessExecutionSelectionEntry>();
        var nonProcess = 0;
        var supported = reportingVersion <= WorkspaceChatProjectionBuilder.CurrentReportingProjectionVersion;
        foreach (var summary in summaries) {
            cancellationToken.ThrowIfCancellationRequested();
            if (summary is null || summary.ExecutionRunId == Guid.Empty || !ids.Add(summary.ExecutionRunId) || summary.ReportingProjectionVersion < 0) {
                throw new InvalidDataException("The source execution projection has invalid or duplicate run identities.");
            }
            supported &= summary.ReportingProjectionVersion <= WorkspaceChatProjectionBuilder.CurrentReportingProjectionVersion;
            if (summary.ProcessRunId is null || summary.ReportingProjectionVersion != WorkspaceChatProjectionBuilder.CurrentReportingProjectionVersion) {
                runs.Add(new(summary.ExecutionRunId, null));
            } else if (Guid.TryParse(summary.ProcessRunId, out var process) && process != Guid.Empty) {
                runs.Add(new(summary.ExecutionRunId, process));
            } else {
                nonProcess++;
            }
        }
        runs.Sort(static (left, right) => left.ExecutionRunId.CompareTo(right.ExecutionRunId));
        return new(SchemaVersion, version, revision, sessionCount, ids.Count, nonProcess, supported, runs);
    }
}

internal sealed record ProcessExecutionSelectionIndex([property: JsonRequired] int SchemaVersion,
    [property: JsonRequired] string Version, [property: JsonRequired] long Revision,
    [property: JsonRequired] int SessionCount, [property: JsonRequired] int RunCount,
    [property: JsonRequired] int NonProcessRunCount, [property: JsonRequired] bool SupportedProjection,
    [property: JsonRequired] IReadOnlyList<ProcessExecutionSelectionEntry>? Runs);
internal readonly record struct ProcessExecutionSelectionEntry([property: JsonRequired] Guid ExecutionRunId, [property: JsonRequired] Guid? ProcessRunId);
internal sealed record ProcessSelectionCanonicalIndex([property: JsonRequired] string Version, [property: JsonRequired] long Revision,
    [property: JsonRequired] int SessionCount, [property: JsonRequired] int RunCount, long? ProcessSelectionRevision = null);
internal sealed record ProcessSelectionSourceIndex([property: JsonRequired] string Version, [property: JsonRequired] long Revision,
    [property: JsonRequired] IReadOnlyList<ProcessSelectionSourceSession>? SessionSummaries,
    [property: JsonRequired] IReadOnlyList<ProcessSelectionSourceRun>? RunSummaries, int ReportingProjectionVersion = 0,
    long? ProcessSelectionRevision = null);
internal sealed record ProcessSelectionSourceSession(Guid Id);
internal sealed record ProcessSelectionSourceRun([property: JsonRequired] Guid ExecutionRunId, string? ProcessRunId, int ReportingProjectionVersion = 0);
