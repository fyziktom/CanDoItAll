using CanDoItAll.AgentFramework.Models;

namespace CanDoItAll.AgentFramework.Core;

public sealed record AgentImageAnalysisSource(
    string Name,
    string ContentType,
    byte[] Bytes);

public sealed record AgentImageAnalysisRequest(
    ProviderProfile Provider,
    string Model,
    string Prompt,
    IReadOnlyList<AgentImageAnalysisSource> Sources,
    string ModelParameterConfigurationJson = "");

public sealed record AgentImageAnalysisResult(
    string Model,
    string Analysis,
    int InputTokens,
    int OutputTokens);

public interface IAgentImageAnalysisService
{
    Task<AgentImageAnalysisResult> AnalyzeAsync(
        AgentImageAnalysisRequest request,
        CancellationToken cancellationToken = default);
}

public sealed class UnavailableAgentImageAnalysisService : IAgentImageAnalysisService
{
    public Task<AgentImageAnalysisResult> AnalyzeAsync(
        AgentImageAnalysisRequest request,
        CancellationToken cancellationToken = default)
    {
        throw new InvalidOperationException("Image analysis requires a provider-runtime image analysis service.");
    }
}
