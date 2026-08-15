using CanDoItAll.AgentFramework.Core;
using CanDoItAll.Modules.Workbench;
using Microsoft.Extensions.DependencyInjection;

namespace CanDoItAll.Tests.Unit;

public sealed class ProjectStructureAgentRuntimeToolProviderArchitectureTests
{
    private const int DocumentedProviderLineCheckpoint = 3_698;
    private const int PostExtractionProviderLineCeiling = 3_620;

    [Fact]
    public void Provider_uses_injected_runtime_collaborators_and_delegates_node_copy_rules()
    {
        var source = ReadWorkbenchSource(
            "AgentTools",
            "ProjectStructureAgentRuntimeToolProvider.cs");

        Assert.DoesNotContain("new WorkspaceCommandExecutionService", source, StringComparison.Ordinal);
        Assert.DoesNotContain("new LocalWorkspaceProcessHost", source, StringComparison.Ordinal);
        Assert.DoesNotContain("new ProjectStructureAgentProjectCreationCoordinator", source, StringComparison.Ordinal);
        Assert.DoesNotContain(".CopyNodesAsync(", source, StringComparison.Ordinal);
        Assert.Contains("nodeCopyCoordinator.CopyAsync", source, StringComparison.Ordinal);
    }

    [Fact]
    public void Provider_stays_below_the_post_extraction_line_ceiling()
    {
        var source = ReadWorkbenchSource(
            "AgentTools",
            "ProjectStructureAgentRuntimeToolProvider.cs");
        var lineCount = source.ReplaceLineEndings("\n").Split('\n').Length;

        Assert.True(PostExtractionProviderLineCeiling < DocumentedProviderLineCheckpoint);
        Assert.InRange(lineCount, 1, PostExtractionProviderLineCeiling);
    }

    [Fact]
    public void Workbench_module_registers_the_runtime_provider_collaborators_as_scoped()
    {
        var services = new ServiceCollection();

        services.AddWorkbenchModule();

        AssertScoped<ProjectStructureAgentProjectCreationCoordinator>(services);
        AssertScoped<ProjectStructureAgentNodeCopyCoordinator>(services);
        var constructorParameterTypes = Assert.Single(
                typeof(ProjectStructureAgentRuntimeToolProvider).GetConstructors())
            .GetParameters()
            .Select(parameter => parameter.ParameterType)
            .ToArray();
        Assert.Contains(typeof(IWorkspaceCommandExecutionService), constructorParameterTypes);
        Assert.Contains(typeof(ProjectStructureAgentProjectCreationCoordinator), constructorParameterTypes);
        Assert.Contains(typeof(ProjectStructureAgentNodeCopyCoordinator), constructorParameterTypes);
    }

    [Theory]
    [InlineData(false, ProjectStructureClipboardCopyTaskPolicy.AllowCanonicalTasks)]
    [InlineData(true, ProjectStructureClipboardCopyTaskPolicy.NonTaskStructureOnly)]
    public async Task Node_copy_coordinator_maps_the_access_guard_and_forwards_the_exact_request(
        bool requiresNonTaskWriteGuard,
        ProjectStructureClipboardCopyTaskPolicy expectedTaskPolicy)
    {
        var projectId = Guid.NewGuid();
        var request = new ProjectStructureNodesCopyInput(["source"], "destination", "lease-token");
        var agentContext = new ProjectStructureAgentContext(
            "agent-id",
            "Agent",
            "machine",
            "repository",
            "branch",
            "session");
        var expected = new ProjectStructureNodesCopyResult(
            projectId,
            request.DestinationParentNodeId,
            request.SourceNodeIds,
            ["copy"],
            [new ProjectStructureNodeCopyMapping("source", "copy")],
            []);
        using var cancellation = new CancellationTokenSource();
        ProjectStructureClipboardCopyTaskPolicy? observedTaskPolicy = null;
        ProjectStructureAgentNodeCopyOperation operation = (
            receivedProjectId,
            receivedRequest,
            receivedAgentContext,
            receivedTaskPolicy,
            receivedCancellationToken) =>
        {
            Assert.Equal(projectId, receivedProjectId);
            Assert.Same(request, receivedRequest);
            Assert.Same(agentContext, receivedAgentContext);
            Assert.Equal(cancellation.Token, receivedCancellationToken);
            observedTaskPolicy = receivedTaskPolicy;
            return Task.FromResult(expected);
        };
        var coordinator = new ProjectStructureAgentNodeCopyCoordinator(operation);

        var result = await coordinator.CopyAsync(
            projectId,
            request,
            agentContext,
            requiresNonTaskWriteGuard,
            cancellation.Token);

        Assert.Same(expected, result);
        Assert.Equal(expectedTaskPolicy, observedTaskPolicy);
    }

    private static void AssertScoped<TService>(IServiceCollection services)
    {
        Assert.Contains(
            services,
            descriptor => descriptor.ServiceType == typeof(TService) &&
                descriptor.ImplementationType == typeof(TService) &&
                descriptor.Lifetime == ServiceLifetime.Scoped);
    }

    private static string ReadWorkbenchSource(params string[] pathSegments)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "CanDoItAll.slnx")))
        {
            directory = directory.Parent;
        }

        if (directory is null)
        {
            throw new InvalidOperationException("Repository root was not found.");
        }

        return File.ReadAllText(Path.Combine(
            directory.FullName,
            "src",
            "Modules",
            "CanDoItAll.Modules.Workbench",
            Path.Combine(pathSegments)));
    }
}
