using System.Security.Cryptography;
using System.Text;

namespace CanDoItAll.Processes.Drivers.Abstractions.Evidence;

public static class ProcessDriverEvidencePolicy
{
    public static IReadOnlyList<ProcessDriverEvidenceReference> NormalizeEvidenceReferences(
        IReadOnlyList<ProcessDriverEvidenceReference>? evidenceReferences)
    {
        var normalized = new List<ProcessDriverEvidenceReference>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var evidenceReference in evidenceReferences ?? [])
        {
            var normalizedReference = new ProcessDriverEvidenceReference(
                evidenceReference.Kind,
                evidenceReference.Uri.Trim(),
                NormalizeHash(evidenceReference.ContentHash),
                evidenceReference.CoreDescriptorFamily);
            var key = string.Join(
                "|",
                normalizedReference.Kind,
                normalizedReference.Uri,
                normalizedReference.ContentHash,
                normalizedReference.CoreDescriptorFamily);
            if (seen.Add(key))
            {
                normalized.Add(normalizedReference);
            }
        }

        return normalized;
    }

    public static ProcessDriverEvidenceReference CreateTranscriptEvidenceReference(
        ProcessDriverTranscriptReference transcriptReference,
        string transcriptText)
    {
        var transcriptHash = NormalizeHash(transcriptReference.TranscriptHash);

        return new ProcessDriverEvidenceReference(
            ProcessDriverEvidenceReferenceKind.CommandTranscript,
            transcriptReference.Uri.Trim(),
            IsSha256(transcriptHash) ? transcriptHash : ComputeSha256(transcriptText),
            ProcessDriverCoreDescriptorFamily.ExecutionEvidence);
    }

    public static ProcessDriverEvidenceUriPolicyResult ValidateApprovedSuppliedEvidenceUris(
        ProcessDriverTranscriptReference transcriptReference,
        IReadOnlyList<ProcessDriverEvidenceReference> evidenceReferences)
    {
        var transcriptUri = transcriptReference.Uri.Trim();
        if (!IsApprovedSuppliedEvidenceUri(transcriptUri))
        {
            return ProcessDriverEvidenceUriPolicyResult.Denied(transcriptUri);
        }

        foreach (var evidenceReference in evidenceReferences)
        {
            var evidenceUri = evidenceReference.Uri.Trim();
            if (!IsApprovedSuppliedEvidenceUri(evidenceUri))
            {
                return ProcessDriverEvidenceUriPolicyResult.Denied(evidenceUri);
            }
        }

        return ProcessDriverEvidenceUriPolicyResult.Success;
    }

    public static ProcessDriverEvidenceUriPolicyResult ValidateApprovedSuppliedEvidenceUris(
        IReadOnlyList<ProcessDriverEvidenceReference> evidenceReferences)
    {
        foreach (var evidenceReference in evidenceReferences)
        {
            var evidenceUri = evidenceReference.Uri.Trim();
            if (!IsApprovedSuppliedEvidenceUri(evidenceUri))
            {
                return ProcessDriverEvidenceUriPolicyResult.Denied(evidenceUri);
            }
        }

        return ProcessDriverEvidenceUriPolicyResult.Success;
    }

    public static bool IsApprovedSuppliedEvidenceUri(string uri)
    {
        var trimmed = uri.Trim();

        return trimmed.StartsWith("bundle://", StringComparison.OrdinalIgnoreCase) ||
            trimmed.StartsWith("process://", StringComparison.OrdinalIgnoreCase) ||
            trimmed.StartsWith("artifact://", StringComparison.OrdinalIgnoreCase) ||
            trimmed.StartsWith("repo://tests/", StringComparison.OrdinalIgnoreCase);
    }

    public static bool HasValidSha256ContentHash(ProcessDriverEvidenceReference evidenceReference)
    {
        return IsSha256(evidenceReference.ContentHash);
    }

    public static bool TranscriptHashMatches(
        ProcessDriverTranscriptReference transcriptReference,
        string transcriptText)
    {
        var expectedTranscriptHash = NormalizeHash(transcriptReference.TranscriptHash);

        return IsSha256(expectedTranscriptHash) &&
            expectedTranscriptHash == ComputeSha256(transcriptText);
    }

    public static string ComputeSha256(string value)
    {
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)));
    }

    public static string NormalizeHash(string value)
    {
        return value.Trim().ToUpperInvariant();
    }

    public static bool IsSha256(string value)
    {
        return value.Length == 64 && value.All(static character =>
            character is >= '0' and <= '9' or >= 'A' and <= 'F' or >= 'a' and <= 'f');
    }
}

public sealed record ProcessDriverEvidenceUriPolicyResult(
    bool Accepted,
    string RejectedUri)
{
    public static ProcessDriverEvidenceUriPolicyResult Success { get; } = new(true, string.Empty);

    public static ProcessDriverEvidenceUriPolicyResult Denied(string rejectedUri)
    {
        return new ProcessDriverEvidenceUriPolicyResult(false, rejectedUri.Trim());
    }
}
