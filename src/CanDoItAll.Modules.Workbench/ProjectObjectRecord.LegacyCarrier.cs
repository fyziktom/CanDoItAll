namespace CanDoItAll.Modules.Workbench;

public sealed partial class ProjectObjectRecord
{
    public string Route { get; set; } = string.Empty;

    public string ExternalArtifactKind { get; set; } = string.Empty;

    public Guid? ExternalArtifactId { get; set; }

    public string MediaRelativePath { get; set; } = string.Empty;

    public string MediaContentType { get; set; } = string.Empty;

    public string MediaOriginalFileName { get; set; } = string.Empty;

    public string StorageObjectReferenceJson { get; set; } = string.Empty;
}
