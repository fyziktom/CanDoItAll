using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Maf;
using CanDoItAll.AgentFramework.Models;

namespace CanDoItAll.Tests.Unit;

public sealed class WorkspaceFilesystemRuntimePluginTests : IDisposable
{
    private readonly string workspaceRoot = Path.Combine(Path.GetTempPath(), "CanDoItAll.WorkspaceFilesystemRuntimePluginTests", Guid.NewGuid().ToString("N"));

    [Fact]
    public void ListWorkspaceDirectory_delegates_to_shallow_file_service_operation()
    {
        Directory.CreateDirectory(Path.Combine(workspaceRoot, "docs", "nested"));
        File.WriteAllText(Path.Combine(workspaceRoot, "docs", "quote.txt"), "quote");
        File.WriteAllText(Path.Combine(workspaceRoot, "docs", "nested", "details.txt"), "details");
        var plugin = CreatePlugin(AgentWorkspaceToolAccessProfiles.CreateSettings(AgentWorkspaceToolProfileKind.SoftwareDevelopment));

        var result = plugin.ListWorkspaceDirectory("docs");

        Assert.True(result.Succeeded, result.Message);
        Assert.Equal("workspace_list_directory", result.Receipt.Operation);
        Assert.Contains(result.Entries, item => string.Equals(item.RelativePath, "docs/quote.txt", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(result.Entries, item => string.Equals(item.RelativePath, "docs/nested", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(result.Entries, item => string.Equals(item.RelativePath, "docs/nested/details.txt", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void HashZipAndUnzip_are_available_without_WorkspaceRuntimePlugin()
    {
        Directory.CreateDirectory(Path.Combine(workspaceRoot, "docs"));
        File.WriteAllText(Path.Combine(workspaceRoot, "docs", "quote.txt"), "quote");
        var plugin = CreatePlugin(AgentWorkspaceToolAccessProfiles.CreateSettings(AgentWorkspaceToolProfileKind.SoftwareDevelopment));

        var hash = plugin.HashWorkspacePath("docs");
        var zip = plugin.ZipWorkspacePath("docs", "archives/docs.zip");
        var unzip = plugin.UnzipWorkspaceArchive("archives/docs.zip", "expanded");

        Assert.True(hash.Succeeded, hash.Message);
        Assert.Equal("workspace_hash_path", hash.Receipt.Operation);
        Assert.True(zip.Succeeded, zip.Message);
        Assert.Equal("workspace_zip_path", zip.Receipt.Operation);
        Assert.True(unzip.Succeeded, unzip.Message);
        Assert.Equal("workspace_unzip_archive", unzip.Receipt.Operation);
        Assert.True(File.Exists(Path.Combine(workspaceRoot, "expanded", "quote.txt")));
    }

    [Fact]
    public void Write_operations_fail_predictably_for_read_only_access()
    {
        Directory.CreateDirectory(workspaceRoot);
        var plugin = CreatePlugin(AgentWorkspaceToolAccessProfiles.CreateSettings(AgentWorkspaceToolProfileKind.ReadOnly));

        var exception = Assert.Throws<InvalidOperationException>(() => plugin.CreateWorkspaceDirectory("created"));

        Assert.Contains("not allowed to write workspace files", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(workspaceRoot))
            {
                Directory.Delete(workspaceRoot, recursive: true);
            }
        }
        catch
        {
        }
    }

    private WorkspaceFilesystemRuntimePlugin CreatePlugin(AgentWorkspaceToolAccessSettings access)
    {
        Directory.CreateDirectory(workspaceRoot);
        return new WorkspaceFilesystemRuntimePlugin(
            new WorkspaceFileService(workspaceRoot),
            workspaceRoot,
            WorkspaceScopeDescriptor.Sandbox,
            access);
    }
}
