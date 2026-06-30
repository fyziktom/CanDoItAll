namespace CanDoItAll.Modules.Workbench.Pages;

internal static class ProjectStructurePageTitleBuilder
{
    internal const int MaxProjectNameLength = 48;

    public static string Build(string? projectName)
    {
        var normalizedName = string.IsNullOrWhiteSpace(projectName)
            ? "Project Structure"
            : projectName.Trim();

        if (normalizedName.Length > MaxProjectNameLength)
        {
            normalizedName = $"{normalizedName[..(MaxProjectNameLength - 3)]}...";
        }

        return $"PS - {normalizedName}";
    }
}
