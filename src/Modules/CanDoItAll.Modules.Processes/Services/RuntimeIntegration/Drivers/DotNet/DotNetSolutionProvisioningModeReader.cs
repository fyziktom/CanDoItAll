namespace CanDoItAll.Modules.Processes;

internal static class DotNetSolutionProvisioningModeReader
{
    private const string LaunchVariableKey = "DotNetProvisioningMode";

    public static bool TryRead(
        IReadOnlyDictionary<string, string> launchVariables,
        out DotNetSolutionProvisioningMode provisioningMode,
        out string issue)
    {
        ArgumentNullException.ThrowIfNull(launchVariables);

        provisioningMode = default;
        issue = string.Empty;
        if (!launchVariables.TryGetValue(LaunchVariableKey, out var value) || string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        if (string.Equals(value.Trim(), "initialize", StringComparison.OrdinalIgnoreCase))
        {
            provisioningMode = DotNetSolutionProvisioningMode.Initialize;
            return true;
        }

        if (string.Equals(value.Trim(), "verify-existing", StringComparison.OrdinalIgnoreCase))
        {
            provisioningMode = DotNetSolutionProvisioningMode.VerifyExisting;
            return true;
        }

        issue = $"The .NET solution provisioning mode '{value.Trim()}' is not supported.";
        return false;
    }
}
