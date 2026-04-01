using CanDoItAll.Infrastructure.Storage;
using CanDoItAll.Modules.Workbench;
using CanDoItAll.SharedKernel;
using Microsoft.Extensions.Logging.Abstractions;

namespace CanDoItAll.Tests.Unit;

public sealed class ProjectStructureRuntimeLauncherPathResolverTests
{
    [Fact]
    public void Resolve_fails_when_the_project_path_escapes_the_active_workspace_root()
    {
        var sut = CreateSut();
        var node = CreateEnvironmentNode(
            ProjectEnvironmentKind.DotNetWatch,
            new ProjectEnvironmentMetadata
            {
                ProjectPath = @"C:\outside\CanDoItAll.Web.csproj"
            });

        var result = sut.Resolve(node);

        Assert.False(result.IsSuccess);
        Assert.Equal("Project path must stay inside the active workspace root.", result.Message);
    }

    private static ProjectStructureRuntimeLauncher CreateSut()
        => new(
            new WorkspacePathAccessGuard(new TestWorkspacePathResolver(@"C:\workspace")),
            NullLogger<ProjectStructureRuntimeLauncher>.Instance);

    private static ProjectStructureNode CreateEnvironmentNode(ProjectEnvironmentKind kind, ProjectEnvironmentMetadata metadata)
    {
        metadata.EnvironmentKind = kind;
        return new ProjectStructureNode(
            "node-1",
            "project:1",
            ProjectObjectType.Environment,
            "dotnet-watch",
            "Runtime node",
            "Context",
            "Planned",
            string.Empty,
            string.Empty,
            string.Empty,
            null,
            string.Empty,
            string.Empty,
            string.Empty,
            0,
            0,
            new ProjectObjectVisualProfile("rect", "accent", "terminal", string.Empty),
            [],
            string.Empty,
            0,
            string.Empty,
            string.Empty,
            string.Empty,
            0,
            null,
            null,
            ProjectObjectMetadataSerializer.Serialize(new ProjectObjectMetadataEnvelope
            {
                Environment = metadata
            }));
    }

    private sealed class TestWorkspacePathResolver(string workspaceRoot) : IWorkspacePathResolver
    {
        public string ResolveWorkspaceRoot() => workspaceRoot;

        public string ResolveManagedFilesRoot() => Path.Combine(workspaceRoot, "managed-files");

        public string ResolveExportsRoot() => Path.Combine(workspaceRoot, "exports");

        public string ResolveEvidenceRoot() => Path.Combine(workspaceRoot, "evidence");

        public string ResolveManagerArtifactsRoot() => Path.Combine(workspaceRoot, ".artifacts");
    }
}
