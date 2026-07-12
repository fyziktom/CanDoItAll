using CanDoItAll.AgentFramework.Models;

namespace CanDoItAll.AgentFramework.Core.Execution;

public sealed record ManagedProcessArtifactOutcomeReadResult(
    ProcessStepOutcomeStatus? Status,
    string BranchOutcomeKey,
    string? FailureMessage)
{
    public bool HasStatus => Status is not null;

    public bool HasBranchOutcomeKey => !string.IsNullOrWhiteSpace(BranchOutcomeKey);

    public bool IsValid => string.IsNullOrWhiteSpace(FailureMessage);
}

public static class ManagedProcessArtifactOutcomeReader
{
    public static ManagedProcessArtifactOutcomeReadResult Read(string content)
    {
        if (string.IsNullOrWhiteSpace(content))
        {
            return new ManagedProcessArtifactOutcomeReadResult(null, string.Empty, null);
        }

        var lines = content.Split(['\r', '\n']);
        var statuses = new HashSet<ProcessStepOutcomeStatus>();
        var branchOutcomeKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        for (var index = 0; index < lines.Length; index++)
        {
            var metadataLine = NormalizeMetadataLine(lines[index]);
            if (TryReadField(metadataLine, out var fieldKey, out var fieldValue))
            {
                if (IsStatusKey(fieldKey))
                {
                    if (!TryParseStatus(fieldValue, out var status))
                    {
                        return Failure("The managed artifact contains an invalid Status field.");
                    }

                    statuses.Add(status);
                    continue;
                }

                if (IsBranchOutcomeKey(fieldKey))
                {
                    if (!TryAddBranchOutcomeKey(fieldValue, branchOutcomeKeys, out var failure))
                    {
                        return Failure(failure);
                    }

                    continue;
                }
            }

            if (IsStatusKey(metadataLine))
            {
                if (!TryReadFollowingValue(lines, index, out var value) ||
                    !TryParseStatus(value, out var status))
                {
                    return Failure("The managed artifact contains an invalid Status section.");
                }

                statuses.Add(status);
                continue;
            }

            if (IsBranchOutcomeKey(metadataLine))
            {
                if (!TryReadFollowingValue(lines, index, out var value))
                {
                    return Failure("The managed artifact contains an invalid Branch outcome key section.");
                }

                if (!TryAddBranchOutcomeKey(value, branchOutcomeKeys, out var failure))
                {
                    return Failure(failure);
                }

                continue;
            }

            if (IsBranchOutcomeSection(metadataLine) &&
                !TryReadBranchOutcomeSection(lines, index, branchOutcomeKeys, out var sectionFailure))
            {
                return Failure(sectionFailure);
            }
        }

        if (statuses.Count > 1)
        {
            return Failure("The managed artifact contains multiple different Status values.");
        }

        if (branchOutcomeKeys.Count > 1)
        {
            return Failure("The managed artifact contains multiple different Branch outcome key values.");
        }

        return new ManagedProcessArtifactOutcomeReadResult(
            statuses.Count == 1 ? statuses.Single() : null,
            branchOutcomeKeys.SingleOrDefault() ?? string.Empty,
            null);
    }

    private static ManagedProcessArtifactOutcomeReadResult Failure(string message)
        => new(null, string.Empty, message);

    private static bool TryReadBranchOutcomeSection(
        IReadOnlyList<string> lines,
        int headingIndex,
        ISet<string> branchOutcomeKeys,
        out string failureMessage)
    {
        failureMessage = string.Empty;
        for (var index = headingIndex + 1; index < lines.Count; index++)
        {
            if (IsMarkdownHeading(lines[index]))
            {
                return true;
            }

            var line = NormalizeMetadataLine(lines[index]);
            if (!TryReadField(line, out var key, out var value) ||
                !IsBranchOutcomeSectionField(key))
            {
                continue;
            }

            return TryAddBranchOutcomeKey(value, branchOutcomeKeys, out failureMessage);
        }

        return true;
    }

    private static bool TryReadFollowingValue(
        IReadOnlyList<string> lines,
        int headingIndex,
        out string value)
    {
        value = string.Empty;
        for (var index = headingIndex + 1; index < lines.Count; index++)
        {
            if (string.IsNullOrWhiteSpace(lines[index]))
            {
                continue;
            }

            if (IsMarkdownHeading(lines[index]))
            {
                return false;
            }

            value = NormalizeMetadataLine(lines[index]);
            return !string.IsNullOrWhiteSpace(value);
        }

        return false;
    }

    private static bool TryReadField(
        string line,
        out string key,
        out string value)
    {
        key = string.Empty;
        value = string.Empty;
        var separatorIndex = line.IndexOf(':', StringComparison.Ordinal);
        if (separatorIndex <= 0)
        {
            return false;
        }

        key = line[..separatorIndex].Trim(' ', '*', '`');
        value = line[(separatorIndex + 1)..].Trim(' ', '*', '`');
        return key.Length > 0;
    }

    private static bool TryAddBranchOutcomeKey(
        string value,
        ISet<string> branchOutcomeKeys,
        out string failureMessage)
    {
        failureMessage = string.Empty;
        var normalized = NormalizeBranchOutcomeKey(value);
        if (!IsSafeBranchOutcomeKey(normalized))
        {
            failureMessage = "The managed artifact contains an invalid Branch outcome key value.";
            return false;
        }

        branchOutcomeKeys.Add(normalized);
        return true;
    }

    private static bool TryParseStatus(string value, out ProcessStepOutcomeStatus status)
    {
        status = default;
        var normalized = new string(value
            .Trim().Trim('*', '`', '.', ';')
            .TakeWhile(character => character != '#')
            .Where(char.IsLetterOrDigit)
            .Select(char.ToLowerInvariant)
            .ToArray());
        status = normalized switch
        {
            "completed" or "complete" or "succeeded" or "success" => ProcessStepOutcomeStatus.Completed,
            "blocked" or "waiting" or "waitingonchild" or "waitingforchild" => ProcessStepOutcomeStatus.Blocked,
            "failed" or "failure" => ProcessStepOutcomeStatus.Failed,
            "waitingapproval" or "pendingapproval" => ProcessStepOutcomeStatus.WaitingApproval,
            "refused" or "rejected" => ProcessStepOutcomeStatus.Refused,
            _ => default
        };
        return normalized is "completed" or "complete" or "succeeded" or "success" or
            "blocked" or "waiting" or "waitingonchild" or "waitingforchild" or
            "failed" or "failure" or "waitingapproval" or "pendingapproval" or
            "refused" or "rejected";
    }

    private static bool IsStatusKey(string value)
        => string.Equals(NormalizeKey(value), "status", StringComparison.Ordinal);

    private static bool IsBranchOutcomeKey(string value)
        => string.Equals(NormalizeKey(value), "branchoutcomekey", StringComparison.Ordinal);

    private static bool IsBranchOutcomeSection(string value)
        => string.Equals(NormalizeKey(value), "branchoutcome", StringComparison.Ordinal);

    private static bool IsBranchOutcomeSectionField(string value)
        => IsBranchOutcomeKey(value) ||
           string.Equals(NormalizeKey(value), "key", StringComparison.Ordinal);

    private static bool IsMarkdownHeading(string value)
        => value.TrimStart().StartsWith('#');

    private static string NormalizeMetadataLine(string value)
        => value.Trim().TrimStart('#', '-', '*', ' ').Trim(' ', '*', '`');

    private static string NormalizeKey(string value)
        => new(value.Where(char.IsLetterOrDigit).Select(char.ToLowerInvariant).ToArray());

    private static string NormalizeBranchOutcomeKey(string value)
    {
        var trimmed = NormalizeMetadataLine(value).Trim('.', ';');
        var commentIndex = trimmed.IndexOf('#', StringComparison.Ordinal);
        return (commentIndex >= 0 ? trimmed[..commentIndex] : trimmed).Trim(' ', '*', '`', '.', ';');
    }

    private static bool IsSafeBranchOutcomeKey(string value)
        => !string.IsNullOrWhiteSpace(value) &&
           char.IsLetterOrDigit(value[0]) &&
           value.All(character => char.IsLetterOrDigit(character) || character is '-' or '_' or '.');
}
