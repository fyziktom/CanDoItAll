namespace CanDoItAll.Modules.Workbench;

[AttributeUsage(AttributeTargets.Property)]
public sealed class ProjectStructurePreviewFieldAttribute : Attribute
{
    public ProjectStructurePreviewFieldAttribute(string label, int order = 0)
    {
        Label = label;
        Order = order;
    }

    public string Label { get; }

    public int Order { get; }
}
