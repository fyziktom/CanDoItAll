using CanDoItAll.AgentFramework.Models;

namespace CanDoItAll.AgentFramework.Core;

public static class WorkflowExecutorInvocationDeduplicationPolicy
{
    public const int MaximumAttempts = 3;
    public const int MaximumStoredResultCharacters = 262_144;
    private static readonly TimeSpan LeaseSafetyMargin = TimeSpan.FromSeconds(30);

    public static bool Participates(WorkflowExecutorDescriptor descriptor)
    {
        ArgumentNullException.ThrowIfNull(descriptor);
        return descriptor.SideEffects is
        {
            WritesExternalState: true,
            ExternalMutationKind: WorkflowExecutorExternalMutationKind.ProcessedMarker,
            RequiresCommitIdempotencyKey: true,
            AllowsIdempotentRetry: true
        } sideEffects &&
            !string.IsNullOrWhiteSpace(sideEffects.IdempotencyKeyJsonPath) &&
            !string.IsNullOrWhiteSpace(sideEffects.ReceiptSchema) &&
            descriptor.PermissionPolicy.RequiredCapabilities.HasFlag(
                WorkflowExecutorCapabilityFlags.IdempotentExternalMarker);
    }

    public static WorkflowExecutorContractVersion ResolveContractVersion(
        WorkflowExecutorDescriptor descriptor)
    {
        ArgumentNullException.ThrowIfNull(descriptor);
        var settingsVersion = descriptor.SettingsSchema.Version;
        if (string.IsNullOrWhiteSpace(settingsVersion))
        {
            throw new InvalidOperationException(
                $"Workflow executor '{descriptor.Id}' cannot participate in deduplication without a settings contract version.");
        }

        var sourceVersion = string.IsNullOrWhiteSpace(descriptor.Source.SourceVersion)
            ? "unversioned-source"
            : descriptor.Source.SourceVersion;
        return new WorkflowExecutorContractVersion($"{sourceVersion}/{settingsVersion}");
    }

    public static TimeSpan ResolveLeaseDuration(WorkflowExecutorExecutionPolicy executionPolicy)
    {
        ArgumentNullException.ThrowIfNull(executionPolicy);
        var execution = TimeSpan.FromSeconds(
            checked((long)executionPolicy.TimeoutSeconds * (executionPolicy.MaxRetryAttempts + 1L)));
        var retryDelay = TimeSpan.FromMilliseconds(
            checked((long)executionPolicy.RetryDelayMilliseconds * executionPolicy.MaxRetryAttempts));
        return execution + retryDelay + LeaseSafetyMargin;
    }

    public static TimeSpan ResolveLeaseRenewalInterval(TimeSpan leaseDuration)
    {
        if (leaseDuration <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(leaseDuration));
        }

        return TimeSpan.FromTicks(Math.Max(1, leaseDuration.Ticks / 3));
    }

    public static bool CanPersistResult(
        WorkflowExecutorDescriptor descriptor,
        WorkflowNodeExecutionResult result)
    {
        ArgumentNullException.ThrowIfNull(descriptor);
        ArgumentNullException.ThrowIfNull(result);
        return Participates(descriptor) &&
            result.ResultShape == descriptor.ResultShape &&
            result.PayloadJson.Length <= MaximumStoredResultCharacters &&
            string.Equals(
                WorkflowExecutorRedaction.RedactText(result.PayloadJson),
                result.PayloadJson,
                StringComparison.Ordinal);
    }
}
