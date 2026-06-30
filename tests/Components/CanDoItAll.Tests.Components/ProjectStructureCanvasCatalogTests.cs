using System.Reflection;
using CanDoItAll.Components.CanvasLib;
using CanDoItAll.Modules.Workbench.CanvasAdapters;
using CanDoItAll.SharedKernel;

namespace CanDoItAll.Tests.Components;

public sealed class ProjectStructureCanvasCatalogTests
{
    [Fact]
    public void Markdown_create_definition_keeps_text_fields_and_file_upload_enabled()
    {
        var assembly = typeof(ProjectStructureActionCatalogAdapter).Assembly;
        var catalogType = assembly.GetType("CanDoItAll.Modules.Workbench.ProjectStructureCanvasCatalog");

        Assert.NotNull(catalogType);

        var tryResolveMethod = catalogType!
            .GetMethods(BindingFlags.Public | BindingFlags.Static)
            .Single(method =>
            {
                if (!string.Equals(method.Name, "TryResolveCreateDefinition", StringComparison.Ordinal))
                {
                    return false;
                }

                var parameters = method.GetParameters();
                return parameters.Length == 2 &&
                    parameters[0].ParameterType == typeof(string) &&
                    parameters[1].IsOut;
            });

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

    [Fact]
    public void Build_menu_create_actions_assigns_requested_asset_shortcuts_and_word_label()
    {
        var buildMenuCreateActions = GetCatalogMethod("BuildMenuCreateActions", typeof(ProjectObjectType?));
        var arguments = new object?[] { null };

        var actions = Assert.IsAssignableFrom<IReadOnlyList<CanvasWorkbenchAction>>(buildMenuCreateActions.Invoke(null, arguments));
        var assetGroup = Assert.Single(actions, action => action.ActionId == "group-assets");

        Assert.Equal("a", assetGroup.ShortcutKey);
        Assert.Equal("p", Assert.Single(assetGroup.Children, action => action.ActionId == "add-file-pdf").ShortcutKey);
        Assert.Equal("e", Assert.Single(assetGroup.Children, action => action.ActionId == "add-file-excel").ShortcutKey);

        var wordAction = Assert.Single(assetGroup.Children, action => action.ActionId == "add-file-docx");
        Assert.Equal("w", wordAction.ShortcutKey);
        Assert.Equal("Word", wordAction.MenuLabel);
    }

    [Fact]
    public void Generated_image_asset_definition_exposes_provider_prompt_and_format_fields()
    {
        var tryResolveMethod = GetTryResolveCreateDefinitionMethod();
        var arguments = new object?[] { "generate-image-asset", null };
        var resolved = (bool)tryResolveMethod.Invoke(null, arguments)!;

        Assert.True(resolved);

        var definition = arguments[1];

        Assert.NotNull(definition);

        var definitionType = definition!.GetType();
        Assert.Equal(ProjectObjectType.ImageAsset, definitionType.GetProperty("ObjectType")!.GetValue(definition));
        Assert.Equal("generated", definitionType.GetProperty("ObjectSubtype")!.GetValue(definition));
        Assert.False((bool)definitionType.GetProperty("RequiresFile")!.GetValue(definition)!);
        Assert.Equal("Prompt", definitionType.GetProperty("NotesLabel")!.GetValue(definition));
        Assert.Equal("Generate image", definitionType.GetProperty("SubmitLabel")!.GetValue(definition));

        var fields = Assert.IsAssignableFrom<IReadOnlyList<CanvasWorkbenchInputField>>(
            definitionType.GetProperty("InputFields")!.GetValue(definition));
        Assert.Contains(fields, field => field.Key == "imageProviderProfileId" && field.InputMode == "select" && field.IsRequired);
        Assert.Contains(fields, field => field.Key == "imageModel" && field.InputMode == "text");
        Assert.Contains(fields, field => field.Key == "imageSize" && field.Options.Any(option => option.Value == "1536x1024"));
        Assert.Contains(fields, field => field.Key == "imageQuality" && field.Options.Any(option => option.Value == "high"));
        Assert.Contains(fields, field => field.Key == "imageOutputFormat" && field.Options.Any(option => option.Value == "webp"));
    }

    private static MethodInfo GetCatalogMethod(string name, params Type[] parameterTypes)
    {
        var assembly = typeof(ProjectStructureActionCatalogAdapter).Assembly;
        var catalogType = assembly.GetType("CanDoItAll.Modules.Workbench.ProjectStructureCanvasCatalog");

        Assert.NotNull(catalogType);

        return catalogType!
            .GetMethods(BindingFlags.Public | BindingFlags.Static)
            .Single(method =>
            {
                if (!string.Equals(method.Name, name, StringComparison.Ordinal))
                {
                    return false;
                }

                var parameters = method.GetParameters();
                if (parameters.Length != parameterTypes.Length)
                {
                    return false;
                }

                for (var index = 0; index < parameterTypes.Length; index++)
                {
                    if (parameters[index].ParameterType != parameterTypes[index])
                    {
                        return false;
                    }
                }

                return true;
            });
    }

    private static MethodInfo GetTryResolveCreateDefinitionMethod()
    {
        var assembly = typeof(ProjectStructureActionCatalogAdapter).Assembly;
        var catalogType = assembly.GetType("CanDoItAll.Modules.Workbench.ProjectStructureCanvasCatalog");

        Assert.NotNull(catalogType);

        return catalogType!
            .GetMethods(BindingFlags.Public | BindingFlags.Static)
            .Single(method =>
            {
                if (!string.Equals(method.Name, "TryResolveCreateDefinition", StringComparison.Ordinal))
                {
                    return false;
                }

                var parameters = method.GetParameters();
                return parameters.Length == 2 &&
                       parameters[0].ParameterType == typeof(string) &&
                       parameters[1].IsOut;
            });
    }
}
