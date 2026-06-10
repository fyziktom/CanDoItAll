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

internal static class ProcessVerificationHostCapabilityCatalog {
    public const string DryRunExecutionGateKey = "dry-run:execution-capable-future-gate";

    public static IReadOnlyList<ProcessVerificationHostCapabilityDescriptor> StaticDescriptors { get; } =
        BuildStaticDescriptors();

    public static ProcessVerificationHostCapabilityDescriptor Require(string key) {
        if (string.IsNullOrWhiteSpace(key)) {
            throw new ArgumentException("A verification host capability key is required.", nameof(key));
        }

        var descriptor = StaticDescriptors.SingleOrDefault(item =>
            string.Equals(item.Key, key, StringComparison.Ordinal));
        return descriptor ?? throw new InvalidOperationException($"Verification host capability descriptor '{key}' is not registered in the static catalog.");
    }

    public static string VerificationLaneKey(ProcessDriverVerificationGatewayLane lane) {
        return $"verification:{lane}";
    }

    private static IReadOnlyList<ProcessVerificationHostCapabilityDescriptor> BuildStaticDescriptors() {
        return ProcessDriverVerificationGatewayLaneRules.AllowedLanes
            .Select(CreateVerificationDescriptor)
            .Append(CreateDryRunExecutionDescriptor())
            .ToArray();
    }

    private static ProcessVerificationHostCapabilityDescriptor CreateVerificationDescriptor(
        ProcessDriverVerificationGatewayLaneDescriptor lane) {
        return new ProcessVerificationHostCapabilityDescriptor(
            VerificationLaneKey(lane.Lane),
            ProcessVerificationHostCapabilityKind.VerificationLane,
            ProcessRuntimeHostContractSurface.VerificationHost,
            lane.RequiredPermissionMode,
            lane.AllowedOperations,
            ProcessDriverOperationRules.SideEffectOperations,
            ReflectionDiscoveryAllowed: false,
            SelfRegistrationAllowed: false,
            ExecutionAllowed: false);
    }

    private static ProcessVerificationHostCapabilityDescriptor CreateDryRunExecutionDescriptor() {
        return new ProcessVerificationHostCapabilityDescriptor(
            DryRunExecutionGateKey,
            ProcessVerificationHostCapabilityKind.DryRunExecutionGate,
            ProcessRuntimeHostContractSurface.DryRunExecution,
            ProcessDriverPermissionMode.ExecutionCapableFuture,
            ProcessDriverOperationRules.ReadonlyVerificationOperations,
            ProcessDriverOperationRules.SideEffectOperations,
            ReflectionDiscoveryAllowed: false,
            SelfRegistrationAllowed: false,
            ExecutionAllowed: false);
    }
}
