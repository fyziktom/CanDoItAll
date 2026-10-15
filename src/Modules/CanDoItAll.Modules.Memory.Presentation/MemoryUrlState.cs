using CanDoItAll.AppComponents.Navigation;

namespace CanDoItAll.Modules.Memory.Pages;

public enum MemoryWorkspaceTab { Providers, Operations, Events, Feedback, Query, Ingestion, ProviderUi }

public sealed record MemoryUrlState(string? ProviderId = null, MemoryWorkspaceTab Tab = MemoryWorkspaceTab.Providers);

public sealed class MemoryUrlCodec : IUrlStateCodec<MemoryUrlState> {
    public const string Path = "/memory";
    public const string ProviderKey = "providerId";
    public const string TabKey = "tab";
    public const int ProviderIdMaximumLength = 256;

    public static MemoryUrlCodec Instance { get; } = new();

    public static UrlTokenMap<MemoryWorkspaceTab> Tabs { get; } = new(
        (MemoryWorkspaceTab.Providers, "providers"),
        (MemoryWorkspaceTab.Operations, "operations"),
        (MemoryWorkspaceTab.Events, "events"),
        (MemoryWorkspaceTab.Feedback, "feedback"),
        (MemoryWorkspaceTab.Query, "query"),
        (MemoryWorkspaceTab.Ingestion, "ingestion"),
        (MemoryWorkspaceTab.ProviderUi, "provider-ui"));

    public IReadOnlyList<string> Keys { get; } = [ProviderKey, TabKey];

    public MemoryUrlState Read(UrlQuery query) {
        var provider = query.GetText(ProviderKey, ProviderIdMaximumLength);
        return new(provider is not null && !provider.Any(char.IsControl) ? provider : null,
            query.GetToken(TabKey, Tabs, MemoryWorkspaceTab.Providers));
    }

    public void Write(MemoryUrlState state, UrlQueryWriter query) {
        query.Text(ProviderKey, state.ProviderId);
        query.Token(TabKey, state.Tab, Tabs, MemoryWorkspaceTab.Providers);
    }
}
