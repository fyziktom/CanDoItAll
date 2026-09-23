namespace CanDoItAll.SharedProviders.Abstractions;

public enum SharedProviderRelayAdapterClassification
{
    Unspecified,
    Production,
    Test
}

public sealed record SharedProviderRelayAdapterDescriptor
{
    public const int MaximumConnectorPluginKeyLength = 160;

    public SharedProviderRelayAdapterDescriptor(
        string connectorPluginKey,
        SharedProviderPurpose purpose,
        SharedProviderRelayAdapterClassification classification,
        SharedProviderRelaySupportDescriptor support)
    {
        if (!IsConnectorPluginKeyValid(connectorPluginKey))
        {
            throw new ArgumentException("The connector plugin key is invalid.", nameof(connectorPluginKey));
        }

        if (!Enum.IsDefined(purpose))
        {
            throw new ArgumentOutOfRangeException(nameof(purpose));
        }

        if (!Enum.IsDefined(classification) ||
            classification == SharedProviderRelayAdapterClassification.Unspecified)
        {
            throw new ArgumentOutOfRangeException(nameof(classification));
        }

        ArgumentNullException.ThrowIfNull(support);

        ConnectorPluginKey = connectorPluginKey;
        Purpose = purpose;
        Classification = classification;
        Support = support;
    }

    public string ConnectorPluginKey { get; }

    public SharedProviderPurpose Purpose { get; }

    public SharedProviderRelayAdapterClassification Classification { get; }

    public SharedProviderRelaySupportDescriptor Support { get; }

    private static bool IsConnectorPluginKeyValid(string? value)
        => value is { Length: > 0 and <= MaximumConnectorPluginKeyLength } &&
            value == value.Trim() &&
            value.All(character =>
                char.IsAsciiLetterOrDigit(character) ||
                character is '.' or '-');
}

public interface ISharedProviderRelaySupportCatalog
{
    IReadOnlyList<SharedProviderRelayAdapterDescriptor> List();

    bool TryGet(
        string connectorPluginKey,
        SharedProviderPurpose purpose,
        out SharedProviderRelayAdapterDescriptor descriptor);
}
