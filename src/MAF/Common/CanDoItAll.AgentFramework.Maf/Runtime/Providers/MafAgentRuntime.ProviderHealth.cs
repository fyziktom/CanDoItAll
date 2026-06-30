using CanDoItAll.AgentFramework.Models;
using Microsoft.Extensions.AI;

namespace CanDoItAll.AgentFramework.Maf;

public sealed partial class MafAgentRuntime
{
    public Task<ProviderHealthResult> TestProviderAsync(
        ProviderProfile provider,
        CancellationToken cancellationToken = default)
    {
        return providerRuntimeGateway.TestProviderAsync(provider, cancellationToken);
    }

    public Task<ProviderTestChatResult> RunProviderTestChatAsync(
        ProviderProfile provider,
        ProviderTestChatRequest request,
        CancellationToken cancellationToken = default)
    {
        var model = ResolveProviderTestModel(provider, request.Model);
        return providerRuntimeGateway.RunProviderTestChatAsync(provider, request, model, cancellationToken);
    }

    internal static List<ChatMessage> BuildProviderTestInputMessages(ProviderTestChatRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        return (request.Messages ?? [])
            .OrderBy(item => item.CreatedAtUtc)
            .Where(message => !string.IsNullOrWhiteSpace(message.Content))
            .Select(message => new ChatMessage(MapRole(message.Role), message.Content.Trim()))
            .ToList();
    }

    public Task<ProviderModelMaintenanceEditorResult> CreateOrUpdateProviderModelAsync(
        ProviderProfile provider,
        ProviderModelMaintenanceEditorRequest request,
        CancellationToken cancellationToken = default)
    {
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
}
