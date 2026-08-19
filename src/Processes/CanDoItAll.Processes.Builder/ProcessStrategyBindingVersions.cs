namespace CanDoItAll.Processes.Builder;

public static class ProcessStrategyBindingVersions
{
    public const string CurrentBuilder = "builder/1.0";

    public static string ForDriver(string driverVersion)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(driverVersion);
        return $"{CurrentBuilder}:{driverVersion}";
    }
}
