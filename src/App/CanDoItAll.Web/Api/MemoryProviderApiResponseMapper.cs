using CanDoItAll.Memory.Abstractions;
using CanDoItAll.Memory.Application;
using CanDoItAll.Modules.Memory.Services;

namespace CanDoItAll.Web.Api;

internal static class MemoryProviderApiResponseMapper
{
    public static MemoryProviderProfileApiResponse MapProfile(
        MemoryProviderProfileConfigurationSnapshot snapshot)
    {
        var profile = snapshot.Profile;
        var configuration = snapshot.Configuration;
        var supportedCapabilities = profile.Manifest.Capabilities
            .Where(capability => capability.Supported)
            .Select(capability => capability.Id)
            .ToHashSet();
        var capabilities = new MemoryProviderCapabilitiesApiResponse(
            supportedCapabilities.Contains(MemoryCapabilityIds.ContextQuerySync),
            supportedCapabilities.Contains(MemoryCapabilityIds.ContextQueryAsync),
            supportedCapabilities.Contains(MemoryCapabilityIds.OperationStatus),
            supportedCapabilities.Contains(MemoryCapabilityIds.UiRcl),
            supportedCapabilities.Contains(MemoryCapabilityIds.UiIframe));
        var interactionSupport = new MemoryProviderInteractionSupportApiResponse(
            profile.Manifest.InteractionSupport.SupportsSynchronousQueries,
            profile.Manifest.InteractionSupport.SupportsAsynchronousOperations);
        var limits = new MemoryProviderLimitsApiResponse(
            profile.Manifest.Limits.MaxContextSections,
            profile.Manifest.Limits.MaxSourceItems,
            profile.Manifest.Limits.MaxInFlightOperations,
            profile.Manifest.Limits.OperationTimeout.TotalSeconds);

        return new MemoryProviderProfileApiResponse(
            profile.InstanceId.Value,
            profile.DisplayName,
            profile.DriverKind,
            profile.IsEnabled,
            profile.HealthState,
            profile.WorkspaceScope,
            profile.DefaultPolicy.FallbackBehavior,
            profile.Manifest.ProviderKind.Value,
            profile.Manifest.ProtocolVersion.Value,
            profile.SelectionTags.ToArray(),
            capabilities,
            interactionSupport,
            limits,
            configuration.Http is null
                ? null
                : new MemoryProviderHttpTransportApiModel(
                    configuration.Http.BaseUrl,
                    configuration.Http.QueryPath,
                    configuration.Http.HealthPath,
                    configuration.Http.ApiKeyEnvironmentVariable,
                    configuration.Http.AuthHeaderName,
                    configuration.Http.AuthScheme,
                    configuration.Http.TimeoutMilliseconds,
                    configuration.Http.MaxRetryAttempts),
            configuration.Mcp is null
                ? null
                : new MemoryProviderMcpTransportApiModel(
                    configuration.Mcp.DescriptorKind,
                    configuration.Mcp.ServerKey,
                    configuration.Mcp.DisplayName,
                    configuration.Mcp.Description,
                    configuration.Mcp.RemoteEndpoint,
                    configuration.Mcp.AuthHeaderName,
                    configuration.Mcp.AuthHeaderEnvironmentVariable,
                    configuration.Mcp.ContextQueryTool,
                    configuration.Mcp.OperationStatusTool));
    }

    public static MemoryProviderSelectionApiResponse MapSelection(
        MemoryProviderSelectionResult selection) =>
        new(
            selection.Status,
            selection.Reason,
            selection.RequiredCapability.Value,
            selection.DispatchAllowed,
            selection.Diagnostic,
            selection.SelectedProvider?.InstanceId.Value,
            selection.CandidateProviderIds.Select(id => id.Value).ToArray());

    public static MemoryProviderOperationApiResponse? MapOperation(
        MemoryOperationRecord? operation) =>
        operation is null
            ? null
            : new MemoryProviderOperationApiResponse(
                operation.OperationId.Value,
                operation.ProviderInstanceId.Value,
                operation.RequestedCapability.Value,
                operation.OperationKind,
                operation.Status,
                operation.RetryCount,
                operation.TransitionCount,
                operation.CreatedAtUtc,
                operation.UpdatedAtUtc,
                operation.CompletedAtUtc,
                operation.StatusReason);

    public static MemoryContextPackApiResponse? MapContextPack(
        MemoryContextPack? contextPack) =>
        contextPack is null
            ? null
            : new MemoryContextPackApiResponse(
                contextPack.ContextPackId.Value,
                contextPack.Summary,
                contextPack.Sections
                    .Select(section => new MemoryContextSectionApiResponse(
                        section.Title,
                        section.Text,
                        section.Citations
                            .Select(citation => new MemoryCitationApiResponse(citation.SourceRef, citation.Label))
                            .ToArray(),
                        section.Confidence))
                    .ToArray(),
                contextPack.Warnings
                    .Select(warning => new MemoryWarningApiResponse(warning.Kind, warning.Message))
                    .ToArray(),
                contextPack.ProviderConfidence,
                contextPack.FeedbackHandle?.Value);

    public static MemoryAcceptedOperationApiResponse? MapAcceptedOperation(
        MemoryOperationAccepted? acceptedOperation) =>
        acceptedOperation is null
            ? null
            : new MemoryAcceptedOperationApiResponse(
                acceptedOperation.OperationId.Value,
                acceptedOperation.StatusPath,
                acceptedOperation.ExpiresAtUtc,
                acceptedOperation.PollAfter.TotalSeconds,
                acceptedOperation.CallbackAvailable);
}
