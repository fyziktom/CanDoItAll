using System.Text.Json;

namespace CanDoItAll.Processes.Templates;

public sealed record ProcessTemplateMergeResult(
    IReadOnlyList<ProcessTemplatePatchOperation> AutoAppliedOperations,
    IReadOnlyList<ProcessTemplateConflictEntry> Conflicts)
{
    public bool HasConflicts => Conflicts.Count > 0;
}

public static class ProcessTemplateThreeWayMerge
{
    public static ProcessTemplateMergeResult DetectConflicts(
        IReadOnlyList<ProcessTemplatePatchOperation> localOperations,
        IReadOnlySet<string> changedGlobalPointers)
    {
        ArgumentNullException.ThrowIfNull(localOperations);
        ArgumentNullException.ThrowIfNull(changedGlobalPointers);

        var autoApplied = new List<ProcessTemplatePatchOperation>();
        var conflicts = new List<ProcessTemplateConflictEntry>();
        foreach (var operation in localOperations)
        {
            if (changedGlobalPointers.Contains(operation.JsonPointer))
            {
                conflicts.Add(new ProcessTemplateConflictEntry(
                    operation.JsonPointer,
                    null,
                    null,
                    operation.Value,
                    [
                        ProcessTemplateConflictResolutionKind.UseGlobal,
                        ProcessTemplateConflictResolutionKind.UseLocal,
                        ProcessTemplateConflictResolutionKind.EditManually
                    ]));
            }
            else
            {
                autoApplied.Add(operation);
            }
        }

        return new ProcessTemplateMergeResult(autoApplied, conflicts);
    }
}

public static class ProcessTemplateProjectionRules
{
    public static bool HasSourceDrift(
        ProcessTemplateProjectionMetadata metadata,
        string currentSourceJsonHash)
    {
        ArgumentNullException.ThrowIfNull(metadata);

        if (string.IsNullOrWhiteSpace(currentSourceJsonHash))
        {
            throw new ArgumentException("Current source hash cannot be empty.", nameof(currentSourceJsonHash));
        }

        return !string.Equals(metadata.SourceJsonHash, currentSourceJsonHash, StringComparison.Ordinal);
    }
}
