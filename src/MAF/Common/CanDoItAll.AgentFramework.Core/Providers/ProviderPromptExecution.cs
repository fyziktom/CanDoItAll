using CanDoItAll.SharedKernel;

namespace CanDoItAll.AgentFramework.Core;

public sealed record ProviderPromptExecutionRequest(
    Guid ProviderProfileId,
    string Prompt,
    string? ModelOverride = null,
    string OutputFormat = "Markdown",
    bool ContainsSensitiveContent = false);

public sealed record ProviderPromptExecutionResponse(
    string ProviderName,
    string Model,
    string OutputText,
    string OutputFormat,
    bool ContainsWarnings,
    string? WarningSummary = null);

public interface IProviderPromptExecutionService
{
    Task<Result<ProviderPromptExecutionResponse>> ExecuteAsync(
        ProviderPromptExecutionRequest request,
        CancellationToken cancellationToken = default);
}

public sealed record ProviderHealthCheckResult(bool Success, string Message);

public interface IProviderHealthCheckService
{
    Task<ProviderHealthCheckResult> CheckHealthAsync(
        Guid providerProfileId,
        CancellationToken cancellationToken = default);
}
