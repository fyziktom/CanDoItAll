using System.Globalization;
using System.Text;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.SharedKernel;

namespace CanDoItAll.AgentFramework.Core;

public static class AgentChatExternalTargetAccessAttachmentPublisher
{
    public static AgentChatContextAttachmentDraft? CreateReadOnlyDraft(
        IEnumerable<string>? pathsOrAliases,
        DatabaseProfileGeneration databaseProfileGeneration,
        DateTimeOffset capturedAtUtc,
        DateTimeOffset? freshUntilUtc)
        => AgentChatExternalTargetAccessAttachmentFactory.CreateReadOnlyDraft(
            pathsOrAliases,
            databaseProfileGeneration,
            capturedAtUtc,
            freshUntilUtc);

    public static AgentChatContextAttachmentDraft ReuseCurrent(
        AgentChatContextAttachmentDraft? previous,
        AgentChatContextAttachmentDraft candidate,
        DateTimeOffset nowUtc)
        => AgentChatExternalTargetAccessAttachmentFactory.ReuseCurrent(
            previous,
            candidate,
            nowUtc);
}

internal static class AgentChatExternalTargetAccessAttachmentFactory
{
    internal const string AttachmentKindValue =
        "project-structure.external-target-access.v1";
    internal const string TrustedSourceKindValue = "project-structure";
    internal const string TrustedContributorIdValue = "project-structure.selection";

    private const string ContentFingerprintVersion =
        "project-structure.external-target-access.content.v1";
    private const string CoverageFingerprintVersion =
        "project-structure.external-target-access.coverage.v1";
    private const string FreshnessFingerprintVersion =
        "project-structure.external-target-access.freshness.v1";

    internal static AgentChatContextAttachmentDraft? CreateReadOnlyDraft(
        IEnumerable<string>? pathsOrAliases,
        DatabaseProfileGeneration databaseProfileGeneration,
        DateTimeOffset capturedAtUtc,
        DateTimeOffset? freshUntilUtc)
    {
        var aliases = pathsOrAliases?
            .Select(AgentWorkspaceToolAccessMetadata.NormalizeExternalTargetAlias)
            .Where(static alias => !string.IsNullOrWhiteSpace(alias))
            .Cast<string>()
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Order(StringComparer.OrdinalIgnoreCase)
            .ToArray() ?? [];
        if (aliases.Length == 0)
        {
            return null;
        }

        var attachment = new AgentChatExternalTargetAccessAttachment(aliases);
        var contentFingerprint = new SnapshotContentFingerprint(
            ComputeHash(
                ContentFingerprintVersion,
                attachment.ReadOnlyAliases));
        var coverageFingerprint = new SnapshotCoverageFingerprint(
            ComputeCoverageFingerprint());
        var freshnessFingerprint = new SnapshotFreshnessFingerprint(
            ComputeFreshnessFingerprint(
                contentFingerprint,
                coverageFingerprint,
                databaseProfileGeneration));
        var normalizedCapturedAtUtc = capturedAtUtc.ToUniversalTime();

        return new AgentChatContextAttachmentDraft(
            new AgentChatContextAttachmentKind(AttachmentKindValue),
            contentFingerprint,
            coverageFingerprint,
            databaseProfileGeneration,
            freshnessFingerprint,
            normalizedCapturedAtUtc,
            freshUntilUtc?.ToUniversalTime(),
            attachment);
    }

    internal static AgentChatContextAttachmentDraft ReuseCurrent(
        AgentChatContextAttachmentDraft? previous,
        AgentChatContextAttachmentDraft candidate,
        DateTimeOffset nowUtc)
    {
        ArgumentNullException.ThrowIfNull(candidate);
        if (previous is null ||
            previous.FreshUntilUtc is { } freshUntilUtc && nowUtc >= freshUntilUtc)
        {
            return candidate;
        }

        return previous.Kind == candidate.Kind &&
               previous.ContentFingerprint == candidate.ContentFingerprint &&
               previous.CoverageFingerprint == candidate.CoverageFingerprint &&
               previous.DatabaseProfileGeneration == candidate.DatabaseProfileGeneration &&
               previous.FreshnessFingerprint == candidate.FreshnessFingerprint
            ? previous
            : candidate;
    }

    internal static bool TryGetValidatedReadOnlyAliases(
        AgentChatContextAttachmentEnvelope envelope,
        DatabaseProfileGeneration currentDatabaseProfileGeneration,
        DateTimeOffset nowUtc,
        out IReadOnlyList<string> readOnlyAliases)
    {
        ArgumentNullException.ThrowIfNull(envelope);
        readOnlyAliases = [];
        if (!string.Equals(
                envelope.Kind.Value,
                AttachmentKindValue,
                StringComparison.Ordinal) ||
            !envelope.TryGetAttachment<AgentChatExternalTargetAccessAttachment>(
                out var attachment) ||
            envelope.ResolveFreshness(
                currentDatabaseProfileGeneration,
                nowUtc) != AgentChatContextAttachmentFreshness.Current)
        {
            return false;
        }

        var expectedContentFingerprint = new SnapshotContentFingerprint(
            ComputeHash(
                ContentFingerprintVersion,
                attachment.ReadOnlyAliases));
        var expectedCoverageFingerprint = new SnapshotCoverageFingerprint(
            ComputeCoverageFingerprint());
        var expectedFreshnessFingerprint = new SnapshotFreshnessFingerprint(
            ComputeFreshnessFingerprint(
                expectedContentFingerprint,
                expectedCoverageFingerprint,
                envelope.DatabaseProfileGeneration));
        if (envelope.ContentFingerprint != expectedContentFingerprint ||
            envelope.CoverageFingerprint != expectedCoverageFingerprint ||
            envelope.FreshnessFingerprint != expectedFreshnessFingerprint)
        {
            return false;
        }

        readOnlyAliases = attachment.ReadOnlyAliases;
        return true;
    }

    private static string ComputeCoverageFingerprint()
        => ComputeHash(
            CoverageFingerprintVersion,
            ["typed-owning-project-block-root", "read-only"]);

    private static string ComputeFreshnessFingerprint(
        SnapshotContentFingerprint contentFingerprint,
        SnapshotCoverageFingerprint coverageFingerprint,
        DatabaseProfileGeneration databaseProfileGeneration)
        => ComputeHash(
            FreshnessFingerprintVersion,
            [
                contentFingerprint.Value,
                coverageFingerprint.Value,
                databaseProfileGeneration.Value.ToString(CultureInfo.InvariantCulture)
            ]);

    private static string ComputeHash(
        string version,
        IEnumerable<string> values)
    {
        var builder = new StringBuilder();
        Append(builder, version);
        foreach (var value in values)
        {
            Append(builder, value);
        }

        return StableContentHash.ComputeSha256Hex(builder.ToString());
    }

    private static void Append(StringBuilder builder, string value)
    {
        builder
            .Append(value.Length.ToString(CultureInfo.InvariantCulture))
            .Append(':')
            .Append(value)
            .Append('|');
    }
}
