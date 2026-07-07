using System.Security.Cryptography;
using System.Text;
using CanDoItAll.Processes.Abstractions;
using CanDoItAll.Processes.Builder;
using CanDoItAll.Processes.Drivers.Abstractions;

namespace CanDoItAll.Processes.Runtime;

public static class ProcessRuntimeArtifactContracts
{
    public static IReadOnlyList<ProcessRuntimeInputArtifactReceipt> BuildInitialInputArtifacts(
        IReadOnlyList<ProcessRuntimeStepAssignment> assignments,
        IReadOnlyList<ArtifactLedgerSeed> initialLedgerEntries)
    {
        if (assignments.Count == 0)
        {
            return [];
        }

        var initialArtifactBySlot = new Dictionary<ArtifactSlotId, ArtifactLedgerSeed>();
        foreach (var entry in initialLedgerEntries)
        {
            initialArtifactBySlot.TryAdd(entry.SlotId, entry);
        }

        var producerBySlot = new Dictionary<ArtifactSlotId, ProcessStepInstanceId>();
        foreach (var assignment in assignments.OrderBy(item => item.CreatedAtUtc).ThenBy(item => item.StepInstanceId.Value))
        {
            foreach (var slotId in assignment.ProducedArtifactSlotIds)
            {
                producerBySlot.TryAdd(slotId, assignment.StepInstanceId);
            }
        }

        var receipts = new List<ProcessRuntimeInputArtifactReceipt>();
        foreach (var assignment in assignments.OrderBy(item => item.CreatedAtUtc).ThenBy(item => item.StepInstanceId.Value))
        {
            foreach (var requiredSlotId in assignment.RequiredArtifactSlotIds.Distinct().OrderBy(item => item.Value))
            {
                receipts.Add(CreateInitialInputReceipt(
                    assignment.StepInstanceId,
                    requiredSlotId,
                    initialArtifactBySlot,
                    producerBySlot));
            }
        }

        return receipts;
    }

    public static IReadOnlyList<ProcessRuntimeInputArtifactReceipt> ApplyProducedArtifacts(
        ProcessRuntimeStateSnapshot state,
        ProcessStepInstanceId producerStepId,
        StrategyResultEnvelope result)
    {
        if (state.ConnectedInputArtifacts.Count == 0 || result.ProducedArtifacts.Count == 0)
        {
            return state.ConnectedInputArtifacts;
        }

        var producedBySlot = result.ProducedArtifacts
            .GroupBy(artifact => artifact.SlotId)
            .ToDictionary(group => group.Key, group => group.Last());
        var next = new List<ProcessRuntimeInputArtifactReceipt>(state.ConnectedInputArtifacts.Count);
        foreach (var receipt in state.ConnectedInputArtifacts)
        {
            if (!producedBySlot.TryGetValue(receipt.RequiredSlotId, out var producedArtifact) ||
                receipt.ProducerStepInstanceId != producerStepId)
            {
                next.Add(receipt);
                continue;
            }

            next.Add(CreateAvailableInputReceipt(
                receipt.ConsumerStepInstanceId,
                receipt.RequiredSlotId,
                producerStepId,
                producedArtifact.ArtifactId,
                producedArtifact.ContentHash));
        }

        return next;
    }

    public static bool DependenciesSatisfied(
        ProcessRuntimeStateSnapshot state,
        ProcessRuntimeStepState step)
    {
        foreach (var dependencyId in step.DependencyStepIds)
        {
            var dependency = state.Steps.FirstOrDefault(candidate => candidate.StepInstanceId == dependencyId);
            if (dependency is null ||
                !ProcessRuntimeTerminalStates.IsStepTerminal(dependency.Status) ||
                dependency.Status is ProcessRuntimeStepStatus.Failed or ProcessRuntimeStepStatus.Cancelled)
            {
                return false;
            }
        }

        return true;
    }

    public static bool RequiredArtifactsAvailable(
        ProcessRuntimeStateSnapshot state,
        ProcessRuntimeStepState step)
    {
        foreach (var slotId in step.RequiredArtifactSlots)
        {
            if (!HasAvailableInputArtifact(state, step.StepInstanceId, slotId))
            {
                return false;
            }
        }

        return true;
    }

    public static ProcessStepExecutionContract BuildStepContract(
        ProcessRuntimeStateSnapshot state,
        ProcessRuntimeStepState step)
    {
        var requiredArtifacts = new List<RequiredArtifactInputRef>();
        foreach (var slotId in step.RequiredArtifactSlots.OrderBy(item => item.Value))
        {
            var matchingReceipts = state.ConnectedInputArtifacts
                .Where(receipt => receipt.ConsumerStepInstanceId == step.StepInstanceId && receipt.RequiredSlotId == slotId)
                .OrderBy(receipt => receipt.ConnectionHash, StringComparer.Ordinal)
                .ToArray();
            if (matchingReceipts.Length == 0)
            {
                requiredArtifacts.Add(CreateMissingRequiredArtifactInputRef(state.RunId, step.StepInstanceId, slotId));
                continue;
            }

            foreach (var receipt in matchingReceipts)
            {
                requiredArtifacts.Add(new RequiredArtifactInputRef(
                    receipt.RequiredSlotId,
                    receipt.Availability,
                    receipt.ProducerStepInstanceId,
                    receipt.ArtifactId,
                    receipt.ContentHash,
                    receipt.ConnectionHash));
            }
        }

        var expectedProducedArtifacts = step.ProducedArtifactSlots
            .OrderBy(slotId => slotId.Value)
            .Select(slotId => new ExpectedProducedArtifactRef(slotId))
            .ToArray();
        var requiredToolNames = step.RequiredRuntimeToolNames
            .Where(toolName => !string.IsNullOrWhiteSpace(toolName))
            .Select(toolName => toolName.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(toolName => toolName, StringComparer.OrdinalIgnoreCase)
            .ToArray();
        var contractHash = ComputeStepContractHash(requiredArtifacts, expectedProducedArtifacts, requiredToolNames);

        return new ProcessStepExecutionContract(
            requiredArtifacts,
            expectedProducedArtifacts,
            requiredToolNames,
            contractHash);
    }

    public static ProcessStepInstanceId? FindResponsibleStepForMissingArtifact(
        ProcessRuntimeStateSnapshot state,
        ProcessStepInstanceId consumerStepId,
        StrategyResultEnvelope result)
    {
        foreach (var requestedArtifact in result.RequestedArtifacts)
        {
            var producer = FindExpectedProducer(state, consumerStepId, requestedArtifact.SlotId);
            if (producer is not null)
            {
                return producer;
            }
        }

        var consumerStep = state.Steps.FirstOrDefault(step => step.StepInstanceId == consumerStepId);
        if (consumerStep is null)
        {
            return null;
        }

        foreach (var slotId in consumerStep.RequiredArtifactSlots.OrderBy(item => item.Value))
        {
            if (HasAvailableInputArtifact(state, consumerStepId, slotId))
            {
                continue;
            }

            var producer = FindExpectedProducer(state, consumerStepId, slotId);
            if (producer is not null)
            {
                return producer;
            }
        }

        return null;
    }

    private static ProcessRuntimeInputArtifactReceipt CreateInitialInputReceipt(
        ProcessStepInstanceId consumerStepId,
        ArtifactSlotId requiredSlotId,
        IReadOnlyDictionary<ArtifactSlotId, ArtifactLedgerSeed> initialArtifactBySlot,
        IReadOnlyDictionary<ArtifactSlotId, ProcessStepInstanceId> producerBySlot)
    {
        if (initialArtifactBySlot.TryGetValue(requiredSlotId, out var initialArtifact))
        {
            return CreateAvailableInputReceipt(
                consumerStepId,
                requiredSlotId,
                producerStepId: null,
                initialArtifact.ArtifactId,
                initialArtifact.ContentHash);
        }

        if (producerBySlot.TryGetValue(requiredSlotId, out var producerStepId))
        {
            return CreateInputReceipt(
                consumerStepId,
                requiredSlotId,
                ProcessArtifactInputAvailability.Expected,
                producerStepId,
                artifactId: null,
                contentHash: string.Empty);
        }

        return CreateInputReceipt(
            consumerStepId,
            requiredSlotId,
            ProcessArtifactInputAvailability.Missing,
            producerStepInstanceId: null,
            artifactId: null,
            contentHash: string.Empty);
    }

    private static ProcessRuntimeInputArtifactReceipt CreateAvailableInputReceipt(
        ProcessStepInstanceId consumerStepId,
        ArtifactSlotId requiredSlotId,
        ProcessStepInstanceId? producerStepId,
        ArtifactInstanceId artifactId,
        string contentHash)
    {
        return CreateInputReceipt(
            consumerStepId,
            requiredSlotId,
            ProcessArtifactInputAvailability.Available,
            producerStepId,
            artifactId,
            contentHash);
    }

    private static ProcessRuntimeInputArtifactReceipt CreateInputReceipt(
        ProcessStepInstanceId consumerStepId,
        ArtifactSlotId requiredSlotId,
        ProcessArtifactInputAvailability availability,
        ProcessStepInstanceId? producerStepInstanceId,
        ArtifactInstanceId? artifactId,
        string contentHash)
    {
        var normalizedContentHash = string.IsNullOrWhiteSpace(contentHash)
            ? string.Empty
            : contentHash.Trim();
        return new ProcessRuntimeInputArtifactReceipt(
            consumerStepId,
            requiredSlotId,
            availability,
            producerStepInstanceId,
            artifactId,
            normalizedContentHash,
            ComputeConnectionHash(consumerStepId, requiredSlotId, producerStepInstanceId, artifactId, normalizedContentHash, availability));
    }

    private static RequiredArtifactInputRef CreateMissingRequiredArtifactInputRef(
        ProcessRunId runId,
        ProcessStepInstanceId stepId,
        ArtifactSlotId slotId)
    {
        return new RequiredArtifactInputRef(
            slotId,
            ProcessArtifactInputAvailability.Missing,
            ProducerStepId: null,
            ArtifactId: null,
            ContentHash: string.Empty,
            ConnectionHash: ComputeHash($"missing-input:{runId.Value:N}:{stepId.Value:N}:{slotId.Value:N}"));
    }

    private static bool HasAvailableInputArtifact(
        ProcessRuntimeStateSnapshot state,
        ProcessStepInstanceId stepId,
        ArtifactSlotId slotId)
    {
        return state.ConnectedInputArtifacts.Any(receipt =>
            receipt.ConsumerStepInstanceId == stepId &&
            receipt.RequiredSlotId == slotId &&
            receipt.Availability == ProcessArtifactInputAvailability.Available &&
            receipt.ArtifactId is not null &&
            !string.IsNullOrWhiteSpace(receipt.ContentHash));
    }

    private static ProcessStepInstanceId? FindExpectedProducer(
        ProcessRuntimeStateSnapshot state,
        ProcessStepInstanceId consumerStepId,
        ArtifactSlotId slotId)
    {
        return state.ConnectedInputArtifacts
            .Where(receipt =>
                receipt.ConsumerStepInstanceId == consumerStepId &&
                receipt.RequiredSlotId == slotId &&
                receipt.Availability != ProcessArtifactInputAvailability.Available)
            .Select(receipt => receipt.ProducerStepInstanceId)
            .FirstOrDefault(producerStepId => producerStepId is not null);
    }

    private static string ComputeConnectionHash(
        ProcessStepInstanceId consumerStepId,
        ArtifactSlotId requiredSlotId,
        ProcessStepInstanceId? producerStepId,
        ArtifactInstanceId? artifactId,
        string contentHash,
        ProcessArtifactInputAvailability availability)
    {
        var payload = string.Join(
            ':',
            "input",
            consumerStepId.Value.ToString("N"),
            requiredSlotId.Value.ToString("N"),
            producerStepId?.Value.ToString("N") ?? "none",
            artifactId?.Value.ToString("N") ?? "none",
            availability,
            contentHash);
        return ComputeHash(payload);
    }

    private static string ComputeStepContractHash(
        IReadOnlyList<RequiredArtifactInputRef> requiredArtifacts,
        IReadOnlyList<ExpectedProducedArtifactRef> expectedProducedArtifacts,
        IReadOnlyList<string> requiredToolNames)
    {
        var builder = new StringBuilder("step-contract");
        foreach (var artifact in requiredArtifacts)
        {
            builder
                .Append('|')
                .Append(artifact.SlotId.Value.ToString("N"))
                .Append(':')
                .Append(artifact.Availability)
                .Append(':')
                .Append(artifact.ProducerStepId?.Value.ToString("N") ?? "none")
                .Append(':')
                .Append(artifact.ArtifactId?.Value.ToString("N") ?? "none")
                .Append(':')
                .Append(artifact.ContentHash)
                .Append(':')
                .Append(artifact.ConnectionHash);
        }

        foreach (var artifact in expectedProducedArtifacts)
        {
            builder
                .Append("|out:")
                .Append(artifact.SlotId.Value.ToString("N"));
        }

        foreach (var toolName in requiredToolNames)
        {
            builder
                .Append("|tool:")
                .Append(toolName);
        }

        return ComputeHash(builder.ToString());
    }

    private static string ComputeHash(string value)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(value));
        return $"sha256:{Convert.ToHexString(bytes).ToLowerInvariant()}";
    }
}
