using System.Text.RegularExpressions;

namespace CanDoItAll.Processes.Drivers.Abstractions.Audit;

public static partial class ProcessDriverRedactionPolicy
{
    public const int DefaultMaxRedactedTextLength = 4096;
    public const int DefaultMaxAuditSummaryLength = 1024;

    public static ProcessDriverRedactionResult Redact(
        string value,
        int maxLength = DefaultMaxRedactedTextLength)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxLength);

        var appliedKinds = new HashSet<ProcessDriverRedactionKind>();
        var redacted = value;
        redacted = RedactPattern(
            redacted,
            ConnectionStringPattern(),
            "[redacted-connection-string]",
            ProcessDriverRedactionKind.ConnectionString,
            appliedKinds);
        redacted = RedactPattern(
            redacted,
            AccessTokenPattern(),
            "[redacted-access-token]",
            ProcessDriverRedactionKind.AccessToken,
            appliedKinds);
        redacted = RedactPattern(
            redacted,
            SecretPattern(),
            "[redacted-secret]",
            ProcessDriverRedactionKind.Secret,
            appliedKinds);
        redacted = RedactPattern(
            redacted,
            EmailPattern(),
            "[redacted-email]",
            ProcessDriverRedactionKind.EmailAddress,
            appliedKinds);

        var wasTruncated = redacted.Length > maxLength;
        if (wasTruncated)
        {
            redacted = redacted[..maxLength];
        }

        var status = appliedKinds.Count == 0 && !wasTruncated
            ? ProcessDriverRedactionStatus.None
            : ProcessDriverRedactionStatus.Redacted;
        var descriptor = new ProcessDriverRedactionDescriptor(
            status,
            appliedKinds.Order().ToArray(),
            ProcessDriverEvidenceHash.ComputeSha256(redacted));

        return new ProcessDriverRedactionResult(descriptor, redacted, wasTruncated);
    }

    public static ProcessDriverRedactionResult RedactDiagnosticSummary(string value)
    {
        return Redact(value, DefaultMaxAuditSummaryLength);
    }

    private static string RedactPattern(
        string value,
        Regex pattern,
        string replacement,
        ProcessDriverRedactionKind kind,
        HashSet<ProcessDriverRedactionKind> appliedKinds)
    {
        var redacted = pattern.Replace(value, replacement);
        if (!string.Equals(redacted, value, StringComparison.Ordinal))
        {
            appliedKinds.Add(kind);
        }

        return redacted;
    }

    [GeneratedRegex(@"(?i)\b(?:connectionstring|connection\s+string)\s*[:=]\s*[^;\r\n]+(?:;[^;\r\n]+)*", RegexOptions.CultureInvariant)]
    private static partial Regex ConnectionStringPattern();

    [GeneratedRegex(@"(?i)\b(?:api[_-]?key|access[_-]?token|bearer)\s*[:= ]\s*[^;\s]+", RegexOptions.CultureInvariant)]
    private static partial Regex AccessTokenPattern();

    [GeneratedRegex(@"(?i)\b(?:token|password|secret)\s*[:=]\s*[^;\s]+", RegexOptions.CultureInvariant)]
    private static partial Regex SecretPattern();

    [GeneratedRegex(@"\b[A-Z0-9._%+-]+@[A-Z0-9.-]+\.[A-Z]{2,}\b", RegexOptions.CultureInvariant | RegexOptions.IgnoreCase)]
    private static partial Regex EmailPattern();
}

public sealed record ProcessDriverRedactionResult(
    ProcessDriverRedactionDescriptor Descriptor,
    string RedactedText,
    bool WasTruncated);

internal static class ProcessDriverEvidenceHash
{
    public static string ComputeSha256(string value)
    {
        return CanDoItAll.Processes.Drivers.Abstractions.Evidence.ProcessDriverEvidencePolicy.ComputeSha256(value);
    }
}
