using CanDoItAll.Processes.Abstractions;

namespace CanDoItAll.Processes.Core;

public sealed record ProcessArtifactDefinition(
    ArtifactDefinitionId Id,
    string Key,
    ProcessArtifactSensitivity Sensitivity);

public sealed record ProcessArtifactSlotDefinition(
    ArtifactSlotId Id,
    string Key,
    ArtifactDefinitionId ArtifactDefinitionId,
    ProcessArtifactRequirementMode RequirementMode,
    ProcessArtifactScope Scope,
    bool HasBoundaryPolicy);

public sealed record ProcessArtifactReference(
    ArtifactSlotId SlotId,
    ArtifactInstanceId ArtifactId,
    ProcessArtifactScope Scope,
    string ContentHash);

public enum ProcessArtifactRequirementMode
{
    Required,
    Optional,
    Produced
}

public enum ProcessArtifactScope
{
    Local,
    Parent,
    Child,
    External
}

public enum ProcessArtifactSensitivity
{
    Unspecified,
    Normal,
    Restricted
}

public static class ProcessArtifactRules
{
    public static ProcessValidationResult Validate(
        IReadOnlyList<ProcessArtifactDefinition> artifacts,
        IReadOnlyList<ProcessArtifactSlotDefinition> slots)
    {
        ArgumentNullException.ThrowIfNull(artifacts);
        ArgumentNullException.ThrowIfNull(slots);

        var failures = new List<ProcessValidationFailure>();
        var artifactIds = new HashSet<ArtifactDefinitionId>();
        var artifactKeys = new HashSet<string>(StringComparer.Ordinal);
        foreach (var artifact in artifacts)
        {
            if (!artifactIds.Add(artifact.Id))
            {
                failures.Add(new ProcessValidationFailure(
                    "Artifact.DuplicateId",
                    $"Artifact id '{artifact.Id}' appears more than once."));
            }

            if (string.IsNullOrWhiteSpace(artifact.Key))
            {
                failures.Add(new ProcessValidationFailure(
                    "Artifact.EmptyKey",
                    $"Artifact '{artifact.Id}' must have a key."));
            }
            else if (!artifactKeys.Add(artifact.Key))
            {
                failures.Add(new ProcessValidationFailure(
                    "Artifact.DuplicateKey",
                    $"Artifact key '{artifact.Key}' appears more than once."));
            }

            if (artifact.Sensitivity == ProcessArtifactSensitivity.Unspecified)
            {
                failures.Add(new ProcessValidationFailure(
                    "Artifact.MissingSensitivity",
                    $"Artifact '{artifact.Key}' must declare sensitivity."));
            }
        }

        ValidateSlots(slots, artifactIds, failures);
        return ProcessValidationResult.From(failures);
    }

    public static ProcessValidationResult ValidateReferences(
        IReadOnlyList<ProcessArtifactReference> references,
        IReadOnlySet<ArtifactSlotId> declaredSlots)
    {
        ArgumentNullException.ThrowIfNull(references);
        ArgumentNullException.ThrowIfNull(declaredSlots);

        var failures = new List<ProcessValidationFailure>();
        foreach (var reference in references)
        {
            if (!declaredSlots.Contains(reference.SlotId))
            {
                failures.Add(new ProcessValidationFailure(
                    "ArtifactReference.UnknownSlot",
                    $"Artifact reference uses undeclared slot '{reference.SlotId}'."));
            }

            if (string.IsNullOrWhiteSpace(reference.ContentHash))
            {
                failures.Add(new ProcessValidationFailure(
                    "ArtifactReference.MissingContentHash",
                    $"Artifact reference '{reference.ArtifactId}' must include a content hash."));
            }
        }

        return ProcessValidationResult.From(failures);
    }

    private static void ValidateSlots(
        IReadOnlyList<ProcessArtifactSlotDefinition> slots,
        IReadOnlySet<ArtifactDefinitionId> artifactIds,
        ICollection<ProcessValidationFailure> failures)
    {
        var slotIds = new HashSet<ArtifactSlotId>();
        var slotKeys = new HashSet<string>(StringComparer.Ordinal);
        foreach (var slot in slots)
        {
            if (!slotIds.Add(slot.Id))
            {
                failures.Add(new ProcessValidationFailure(
                    "ArtifactSlot.DuplicateId",
                    $"Artifact slot id '{slot.Id}' appears more than once."));
            }

            if (string.IsNullOrWhiteSpace(slot.Key))
            {
                failures.Add(new ProcessValidationFailure(
                    "ArtifactSlot.EmptyKey",
                    $"Artifact slot '{slot.Id}' must have a key."));
            }
            else if (!slotKeys.Add(slot.Key))
            {
                failures.Add(new ProcessValidationFailure(
                    "ArtifactSlot.DuplicateKey",
                    $"Artifact slot key '{slot.Key}' appears more than once."));
            }

            if (!artifactIds.Contains(slot.ArtifactDefinitionId))
            {
                failures.Add(new ProcessValidationFailure(
                    "ArtifactSlot.UnknownArtifact",
                    $"Artifact slot '{slot.Key}' references unknown artifact definition '{slot.ArtifactDefinitionId}'."));
            }

            if (slot.Scope != ProcessArtifactScope.Local && !slot.HasBoundaryPolicy)
            {
                failures.Add(new ProcessValidationFailure(
                    "ArtifactSlot.MissingBoundaryPolicy",
                    $"Artifact slot '{slot.Key}' crosses a process boundary and must declare a policy."));
            }
        }
    }
}
