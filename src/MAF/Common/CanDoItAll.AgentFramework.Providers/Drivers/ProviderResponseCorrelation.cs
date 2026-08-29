using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.ProviderHistory;

namespace CanDoItAll.AgentFramework.Providers;

internal static class ProviderResponseCorrelation {
    private const string PublisherRequestHeader = "CanDoItAll-Request-Id";

    public static RemoteRequestReference? Read(ProviderProfile provider, HttpResponseMessage response) {
        if (provider.CredentialBinding is not { Purpose: ProviderCredentialPurpose.SourceAccessToken,
                ConsumerKind: ProviderCredentialConsumerKind.Source } binding ||
            binding.ConsumerId == Guid.Empty || !response.Headers.TryGetValues(PublisherRequestHeader, out var values)) {
            return null;
        }
        using var enumerator = values.GetEnumerator();
        if (!enumerator.MoveNext()) {
            return null;
        }
        var value = enumerator.Current;
        if (value.Length is < 1 or > 128 || value.Any(char.IsControl) || enumerator.MoveNext()) {
            return null;
        }
        return new(binding.ConsumerId, value);
    }
}
