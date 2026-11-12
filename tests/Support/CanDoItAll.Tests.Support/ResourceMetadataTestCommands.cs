using CanDoItAll.Modules.Resources;

namespace CanDoItAll.Tests.Support;

public static class ResourceMetadataTestCommands {
    public static async Task<ReplaceResourceMetadataCommand> NewAsync(ResourcesService owner, ResourceEditorModel editor,
        Guid? resourceId = null, CancellationToken cancellationToken = default) {
        if (editor.Id is not null) {
            throw new ArgumentException("New Resource fixture commands require a fresh editor; replacements need their original snapshot.", nameof(editor));
        }
        var values = ResourceMetadataValues.Capture(editor);
        var admission = editor.ExpectedProjectAdmission ?? throw new ArgumentException("An explicit target admission is required.", nameof(editor));
        var context = await owner.ReadContextAsync(cancellationToken);
        return new(new ResourceMetadataExpectation.Absent(context, resourceId ?? Guid.NewGuid()), admission, values);
    }

    public static ReplaceResourceMetadataCommand Replace(ResourceMetadataSnapshot original, ResourceEditorModel editor)
        => new(original.Expected, editor.ExpectedProjectAdmission ?? throw new ArgumentException("An explicit target admission is required.", nameof(editor)),
            ResourceMetadataValues.Capture(editor));

    public static async Task<ResourceMetadataSnapshot> FoundAsync(ResourcesService owner, Guid id, CancellationToken cancellationToken = default)
        => await owner.ReadAsync(id, cancellationToken) is ResourceMetadataReadResult.Found found ? found.Snapshot
            : throw new InvalidOperationException("The Resource fixture expected an exact readable snapshot.");
}
