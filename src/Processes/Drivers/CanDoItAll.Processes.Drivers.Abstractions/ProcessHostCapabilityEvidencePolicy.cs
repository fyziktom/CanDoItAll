namespace CanDoItAll.Processes.Drivers.Abstractions;

public static class ProcessHostCapabilityEvidencePolicy
{
    public static bool IsValid(ProcessHostCapabilityEvaluationEvidence? evidence)
    {
        if (evidence is null)
        {
            return true;
        }

        return new ProcessHostCapabilitySnapshot(
            evidence.ProfileId,
            evidence.Capabilities).IsStructurallyValid();
    }

    public static bool TryMerge(
        ProcessHostCapabilityEvaluationEvidence? first,
        ProcessHostCapabilityEvaluationEvidence? second,
        out ProcessHostCapabilityEvaluationEvidence? merged)
    {
        merged = null;
        if (!IsValid(first) || !IsValid(second))
        {
            return false;
        }

        if (first is null)
        {
            merged = second;
            return true;
        }

        if (second is null)
        {
            merged = first;
            return true;
        }

        if (first.ProfileId != second.ProfileId)
        {
            return false;
        }

        var facts = new Dictionary<ProcessHostCapabilityId, ProcessHostCapabilityFact>();
        foreach (var fact in first.Capabilities)
        {
            facts.Add(fact.Id, fact);
        }

        foreach (var fact in second.Capabilities)
        {
            if (facts.TryGetValue(fact.Id, out var existing) && existing != fact)
            {
                return false;
            }

            facts.TryAdd(fact.Id, fact);
        }

        if (facts.Count > ProcessHostCapabilitySnapshot.MaximumCapabilities)
        {
            return false;
        }

        merged = new ProcessHostCapabilityEvaluationEvidence(
            first.ProfileId,
            facts.Values
                .OrderBy(fact => fact.Id.Value, StringComparer.Ordinal)
                .ToArray());
        return true;
    }

    public static ProcessHostCapabilityEvaluationEvidence CreateUnstableEvidence(
        ProcessHostCapabilityEvaluationEvidence? first,
        ProcessHostCapabilityEvaluationEvidence? second)
    {
        var firstFacts = IsValid(first)
            ? first?.Capabilities ?? []
            : [];
        var secondFacts = IsValid(second)
            ? second?.Capabilities ?? []
            : [];
        var ids = firstFacts
            .Concat(secondFacts)
            .Select(fact => fact.Id)
            .Distinct()
            .OrderBy(id => id.Value, StringComparer.Ordinal)
            .Take(ProcessHostCapabilitySnapshot.MaximumCapabilities)
            .ToArray();
        return new ProcessHostCapabilityEvaluationEvidence(
            new ProcessHostProfileId("unstable"),
            ids.Select(id => new ProcessHostCapabilityFact(
                    id,
                    ProcessHostCapabilityAvailability.Unverified,
                    ProcessHostCapabilityReason.ProbePending,
                    ProcessHostExecutionPort.None))
                .ToArray());
    }
}
