namespace CanDoItAll.Modules.Workbench;

public static class ProjectProgressPolicy
{
    public const int UntrackedPercent = -1;

    public static bool IsTrackedPercent(int value)
    {
        return value is >= 0 and <= 100;
    }
}
