using CanDoItAll.Components.CanvasLib;
using CanDoItAll.Modules.Workbench;
using CanDoItAll.SharedKernel;

namespace CanDoItAll.Tests.Unit.Projects;

public sealed class ProjectStructureRuntimeMetadataCompositionTests
{
    [Fact]
    public void Runtime_catalog_exposes_explicit_POSIX_shell_kind()
    {
        Assert.True(ProjectStructureCanvasCatalog.TryResolveCreateDefinition(
            "add-script-posix-shell",
            out var definition));

        Assert.Equal(ProjectObjectType.Script, definition.ObjectType);
        Assert.Equal("posix-shell", definition.ObjectSubtype);
        Assert.Equal(ProjectScriptKind.PosixShell, ProjectNodeKindRegistry.ResolveScriptKind(definition.ObjectSubtype));
    }

    [Fact]
    public void Python_create_composer_preserves_entry_point_and_typed_arguments()
    {
        Assert.True(ProjectStructureCanvasCatalog.TryResolveCreateDefinition(
            "add-environment-python",
            out var definition));
        var request = new CanvasWorkbenchCreateActionRequest(
            definition.ActionId,
            "parent",
            10,
            20,
            "parent",
            "Python API",
            "Planned",
            "Run the API",
            "child",
            "environment",
            definition.ObjectSubtype,
            null,
            [
                Input("environmentKind", "pythonEnvironment"),
                Input("pythonProvider", "python"),
                Input("environmentName", ".venv"),
                Input("projectPath", "src/python-api"),
                Input("entryPoint", "app.py"),
                Input("environmentArguments", "--port 8000")
            ]);

        var prepared = ProjectStructureCreateRequestComposer.Compose(
            definition,
            request,
            "parent",
            (10, 20));
        var metadata = ProjectObjectMetadataSerializer.Parse(prepared.Request.MetadataJson).Environment;

        Assert.NotNull(metadata);
        Assert.Equal("app.py", metadata!.EntryPoint);
        Assert.Equal("--port 8000", metadata.Arguments);
    }

    [Fact]
    public void Delivery_block_create_composer_preserves_typed_root_authority()
    {
        Assert.True(ProjectStructureCanvasCatalog.TryResolveCreateDefinition(
            "add-block-delivery",
            out var definition));
        var request = new CanvasWorkbenchCreateActionRequest(
            definition.ActionId,
            "parent",
            10,
            20,
            "parent",
            "Delivery",
            "Release",
            "Deliver the product",
            "child",
            "project-block",
            definition.ObjectSubtype,
            null,
            [
                Input("outputRoot", @"C:\products\app"),
                Input("targetRoot", @"C:\products\target"),
                Input("repositoryRoot", @"C:\repositories\product")
            ]);

        var prepared = ProjectStructureCreateRequestComposer.Compose(
            definition,
            request,
            "parent",
            (10, 20));
        var metadata = ProjectObjectMetadataSerializer.Parse(prepared.Request.MetadataJson).ProjectBlock;

        Assert.NotNull(metadata);
        Assert.Equal(@"C:\products\app", metadata!.OutputRoot);
        Assert.Equal(@"C:\products\target", metadata.TargetRoot);
        Assert.Equal(@"C:\repositories\product", metadata.RepositoryRoot);
    }

    private static CanvasWorkbenchInputValue Input(string key, string value)
        => new()
        {
            Key = key,
            Value = value
        };
}
