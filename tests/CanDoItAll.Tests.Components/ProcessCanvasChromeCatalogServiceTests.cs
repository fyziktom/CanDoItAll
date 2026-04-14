using System.Text.Json;

using CanDoItAll.Modules.Processes;

namespace CanDoItAll.Tests.Components;

public sealed class ProcessCanvasChromeCatalogServiceTests
{
    private static readonly string[] ExpectedQuickCreateActionIds =
    [
        "process-definition.open-toolbox",
        "process-role.product-owner",
        "process-role.solution-architect",
        "process-step.intake",
        "process-step.architecture",
        "process-step.implementation",
        "process-step.qa",
        "process-step.release-approval"
    ];

    private static readonly string[] ExpectedGroupContextActionIds =
    [
        "process-definition.edit-step",
        "process-definition.add-dependent-step",
        "process-definition.add-role-binding",
        "process-definition.add-artifact-expectation",
        "process-definition.remove-step"
    ];

    [Fact]
    public void GetDefinitionChrome_loads_sidecar_actions_in_configured_order()
    {
        var service = CreateService();

        var chrome = service.GetDefinitionChrome();

        Assert.Equal(ExpectedQuickCreateActionIds, chrome.DefinitionQuickCreateActions.Select(action => action.ActionId));
        Assert.Equal(ExpectedGroupContextActionIds, chrome.DefinitionGroupContextActions.Select(action => action.ActionId));
    }

    [Fact]
    public void GetDefinitionChrome_throws_when_sidecar_file_is_missing()
    {
        using var packClone = CreatePackClone();
        File.Delete(Path.Combine(packClone.RootPath, "toolbox", "chrome-actions.json"));
        var service = CreateService(packClone.RootPath);

        var exception = Assert.Throws<FileNotFoundException>(() => service.GetDefinitionChrome());

        Assert.Contains("chrome-actions.json", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void GetDefinitionChrome_throws_when_sidecar_contains_unknown_action()
    {
        using var packClone = CreatePackClone();
        var chromeActionsPath = Path.Combine(packClone.RootPath, "toolbox", "chrome-actions.json");
        var chromeActions = JsonSerializer.Deserialize<ProcessTemplateToolboxChromeCatalog>(
                File.ReadAllText(chromeActionsPath),
                new JsonSerializerOptions(JsonSerializerDefaults.Web)
                {
                    PropertyNameCaseInsensitive = true
                })
            ?? new ProcessTemplateToolboxChromeCatalog();
        chromeActions.DefinitionQuickCreateActions[0] = "process-definition.unsupported-action";
        File.WriteAllText(
            chromeActionsPath,
            JsonSerializer.Serialize(
                chromeActions,
                new JsonSerializerOptions(JsonSerializerDefaults.Web)
                {
                    WriteIndented = true
                }));
        var service = CreateService(packClone.RootPath);

        var exception = Assert.Throws<InvalidOperationException>(() => service.GetDefinitionChrome());

        Assert.Contains("unsupported-action", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    private static ProcessCanvasChromeCatalogService CreateService(string? packRoot = null)
    {
        return new ProcessCanvasChromeCatalogService(new ProcessTemplatePackLoader(packRoot));
    }

    private static PackClone CreatePackClone()
    {
        var sourceRoot = ProcessTemplatePackLoader.FindPackRoot();
        return new PackClone(sourceRoot);
    }

    private sealed class PackClone : IDisposable
    {
        public PackClone(string sourceRoot)
        {
            RootPath = Path.Combine(Path.GetTempPath(), "candoitall-pack-" + Guid.NewGuid().ToString("N"));
            CopyDirectory(sourceRoot, RootPath);
        }

        public string RootPath { get; }

        public void Dispose()
        {
            if (Directory.Exists(RootPath))
            {
                Directory.Delete(RootPath, recursive: true);
            }
        }

        private static void CopyDirectory(string sourceRoot, string destinationRoot)
        {
            foreach (var directoryPath in Directory.GetDirectories(sourceRoot, "*", SearchOption.AllDirectories))
            {
                var relativePath = Path.GetRelativePath(sourceRoot, directoryPath);
                Directory.CreateDirectory(Path.Combine(destinationRoot, relativePath));
            }

            Directory.CreateDirectory(destinationRoot);
            foreach (var filePath in Directory.GetFiles(sourceRoot, "*", SearchOption.AllDirectories))
            {
                var relativePath = Path.GetRelativePath(sourceRoot, filePath);
                var destinationPath = Path.Combine(destinationRoot, relativePath);
                Directory.CreateDirectory(Path.GetDirectoryName(destinationPath)!);
                File.Copy(filePath, destinationPath, overwrite: true);
            }
        }
    }
}
