using System.Security.Cryptography;
using System.Text;
using CanDoItAll.Processes.Abstractions;

namespace CanDoItAll.Processes.Core;

public sealed record LoopFingerprintInput(
    ProcessRunId RootRunId,
    ProcessStepDefinitionId SourceStepId,
    BranchFamilyId BranchFamilyId,
    BranchOutcomeId OutcomeId,
    LoopFingerprintPolicyId PolicyId,
    IReadOnlyList<string> EvidenceKeys);

public static class ProcessLoopFingerprint
{
    public static string Create(LoopFingerprintInput input)
    {
        ArgumentNullException.ThrowIfNull(input);

        var builder = new StringBuilder();
        Append(builder, input.RootRunId.ToString());
        Append(builder, input.SourceStepId.ToString());
        Append(builder, input.BranchFamilyId.Value);
        Append(builder, input.OutcomeId.Value);
        Append(builder, input.PolicyId.Value);

        foreach (var key in input.EvidenceKeys.Order(StringComparer.Ordinal))
        {
            Append(builder, key);
        }

        var bytes = Encoding.UTF8.GetBytes(builder.ToString());
        var hash = SHA256.HashData(bytes);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    private static void Append(StringBuilder builder, string value)
    {
        builder.Append(value.Length);
        builder.Append(':');
        builder.Append(value);
        builder.Append('|');
    }
}
