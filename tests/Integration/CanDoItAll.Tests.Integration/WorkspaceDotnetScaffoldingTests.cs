using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Infrastructure;
using CanDoItAll.Infrastructure.Storage;

namespace CanDoItAll.Tests.Integration.Runtime;

[Trait("Category", "HostPlatform")]
public sealed class WorkspaceDotnetScaffoldingTests {
    [Theory]
    [InlineData("console")]
    [InlineData("blazorwasm")]
    public async Task A_solution_accepts_a_child_project_and_scaffolding_cannot_overwrite_it(string template) {
        var workspaceRoot = Path.Combine(Path.GetTempPath(), $"cdia-new-{Guid.NewGuid():N}");
        Directory.CreateDirectory(workspaceRoot);

        try {
            var commands = new WorkspaceCommandExecutionService(
                workspaceRoot,
                new LocalWorkspaceProcessHost(),
                new PhysicalFileSystemPathPolicyFactory(),
                WorkspaceScopeDescriptor.Sandbox);
            var solution = await commands.DotnetNew("sln", "SampleSuite", "product");
            Assert.True(solution.Succeeded, Describe(solution));

            var productRoot = Path.Combine(workspaceRoot, "product");
            var solutionPath = Assert.Single(Directory.EnumerateFiles(productRoot, "SampleSuite.sln*"));
            var originalSolution = await File.ReadAllBytesAsync(solutionPath);
            var project = await commands.DotnetNew(template, "SampleApp", "product", targetFramework: "net10.0");
            Assert.True(project.Succeeded, Describe(project));

            var childRoot = Path.Combine(productRoot, "SampleApp");
            var projectPath = Path.Combine(childRoot, "SampleApp.csproj");
            Assert.True(File.Exists(projectPath), Describe(project));
            Assert.Empty(Directory.EnumerateFiles(productRoot, "*.csproj"));
            Assert.False(File.Exists(Path.Combine(productRoot, "Program.cs")));
            Assert.Equal(originalSolution, await File.ReadAllBytesAsync(solutionPath));
            Assert.Equal(["product/SampleApp"], project.Receipt.TargetPaths);

            var existingProject = await File.ReadAllBytesAsync(projectPath);
            using var capture = AgentToolInvocationEffectScope.Begin();
            var repeated = await commands.DotnetNew(template, "SampleApp", "product", force: true);
            Assert.False(repeated.Succeeded);
            Assert.True(capture.ProhibitedBeforeEffect);
            Assert.Equal(existingProject, await File.ReadAllBytesAsync(projectPath));
            Assert.Equal(originalSolution, await File.ReadAllBytesAsync(solutionPath));
        } finally {
            Directory.Delete(workspaceRoot, recursive: true);
        }
    }

    private static string Describe(WorkspaceCommandExecutionResult result)
        => $"{result.Message}{Environment.NewLine}{result.StdoutPreview}{Environment.NewLine}{result.StderrPreview}";
}
