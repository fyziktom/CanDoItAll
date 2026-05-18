using CanDoItAll.AgentFramework.Models;

namespace CanDoItAll.Modules.CognitiveMemory;

public static class CognitiveMemoryModelAccessPolicy
{
    public static CognitiveMemoryModelAccessDecision Evaluate(
        CognitiveMemoryAutomationSettings settings,
        ProviderProfile provider)
    {
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(provider);

        var isLocalProvider = IsLocalProvider(provider);
        return settings.ModelAccessMode switch
        {
            CognitiveMemoryModelAccessMode.Disabled => new(
                IsAllowed: false,
                Reason: "cognitive-memory-model-access-disabled",
                IsLocalProvider: isLocalProvider),

            CognitiveMemoryModelAccessMode.LocalProvidersOnly when !isLocalProvider => new(
                IsAllowed: false,
                Reason: "provider-is-not-local",
                IsLocalProvider: false),

            CognitiveMemoryModelAccessMode.SelectedProvidersOnly when !IsSelectedProvider(settings, provider.Id) => new(
                IsAllowed: false,
                Reason: "provider-not-allowed-for-cognitive-memory",
                IsLocalProvider: isLocalProvider),

            _ => new(
                IsAllowed: true,
                Reason: "allowed",
                IsLocalProvider: isLocalProvider)
        };
    }

    public static bool IsLocalProvider(ProviderProfile provider)
    {
        ArgumentNullException.ThrowIfNull(provider);
        if (TryCreateProviderUri(provider.BaseUrl, out var uri))
        {
            return uri.IsLoopback;
        }

        return provider.Kind == ProviderKind.Ollama && string.IsNullOrWhiteSpace(provider.BaseUrl);
    }

    private static bool IsSelectedProvider(
        CognitiveMemoryAutomationSettings settings,
        Guid providerId)
        => settings.DefaultProviderProfileId == providerId ||
           settings.AllowedProviderProfileIds.Contains(providerId);

    private static bool TryCreateProviderUri(
        string value,
        out Uri uri)
    {
        var text = value.Trim();
        if (string.IsNullOrWhiteSpace(text))
        {
            uri = null!;
            return false;
        }

        if (text.Contains("://", StringComparison.Ordinal) &&
            Uri.TryCreate(text, UriKind.Absolute, out uri!))
        {
            return true;
        }

        return Uri.TryCreate($"http://{text}", UriKind.Absolute, out uri!);
    }
}

public sealed record CognitiveMemoryModelAccessDecision(
    bool IsAllowed,
    string Reason,
    bool IsLocalProvider);
