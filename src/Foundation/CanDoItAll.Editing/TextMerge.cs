namespace CanDoItAll.Editing;

public sealed record TextChange(int StartLine, int RemovedLines, IReadOnlyList<string> AddedLines);

public sealed record TextMergeResult(MergeField<string> Field, TextChange? LocalChange, TextChange? CurrentChange,
    bool RequiresManualComparison);

public static class TextMerge {
    public const int MaximumAutomaticCharacters = 256 * 1024;
    public const int MaximumAutomaticLines = 10_000;

    public static TextMergeResult Compare(string original, string local, string current) {
        ArgumentNullException.ThrowIfNull(original);
        ArgumentNullException.ThrowIfNull(local);
        ArgumentNullException.ThrowIfNull(current);
        var field = new MergeField<string>(original, local, current, StringComparer.Ordinal);
        if (Math.Max(original.Length, Math.Max(local.Length, current.Length)) > MaximumAutomaticCharacters) {
            return new(field, null, null, !field.IsResolved);
        }
        var b = Lines(original);
        var l = Lines(local);
        var r = Lines(current);
        if (Math.Max(b.Length, Math.Max(l.Length, r.Length)) > MaximumAutomaticLines) {
            return new(field, null, null, !field.IsResolved);
        }
        var left = Difference(b, l);
        var right = Difference(b, r);
        if (!field.IsResolved && left is not null && right is not null) {
            var (first, second) = left.StartLine <= right.StartLine ? (left, right) : (right, left);
            // Each side has one conservative enclosing hunk. Only strictly disjoint edits are combined.
            if (first.StartLine + first.RemovedLines <= second.StartLine && first.StartLine != second.StartLine) {
                field.SetAutomatic(string.Concat(b.Take(first.StartLine)
                    .Concat(first.AddedLines)
                    .Concat(b.Skip(first.StartLine + first.RemovedLines)
                        .Take(second.StartLine - first.StartLine - first.RemovedLines))
                    .Concat(second.AddedLines)
                    .Concat(b.Skip(second.StartLine + second.RemovedLines))));
            }
        }
        return new(field, left, right, false);
    }

    private static string[] Lines(string value) {
        var lines = new List<string>();
        var start = 0;
        for (var index = 0; index < value.Length; index++) {
            if (value[index] != '\n') {
                continue;
            }
            lines.Add(value[start..(index + 1)]);
            start = index + 1;
        }
        if (start < value.Length) {
            lines.Add(value[start..]);
        }
        return [.. lines];
    }

    private static TextChange? Difference(string[] original, string[] edited) {
        var start = 0;
        while (start < original.Length && start < edited.Length && original[start] == edited[start]) {
            start++;
        }
        if (start == original.Length && start == edited.Length) {
            return null;
        }
        var end = 0;
        while (end < original.Length - start && end < edited.Length - start &&
                original[^(end + 1)] == edited[^(end + 1)]) {
            end++;
        }
        return new(start, original.Length - start - end, edited[start..(edited.Length - end)]);
    }
}
