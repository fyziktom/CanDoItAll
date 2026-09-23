namespace CanDoItAll.Modules.AgentFramework.ProviderManagement;

public static class SharedProviderProfileOwnershipPolicy
{
    public const string GenericSaveRejectionMessage =
        "Shared-provider profiles are source-managed. Create and update them through shared-provider source/import management, not the generic provider editor.";

    public static bool IsSourceManagedConnector(string? connectorPluginKey)
    {
        return string.Equals(
            connectorPluginKey?.Trim(),
            ProviderConnectorKeys.SharedImport,
            StringComparison.OrdinalIgnoreCase);
    }
}
