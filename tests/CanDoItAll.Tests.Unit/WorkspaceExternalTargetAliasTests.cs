using CanDoItAll.AgentFramework.Core;

namespace CanDoItAll.Tests.Unit;

public sealed class WorkspaceExternalTargetAliasTests : IDisposable
{
    private readonly string rootPath = Path.Combine(Path.GetTempPath(), "CanDoItAll.WorkspaceExternalTargetAliasTests", Guid.NewGuid().ToString("N"));

    [Fact]
    public void TryResolveWorkspacePath_maps_external_target_alias_to_real_external_path()
    {
        var workspaceRoot = CreateDirectory("workspace");
        var externalFilePath = Path.Combine(CreateDirectory("external-target-root"), "Calculator", "Calculator.sln");
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
        var externalDirectory = Path.Combine(CreateDirectory("external-target-root"), "Calculator", "Calculator.App");
        var externalFilePath = Path.Combine(externalDirectory, "CalculatorService.cs");
        var aliasPath = BuildExternalTargetAlias(externalFilePath);
        var policy = new WorkspacePathPolicy(workspaceRoot);
        var receiptWriter = new WorkspaceFileReceiptWriter(workspaceRoot);
        var service = new WorkspaceFileMutationService(policy, receiptWriter);

        var result = service.WriteTextFile(aliasPath, "public static class CalculatorService {}");

        Assert.True(result.Succeeded);
        Assert.Equal(aliasPath, result.Path);
        Assert.True(File.Exists(externalFilePath));
        Assert.Contains("CalculatorService", File.ReadAllText(externalFilePath), StringComparison.Ordinal);
    }

    [Fact]
    public void ReadTextFile_reads_from_real_external_target_for_alias_path()
    {
        var workspaceRoot = CreateDirectory("workspace");
        var externalDirectory = Path.Combine(CreateDirectory("external-target-root"), "Calculator", "Calculator.App");
        Directory.CreateDirectory(externalDirectory);
        var externalFilePath = Path.Combine(externalDirectory, "CalculatorService.cs");
        File.WriteAllText(externalFilePath, "public static class CalculatorService { public static int Add(int left, int right) => left + right; }");
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
        var externalDirectory = Path.Combine(CreateDirectory("external-target-root"), "Calculator");
        Directory.CreateDirectory(externalDirectory);
        var externalSolutionPath = Path.Combine(externalDirectory, "Calculator.sln");
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

        var plan = builder.BuildDotnetNew("blazor", "CalculatorApp", force: true);

        Assert.Contains("--force", plan.Arguments, StringComparer.Ordinal);
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
