using CanDoItAll.SharedKernel.Configuration;

namespace CanDoItAll.Modules.Workspace;

public sealed class ConnectorConfigState : ConfigurationState
{
    public ConnectorConfigState()
        : base()
    {
    }

    public ConnectorConfigState(IReadOnlyDictionary<string, string>? values)
        : base(values)
    {
    }

    public new ConnectorConfigState Clone()
    {
        return new ConnectorConfigState(Values.ToDictionary(
            item => item.Key,
            item => item.Value,
            StringComparer.OrdinalIgnoreCase));
    }

    public static new ConnectorConfigState FromJson(string? json)
    {
        var state = ConfigurationState.FromJson(json);
        return new ConnectorConfigState(state.Values.ToDictionary(
            item => item.Key,
            item => item.Value,
            StringComparer.OrdinalIgnoreCase));
    }
}
