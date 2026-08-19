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

    private static CanvasWorkbenchInputValue Input(string key, string value)
        => new()
        {
            Key = key,
            Value = value
        };
}
