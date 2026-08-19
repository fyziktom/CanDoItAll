using CanDoItAll.Modules.Workbench;

namespace CanDoItAll.Tests.Unit.Projects;

public sealed class ProjectStructureNodeEditorTests
{
    [Theory]
    [InlineData("mermaidText")]
    [InlineData("imageProviderProfileId")]
    [InlineData("imageModel")]
    [InlineData("imageSize")]
    [InlineData("imageQuality")]
    [InlineData("imageOutputFormat")]
    public void Content_generation_fields_are_not_advertised_as_metadata_edit_fields(string fieldKey)
    {
        Assert.False(ProjectStructureNodeEditor.SupportsEditingField(fieldKey));
    }
}
