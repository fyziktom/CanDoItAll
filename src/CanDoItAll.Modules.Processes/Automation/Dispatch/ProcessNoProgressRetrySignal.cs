using System.Text.Json;

namespace CanDoItAll.Modules.Processes;

internal sealed partial class ProcessRunAutomationDispatchService
{
    internal sealed record NoProgressRetrySignal(
        string Fingerprint,
        Guid ExecutionRunId,
        string ToolSignature,
        string ArtifactValidationFingerprint,
        string MutationDelta,
        string ProofDelta);

    private static class ProcessNoProgressRetryLedgerRules
    {
        public static bool HasPriorSignal(
            IEnumerable<ProcessJournalEntry> journalEntries,
            NoProgressRetrySignal signal)
        {
            ArgumentNullException.ThrowIfNull(journalEntries);
            ArgumentNullException.ThrowIfNull(signal);

            return journalEntries.Any(entry =>
                IsLedgerEvent(entry.EventType) &&
                string.Equals(entry.CorrelationId, signal.Fingerprint, StringComparison.Ordinal) &&
                TryResolveExecutionRunId(entry.ReplayContextJson, out var priorExecutionRunId) &&
                priorExecutionRunId != signal.ExecutionRunId);
        }

        public static bool IsLedgerEvent(string eventType)
        {
            return string.Equals(eventType, ProcessRuntimeEventTypes.NoProgressRetryObserved, StringComparison.Ordinal) ||
                   string.Equals(eventType, ProcessRuntimeEventTypes.NoProgressRetryCompressed, StringComparison.Ordinal);
        }

        public static bool TryResolveExecutionRunId(
            string? replayContextJson,
            out Guid executionRunId)
        {
            executionRunId = default;
            if (string.IsNullOrWhiteSpace(replayContextJson))
            {
                return false;
            }

            try
            {
                using var document = JsonDocument.Parse(replayContextJson);
                if (!document.RootElement.TryGetProperty(nameof(NoProgressRetrySignal.ExecutionRunId), out var executionRunIdElement))
                {
                    return false;
                }

                if (executionRunIdElement.ValueKind == JsonValueKind.String &&
                    Guid.TryParse(executionRunIdElement.GetString(), out executionRunId))
                {
                    return true;
                }

                return executionRunIdElement.TryGetGuid(out executionRunId);
            }
            catch (JsonException)
            {
                return false;
            }
        }
    }
}
