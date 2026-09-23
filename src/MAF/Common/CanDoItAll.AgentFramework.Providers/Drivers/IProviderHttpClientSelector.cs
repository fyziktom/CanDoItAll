using System.Diagnostics.CodeAnalysis;
using CanDoItAll.AgentFramework.Models;

namespace CanDoItAll.AgentFramework.Providers;

public interface IProviderHttpClientSelector
{
    bool TryGetClient(
        ProviderProfile provider,
        [NotNullWhen(true)] out HttpClient? client);
}

public sealed class ProviderHttpClientSelectionException(
    Guid providerId,
    string message,
    Exception? innerException = null) : InvalidOperationException(
    message,
    innerException)
{
    public Guid ProviderId { get; } = providerId;
}
