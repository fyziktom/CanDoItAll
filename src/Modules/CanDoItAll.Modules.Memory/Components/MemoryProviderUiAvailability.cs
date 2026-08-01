using CanDoItAll.Memory.Abstractions;
using CanDoItAll.Memory.Application;
using CanDoItAll.Modules.Memory.Services;

namespace CanDoItAll.Modules.Memory.Components;

internal static class MemoryProviderUiAvailability
{
    public static bool CanRunQuery(MemoryProviderManagementProfile? provider, bool useAsyncQuery)
    {
        var capability = useAsyncQuery
            ? MemoryCapabilityIds.ContextQueryAsync
            : MemoryCapabilityIds.ContextQuerySync;
        return CanUseCapability(provider, capability);
    }

    public static bool CanRunManualIngestion(MemoryProviderManagementProfile? provider) =>
        CanUseCapability(provider, MemoryCapabilityIds.IngestionSnapshot);

    public static bool CanRefreshOperationStatus(MemoryProviderManagementProfile? provider) =>
        CanUseCapability(provider, MemoryCapabilityIds.OperationStatus);

    public static bool CanCancelOperation(MemoryProviderManagementProfile? provider) =>
        provider is not null && MemoryProviderCapabilityPolicy.CanCancelOperation(provider.DriverKind);

    public static bool CanSubmitFeedback(
        MemoryProviderManagementProfile? provider,
        MemoryFeedbackStage stage) =>
        CanUseCapability(
            provider,
            stage is MemoryFeedbackStage.ContextUsed or MemoryFeedbackStage.ImmediateToolResult
                ? MemoryCapabilityIds.FeedbackImmediate
                : MemoryCapabilityIds.FeedbackDelayed);

    public static bool CanAcknowledgeEvents(MemoryProviderManagementProfile? provider) =>
        CanUseCapability(provider, MemoryCapabilityIds.EventsProviderPush);

    public static string QueryDiagnostic(MemoryProviderManagementProfile? provider, bool useAsyncQuery)
    {
        var unavailable = ProviderUnavailableDiagnostic(provider);
        if (unavailable is not null)
        {
            return unavailable;
        }

        var capability = useAsyncQuery
            ? MemoryCapabilityIds.ContextQueryAsync
            : MemoryCapabilityIds.ContextQuerySync;
        if (!ClaimsCapability(provider!, capability))
        {
            return $"Selected provider does not declare {(useAsyncQuery ? "asynchronous" : "synchronous")} query capability.";
        }

        return MemoryProviderCapabilityPolicy.CanExecute(provider!.DriverKind, capability)
            ? $"Selected provider: {provider.DisplayName}."
            : $"{provider.DriverKind} driver cannot execute {(useAsyncQuery ? "asynchronous" : "synchronous")} context queries.";
    }

    public static string ManualIngestionDiagnostic(MemoryProviderManagementProfile? provider)
    {
        var unavailable = ProviderUnavailableDiagnostic(provider);
        if (unavailable is not null)
        {
            return unavailable.Replace("Provider-backed actions", "Manual ingestion", StringComparison.Ordinal);
        }

        if (!ClaimsCapability(provider!, MemoryCapabilityIds.IngestionSnapshot))
        {
            return "Selected provider does not declare snapshot ingestion capability.";
        }

        return MemoryProviderCapabilityPolicy.CanExecute(
            provider!.DriverKind,
            MemoryCapabilityIds.IngestionSnapshot)
            ? $"Selected provider: {provider.DisplayName}."
            : $"{provider.DriverKind} driver cannot execute snapshot ingestion.";
    }

    public static string HealthTone(MemoryProviderHealthState healthState) => healthState switch
    {
        MemoryProviderHealthState.Healthy => "success",
        MemoryProviderHealthState.Degraded => "warning",
        MemoryProviderHealthState.Unreachable => "danger",
        _ => "neutral"
    };

    public static string HandlerTone(MemoryOperationHandlerStatus status) => status switch
    {
        MemoryOperationHandlerStatus.Completed => "success",
        MemoryOperationHandlerStatus.Accepted => "info",
        MemoryOperationHandlerStatus.Cancelled => "neutral",
        _ => "warning"
    };

    private static bool CanUseCapability(
        MemoryProviderManagementProfile? provider,
        MemoryCapabilityId capability) =>
        provider is not null &&
        provider.IsEnabled &&
        provider.HealthState == MemoryProviderHealthState.Healthy &&
        MemoryProviderCapabilityPolicy.CanExecute(provider.DriverKind, capability) &&
        ClaimsCapability(provider, capability);

    private static bool ClaimsCapability(
        MemoryProviderManagementProfile provider,
        MemoryCapabilityId capability) =>
        provider.Capabilities.Any(item => item.Supported && item.Id == capability);

    private static string? ProviderUnavailableDiagnostic(MemoryProviderManagementProfile? provider)
    {
        if (provider is null)
        {
            return "No provider is selected. Provider-backed actions are disabled.";
        }

        if (!provider.IsEnabled)
        {
            return "Selected provider is disabled.";
        }

        return provider.HealthState == MemoryProviderHealthState.Healthy
            ? null
            : "Selected provider health is not healthy.";
    }
}
