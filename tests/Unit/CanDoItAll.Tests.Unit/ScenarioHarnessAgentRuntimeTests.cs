using System.Reflection;
using System.Xml.Linq;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Modules.AgentFramework.Hosting;

namespace CanDoItAll.Tests.Unit.AgentFramework;

public sealed class ScenarioHarnessAgentRuntimeTests
{
    [Fact]
    public async Task Sc03_writes_local_msbuild_boundary_before_controlled_build()
    {
        var workspaceRoot = Path.Combine(
            Path.GetTempPath(),
            "CanDoItAll.Tests",
            nameof(ScenarioHarnessAgentRuntimeTests),
            Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(workspaceRoot);

        try
        {
            var commandService = DispatchProxy.Create<IWorkspaceCommandExecutionService, RecordingCommandExecutionProxy>();
            var commandRecorder = (RecordingCommandExecutionProxy)(object)commandService;
            commandRecorder.WorkspaceRoot = workspaceRoot;
            // SB18: the interception core no longer holds an inner fallback runtime — it never
            // falls through for a scenario prompt, so there is nothing to prove "must not be
            // called" against here directly (that boundary now belongs to the decorator; see
            // RuntimePortDecoratorFallthroughTests).
            var runtime = new ScenarioHarnessAgentRuntime(
                workspaceRoot,
                WorkspaceScopeDescriptor.Sandbox,
                TestWorkspaceServices.CreateFileService(workspaceRoot, WorkspaceScopeDescriptor.Sandbox),
                commandService);

            var response = await runtime.ExecuteScenarioRunAsync(
                prompt: "SC03 - generate and build the controlled Blazor sample.",
                progressCallback: static (_, _, _) => Task.CompletedTask,
                suppressApprovalRequirements: false,
                structuredOutput: null,
                executionOptions: null);

            Assert.Equal(1, commandRecorder.DotnetBuildCallCount);
            Assert.NotNull(commandRecorder.BuildBoundaryPath);
            Assert.True(commandRecorder.BuildBoundaryExistedAtBuild);
            Assert.NotNull(commandRecorder.BuildBoundaryContentAtBuild);

            var boundaryDocument = XDocument.Parse(commandRecorder.BuildBoundaryContentAtBuild);
            var copyPolicy = Assert.Single(boundaryDocument.Descendants("CopyRepositoryTemplatesToOutput"));
            Assert.Equal("false", copyPolicy.Value);
            Assert.Equal(
                WorkspaceScopeDescriptor.NormalizeRelativePath(
                    Path.Combine(commandRecorder.BuildWorkingDirectory!, "Directory.Build.targets")),
                WorkspaceScopeDescriptor.NormalizeRelativePath(
                    Path.GetRelativePath(workspaceRoot, commandRecorder.BuildBoundaryPath!)));

            var reportPath = Assert.Single(
                Directory.EnumerateFiles(workspaceRoot, "generation-report.md", SearchOption.AllDirectories));
            var report = await File.ReadAllTextAsync(reportPath);
            Assert.Contains("Build boundary:", report, StringComparison.Ordinal);
            Assert.Contains("Directory.Build.targets", report, StringComparison.Ordinal);
            Assert.Equal(7, response.ToolCalls);
        }
        finally
        {
            Directory.Delete(workspaceRoot, recursive: true);
        }
    }

    public class RecordingCommandExecutionProxy : DispatchProxy
    {
        public required string WorkspaceRoot { get; set; }

        public int DotnetBuildCallCount { get; private set; }

        public string? BuildWorkingDirectory { get; private set; }

        public string? BuildBoundaryPath { get; private set; }

        public bool BuildBoundaryExistedAtBuild { get; private set; }

        public string? BuildBoundaryContentAtBuild { get; private set; }

        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
        {
            ArgumentNullException.ThrowIfNull(targetMethod);
            ArgumentNullException.ThrowIfNull(args);

            return targetMethod.Name switch
            {
                nameof(IWorkspaceCommandExecutionService.DotnetNew) => ExecuteDotnetNew(args),
                nameof(IWorkspaceCommandExecutionService.DotnetBuild) => ExecuteDotnetBuild(args),
                _ => throw new NotSupportedException(
                    $"Unexpected workspace command call '{targetMethod.Name}'.")
            };
        }

        private Task<WorkspaceCommandExecutionResult> ExecuteDotnetNew(object?[] args)
        {
            var name = Assert.IsType<string>(args[1]);
            var parentDirectory = Assert.IsType<string>(args[2]);
            var projectRoot = Path.Combine(WorkspaceRoot, parentDirectory, name);

            Directory.CreateDirectory(Path.Combine(projectRoot, "Components", "Pages"));
            File.WriteAllText(
                Path.Combine(projectRoot, $"{name}.csproj"),
                """<Project Sdk="Microsoft.NET.Sdk.Web" />""");

            return Task.FromResult(CreateSucceededResult("workspace_dotnet_new", "dotnet_new"));
        }

        private Task<WorkspaceCommandExecutionResult> ExecuteDotnetBuild(object?[] args)
        {
            DotnetBuildCallCount++;
            BuildWorkingDirectory = Assert.IsType<string>(args[3]);
            BuildBoundaryPath = Path.Combine(WorkspaceRoot, BuildWorkingDirectory, "Directory.Build.targets");
            BuildBoundaryExistedAtBuild = File.Exists(BuildBoundaryPath);
            BuildBoundaryContentAtBuild = BuildBoundaryExistedAtBuild
                ? File.ReadAllText(BuildBoundaryPath)
                : null;

            return Task.FromResult(CreateSucceededResult("workspace_dotnet_build", "dotnet_build"));
        }

        private static WorkspaceCommandExecutionResult CreateSucceededResult(
            string toolName,
            string recipeId)
        {
            var now = DateTimeOffset.UtcNow;
            return new WorkspaceCommandExecutionResult(
                Succeeded: true,
                Message: "Succeeded.",
                Receipt: new WorkspaceToolReceipt(
                    Operation: toolName,
                    MutatesWorkspace: true,
                    Boundary: "test",
                    Outcome: "Succeeded",
                    Message: "Succeeded.",
                    ReceiptRelativePath: string.Empty,
                    TargetPaths: [],
                    ArtifactReferences: [],
                    StartedAtUtc: now,
                    CompletedAtUtc: now),
                ToolName: toolName,
                RecipeId: recipeId,
                RiskClass: "WorkspaceMutation",
                ApprovalRequired: false,
                Boundary: ExecutionBoundaryDescriptor.Unknown,
                WorkingDirectory: ".",
                ArgumentsSummary: string.Empty,
                ExitCode: 0,
                StdoutPreview: "Build succeeded.",
                StderrPreview: string.Empty,
                StdoutTruncated: false,
                StderrTruncated: false);
        }
    }
}
