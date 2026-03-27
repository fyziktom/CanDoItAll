using System.Reflection;
using CanDoItAll.Modules.Workbench.CanvasAdapters;

namespace CanDoItAll.Tests.Components;

public sealed class ProjectStructureCanvasCatalogTests
{
    [Fact]
    public void Markdown_create_definition_keeps_text_fields_and_file_upload_enabled()
    {
        var assembly = typeof(ProjectStructureActionCatalogAdapter).Assembly;
        var catalogType = assembly.GetType("CanDoItAll.Modules.Workbench.ProjectStructureCanvasCatalog");

        Assert.NotNull(catalogType);

        var tryResolveMethod = catalogType!.GetMethod(
            "TryResolveCreateDefinition",
            BindingFlags.Public | BindingFlags.Static);

        Assert.NotNull(tryResolveMethod);

        var arguments = new object?[] { "add-file-markdown", null };
        var resolved = (bool)tryResolveMethod!.Invoke(null, arguments)!;

        Assert.True(resolved);

        var definition = arguments[1];

        Assert.NotNull(definition);

        var definitionType = definition!.GetType();

        Assert.True((bool)definitionType.GetProperty("RequiresFile")!.GetValue(definition)!);
        Assert.True((bool)definitionType.GetProperty("ShowDefaultTextFields")!.GetValue(definition)!);
        Assert.Equal(
            ".md,.markdown,.txt,text/markdown,text/plain",
            definitionType.GetProperty("AcceptedFileTypes")!.GetValue(definition));
        Assert.Equal(
            "Paste markdown below, drop a markdown file here, or choose one.",
            definitionType.GetProperty("FilePrompt")!.GetValue(definition));
    }
}
