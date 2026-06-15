using CanDoItAll.Processes.Contracts;
using CanDoItAll.Processes.Drivers.Abstractions.Gateway;
using CanDoItAll.Processes.Drivers.Abstractions.Permissions;

namespace CanDoItAll.Modules.Processes;

internal enum ProcessVerificationHostCapabilityKind {
    VerificationLane = 1,
    DryRunExecutionGate = 2
}

internal sealed record ProcessVerificationHostCapabilityDescriptor(
    string Key,
    ProcessVerificationHostCapabilityKind Kind,
    ProcessRuntimeHostContractSurface ContractSurface,
    ProcessRuntimeHostOperationCategory OperationCategory,
    ProcessDriverPermissionMode PermissionMode,
    IReadOnlyList<ProcessDriverOperation> AllowedOperations,
    IReadOnlyList<ProcessDriverOperation> DeniedOperations,
    bool ReflectionDiscoveryAllowed,
    bool SelfRegistrationAllowed,
    bool ExecutionAllowed) {
    public bool IsStaticReadOnlyDescriptor =>
        !ReflectionDiscoveryAllowed &&
        !SelfRegistrationAllowed &&
        !ExecutionAllowed &&
        DeniedOperations.All(ProcessDriverOperationRules.IsSideEffectOperation) &&
        AllowedOperations.All(ProcessDriverOperationRules.IsReadonlyVerificationOperation);
}

internal interface IProcessVerificationHostCapabilityProvider
{
    IReadOnlyList<ProcessVerificationHostCapabilityDescriptor> ListDescriptors();

    ProcessVerificationHostCapabilityDescriptor Require(string key);
}

internal sealed class ProcessStaticVerificationHostCapabilityProvider : IProcessVerificationHostCapabilityProvider
{
    public static ProcessStaticVerificationHostCapabilityProvider Instance { get; } = new();

    private readonly IReadOnlyList<ProcessVerificationHostCapabilityDescriptor> descriptors = BuildStaticDescriptors();

    private ProcessStaticVerificationHostCapabilityProvider()
    {
    }

    public IReadOnlyList<ProcessVerificationHostCapabilityDescriptor> ListDescriptors()
        => descriptors;

    public ProcessVerificationHostCapabilityDescriptor Require(string key)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            throw new ArgumentException("A verification host capability key is required.", nameof(key));
        }

        var descriptor = descriptors.SingleOrDefault(item =>
            string.Equals(item.Key, key, StringComparison.Ordinal));
        return descriptor ?? throw new InvalidOperationException($"Verification host capability descriptor '{key}' is not registered in the static catalog.");
    }

    private static IReadOnlyList<ProcessVerificationHostCapabilityDescriptor> BuildStaticDescriptors()
    {
        return ProcessDriverVerificationGatewayLaneRules.AllowedLanes
            .Select(CreateVerificationDescriptor)
            .Append(CreateDryRunExecutionDescriptor())
            .ToArray();
    }

    private static ProcessVerificationHostCapabilityDescriptor CreateVerificationDescriptor(
        ProcessDriverVerificationGatewayLaneDescriptor lane)
    {
        return new ProcessVerificationHostCapabilityDescriptor(
            ProcessVerificationHostCapabilityCatalog.VerificationLaneKey(lane.Lane),
            ProcessVerificationHostCapabilityKind.VerificationLane,
            ProcessRuntimeHostContractSurface.VerificationHost,
            ProcessRuntimeHostOperationCategory.ReadOnlyInspection,
            lane.RequiredPermissionMode,
            lane.AllowedOperations,
            ProcessDriverOperationRules.SideEffectOperations,
            ReflectionDiscoveryAllowed: false,
            SelfRegistrationAllowed: false,
            ExecutionAllowed: false);
    }

    private static ProcessVerificationHostCapabilityDescriptor CreateDryRunExecutionDescriptor()
    {
        return new ProcessVerificationHostCapabilityDescriptor(
            ProcessVerificationHostCapabilityCatalog.DryRunExecutionGateKey,
            ProcessVerificationHostCapabilityKind.DryRunExecutionGate,
            ProcessRuntimeHostContractSurface.DryRunExecution,
            ProcessRuntimeHostOperationCategory.DryRunPlanning,
            ProcessDriverPermissionMode.ExecutionCapableFuture,
            ProcessDriverOperationRules.ReadonlyVerificationOperations,
            ProcessDriverOperationRules.SideEffectOperations,
            ReflectionDiscoveryAllowed: false,
            SelfRegistrationAllowed: false,
            ExecutionAllowed: false);
    }
}

internal static class ProcessVerificationHostCapabilityCatalog {
    public const string DryRunExecutionGateKey = "dry-run:execution-capable-future-gate";

    public static IProcessVerificationHostCapabilityProvider StaticProvider { get; } =
        ProcessStaticVerificationHostCapabilityProvider.Instance;

    public static IReadOnlyList<ProcessVerificationHostCapabilityDescriptor> StaticDescriptors { get; } =
        StaticProvider.ListDescriptors();

    public static ProcessVerificationHostCapabilityDescriptor Require(string key)
        => StaticProvider.Require(key);

    public static string VerificationLaneKey(ProcessDriverVerificationGatewayLane lane) {
        return $"verification:{lane}";
    }
}
