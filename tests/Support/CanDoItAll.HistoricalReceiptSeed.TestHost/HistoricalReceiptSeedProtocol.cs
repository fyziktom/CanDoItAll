using System.Text.Json;
using CanDoItAll.AgentFramework.Models;

namespace CanDoItAll.Tests.HistoricalReceipts;

internal enum HistoricalReceiptSeedKind { Interactive, Process }

internal sealed record HistoricalReceiptDigest(Guid OperationId, string Sha256);

internal sealed record HistoricalReceiptSeeded(int SchemaVersion, Guid Nonce, int ProcessId, DateTimeOffset ProcessStartedAtUtc,
    HistoricalReceiptSeedKind Kind, Guid ProfileId, long OriginalGeneration, Guid NamespaceId, Guid ProjectId,
    Guid ProjectLifetimeId, Guid SourceAgentId, Guid? ProcessRunId, Guid? ProcessStepId,
    AgentHistoricalReceiptReviewRequest Request, string RunFileSha256, IReadOnlyList<HistoricalReceiptDigest> Receipts);

internal static class HistoricalReceiptSeedProtocol {
    internal const int Version = 1;
    internal const int MaximumFrameBytes = 64 * 1024;
    internal const string OutputRootEnvironment = "CDITALL_HISTORICAL_RESTART_OUTPUT_ROOT";
    internal const string EnvironmentRootEnvironment = "CDITALL_HISTORICAL_SEED_ENVIRONMENT_ROOT";
    internal const string ToolProviderKey = "project-structure.runtime-tools";
    internal const string ProcessClaimMetadataKey = "agentProcessDispatchClaimIdentity";
    internal static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    internal static void Validate(HistoricalReceiptSeeded frame) {
        if (frame.SchemaVersion != Version || frame.Nonce == Guid.Empty || frame.ProcessId <= 0 ||
            frame.ProcessStartedAtUtc == default || !Enum.IsDefined(frame.Kind) || frame.ProfileId == Guid.Empty ||
            frame.OriginalGeneration <= 0 || frame.NamespaceId == Guid.Empty || frame.ProjectId == Guid.Empty ||
            frame.ProjectLifetimeId == Guid.Empty || frame.SourceAgentId == Guid.Empty || frame.Request is null ||
            frame.Request.Cursor is not null || frame.Receipts is not { Count: 5 } || !IsDigest(frame.RunFileSha256)) {
            throw new InvalidDataException("The seed child did not publish a complete bounded result.");
        }
        if (frame.Kind == HistoricalReceiptSeedKind.Interactive &&
                (frame.ProcessRunId is not null || frame.ProcessStepId is not null || frame.Request.ChatSessionId is null || frame.SourceAgentId != frame.Request.AgentId) ||
            frame.Kind == HistoricalReceiptSeedKind.Process &&
                (frame.ProcessRunId is null || frame.ProcessRunId == Guid.Empty || frame.ProcessStepId is null || frame.ProcessStepId == Guid.Empty ||
                    frame.Request.ChatSessionId is not null || frame.SourceAgentId == frame.Request.AgentId)) {
            throw new InvalidDataException("The seed source identities do not match the selected scenario.");
        }
        if (frame.Receipts.Any(item => item is null || item.OperationId == Guid.Empty || !IsDigest(item.Sha256)) ||
            frame.Receipts.Select(item => item.OperationId).Distinct().Count() != 5) {
            throw new InvalidDataException("The seed must retain five distinct actual owner receipts.");
        }
    }

    internal static bool IsDigest(string? value)
        => value is { Length: 64 } && value.All(character => character is >= '0' and <= '9' or >= 'A' and <= 'F');
}
