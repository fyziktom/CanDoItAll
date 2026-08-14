using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Infrastructure.Storage;
using CanDoItAll.SharedKernel;

namespace CanDoItAll.Tests.Unit;

public sealed class WorkspaceExternalTargetAliasTests : IDisposable
{
    private readonly string rootPath = Path.Combine(Path.GetTempPath(), "CanDoItAll.WorkspaceExternalTargetAliasTests", Guid.NewGuid().ToString("N"));
    private readonly ExternalTargetPathRegistry externalTargets = new();

    [Fact]
    public void Receipt_writer_keeps_case_distinct_versioned_alias_targets()
    {
        const string rootId = "0123456789abcdef01234567";
        var upperAlias = ExternalTargetAliasCodec.BuildAlias(rootId, ["Foo"]);
        var lowerAlias = ExternalTargetAliasCodec.BuildAlias(rootId, ["foo"]);
        var writer = new WorkspaceFileReceiptWriter(CreateDirectory("workspace"));

        var references = writer.BuildTargetArtifactReferences([upperAlias, lowerAlias], "test");

        Assert.Equal(2, references.Count);
        Assert.Contains(references, item => string.Equals(item.RelativePath, upperAlias, StringComparison.Ordinal));
        Assert.Contains(references, item => string.Equals(item.RelativePath, lowerAlias, StringComparison.Ordinal));
    }

    [Fact]
    public void TryResolveWorkspacePath_maps_external_target_alias_to_real_external_path()
    {
        var workspaceRoot = CreateDirectory("workspace");
        var externalFilePath = Path.Combine(CreateDirectory("external-target-root"), "Workflow", "Workflow.sln");
        var aliasPath = BuildExternalTargetAlias(externalFilePath);
        var policy = TestWorkspaceServices.CreatePathPolicy(workspaceRoot, externalTargetRegistry: externalTargets);

        var succeeded = policy.TryResolveWorkspacePath(aliasPath, allowWorkspaceRoot: false, out var resolution, out var validationMessage);

        Assert.True(succeeded);
        Assert.True(string.IsNullOrWhiteSpace(validationMessage));
        Assert.Equal(Path.GetFullPath(externalFilePath), resolution.FullPath);
        Assert.Equal(aliasPath, resolution.RelativePath);
        Assert.Equal(aliasPath, policy.ToRelativePath(resolution.FullPath));
        Assert.Equal(aliasPath, policy.ToDisplayPath(resolution.FullPath));
    }

    [Fact]
    public void TryResolveWorkspacePath_rejects_external_drive_root()
    {
        var workspaceRoot = CreateDirectory("workspace");
        var policy = TestWorkspaceServices.CreatePathPolicy(workspaceRoot, externalTargetRegistry: externalTargets);

        var succeeded = policy.TryResolveWorkspacePath(
            "external-target/C",
            allowWorkspaceRoot: false,
            out _,
            out var validationMessage);

        Assert.False(succeeded);
        if (OperatingSystem.IsWindows())
        {
            Assert.Contains("external drive root", validationMessage, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("specific grounded path", validationMessage, StringComparison.OrdinalIgnoreCase);
        }
        else
        {
            Assert.Contains("legacy Windows drive alias", validationMessage, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("rebind", validationMessage, StringComparison.OrdinalIgnoreCase);
        }
    }

    [Theory]
    [InlineData("external-target/C/repositories/demo/../secret")]
    [InlineData("external-target/C/repositories/demo/./src")]
    [InlineData("external-target/C/repositories/demo/..")]
    [InlineData("external-target/C/repositories/demo/.")]
    [InlineData(@"external-target\C\repositories\demo\..\secret")]
    public void TryResolveWorkspacePath_rejects_external_target_dot_segments(string aliasPath)
    {
        var workspaceRoot = CreateDirectory("workspace");
        var policy = TestWorkspaceServices.CreatePathPolicy(workspaceRoot, externalTargetRegistry: externalTargets);

        var succeeded = policy.TryResolveWorkspacePath(
            aliasPath,
            allowWorkspaceRoot: false,
            out _,
            out var validationMessage);

        Assert.False(succeeded);
        Assert.Contains("traversal segments", validationMessage, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void TryResolveWorkspacePath_rejects_reparse_point_traversal()
    {
        var workspaceRoot = CreateDirectory("workspace");
        var outsideRoot = CreateDirectory("outside");
        File.WriteAllText(Path.Combine(outsideRoot, "secret.txt"), "secret");
        var linkedRoot = Path.Combine(workspaceRoot, "linked");
        Directory.CreateSymbolicLink(linkedRoot, outsideRoot);
        var policy = TestWorkspaceServices.CreatePathPolicy(workspaceRoot, externalTargetRegistry: externalTargets);

        var succeeded = policy.TryResolveWorkspacePath(
            "linked/secret.txt",
            allowWorkspaceRoot: false,
            out _,
            out var validationMessage);

        Assert.False(succeeded);
        // The deny stays reparse-specific; the message wording follows the typed
        // WorkspacePathResolutionException reparse-point failure.
        Assert.Contains("reparse", validationMessage, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void WriteTextFile_writes_to_real_external_target_for_alias_path()
    {
        var workspaceRoot = CreateDirectory("workspace");
        var externalDirectory = Path.Combine(CreateDirectory("external-target-root"), "Workflow", "Workflow.App");
        var externalFilePath = Path.Combine(externalDirectory, "WorkflowService.cs");
        Directory.CreateDirectory(externalDirectory);
        var aliasPath = BuildExternalTargetAlias(externalFilePath);
        var policy = TestWorkspaceServices.CreatePathPolicy(workspaceRoot, externalTargetRegistry: externalTargets);
        var receiptWriter = new WorkspaceFileReceiptWriter(workspaceRoot);
        var service = new WorkspaceFileMutationService(policy, receiptWriter);

        var result = service.WriteTextFile(aliasPath, "public static class WorkflowService {}");

        Assert.True(result.Succeeded);
        Assert.Equal(aliasPath, result.Path);
        Assert.True(File.Exists(externalFilePath));
        Assert.Contains("WorkflowService", File.ReadAllText(externalFilePath), StringComparison.Ordinal);
    }

    [Fact]
    public void WriteTextFile_allows_razor_content_without_language_specific_workspace_policy()
    {
        var workspaceRoot = CreateDirectory("workspace");
        var externalFilePath = Path.Combine(CreateDirectory("external-target-root"), "Workflow", "Components", "Pages", "Home.razor");
        Directory.CreateDirectory(Path.GetDirectoryName(externalFilePath)!);
        var aliasPath = BuildExternalTargetAlias(externalFilePath);
        var policy = TestWorkspaceServices.CreatePathPolicy(workspaceRoot, externalTargetRegistry: externalTargets);
        var receiptWriter = new WorkspaceFileReceiptWriter(workspaceRoot);
        var service = new WorkspaceFileMutationService(policy, receiptWriter);
        var content = """
@page "/"
<button @onclick="() => AppendToResult('1')">1</button>
<button @onclick="() => SetOperation('+')">+</button>

@code {
    private void AppendToResult(string value) {
    }

    private void SetOperation(string op) {
    }
}
""";

        var result = service.WriteTextFile(aliasPath, content);

        Assert.True(result.Succeeded);
        Assert.True(File.Exists(externalFilePath));
    }

    [Fact]
    public void WriteTextFile_allows_razor_char_callbacks_to_char_handlers()
    {
        var workspaceRoot = CreateDirectory("workspace");
        var externalFilePath = Path.Combine(CreateDirectory("external-target-root"), "Workflow", "Components", "Pages", "Home.razor");
        Directory.CreateDirectory(Path.GetDirectoryName(externalFilePath)!);
        var aliasPath = BuildExternalTargetAlias(externalFilePath);
        var policy = TestWorkspaceServices.CreatePathPolicy(workspaceRoot, externalTargetRegistry: externalTargets);
        var receiptWriter = new WorkspaceFileReceiptWriter(workspaceRoot);
        var service = new WorkspaceFileMutationService(policy, receiptWriter);
        var content = """
@page "/"
<button @onclick="() => AppendDigit('1')">1</button>

@code {
    private void AppendDigit(char digit) {
    }
}
""";

        var result = service.WriteTextFile(aliasPath, content);

        Assert.True(result.Succeeded);
        Assert.True(File.Exists(externalFilePath));
    }

    [Fact]
    public void WriteTextFile_rejects_local_test_framework_shims()
    {
        var workspaceRoot = CreateDirectory("workspace");
        var externalFilePath = Path.Combine(CreateDirectory("external-target-root"), "InventoryApp", "tests", "InventoryApp.Tests", "TestingFallback.cs");
        var aliasPath = BuildExternalTargetAlias(externalFilePath);
        var policy = TestWorkspaceServices.CreatePathPolicy(workspaceRoot, externalTargetRegistry: externalTargets);
        var receiptWriter = new WorkspaceFileReceiptWriter(workspaceRoot);
        var service = new WorkspaceFileMutationService(policy, receiptWriter);
        var content = """
namespace Microsoft.VisualStudio.TestTools.UnitTesting;

public sealed class TestClassAttribute : Attribute;
public sealed class TestMethodAttribute : Attribute;
""";

        var result = service.WriteTextFile(aliasPath, content);

        Assert.False(result.Succeeded);
        Assert.Contains("Do not fake package, runtime, or test APIs", result.Message, StringComparison.Ordinal);
        Assert.False(File.Exists(externalFilePath));
    }

    [Fact]
    public void WriteTextFile_allows_content_without_framework_specific_workspace_policy()
    {
        var workspaceRoot = CreateDirectory("workspace");
        var externalFilePath = Path.Combine(CreateDirectory("external-target-root"), "Workflow", "Components", "Pages", "Home.razor");
        Directory.CreateDirectory(Path.GetDirectoryName(externalFilePath)!);
        var aliasPath = BuildExternalTargetAlias(externalFilePath);
        var policy = TestWorkspaceServices.CreatePathPolicy(workspaceRoot, externalTargetRegistry: externalTargets);
        var receiptWriter = new WorkspaceFileReceiptWriter(workspaceRoot);
        var service = new WorkspaceFileMutationService(policy, receiptWriter);
        var content = """
@page "/"
<button @onclick="() => AppendToResult("1")">1</button>

@code {
    private void AppendToResult(string value) {
    }
}
""";

        var result = service.WriteTextFile(aliasPath, content);

        Assert.True(result.Succeeded);
        Assert.True(File.Exists(externalFilePath));
    }

    [Fact]
    public void WriteTextFile_allows_host_file_content_without_framework_specific_workspace_policy()
    {
        var workspaceRoot = CreateDirectory("workspace");
        var projectDirectory = CreateCurrentBlazorWebAppProject("FerryKiosk");
        var hostFilePath = Path.Combine(projectDirectory, "Pages", "_Host.cshtml");
        var aliasPath = BuildExternalTargetAlias(hostFilePath);
        var policy = TestWorkspaceServices.CreatePathPolicy(workspaceRoot, externalTargetRegistry: externalTargets);
        var receiptWriter = new WorkspaceFileReceiptWriter(workspaceRoot);
        var service = new WorkspaceFileMutationService(policy, receiptWriter);
        var content = """
@page "/_Host"
@namespace FerryKiosk.Pages
<component type="typeof(App)" render-mode="ServerPrerendered" />
""";

        var result = service.WriteTextFile(aliasPath, content);

        Assert.True(result.Succeeded);
        Assert.True(File.Exists(hostFilePath));
    }

    [Fact]
    public void WriteTextFile_allows_program_content_without_framework_specific_workspace_policy()
    {
        var workspaceRoot = CreateDirectory("workspace");
        var projectDirectory = CreateCurrentBlazorWebAppProject("FerryKiosk");
        var programFilePath = Path.Combine(projectDirectory, "Program.cs");
        var aliasPath = BuildExternalTargetAlias(programFilePath);
        var policy = TestWorkspaceServices.CreatePathPolicy(workspaceRoot, externalTargetRegistry: externalTargets);
        var receiptWriter = new WorkspaceFileReceiptWriter(workspaceRoot);
        var service = new WorkspaceFileMutationService(policy, receiptWriter);
        var content = """
var builder = WebApplication.CreateBuilder(args);

builder.Services.AddRazorPages();
builder.Services.AddServerSideBlazor();

var app = builder.Build();
app.MapBlazorHub();
app.MapFallbackToPage("/_Host");
app.Run();
""";

        var result = service.WriteTextFile(aliasPath, content);

        Assert.True(result.Succeeded);
        Assert.True(File.Exists(programFilePath));
    }

    [Fact]
    public void WriteTextFile_allows_current_blazor_host_apis_in_current_blazor_web_app()
    {
        var workspaceRoot = CreateDirectory("workspace");
        var projectDirectory = CreateCurrentBlazorWebAppProject("FerryKiosk");
        var programFilePath = Path.Combine(projectDirectory, "Program.cs");
        var aliasPath = BuildExternalTargetAlias(programFilePath);
        var policy = TestWorkspaceServices.CreatePathPolicy(workspaceRoot, externalTargetRegistry: externalTargets);
        var receiptWriter = new WorkspaceFileReceiptWriter(workspaceRoot);
        var service = new WorkspaceFileMutationService(policy, receiptWriter);
        var content = """
using FerryKiosk.Components;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

var app = builder.Build();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();
app.Run();
""";

        var result = service.WriteTextFile(aliasPath, content);

        Assert.True(result.Succeeded);
        Assert.True(File.Exists(programFilePath));
    }

    [Fact]
    public void ReadTextFile_reads_from_real_external_target_for_alias_path()
    {
        var workspaceRoot = CreateDirectory("workspace");
        var externalDirectory = Path.Combine(CreateDirectory("external-target-root"), "Workflow", "Workflow.App");
        Directory.CreateDirectory(externalDirectory);
        var externalFilePath = Path.Combine(externalDirectory, "WorkflowService.cs");
        File.WriteAllText(externalFilePath, "public static class WorkflowService { public static int Add(int left, int right) => left + right; }");
        var aliasPath = BuildExternalTargetAlias(externalFilePath);
        var policy = TestWorkspaceServices.CreatePathPolicy(workspaceRoot, externalTargetRegistry: externalTargets);
        var receiptWriter = new WorkspaceFileReceiptWriter(workspaceRoot);
        var service = new WorkspaceFileQueryService(policy, receiptWriter, new WorkspaceTextContentGuard());

        var result = service.ReadTextFile(aliasPath);

        Assert.True(result.Succeeded);
        Assert.Equal(aliasPath, result.Path);
        Assert.Contains("Add", result.Content, StringComparison.Ordinal);
    }

    [Fact]
    public void BuildDotnetBuild_uses_absolute_target_argument_for_external_target_alias()
    {
        var workspaceRoot = CreateDirectory("workspace");
        var externalDirectory = Path.Combine(CreateDirectory("external-target-root"), "Workflow");
        Directory.CreateDirectory(externalDirectory);
        var externalSolutionPath = Path.Combine(externalDirectory, "Workflow.sln");
        File.WriteAllText(externalSolutionPath, string.Empty);
        var aliasPath = BuildExternalTargetAlias(externalSolutionPath);
        var builder = new WorkspaceCommandPlanBuilder(
            TestWorkspaceServices.CreatePathPolicy(workspaceRoot, externalTargetRegistry: externalTargets));

        var plan = builder.BuildDotnetBuild(aliasPath);

        Assert.Contains("build", plan.Arguments, StringComparer.OrdinalIgnoreCase);
        Assert.Contains(Path.GetFullPath(externalSolutionPath), plan.Arguments, StringComparer.OrdinalIgnoreCase);
        Assert.DoesNotContain(plan.Arguments, argument => argument.Contains("..", StringComparison.Ordinal));
        Assert.Contains(aliasPath, plan.TargetPaths, StringComparer.OrdinalIgnoreCase);
    }

    [Fact]
    public void BuildDotnetNew_includes_force_argument_when_requested()
    {
        var workspaceRoot = CreateDirectory("workspace");
        var builder = new WorkspaceCommandPlanBuilder(
            TestWorkspaceServices.CreatePathPolicy(workspaceRoot, externalTargetRegistry: externalTargets));

        var plan = builder.BuildDotnetNew("blazor", "WorkflowApp", force: true);

        Assert.Contains("--force", plan.Arguments, StringComparer.Ordinal);
    }

    [Fact]
    public void BuildDotnetNew_fails_closed_when_existing_target_tree_cannot_be_fully_inspected()
    {
        var workspaceRoot = CreateDirectory("workspace-inaccessible-target");
        Directory.CreateDirectory(Path.Combine(workspaceRoot, "apps", "WorkflowApp"));
        var builder = new WorkspaceCommandPlanBuilder(
            TestWorkspaceServices.CreatePathPolicy(workspaceRoot, externalTargetRegistry: externalTargets),
            _ => throw new UnauthorizedAccessException(@"C:\private\native-path"));

        var exception = Assert.Throws<WorkspaceToolAccessDeniedException>(
            () => builder.BuildDotnetNew("blazor", "WorkflowApp", "apps"));

        Assert.Equal(WorkspaceToolAccessDeniedException.FailureCode, exception.ErrorCode);
        Assert.Contains("apps/WorkflowApp", exception.SafeMessage, StringComparison.Ordinal);
        Assert.DoesNotContain(@"C:\private\native-path", exception.SafeMessage, StringComparison.OrdinalIgnoreCase);
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(rootPath))
            {
                Directory.Delete(rootPath, recursive: true);
            }
        }
        catch
        {
        }
    }

    private string CreateDirectory(string name)
    {
        var path = Path.Combine(rootPath, name);
        Directory.CreateDirectory(path);
        return path;
    }

    private string CreateCurrentBlazorWebAppProject(string projectName)
    {
        var projectDirectory = Path.Combine(CreateDirectory("external-target-root"), projectName);
        var componentsDirectory = Path.Combine(projectDirectory, "Components");
        Directory.CreateDirectory(componentsDirectory);
        File.WriteAllText(
            Path.Combine(projectDirectory, $"{projectName}.csproj"),
            """
<Project Sdk="Microsoft.NET.Sdk.Web">
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
  </PropertyGroup>
</Project>
""");
        File.WriteAllText(Path.Combine(componentsDirectory, "App.razor"), "<Routes />");
        File.WriteAllText(Path.Combine(componentsDirectory, "Routes.razor"), "<Router AppAssembly=\"typeof(Program).Assembly\" />");
        return projectDirectory;
    }

    private string BuildExternalTargetAlias(string fullPath)
    {
        return externalTargets.TryCreateAlias(fullPath, out var alias)
            ? alias
            : throw new InvalidOperationException($"Could not create an external-target alias for '{fullPath}'.");
    }
}
