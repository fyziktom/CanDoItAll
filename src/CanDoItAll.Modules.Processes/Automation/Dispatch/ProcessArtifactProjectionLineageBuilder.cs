using System.Security.Cryptography;
using System.Text;

namespace CanDoItAll.Modules.Processes;

internal sealed record ProcessArtifactRecoveryProjectionContext(
    Guid? RecoveryExecutionRunId,
    Guid? RecoveredForExecutionRunId,
    Guid? ReworkPacketId)
{
    public static ProcessArtifactRecoveryProjectionContext None { get; } = new(null, null, null);

    public bool HasRecoveryLineage => RecoveryExecutionRunId.HasValue && RecoveredForExecutionRunId.HasValue;
}

internal static class ProcessArtifactProjectionLineageBuilder
{
    public static string ApplyRecoveryLineage(
        string sourceExternalReferenceKey,
        Guid projectedExecutionRunId,
        ProcessArtifactRecoveryProjectionContext recoveryContext)
    {
        ArgumentNullException.ThrowIfNull(recoveryContext);

        if (!recoveryContext.HasRecoveryLineage)
        {
            return sourceExternalReferenceKey;
        }

        var hashInput = string.Join(
            "|",
            recoveryContext.RecoveryExecutionRunId!.Value.ToString("D"),
            recoveryContext.RecoveredForExecutionRunId!.Value.ToString("D"),
            projectedExecutionRunId.ToString("D"),
            recoveryContext.ReworkPacketId?.ToString("D") ?? string.Empty,
            sourceExternalReferenceKey);
        var hash = Convert
            .ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(hashInput)).AsSpan(0, 16))
            .ToLowerInvariant();

        return $"manager-recovery-artifact|sha256:{hash}";
    }

    public static ProcessArtifactProjectionLineage BuildLineage(
        ProcessArtifactProjectionSourceKind sourceKind,
        Guid? sourceExecutionRunId,
        ProcessArtifactRecoveryProjectionContext recoveryContext,
        Guid? sourceArtifactId = null,
        string sourceExternalReferenceKey = "")
    {
        ArgumentNullException.ThrowIfNull(recoveryContext);

        return new ProcessArtifactProjectionLineage
        {
            SourceKind = sourceKind,
            SourceExecutionRunId = sourceExecutionRunId,
            RecoveryExecutionRunId = recoveryContext.RecoveryExecutionRunId,
            RecoveredForExecutionRunId = recoveryContext.RecoveredForExecutionRunId,
            ProjectedExecutionRunId = sourceExecutionRunId,
            SourceArtifactId = sourceArtifactId,
            ReworkPacketId = recoveryContext.ReworkPacketId,
            SourceExternalReferenceKey = sourceExternalReferenceKey
        };
    }

    public static string BuildProvenance(
        string baseProvenance,
        Guid projectedExecutionRunId,
        ProcessArtifactRecoveryProjectionContext recoveryContext)
    {
        ArgumentNullException.ThrowIfNull(recoveryContext);

        if (!recoveryContext.HasRecoveryLineage)
        {
            return baseProvenance;
        }

        var reworkPacketSummary = recoveryContext.ReworkPacketId.HasValue
            ? $" Rework packet id: {recoveryContext.ReworkPacketId.Value:D}."
            : string.Empty;

        return $"{baseProvenance} Manager recovery lineage: recovery execution run {recoveryContext.RecoveryExecutionRunId!.Value:D}; recovered-for execution run {recoveryContext.RecoveredForExecutionRunId!.Value:D}; projected execution run {projectedExecutionRunId:D}.{reworkPacketSummary}";
    }
}
