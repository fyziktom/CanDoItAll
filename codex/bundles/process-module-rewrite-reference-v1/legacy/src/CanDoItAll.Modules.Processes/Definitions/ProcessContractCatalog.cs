using CanDoItAll.AgentFramework.Models;

namespace CanDoItAll.Modules.Processes;

public sealed record ProcessOperationContractDescriptor(
    ProcessStepOperation Operation,
    ProcessStepTargetScope? ImpliedTargetScope,
    bool CanMutateProductTarget,
    bool CanExecuteExternalAction);

public static class ProcessContractCatalog
{
    public static IReadOnlyList<ProcessOperationContractDescriptor> OperationDescriptors { get; } =
    [
        new(ProcessStepOperation.ReadProcessContext, null, CanMutateProductTarget: false, CanExecuteExternalAction: false),
        new(ProcessStepOperation.ReadProjectStructure, ProcessStepTargetScope.ExternalProductTargetReadOnly, CanMutateProductTarget: false, CanExecuteExternalAction: false),
        new(ProcessStepOperation.ReadUpstreamArtifacts, null, CanMutateProductTarget: false, CanExecuteExternalAction: false),
        new(ProcessStepOperation.WriteManagedProcessArtifacts, ProcessStepTargetScope.ManagedProcessArtifactsOnly, CanMutateProductTarget: false, CanExecuteExternalAction: false),
        new(ProcessStepOperation.WriteExternalArtifactDestination, ProcessStepTargetScope.ExternalArtifactDestination, CanMutateProductTarget: false, CanExecuteExternalAction: false),
        new(ProcessStepOperation.MutateProductTarget, ProcessStepTargetScope.ExternalProductTargetMutable, CanMutateProductTarget: true, CanExecuteExternalAction: false),
        new(ProcessStepOperation.RunValidation, null, CanMutateProductTarget: false, CanExecuteExternalAction: false),
        new(ProcessStepOperation.LaunchRuntime, null, CanMutateProductTarget: false, CanExecuteExternalAction: false),
        new(ProcessStepOperation.CaptureRuntimeProof, null, CanMutateProductTarget: false, CanExecuteExternalAction: false),
        new(ProcessStepOperation.ExecuteExternalAction, ProcessStepTargetScope.ExternalActionControlled, CanMutateProductTarget: false, CanExecuteExternalAction: true),
        new(ProcessStepOperation.RecoverArtifactsOnly, ProcessStepTargetScope.ManagedProcessArtifactsOnly, CanMutateProductTarget: false, CanExecuteExternalAction: false),
        new(ProcessStepOperation.EscalateOrDecide, null, CanMutateProductTarget: false, CanExecuteExternalAction: false)
    ];

    public static IReadOnlyList<string> OperationNames { get; } =
        OperationDescriptors.Select(descriptor => descriptor.Operation.ToString()).ToArray();

    public static IReadOnlyList<string> TargetScopeNames { get; } =
        Enum.GetNames<ProcessStepTargetScope>();

    public static IReadOnlyList<string> ArtifactSatisfactionStatusNames { get; } =
        Enum.GetNames<ProcessArtifactExpectationSatisfactionStatus>();

    public static bool IsKnownOperationName(string? value)
        => ProcessOperationContractNames.IsOperationName(value) &&
           OperationNames.Contains(value!.Trim(), StringComparer.Ordinal);

    public static bool IsKnownTargetScopeName(string? value)
        => ProcessOperationContractNames.IsTargetScopeName(value) &&
           TargetScopeNames.Contains(value!.Trim(), StringComparer.Ordinal);

    public static IReadOnlyList<string> FindUnknownOperationNames(IEnumerable<string> values)
    {
        ArgumentNullException.ThrowIfNull(values);
        return values
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => value.Trim())
            .Where(value => !IsKnownOperationName(value))
            .Distinct(StringComparer.Ordinal)
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToArray();
    }
}
