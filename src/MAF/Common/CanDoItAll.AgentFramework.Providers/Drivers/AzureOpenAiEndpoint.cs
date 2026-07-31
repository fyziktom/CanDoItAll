using CanDoItAll.AgentFramework.Models;

namespace CanDoItAll.AgentFramework.Providers;

public sealed class AzureOpenAiEndpoint
{
    private const string OpenAiPath = "/openai";
    private const string StableV1Path = "/openai/v1";
    private const string LegacyDeploymentsPath = "/openai/deployments";
    private readonly Uri resourceEndpoint;

    private AzureOpenAiEndpoint(Uri resourceEndpoint)
    {
        this.resourceEndpoint = resourceEndpoint;
        V1Endpoint = new Uri(resourceEndpoint, "openai/v1/");
    }

    public Uri V1Endpoint { get; }

    public static AzureOpenAiEndpoint Parse(ProviderProfile provider)
    {
        ArgumentNullException.ThrowIfNull(provider);

        var baseUrl = provider.BaseUrl?.Trim();
        if (string.IsNullOrWhiteSpace(baseUrl) ||
            !Uri.TryCreate(baseUrl, UriKind.Absolute, out var endpoint) ||
            endpoint.Scheme is not ("http" or "https"))
        {
            throw new InvalidOperationException(
                $"Azure OpenAI provider '{provider.Name}' must define an absolute HTTP or HTTPS endpoint.");
        }

        if (baseUrl.Contains('?') ||
            baseUrl.Contains('#'))
        {
            throw new InvalidOperationException(
                $"Azure OpenAI provider '{provider.Name}' endpoint must not contain query parameters or fragments.");
        }

        var path = endpoint.AbsolutePath.TrimEnd('/');
        if (IsLegacyDeploymentPath(path))
        {
            throw new InvalidOperationException(
                $"Azure OpenAI provider '{provider.Name}' uses a legacy deployment endpoint. Configure the resource base URL, '/openai', or stable '/openai/v1/' endpoint.");
        }

        if (path.Length > 0 &&
            !path.Equals(OpenAiPath, StringComparison.OrdinalIgnoreCase) &&
            !path.Equals(StableV1Path, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                $"Azure OpenAI provider '{provider.Name}' must use the resource base URL, '/openai', or stable '/openai/v1/' endpoint.");
        }

        var builder = new UriBuilder(endpoint)
        {
            Path = "/",
            Query = string.Empty,
            Fragment = string.Empty
        };
        return new AzureOpenAiEndpoint(builder.Uri);
    }

    public Uri BuildDeploymentChatCompletionsEndpoint(
        string deploymentName,
        string apiVersion)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(deploymentName);
        ArgumentException.ThrowIfNullOrWhiteSpace(apiVersion);

        var builder = new UriBuilder(
            new Uri(
                resourceEndpoint,
                $"openai/deployments/{Uri.EscapeDataString(deploymentName)}/chat/completions"))
        {
            Query = $"api-version={Uri.EscapeDataString(apiVersion)}"
        };
        return builder.Uri;
    }

    private static bool IsLegacyDeploymentPath(string path)
    {
        return path.Equals(LegacyDeploymentsPath, StringComparison.OrdinalIgnoreCase) ||
               path.StartsWith($"{LegacyDeploymentsPath}/", StringComparison.OrdinalIgnoreCase);
    }
}
