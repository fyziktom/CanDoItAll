using CanDoItAll.Processes.Abstractions;
using CanDoItAll.Processes.Contracts;
using CanDoItAll.Processes.Core;
using CanDoItAll.Processes.Drivers.Abstractions;

namespace CanDoItAll.Processes.Application;

public sealed record ProcessAcceptedStepRequirements(ProcessStepDefinitionId StepDefinitionId, ProcessStepKind Kind,
    bool StartsSubprocess, IReadOnlyList<string> RequiredRuntimeToolNames,
    IReadOnlyList<ProcessHostCapabilityId> RequiredHostCapabilities, IReadOnlyList<BranchOutcomeId> ConfiguredBranchOutcomeIds,
    IReadOnlyList<ProcessArtifactSlotDescriptor> ArtifactDescriptors,
    IReadOnlyList<SubprocessArtifactMappingDescriptor> SubprocessArtifactMappings) {
    public const int MaximumDeclarations = 4096;

    public void Validate() {
        if (StepDefinitionId.Value == Guid.Empty || !Enum.IsDefined(Kind) ||
                !ProcessRequiredRuntimeToolNames.IsValidBoundedContract(RequiredRuntimeToolNames) ||
                !IsBounded(RequiredHostCapabilities) || !IsBounded(ConfiguredBranchOutcomeIds) ||
                !IsBounded(ArtifactDescriptors) || !IsBounded(SubprocessArtifactMappings)) {
            throw Invalid();
        }
        if (RequiredHostCapabilities.Any(item => !IsIdentifier(item.Value)) ||
                RequiredHostCapabilities.Distinct().Count() != RequiredHostCapabilities.Count ||
                ConfiguredBranchOutcomeIds.Any(item => !IsIdentifier(item.Value)) ||
                ArtifactDescriptors.Any(item => item is null || item.SlotId.Value == Guid.Empty ||
                    item.SlotKey is null || item.StepKey is null || item.ArtifactExpectationKey is null ||
                    item.ArtifactTitle is null || item.ArtifactKind is null || item.PrimaryManagedRef is null ||
                    item.PayloadSchema is null || !Enum.IsDefined(item.MaterializationMode)) ||
                SubprocessArtifactMappings.Any(item => item is null || item.ParentSlotId.Value == Guid.Empty ||
                    item.ParentArtifactExpectationKey is null || item.ChildProcessDefinitionKey is null ||
                    !IsBounded(item.AcceptedChildOutputs) || !IsBounded(item.NoGoChildOutputs) ||
                    item.AcceptedChildOutputs.Concat(item.NoGoChildOutputs).Any(child => child is null ||
                        child.StepKey is null || child.ArtifactExpectationKey is null || child.ArtifactTitle is null ||
                        child.BranchOutcomeKey is null))) {
            throw Invalid();
        }
    }

    private static bool IsBounded<T>(IReadOnlyCollection<T>? values)
        => values is not null && values.Count <= MaximumDeclarations;

    private static bool IsIdentifier(string? value)
        => !string.IsNullOrWhiteSpace(value) && value.Length <= ProcessStrategyResultLimits.MaximumIdentifierLength &&
            string.Equals(value, value.Trim(), StringComparison.Ordinal);

    private static InvalidDataException Invalid()
        => new("The original Process step requirements have missing, invalid or unbounded declarations.");
}
