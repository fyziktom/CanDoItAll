using CanDoItAll.AgentFramework.Models;
using Microsoft.Extensions.AI;

namespace CanDoItAll.AgentFramework.Maf;

internal static class ProviderRuntimeDiagnostics
{
    public static Task<ProviderHealthResult> TestProviderAsync(
        IMafProviderRuntimeGateway providerRuntimeGateway,
        ProviderProfile provider,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(providerRuntimeGateway);
        return providerRuntimeGateway.TestProviderAsync(provider, cancellationToken);
    }

    public static Task<ProviderTestChatResult> RunProviderTestChatAsync(
        IMafProviderRuntimeGateway providerRuntimeGateway,
        ProviderProfile provider,
        ProviderTestChatRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(providerRuntimeGateway);

        var model = ResolveProviderTestModel(provider, request.Model);
        return providerRuntimeGateway.RunProviderTestChatAsync(provider, request, model, cancellationToken);
    }

    internal static List<ChatMessage> BuildProviderTestInputMessages(ProviderTestChatRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        return (request.Messages ?? [])
            .OrderBy(item => item.CreatedAtUtc)
            .Where(message => !string.IsNullOrWhiteSpace(message.Content))
            .Select(message => new ChatMessage(MafRuntimeSessionBuilder.MapRole(message.Role), message.Content.Trim()))
            .ToList();
    }

    public static Task<ProviderModelMaintenanceEditorResult> CreateOrUpdateProviderModelAsync(
        IMafProviderRuntimeGateway providerRuntimeGateway,
        ProviderProfile provider,
        ProviderModelMaintenanceEditorRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(providerRuntimeGateway);
        return providerRuntimeGateway.CreateOrUpdateProviderModelAsync(provider, request, cancellationToken);
    }

    private static string ResolveProviderTestModel(
        ProviderProfile provider,
        string? requestedModel)
    {
        if (!string.IsNullOrWhiteSpace(requestedModel))
        {
            return requestedModel.Trim();
        }

        return provider.Kind switch
        {
            ProviderKind.Ollama => ResolveHealthCheckModel(provider, provider.SuggestedModels, "qwen3.5:9b"),
            _ => ResolveHealthCheckModel(provider, provider.SuggestedModels, ManagedSeedProviderFallbacks.OpenAiDefaultModel)
        };
    }

    private static string ResolveHealthCheckModel(
        ProviderProfile provider,
        IEnumerable<string> candidateModels,
        string fallbackModel)
    {
        if (!string.IsNullOrWhiteSpace(provider.DefaultModel))
        {
            return provider.DefaultModel;
        }

        var discoveredModel = candidateModels.FirstOrDefault(item => !string.IsNullOrWhiteSpace(item));
        return string.IsNullOrWhiteSpace(discoveredModel) ? fallbackModel : discoveredModel;
    }
}
