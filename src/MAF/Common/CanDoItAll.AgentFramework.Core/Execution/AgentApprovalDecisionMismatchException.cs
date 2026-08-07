using System.Text;
using CanDoItAll.AgentFramework.Models;

namespace CanDoItAll.AgentFramework.Core;

/// <summary>
/// Thrown when a caller submits per-proposal approval decisions that do not exactly match the
/// execution run's (or chat session's) current pending-approval set: every pending approval must
/// receive exactly one decision, every decision must reference a pending approval, and no
/// approval id may be decided twice. This is the strict, fail-closed validation SB15 requires —
/// a partial or stale decision set is rejected rather than silently ignored or guessed at.
/// </summary>
public sealed class AgentApprovalDecisionMismatchException : InvalidOperationException
{
    public AgentApprovalDecisionMismatchException(
        IReadOnlyList<string> missingApprovalIds,
        IReadOnlyList<string> unknownApprovalIds,
        IReadOnlyList<string> duplicateApprovalIds)
        : base(BuildMessage(missingApprovalIds, unknownApprovalIds, duplicateApprovalIds))
    {
        MissingApprovalIds = missingApprovalIds;
        UnknownApprovalIds = unknownApprovalIds;
        DuplicateApprovalIds = duplicateApprovalIds;
    }

    /// <summary>Pending approvals that received no decision.</summary>
    public IReadOnlyList<string> MissingApprovalIds { get; }

    /// <summary>Decisions whose approval id does not match any pending approval.</summary>
    public IReadOnlyList<string> UnknownApprovalIds { get; }

    /// <summary>Approval ids that received more than one decision.</summary>
    public IReadOnlyList<string> DuplicateApprovalIds { get; }

    private static string BuildMessage(
        IReadOnlyList<string> missingApprovalIds,
        IReadOnlyList<string> unknownApprovalIds,
        IReadOnlyList<string> duplicateApprovalIds)
    {
        var builder = new StringBuilder(
            "The submitted approval decisions do not exactly match this run's pending approvals.");

        if (missingApprovalIds.Count > 0)
        {
            builder.Append(" Missing decisions for: ").Append(string.Join(", ", missingApprovalIds)).Append('.');
        }

        if (unknownApprovalIds.Count > 0)
        {
            builder.Append(" Unknown approval ids: ").Append(string.Join(", ", unknownApprovalIds)).Append('.');
        }

        if (duplicateApprovalIds.Count > 0)
        {
            builder.Append(" Duplicate decisions for: ").Append(string.Join(", ", duplicateApprovalIds)).Append('.');
        }

        return builder.ToString();
    }

    /// <summary>
    /// Validates that <paramref name="decisions"/> covers <paramref name="pendingApprovals"/>
    /// exactly — no partial sets, no unknown ids, no duplicates. Throws
    /// <see cref="AgentApprovalDecisionMismatchException"/> on any mismatch.
    /// </summary>
    public static void ValidateExactCoverage(
        IReadOnlyList<PendingToolApprovalDecision> decisions,
        IReadOnlyList<PendingToolApprovalRecord> pendingApprovals)
    {
        ArgumentNullException.ThrowIfNull(decisions);
        ArgumentNullException.ThrowIfNull(pendingApprovals);

        if (decisions.Count == 0)
        {
            throw new ArgumentException(
                "At least one approval decision is required.",
                nameof(decisions));
        }

        var pendingIds = pendingApprovals
            .Select(item => item.ApprovalId)
            .ToArray();
        var pendingIdSet = new HashSet<string>(pendingIds, StringComparer.Ordinal);

        var seen = new HashSet<string>(StringComparer.Ordinal);
        var unknown = new List<string>();
        var duplicate = new List<string>();

        foreach (var decision in decisions)
        {
            if (!pendingIdSet.Contains(decision.ApprovalId))
            {
                unknown.Add(decision.ApprovalId);
                continue;
            }

            if (!seen.Add(decision.ApprovalId))
            {
                duplicate.Add(decision.ApprovalId);
            }
        }

        var missing = pendingIds
            .Where(id => !seen.Contains(id))
            .ToArray();

        if (missing.Length == 0 && unknown.Count == 0 && duplicate.Count == 0)
        {
            return;
        }

        throw new AgentApprovalDecisionMismatchException(
            missing,
            unknown.Distinct(StringComparer.Ordinal).ToArray(),
            duplicate.Distinct(StringComparer.Ordinal).ToArray());
    }
}
