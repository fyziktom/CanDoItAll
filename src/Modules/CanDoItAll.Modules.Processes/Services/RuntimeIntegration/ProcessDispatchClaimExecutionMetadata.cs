using System.Text.Json;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Processes.Drivers.Abstractions;

namespace CanDoItAll.Modules.Processes;

internal static class ProcessDispatchClaimExecutionMetadata
{
    internal const string MetadataKey = "agentProcessDispatchClaimIdentity";

    internal static void Add(
        IDictionary<string, object> metadata,
        ProcessDispatchClaimIdentity identity)
    {
        ArgumentNullException.ThrowIfNull(metadata);

        if (identity.Value == Guid.Empty)
        {
            throw new InvalidOperationException("A recorded process dispatch claim identity must be non-empty.");
        }

        metadata.Add(MetadataKey, identity.Value.ToString("D"));
    }

    internal static bool Matches(
        ExecutionRunRecord executionRun,
        ProcessDispatchClaimIdentity expectedIdentity)
    {
        ArgumentNullException.ThrowIfNull(executionRun);

        return TryRead(executionRun, out var recordedIdentity) &&
               recordedIdentity == expectedIdentity;
    }

    internal static bool TryRead(
        ExecutionRunRecord executionRun,
        out ProcessDispatchClaimIdentity identity)
    {
        ArgumentNullException.ThrowIfNull(executionRun);

        identity = default;
        if (string.IsNullOrWhiteSpace(executionRun.MetadataJson))
        {
            return false;
        }

        try
        {
            using var document = JsonDocument.Parse(executionRun.MetadataJson);
            if (document.RootElement.ValueKind != JsonValueKind.Object ||
                !document.RootElement.TryGetProperty(MetadataKey, out var value) ||
                value.ValueKind != JsonValueKind.String ||
                !Guid.TryParse(value.GetString(), out var claimIdentity) ||
                claimIdentity == Guid.Empty)
            {
                return false;
            }

            identity = new ProcessDispatchClaimIdentity(claimIdentity);
            return true;
        }
        catch (JsonException)
        {
            return false;
        }
    }
}
