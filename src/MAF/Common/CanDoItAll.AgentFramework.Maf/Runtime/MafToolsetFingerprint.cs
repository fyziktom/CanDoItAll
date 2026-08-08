using System.Security.Cryptography;
using System.Text;
using CanDoItAll.AgentFramework.Core;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;

namespace CanDoItAll.AgentFramework.Maf;

/// <summary>
/// Computes the toolset fingerprint threaded into <c>AgentRuntimeExecutionOptions</c> and, from
/// there, into the runtime-state envelope. Must be computed from the composed runtime tool
/// names (the capability-composition result), not the requested capability list, so it detects
/// provider/MCP/hosted-tool composition drift that the capability catalog alone would miss.
/// </summary>
internal static class MafToolsetFingerprint
{
    // ASCII Unit Separator (0x1F): a control character effectively never present in a tool
    // name, used to keep e.g. ["ab","c"] and ["a","bc"] from hashing to the same digest.
    private const char NameSeparator = (char)0x1F;

    /// <summary>
    /// Stable SHA-256 hex digest of the ordered, deduplicated tool name set. Always produces a
    /// real hash — including for an empty toolset — so an empty string stays an unambiguous
    /// "not computed" sentinel everywhere else in the pipeline.
    /// </summary>
    public static string Compute(IEnumerable<string?> toolNames)
    {
        ArgumentNullException.ThrowIfNull(toolNames);

        var ordered = toolNames
            .Select(name => name?.Trim() ?? string.Empty)
            .Where(name => name.Length > 0)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToArray();

        var payload = string.Join(NameSeparator, ordered);
        var payloadBytes = Encoding.UTF8.GetBytes(payload);
        var digestBytes = SHA256.HashData(payloadBytes);
        return Convert.ToHexString(digestBytes).ToLowerInvariant();
    }

    /// <summary>
    /// Stable SHA-256 hex digest of the composed tool contracts: for every
    /// tool, its name, invocation-policy classification, approval-wrapper
    /// flag, and (for functions) the JSON schema. Unlike the names-only
    /// digest, a schema, classification, or approval change with the same
    /// tool name produces a different fingerprint, so stale state cannot be
    /// restored across a tool-contract change (schema v2 dimension).
    /// </summary>
    public static string ComputeContractFingerprint(IEnumerable<AITool> tools)
    {
        ArgumentNullException.ThrowIfNull(tools);

        var entries = tools
            .Where(tool => !string.IsNullOrWhiteSpace(tool.Name))
            .Select(tool =>
            {
                var schemaText = tool is AIFunction function
                    ? function.JsonSchema.GetRawText()
                    : string.Empty;
                var classification = AgentToolInvocationPolicyMetadata.Classify(tool.Name);
                var approvalWrapped = tool is ApprovalRequiredAIFunction;
                return string.Join(
                    NameSeparator,
                    tool.Name.Trim(),
                    classification.ToString(),
                    approvalWrapped ? "approval" : "direct",
                    schemaText);
            })
            .Distinct(StringComparer.Ordinal)
            .OrderBy(entry => entry, StringComparer.Ordinal)
            .ToArray();

        var payload = string.Join("", entries);
        var digestBytes = SHA256.HashData(Encoding.UTF8.GetBytes(payload));
        return Convert.ToHexString(digestBytes).ToLowerInvariant();
    }
}
