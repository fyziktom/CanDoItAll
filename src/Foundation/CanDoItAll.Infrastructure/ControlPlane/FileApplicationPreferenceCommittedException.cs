namespace CanDoItAll.Infrastructure.ControlPlane;

public sealed class FileApplicationPreferenceCommittedException(FileApplicationExtension extension, bool deleted)
    : Exception("The file application preference was committed; logging failed.") {
    public FileApplicationExtension Extension { get; } = extension;
    public bool Deleted { get; } = deleted;
}
