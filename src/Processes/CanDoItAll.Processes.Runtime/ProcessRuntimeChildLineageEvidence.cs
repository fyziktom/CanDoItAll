using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using CanDoItAll.Processes.Abstractions;

namespace CanDoItAll.Processes.Runtime;

public sealed record ProcessRuntimeLinkedChildEvidence(
    ProcessRunId RunId,
    ProcessRunId RootRunId,
    ProcessRuntimeStatus Status,
    DateTimeOffset StateUpdatedAtUtc,
    DateTimeOffset LinkCreatedAtUtc);

public sealed record ProcessRuntimeChildLineageEvidence(
    ProcessRunId ParentRunId,
    ProcessStepInstanceId ParentStepInstanceId,
    IReadOnlyList<ProcessRuntimeLinkedChildEvidence> OrderedChildren,
    string EvidenceHash)
{
    private const string Schema = "process-runtime-child-lineage-evidence.v1";

    public static ProcessRuntimeChildLineageEvidence Create(
        ProcessRunId parentRunId,
        ProcessStepInstanceId parentStepInstanceId,
        IEnumerable<ProcessRuntimeLinkedChildEvidence> orderedChildren)
    {
        ArgumentNullException.ThrowIfNull(orderedChildren);

        var children = orderedChildren.ToArray();
        return new ProcessRuntimeChildLineageEvidence(
            parentRunId,
            parentStepInstanceId,
            children,
            ComputeHash(parentRunId, parentStepInstanceId, children));
    }

    public bool HasCanonicalHash()
    {
        if (OrderedChildren is null || string.IsNullOrWhiteSpace(EvidenceHash))
        {
            return false;
        }

        return string.Equals(
            EvidenceHash,
            ComputeHash(ParentRunId, ParentStepInstanceId, OrderedChildren),
            StringComparison.Ordinal);
    }

    public bool Matches(ProcessRuntimeChildLineageEvidence current)
    {
        ArgumentNullException.ThrowIfNull(current);

        return ParentRunId == current.ParentRunId &&
               ParentStepInstanceId == current.ParentStepInstanceId &&
               OrderedChildren is not null &&
               current.OrderedChildren is not null &&
               string.Equals(EvidenceHash, current.EvidenceHash, StringComparison.Ordinal) &&
               OrderedChildren.SequenceEqual(current.OrderedChildren);
    }

    private static string ComputeHash(
        ProcessRunId parentRunId,
        ProcessStepInstanceId parentStepInstanceId,
        IReadOnlyList<ProcessRuntimeLinkedChildEvidence> orderedChildren)
    {
        var canonical = new StringBuilder()
            .Append(Schema)
            .Append('|')
            .Append(parentRunId.Value.ToString("D"))
            .Append('|')
            .Append(parentStepInstanceId.Value.ToString("D"))
            .Append('|')
            .Append(orderedChildren.Count.ToString(CultureInfo.InvariantCulture))
            .Append('\n');
        for (var index = 0; index < orderedChildren.Count; index++)
        {
            var child = orderedChildren[index];
            canonical
                .Append(index.ToString(CultureInfo.InvariantCulture))
                .Append('|')
                .Append(child.RunId.Value.ToString("D"))
                .Append('|')
                .Append(child.RootRunId.Value.ToString("D"))
                .Append('|')
                .Append(((int)child.Status).ToString(CultureInfo.InvariantCulture))
                .Append('|')
                .Append(child.StateUpdatedAtUtc.UtcDateTime.Ticks.ToString(CultureInfo.InvariantCulture))
                .Append('|')
                .Append(child.LinkCreatedAtUtc.UtcDateTime.Ticks.ToString(CultureInfo.InvariantCulture))
                .Append('\n');
        }

        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(canonical.ToString()));
        return "sha256:" + Convert.ToHexString(hash).ToLowerInvariant();
    }
}

public static class ProcessRuntimeChildLineageEvidenceRules
{
    public const int MaximumLinkedChildRunCount =
        IProcessRuntimeStateStore.MaximumBatchRunCount + 1;

    public static string? FindIssue(
        ProcessRuntimeChildLineageEvidence evidence,
        ProcessRunId expectedParentRunId,
        ProcessStepInstanceId expectedParentStepInstanceId,
        ProcessRunId expectedRootRunId,
        ProcessRunId expectedRelatedChildRunId,
        DateTimeOffset expectedRelatedChildUpdatedAtUtc)
    {
        ArgumentNullException.ThrowIfNull(evidence);

        if (evidence.ParentRunId != expectedParentRunId ||
            evidence.ParentStepInstanceId != expectedParentStepInstanceId)
        {
            return "The child-lineage evidence is bound to a different parent run or step.";
        }

        if (evidence.OrderedChildren is null ||
            string.IsNullOrWhiteSpace(evidence.EvidenceHash))
        {
            return "The child-lineage evidence is missing its ordered children or canonical hash.";
        }

        if (evidence.OrderedChildren.Count is < 1 or > MaximumLinkedChildRunCount)
        {
            return "The child-lineage evidence has an invalid bounded child count.";
        }

        if (!evidence.HasCanonicalHash())
        {
            return "The child-lineage evidence hash is not canonical.";
        }

        if (evidence.OrderedChildren
                .Select(child => child.RunId)
                .Distinct()
                .Count() != evidence.OrderedChildren.Count)
        {
            return "The child-lineage evidence contains duplicate child run ids.";
        }

        var canonicalOrder = evidence.OrderedChildren
            .OrderByDescending(child => child.LinkCreatedAtUtc)
            .ThenByDescending(child => child.RunId.Value)
            .ToArray();
        if (!evidence.OrderedChildren.SequenceEqual(canonicalOrder))
        {
            return "The child-lineage evidence is not in canonical newest-link order.";
        }

        if (evidence.OrderedChildren.Any(child => child.RootRunId != expectedRootRunId))
        {
            return "The child-lineage evidence includes a run outside the parent process tree.";
        }

        var relatedChild = evidence.OrderedChildren[0];
        if (relatedChild.RunId != expectedRelatedChildRunId)
        {
            return "The related child is no longer the newest linked child run.";
        }

        if (relatedChild.Status != ProcessRuntimeStatus.Completed ||
            relatedChild.StateUpdatedAtUtc != expectedRelatedChildUpdatedAtUtc)
        {
            return "The related child completion state no longer matches the authorized version.";
        }

        if (evidence.OrderedChildren
            .Skip(1)
            .Any(child => !ProcessRuntimeTerminalStates.IsChildRunStopped(child.Status)))
        {
            return "The child-lineage evidence includes a sibling child run that is not stopped.";
        }

        return null;
    }
}
