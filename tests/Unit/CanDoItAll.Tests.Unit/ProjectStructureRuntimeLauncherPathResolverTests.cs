using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Infrastructure.Storage;
using CanDoItAll.Modules.Workbench;
using CanDoItAll.SharedKernel;
using Microsoft.Extensions.Logging.Abstractions;

namespace CanDoItAll.Tests.Unit;

public sealed class ProjectStructureRuntimeLauncherPathResolverTests
{
    private static readonly string WorkspaceRoot = CreateWorkspaceRoot();

    [Fact]
    public void Resolve_reports_a_missing_external_project_path()
    {
        var sut = CreateSut();
        var node = CreateEnvironmentNode(
            ProjectEnvironmentKind.DotNetWatch,
            new ProjectEnvironmentMetadata
            {
                ProjectPath = Path.Combine(
                    Path.GetTempPath(),
                    $"CanDoItAll.RuntimeLauncher.Missing.{Guid.NewGuid():N}",
                    "CanDoItAll.Web.csproj")
            });

        var result = sut.Resolve(node);

        Assert.False(result.IsSuccess);
        Assert.Equal("Project path does not exist or is not accessible.", result.Message);
    }

    [Fact]
    public void Resolve_does_not_inspect_an_external_project_outside_the_current_run_authority()
    {
        var externalRoot = Path.Combine(
            Path.GetTempPath(),
            $"CanDoItAll.RuntimeLauncher.External.{Guid.NewGuid():N}");
        Directory.CreateDirectory(externalRoot);
        var projectPath = Path.Combine(externalRoot, "Calculator.csproj");
        File.WriteAllText(projectPath, "<Project Sdk=\"Microsoft.NET.Sdk\" />");

        try
        {
            using var auditScope = WorkspaceExecutionAuditContext.BeginScope(
                CreateExecutionRun(readOnlyExternalTargetAlias: null));

            var result = CreateSut().Resolve(CreateEnvironmentNode(
                ProjectEnvironmentKind.DotNetWatch,
                new ProjectEnvironmentMetadata
                {
                    ProjectPath = projectPath
                }));

            Assert.False(result.IsSuccess);
            Assert.Contains("outside the active workspace", result.Message, StringComparison.Ordinal);
            Assert.Contains("not authorized", result.Message, StringComparison.Ordinal);
        }
        finally
        {
            Directory.Delete(externalRoot, recursive: true);
        }
    }

    [Fact]
    public void Resolve_agent_mode_rejects_an_external_project_without_an_audited_execution()
    {
        var externalRoot = Path.Combine(
            Path.GetTempPath(),
            $"CanDoItAll.RuntimeLauncher.External.{Guid.NewGuid():N}");
        Directory.CreateDirectory(externalRoot);
        var projectPath = Path.Combine(externalRoot, "Calculator.csproj");
        File.WriteAllText(projectPath, "<Project Sdk=\"Microsoft.NET.Sdk\" />");

        try
        {
            var node = CreateEnvironmentNode(
                ProjectEnvironmentKind.DotNetWatch,
                new ProjectEnvironmentMetadata
                {
                    ProjectPath = projectPath
                });

            var result = CreateSut(new UnexpectedProjectTargetResolver()).Resolve(
                node.ObjectType,
                node.ObjectSubtype,
                node.Notes,
                node.MetadataJson,
                ProjectStructureRuntimePathAuthorityMode.AgentExecution);

            Assert.False(result.IsSuccess);
            Assert.Contains("not authorized for this agent execution", result.Message, StringComparison.Ordinal);
        }
        finally
        {
            Directory.Delete(externalRoot, recursive: true);
        }
    }

    [Fact]
    public void Resolve_can_inspect_an_external_project_inside_the_current_run_read_authority()
    {
        var externalRoot = Path.Combine(
            Path.GetTempPath(),
            $"CanDoItAll.RuntimeLauncher.External.{Guid.NewGuid():N}");
        Directory.CreateDirectory(externalRoot);
        var projectPath = Path.Combine(externalRoot, "Calculator.csproj");
        File.WriteAllText(projectPath, "<Project Sdk=\"Microsoft.NET.Sdk\" />");

        try
        {
            var externalTargetsFactory = new ExternalTargetPathRegistryFactory();
            var externalTargets = externalTargetsFactory.Create([]);
            Assert.True(externalTargets.TryCreateAlias(externalRoot, out var externalAlias));
            string projectAlias = $"{externalAlias}/Calculator.csproj";
            var bindings = externalTargets.ExportBindings([externalAlias]);
            using var auditScope = WorkspaceExecutionAuditContext.BeginScope(
                CreateExecutionRun(externalAlias, bindings));

            var node = CreateEnvironmentNode(
                ProjectEnvironmentKind.DotNetWatch,
                new ProjectEnvironmentMetadata
                {
                    ProjectPath = projectAlias
                });
            var result = CreateSut(externalTargetPathRegistryFactory: externalTargetsFactory).Resolve(
                node.ObjectType,
                node.ObjectSubtype,
                node.Notes,
                node.MetadataJson,
                ProjectStructureRuntimePathAuthorityMode.AgentExecution);

            Assert.True(result.IsSuccess, result.Message);
            Assert.Equal(projectPath, result.Plan!.Target!.Path);
        }
        finally
        {
            Directory.Delete(externalRoot, recursive: true);
        }
    }

    [Fact]
    public void Resolve_rejects_an_authorized_external_project_through_a_symbolic_link()
    {
        var externalRoot = Path.Combine(
            Path.GetTempPath(),
            $"CanDoItAll.RuntimeLauncher.External.{Guid.NewGuid():N}");
        var linkedTargetRoot = Path.Combine(
            Path.GetTempPath(),
            $"CanDoItAll.RuntimeLauncher.LinkedTarget.{Guid.NewGuid():N}");
        Directory.CreateDirectory(externalRoot);
        Directory.CreateDirectory(linkedTargetRoot);
        var projectPath = Path.Combine(linkedTargetRoot, "Calculator.csproj");
        File.WriteAllText(projectPath, "<Project Sdk=\"Microsoft.NET.Sdk\" />");
        var linkedRoot = Path.Combine(externalRoot, "linked");

        try
        {
            Directory.CreateSymbolicLink(linkedRoot, linkedTargetRoot);
            var externalTargetsFactory = new ExternalTargetPathRegistryFactory();
            var externalTargets = externalTargetsFactory.Create([]);
            Assert.True(externalTargets.TryCreateAlias(externalRoot, out var externalAlias));
            string projectAlias = $"{externalAlias}/linked/Calculator.csproj";
            var bindings = externalTargets.ExportBindings([externalAlias]);
            using var auditScope = WorkspaceExecutionAuditContext.BeginScope(
                CreateExecutionRun(externalAlias, bindings));

            var result = CreateSut(externalTargetPathRegistryFactory: externalTargetsFactory).Resolve(CreateEnvironmentNode(
                ProjectEnvironmentKind.DotNetWatch,
                new ProjectEnvironmentMetadata
                {
                    ProjectPath = projectAlias
                }));

            Assert.False(result.IsSuccess);
            Assert.True(
                result.Message.Contains("symbolic", StringComparison.OrdinalIgnoreCase) &&
                result.Message.Contains("reparse", StringComparison.OrdinalIgnoreCase),
                result.Message);
        }
        finally
        {
            if (Directory.Exists(linkedRoot))
            {
                Directory.Delete(linkedRoot);
            }

            Directory.Delete(externalRoot, recursive: true);
            Directory.Delete(linkedTargetRoot, recursive: true);
        }
    }

    private static ProjectStructureRuntimeLauncher CreateSut(
        IProjectStructureDotNetProjectTargetResolver? projectTargetResolver = null,
        IExternalTargetPathRegistryFactory? externalTargetPathRegistryFactory = null)
        => ProjectStructureRuntimeTestFactory.CreateLauncher(
            WorkspaceRoot,
            projectTargetResolver ?? new ExistingProjectTargetResolver(),
            externalTargetPathRegistryFactory ?? new ExternalTargetPathRegistryFactory());

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
            [],
            0,
            null,
            null,
            ProjectObjectMetadataSerializer.Serialize(new ProjectObjectMetadataEnvelope
            {
                Environment = metadata
            }));
    }

    private static string CreateWorkspaceRoot()
    {
        var path = Path.Combine(Path.GetTempPath(), "CanDoItAll.RuntimeLauncher.ManagedWorkspace");
        Directory.CreateDirectory(path);
        return path;
    }

    private sealed class ExistingProjectTargetResolver : IProjectStructureDotNetProjectTargetResolver
    {
        public ProjectStructureDotNetProjectTargetResolution Resolve(string path)
            => new(path, "Verified by the launcher path test boundary.");
    }

    private sealed class UnexpectedProjectTargetResolver : IProjectStructureDotNetProjectTargetResolver
    {
        public ProjectStructureDotNetProjectTargetResolution Resolve(string path)
            => throw new InvalidOperationException(
                "An unauthorized external runtime path must be rejected before project inspection.");
    }

    private static ExecutionRunRecord CreateExecutionRun(
        string? readOnlyExternalTargetAlias,
        IReadOnlyList<ExternalTargetRootBinding>? externalTargetRootBindings = null)
    {
        var now = DateTimeOffset.UtcNow;
        var metadataJson = readOnlyExternalTargetAlias is null
            ? "{}"
            : System.Text.Json.JsonSerializer.Serialize(
                new Dictionary<string, object>
                {
                    [ExecutionInvocationMetadata.ReadOnlyExternalTargetAliasesMetadataKey] =
                        new[] { readOnlyExternalTargetAlias! },
                    [ExecutionInvocationMetadata.ExternalTargetRootBindingsMetadataKey] =
                        externalTargetRootBindings ?? []
                });
        return new ExecutionRunRecord(
            Id: Guid.NewGuid(),
            AgentId: Guid.NewGuid(),
            ChatSessionId: null,
            Title: "Runtime launcher external authority test",
            SourceKind: "test",
            SourceId: "project-structure-runtime-launcher",
            CorrelationId: Guid.NewGuid().ToString("D"),
            CausationId: string.Empty,
            RequestedBy: "unit-test",
            RequestedByKind: "system",
            MetadataJson: metadataJson,
            InputSummary: string.Empty,
            ResultSummary: string.Empty,
            ProviderName: "test",
            Model: "test",
            State: ExecutionState.Running,
            Outcome: null,
            CreatedAtUtc: now,
            UpdatedAtUtc: now,
            StartedAtUtc: now,
            CompletedAtUtc: null,
            RuntimeSessionKey: string.Empty,
            SerializedSessionStateJson: null,
            PendingApprovals: []);
    }
}
