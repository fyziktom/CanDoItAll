using System.Text;
using System.Text.Json;
using CanDoItAll.AgentFramework.Models;

namespace CanDoItAll.AgentFramework.Core;

public static class AgentPlanningProviderBounds {
    public const int MaximumResponseUtf8Bytes = 262_144;
    public const int MaximumArgumentsUtf8Bytes = 262_144;
    public const int MaximumTextUtf8Bytes = 131_072;
    public const int MaximumInputUtf8Bytes = 2_097_152;
    public const int MaximumOutputTokens = 8_192;
    private const int JsonEscapeExpansion = 6;
    private const int MetadataReserveBytes = 131_072;
    private const int MaximumProtocolNameCharacters = 512;
    private const int SourceBytes = AgentToolPreparedPayload.MaximumSourcePreparationUtf8Bytes;
    private const int JournalReservation = JsonEscapeExpansion * (MaximumResponseUtf8Bytes + MaximumArgumentsUtf8Bytes + 2 * SourceBytes) + MetadataReserveBytes;
    private const int DecisionReservation = JsonEscapeExpansion * SourceBytes + MetadataReserveBytes;

    public static bool SupportsPublication(AgentToolPreparedCall call)
        => call is { Payload: not null } && !string.IsNullOrWhiteSpace(call.CallId) &&
            call.CallId.Length <= MaximumProtocolNameCharacters && call.Payload.ToolName.Length <= MaximumProtocolNameCharacters &&
            Encoding.UTF8.GetByteCount(call.Payload.ArgumentsJson) <= MaximumArgumentsUtf8Bytes;

    internal static void RequireCapacity(AgentToolJournalRecord journal) {
        var decision = journal.PlanningDecision ?? throw new ArgumentException("Provider capacity requires an original planning decision.", nameof(journal));
        using var journalSize = new CapacityStream(AgentToolJournalRecord.MaximumUtf8Bytes - JournalReservation, decision);
        JsonSerializer.Serialize(journalSize, journal);
        using var decisionSize = new CapacityStream(AgentPlanningDecisionRecord.MaximumUtf8Bytes - DecisionReservation, decision);
        JsonSerializer.Serialize(decisionSize, decision);
    }

    private sealed class CapacityStream(long maximum, AgentPlanningDecisionRecord decision) : Stream {
        private long bytes;
        public override bool CanRead => false;
        public override bool CanSeek => false;
        public override bool CanWrite => true;
        public override long Length => bytes;
        public override long Position { get => bytes; set => throw new NotSupportedException(); }
        public override void Flush() { }
        public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => Count(count);
        public override void Write(ReadOnlySpan<byte> buffer) => Count(buffer.Length);
        private void Count(int count) {
            bytes = checked(bytes + count);
            if (bytes > maximum) {
                throw new AgentPlanningAdmissionException(decision.DecisionId, decision.CurrentGeneration, AgentPlanningAdmissionRefusal.HistoryCapacity);
            }
        }
    }
}
