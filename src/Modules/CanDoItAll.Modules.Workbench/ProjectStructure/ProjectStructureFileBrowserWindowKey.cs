namespace CanDoItAll.Modules.Workbench;

internal readonly record struct ProjectStructureFileBrowserWindowKey
{
    public static ProjectStructureFileBrowserWindowKey Persisted { get; } = new("project-structure.fileBrowser");

    private ProjectStructureFileBrowserWindowKey(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        Value = value;
    }

    public string Value { get; }
}
