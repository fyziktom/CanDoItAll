using CanDoItAll.AgentFramework.Core;

namespace CanDoItAll.Tests.Unit;

public sealed class WorkspaceExternalTargetAliasTests : IDisposable
{
    private readonly string rootPath = Path.Combine(Path.GetTempPath(), "CanDoItAll.WorkspaceExternalTargetAliasTests", Guid.NewGuid().ToString("N"));

    [Fact]
    public void TryResolveWorkspacePath_maps_external_target_alias_to_real_external_path()
    {
        var workspaceRoot = CreateDirectory("workspace");
        var externalFilePath = Path.Combine(CreateDirectory("external-target-root"), "Workflow", "Workflow.sln");
        var aliasPath = BuildExternalTargetAlias(externalFilePath);
        var policy = new WorkspacePathPolicy(workspaceRoot);

        var succeeded = policy.TryResolveWorkspacePath(aliasPath, allowWorkspaceRoot: false, out var resolution, out var validationMessage);

        Assert.True(succeeded);
        Assert.True(string.IsNullOrWhiteSpace(validationMessage));
        Assert.Equal(Path.GetFullPath(externalFilePath), resolution.FullPath);
        Assert.Equal(aliasPath, resolution.RelativePath);
        Assert.Equal(aliasPath, policy.ToRelativePath(resolution.FullPath));
        Assert.Equal(aliasPath, policy.ToDisplayPath(resolution.FullPath));
    }

    [Fact]
    public void WriteTextFile_writes_to_real_external_target_for_alias_path()
    {
        var workspaceRoot = CreateDirectory("workspace");
        var externalDirectory = Path.Combine(CreateDirectory("external-target-root"), "Workflow", "Workflow.App");
        var externalFilePath = Path.Combine(externalDirectory, "WorkflowService.cs");
        var aliasPath = BuildExternalTargetAlias(externalFilePath);
        var policy = new WorkspacePathPolicy(workspaceRoot);
        var receiptWriter = new WorkspaceFileReceiptWriter(workspaceRoot);
        var service = new WorkspaceFileMutationService(policy, receiptWriter);

        var result = service.WriteTextFile(aliasPath, "public static class WorkflowService {}");

        Assert.True(result.Succeeded);
        Assert.Equal(aliasPath, result.Path);
        Assert.True(File.Exists(externalFilePath));
        Assert.Contains("WorkflowService", File.ReadAllText(externalFilePath), StringComparison.Ordinal);
    }

    [Fact]
    public void WriteTextFile_rejects_razor_char_callbacks_to_string_handlers()
    {
        var workspaceRoot = CreateDirectory("workspace");
        var externalFilePath = Path.Combine(CreateDirectory("external-target-root"), "Workflow", "Components", "Pages", "Home.razor");
        var aliasPath = BuildExternalTargetAlias(externalFilePath);
        var policy = new WorkspacePathPolicy(workspaceRoot);
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

        Assert.False(result.Succeeded);
        Assert.Contains("CS1503", result.Message, StringComparison.Ordinal);
        Assert.Contains("AppendToResult", result.Message, StringComparison.Ordinal);
        Assert.Contains("SetOperation", result.Message, StringComparison.Ordinal);
        Assert.False(File.Exists(externalFilePath));
    }

    [Fact]
    public void WriteTextFile_allows_razor_char_callbacks_to_char_handlers()
    {
        var workspaceRoot = CreateDirectory("workspace");
        var externalFilePath = Path.Combine(CreateDirectory("external-target-root"), "Workflow", "Components", "Pages", "Home.razor");
        var aliasPath = BuildExternalTargetAlias(externalFilePath);
        var policy = new WorkspacePathPolicy(workspaceRoot);
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
        var policy = new WorkspacePathPolicy(workspaceRoot);
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
    public void WriteTextFile_rejects_razor_double_quoted_string_callback_inside_double_quoted_attribute()
    {
        var workspaceRoot = CreateDirectory("workspace");
        var externalFilePath = Path.Combine(CreateDirectory("external-target-root"), "Workflow", "Components", "Pages", "Home.razor");
        var aliasPath = BuildExternalTargetAlias(externalFilePath);
        var policy = new WorkspacePathPolicy(workspaceRoot);
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

        Assert.False(result.Succeeded);
        Assert.Contains("unescaped double-quoted string literal", result.Message, StringComparison.Ordinal);
        Assert.False(File.Exists(externalFilePath));
    }

    [Fact]
    public void WriteTextFile_rejects_legacy_blazor_host_file_in_current_blazor_web_app()
    {
        var workspaceRoot = CreateDirectory("workspace");
        var projectDirectory = CreateCurrentBlazorWebAppProject("FerryKiosk");
        var hostFilePath = Path.Combine(projectDirectory, "Pages", "_Host.cshtml");
        var aliasPath = BuildExternalTargetAlias(hostFilePath);
        var policy = new WorkspacePathPolicy(workspaceRoot);
        var receiptWriter = new WorkspaceFileReceiptWriter(workspaceRoot);
        var service = new WorkspaceFileMutationService(policy, receiptWriter);
        var content = """
@page "/_Host"
@namespace FerryKiosk.Pages
<component type="typeof(App)" render-mode="ServerPrerendered" />
""";

        var result = service.WriteTextFile(aliasPath, content);

        Assert.False(result.Succeeded);
        Assert.Contains("current Blazor Web App surface", result.Message, StringComparison.Ordinal);
        Assert.Contains("_Host.cshtml", result.Message, StringComparison.Ordinal);
        Assert.False(File.Exists(hostFilePath));
    }

    [Fact]
    public void WriteTextFile_rejects_legacy_blazor_host_apis_in_current_blazor_web_app()
    {
        var workspaceRoot = CreateDirectory("workspace");
        var projectDirectory = CreateCurrentBlazorWebAppProject("FerryKiosk");
        var programFilePath = Path.Combine(projectDirectory, "Program.cs");
        var aliasPath = BuildExternalTargetAlias(programFilePath);
        var policy = new WorkspacePathPolicy(workspaceRoot);
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

        Assert.False(result.Succeeded);
        Assert.Contains("legacy Blazor Server hosting API", result.Message, StringComparison.Ordinal);
        Assert.Contains("AddServerSideBlazor", result.Message, StringComparison.Ordinal);
        Assert.False(File.Exists(programFilePath));
    }

    [Fact]
    public void WriteTextFile_allows_current_blazor_host_apis_in_current_blazor_web_app()
    {
        var workspaceRoot = CreateDirectory("workspace");
        var projectDirectory = CreateCurrentBlazorWebAppProject("FerryKiosk");
        var programFilePath = Path.Combine(projectDirectory, "Program.cs");
        var aliasPath = BuildExternalTargetAlias(programFilePath);
        var policy = new WorkspacePathPolicy(workspaceRoot);
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
        var policy = new WorkspacePathPolicy(workspaceRoot);
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
        var builder = new WorkspaceCommandPlanBuilder(new WorkspacePathPolicy(workspaceRoot));

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
        var builder = new WorkspaceCommandPlanBuilder(new WorkspacePathPolicy(workspaceRoot));

        var plan = builder.BuildDotnetNew("blazor", "WorkflowApp", force: true);

        Assert.Contains("--force", plan.Arguments, StringComparer.Ordinal);
    }

    [Fact]
    public void BuildConvertDocumentWithMarkItDown_does_not_require_approval_for_bounded_document_conversion()
    {
        var workspaceRoot = CreateDirectory("workspace");
        var sourcePath = Path.Combine(workspaceRoot, "quote.pdf");
        File.WriteAllText(sourcePath, "%PDF");
        var builder = new WorkspaceCommandPlanBuilder(new WorkspacePathPolicy(workspaceRoot));

        var plan = builder.BuildConvertDocumentWithMarkItDown(
            "quote.pdf",
            "artifacts/converted/quote.md");

        Assert.False(plan.Decision.ApprovalRequired);
        Assert.False(plan.Decision.NetworkAllowed);
        Assert.True(plan.MutatesWorkspace);
        Assert.Contains("quote.pdf", plan.TargetPaths, StringComparer.OrdinalIgnoreCase);
        Assert.Contains("artifacts/converted/quote.md", plan.TargetPaths, StringComparer.OrdinalIgnoreCase);
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

    private static string BuildExternalTargetAlias(string fullPath)
    {
        var normalizedFullPath = Path.GetFullPath(fullPath);
        var root = Path.GetPathRoot(normalizedFullPath)
            ?? throw new InvalidOperationException($"Could not resolve a drive root for '{fullPath}'.");
        var trimmedRoot = root.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        if (trimmedRoot.Length != 2 || trimmedRoot[1] != ':')
        {
            throw new InvalidOperationException($"External-target alias tests require a drive-letter path. Received '{fullPath}'.");
        }

        var driveLetter = char.ToUpperInvariant(trimmedRoot[0]);
        var relativeWithinDrive = normalizedFullPath.Length <= root.Length
            ? string.Empty
            : normalizedFullPath[root.Length..]
                .Replace(Path.AltDirectorySeparatorChar, Path.DirectorySeparatorChar)
                .TrimStart(Path.DirectorySeparatorChar);

        return string.IsNullOrWhiteSpace(relativeWithinDrive)
            ? $"external-target/{driveLetter}"
            : WorkspacePathPolicy.NormalizeRelativePath(Path.Combine("external-target", driveLetter.ToString(), relativeWithinDrive));
    }
}
