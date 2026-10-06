using CanDoItAll.Modules.Projects;

namespace CanDoItAll.Modules.Workbench;

public sealed record ProjectStructureContentMediaReceipt(ProjectWriteAdmission Project, string NodeId, Guid RecordId,
    string StorageObjectReferenceJson, string FileName, string ContentType);

public sealed class ProjectStructureContentMediaWriteException(ProjectStructureContentMediaReceipt receipt, Exception failure)
    : IOException("Generated media was stored, but its original node attachment could not be confirmed. Observe the original operation before repeating it.", failure) {
    public ProjectStructureContentMediaReceipt Receipt { get; } = receipt;
}
