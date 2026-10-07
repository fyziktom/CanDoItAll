using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using CanDoItAll.FileTools.FileBrowser;
using CanDoItAll.FileTools.FileInteraction;
using CanDoItAll.FileTools.Integration;
using CanDoItAll.Infrastructure.ControlPlane;
using CanDoItAll.Infrastructure.Storage;
using CanDoItAll.Modules.Projects;
using CanDoItAll.Modules.Workbench;
using CanDoItAll.SharedKernel;
using CanDoItAll.Tests.Support;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace CanDoItAll.Tests.Integration.ProjectStructure;

[Trait("Category", "LiveProcess")]
[Trait("Category", "HostPlatform")]
public sealed class ProjectStructureNativeLocalActionsTests {
    private const string ProbeVariable = "CANDOITALL_FILE_OPEN_PROBE";
    private const string ProbeRootVariable = "CANDOITALL_FILE_OPEN_PROBE_ROOT";
    private const string ReceiptVariable = "CANDOITALL_FILE_OPEN_PROBE_RECEIPT";

    [Fact]
    public async Task Native_download_leases_and_harmless_preferred_launch_keep_exact_file_authority() {
        var executable = Environment.GetEnvironmentVariable(ProbeVariable);
        var receiptPath = Environment.GetEnvironmentVariable(ReceiptVariable);
        Assert.True(OperatingSystem.IsWindows() && Environment.UserInteractive, "This explicit host journey requires interactive Windows.");
        Assert.True(File.Exists(executable), "Provide the reviewed harmless file-open probe executable.");
        Assert.False(string.IsNullOrWhiteSpace(receiptPath), "Provide a new owned receipt path.");
        Assert.False(File.Exists(receiptPath), "An existing receipt must be inspected before any further launch.");
        await using var application = await TestApplication.CreateAsync(new TestHarnessOptions {
            ConfigurationOverrides = new Dictionary<string, string?> {
                [$"{FileToolsDesktopLaunchOptions.SectionName}:{nameof(FileToolsDesktopLaunchOptions.Enabled)}"] = "true",
                [$"{FileToolsDesktopLaunchOptions.SectionName}:{nameof(FileToolsDesktopLaunchOptions.HostProfileAllowsDesktop)}"] = "true"
            }
        });
        await using var scope = application.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var paths = services.GetRequiredService<IControlPlanePathResolver>();
        Assert.StartsWith(application.RootPath, paths.ResolveRootPath(), StringComparison.OrdinalIgnoreCase);
        var project = await services.GetRequiredService<ProjectsService>().SaveAsync(new ProjectEditorModel {
            Name = "Native content host journey", Objective = "Exact file authority", CurrentPhase = "Verification"
        });
        Assert.True(project.IsSuccess);
        var projectId = project.Value;
        var workbench = services.GetRequiredService<ProjectWorkbenchService>();
        var original = await workbench.GetStructureAsync(projectId);
        byte[] expected = Encoding.UTF8.GetBytes("Native local launch reads exact bytes.\r\nŽluťoučký 東京\r\n");
        var downloadable = await workbench.CreateObjectAsync(projectId,
            new(ProjectObjectType.File, "Native download", "", "", $"project:{projectId:D}",
                ObjectSubtype: "text", Media: new("wb4-native-download.txt", "text/plain", Convert.ToBase64String(expected))) {
                ExpectedProjectAdmission = original.ExpectedProjectAdmission
            });
        var files = services.GetRequiredService<ProjectStructureFileActionCoordinator>();
        await using var workspace = await files.OpenAsync(new ProjectStructureProjectFileCollectionRequest(projectId, "Native files"), false);
        await workspace.Browser.InitializeAsync();
        await workspace.Browser.SearchAsync("wb4-native-download", FileBrowserSearchScope.Progressive);
        var item = Assert.Single(workspace.Browser.Snapshot.Items);
        await using var first = await files.AuthorizeDownloadAsync(workspace, item.Key);
        await using var second = await files.AuthorizeDownloadAsync(workspace, item.Key);
        Assert.Equal(expected, await ReadAsync(first));
        await first.DisposeAsync();
        await first.DisposeAsync();
        await Assert.ThrowsAsync<ObjectDisposedException>(async () => await first.OpenReadAsync());
        Assert.Equal(expected, await ReadAsync(second));
        await File.WriteAllTextAsync(receiptPath + ".download.json", JsonSerializer.Serialize(new {
            projectId, downloadable.Id, downloadable.ParentId, downloadable.StorageObjectReferenceJson,
            Sha256 = Convert.ToHexString(SHA256.HashData(expected)), FreshLeases = 2, ReleasedLeaseRejected = true
        }));
        var catalog = services.GetRequiredService<IStorageCatalogService>();
        var bootstrap = await catalog.EnsureBootstrapFileSystemStorageAsync();
        var storageRoot = Path.Combine(bootstrap.EndpointOrRoot, "native-local-actions");
        Directory.CreateDirectory(storageRoot);
        var configuredStorage = await catalog.SaveAsync(StorageCatalogSaveRequest.FromSnapshot(bootstrap) with {
            Id = Guid.NewGuid(),
            Name = "Native launch source",
            IsSystemDefault = false,
            EndpointOrRoot = storageRoot
        });
        await catalog.SaveRuleAsync(new StorageRoutingRuleSaveRequest {
            Name = "Native launch project assets",
            ScopeKind = StorageRoutingScopeKind.Project,
            ProjectId = projectId,
            UsagePurpose = StorageUsagePurpose.ProjectAsset,
            PreferredStorageId = configuredStorage.Id
        });
        var node = await workbench.CreateObjectAsync(projectId,
            new(ProjectObjectType.File, "Native file", "", "Supplemental notes", $"project:{projectId:D}",
                ObjectSubtype: "text", Media: new("wb4-native-open.txt", "text/plain", Convert.ToBase64String(expected))) {
                ExpectedProjectAdmission = original.ExpectedProjectAdmission
            });
        Assert.True(StorageJson.TryParseReference(node.StorageObjectReferenceJson, out var reference));
        Assert.NotNull(reference);
        Assert.Equal(configuredStorage.Id, reference.StorageId);
        var storage = await catalog.GetAsync(configuredStorage.Id);
        Assert.NotNull(storage);
        Assert.False(storage.IsSystemDefault);
        var fullPath = services.GetRequiredService<FileSystemStoragePathPolicy>().ResolveTrustedLocalOpenPath(storage, reference.Locator);
        Assert.StartsWith(application.RootPath, fullPath, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(expected, await File.ReadAllBytesAsync(fullPath));
        await File.WriteAllTextAsync(receiptPath + ".accepted.json", JsonSerializer.Serialize(new {
            projectId, node.Id, node.ParentId, node.StorageObjectReferenceJson, StorageId = storage.Id,
            Target = fullPath, Sha256 = Convert.ToHexString(SHA256.HashData(expected))
        }));
        var neighbor = Path.Combine(application.RootPath, "unrelated-canary.txt");
        await File.WriteAllTextAsync(neighbor, "Never a launch target.");

        await services.GetRequiredService<IFileApplicationPreferenceService>().SaveAsync(new(new(".txt"), executable!));
        var launcher = services.GetRequiredService<ProjectStructureLocalFileActionCoordinator>();
        var previousRoot = Environment.GetEnvironmentVariable(ProbeRootVariable);
        Environment.SetEnvironmentVariable(ProbeRootVariable, application.RootPath);
        try {
            var launched = await launcher.LaunchAsync(projectId, node.Id, FileToolsLocalFileAction.OpenInPreferredApplication);
            Assert.True(launched.IsSuccess, launched.Message);
            var deadline = DateTime.UtcNow.AddSeconds(15);
            while (!File.Exists(receiptPath) && DateTime.UtcNow < deadline) {
                await Task.Delay(50);
            }
            Assert.True(File.Exists(receiptPath), "The accepted launch did not produce a receipt; inspect it without relaunching.");
            var receipts = await File.ReadAllLinesAsync(receiptPath!);
            using var receipt = JsonDocument.Parse(Assert.Single(receipts));
            Assert.Equal(fullPath, receipt.RootElement.GetProperty("Target").GetString());
            Assert.Equal(expected.Length, receipt.RootElement.GetProperty("Bytes").GetInt32());
            Assert.Equal(Convert.ToHexString(SHA256.HashData(expected)), receipt.RootElement.GetProperty("Sha256").GetString());
            Assert.NotEqual(Environment.ProcessId, receipt.RootElement.GetProperty("ProcessId").GetInt32());
            var reduced = await catalog.SaveAsync(StorageCatalogSaveRequest.FromSnapshot(storage) with {
                CapabilityMask = storage.CapabilityMask & ~StorageCapability.OpenLocally
            });
            Assert.False(reduced.CapabilityMask.HasFlag(StorageCapability.OpenLocally));
            var current = await catalog.GetAsync(storage.Id);
            Assert.NotNull(current);
            Assert.False(current.CapabilityMask.HasFlag(StorageCapability.OpenLocally));
            await Assert.ThrowsAsync<FileAccessDeniedException>(async () =>
                await launcher.LaunchAsync(projectId, node.Id, FileToolsLocalFileAction.OpenInPreferredApplication));
            services.GetRequiredService<IOptions<FileToolsDesktopLaunchOptions>>().Value.HostProfileAllowsDesktop = false;
            var denied = await launcher.LaunchAsync(projectId, node.Id, FileToolsLocalFileAction.OpenContainingFolder);
            Assert.False(denied.IsSuccess);
            Assert.Equal(receipts, await File.ReadAllLinesAsync(receiptPath!));
            Assert.Equal(expected, await File.ReadAllBytesAsync(fullPath));
            Assert.Equal("Never a launch target.", await File.ReadAllTextAsync(neighbor));
            await File.WriteAllTextAsync(receiptPath + ".native.json", JsonSerializer.Serialize(new {
                projectId, node.Id, node.ParentId, node.StorageObjectReferenceJson, StorageId = storage.Id, DownloadNodeId = downloadable.Id,
                Sha256 = Convert.ToHexString(SHA256.HashData(expected)), FreshLeases = 2, NativeLaunches = 1,
                DeniedAfterCapabilityLoss = true, DeniedHeadlessFolder = true
            }));
        } finally {
            Environment.SetEnvironmentVariable(ProbeRootVariable, previousRoot);
        }
    }

    private static async Task<byte[]> ReadAsync(IFileToolsDownloadLease lease) {
        await using var content = await lease.OpenReadAsync();
        using var bytes = new MemoryStream();
        await content.Stream.CopyToAsync(bytes);
        return bytes.ToArray();
    }
}
