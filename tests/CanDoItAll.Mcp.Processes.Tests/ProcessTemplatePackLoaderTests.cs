using CanDoItAll.Modules.Processes;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace CanDoItAll.Mcp.Processes.Tests;

public sealed class ProcessTemplatePackLoaderTests
{
    [Fact]
    public void Load_returns_current_architecture_pack_shape()
    {
        var loader = new ProcessTemplatePackLoader();
        var pack = loader.Load();

        Assert.Equal("candoitall-software-process-template-pack", pack.Manifest.PackKey);
        Assert.Equal(9, pack.Processes.Count);
        Assert.Equal(5, pack.BaselineScenarios.Count);
        Assert.True(pack.SharedRoles.ContainsKey("review-lead"));
        Assert.True(pack.Processes.ContainsKey("branching-code-review"));
        Assert.True(pack.Processes.ContainsKey("ai-assisted-change-delivery"));
        Assert.NotEmpty(pack.ChromeActions.DefinitionQuickCreateActions);
    }

    [Fact]
    public void FindPackRoot_prefers_templates_directory_reachable_from_current_working_directory()
    {
        using var repoClone = CreateRepoLayoutClone(ProcessTemplatePackLoader.FindPackRoot());
        var originalCurrentDirectory = Directory.GetCurrentDirectory();

        try
        {
            Directory.SetCurrentDirectory(repoClone.RootPath);

            var root = ProcessTemplatePackLoader.FindPackRoot();

            Assert.True(File.Exists(Path.Combine(root, "manifest.json")));
            Assert.Equal(Path.Combine(repoClone.RootPath, "Templates", "Processes"), root, ignoreCase: true);
        }
        finally
        {
            Directory.SetCurrentDirectory(originalCurrentDirectory);
        }
    }

    [Fact]
    public void Load_accepts_an_explicit_manifest_path()
    {
        var root = ProcessTemplatePackLoader.FindPackRoot();
        var loader = new ProcessTemplatePackLoader(Path.Combine(root, "manifest.json"));

        var pack = loader.Load();

        Assert.Equal(root, pack.RootPath);
    }

    [Fact]
    public void AddProcessesModule_resolves_loader_from_configured_pack_root()
    {
        using var packClone = CreatePackClone(ProcessTemplatePackLoader.FindPackRoot());
        var services = new ServiceCollection();
        var configuration = new ConfigurationBuilder().Build();
        services.AddSingleton<IConfiguration>(configuration);
        services.AddLogging();
        services.AddProcessesModule(configuration);
        services.Configure<ProcessTemplatePackOptions>(options => options.PackRoot = packClone.RootPath);
        using var provider = services.BuildServiceProvider();

        var loader = provider.GetRequiredService<ProcessTemplatePackLoader>();
        var pack = loader.Load();

        Assert.Equal(packClone.RootPath, pack.RootPath);
    }

    private sealed class PackClone : IDisposable
    {
        public PackClone(string sourceRoot, string? relativeDestinationPath = null)
        {
            RootPath = Path.Combine(Path.GetTempPath(), "candoitall-pack-loader-" + Guid.NewGuid().ToString("N"));
            PackRootPath = string.IsNullOrWhiteSpace(relativeDestinationPath)
                ? RootPath
                : Path.Combine(RootPath, relativeDestinationPath);
            CopyDirectory(sourceRoot, PackRootPath);
        }

        public string RootPath { get; }

        public string PackRootPath { get; }

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

    private static PackClone CreatePackClone(string sourceRoot)
    {
        return new PackClone(sourceRoot);
    }

    private static PackClone CreateRepoLayoutClone(string sourceRoot)
    {
        return new PackClone(sourceRoot, Path.Combine("Templates", "Processes"));
    }
}
