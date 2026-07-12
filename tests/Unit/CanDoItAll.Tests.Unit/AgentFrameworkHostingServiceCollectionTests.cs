using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Hosting;
using CanDoItAll.AgentFramework.Maf;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.Workflows.Abstractions;
using Microsoft.Extensions.DependencyInjection;

namespace CanDoItAll.Tests.Unit;

public sealed class AgentFrameworkHostingServiceCollectionTests
{
    [Fact]
    public async Task AddAgentFrameworkCore_builds_with_scope_validation()
    {
        var workspaceRoot = Path.Combine(
            Path.GetTempPath(),
            $"candoitall-agent-framework-hosting-{Guid.NewGuid():N}");
        Directory.CreateDirectory(workspaceRoot);

        try
        {
            var services = new ServiceCollection();
            services.AddAgentFrameworkCore(workspaceRoot);

            await using var provider = services.BuildServiceProvider(new ServiceProviderOptions
            {
                ValidateOnBuild = true,
                ValidateScopes = true
            });
            await using var scope = provider.CreateAsyncScope();

            Assert.IsType<MafWorkflowCompiler>(scope.ServiceProvider.GetRequiredService<IWorkflowMafCompiler>());
            Assert.IsType<CompositeWorkflowExecutorExecutionObserver>(
                scope.ServiceProvider.GetRequiredService<IWorkflowExecutorExecutionObserver>());
            var backendCatalog = scope.ServiceProvider.GetRequiredService<IWorkflowRuntimeBackendCatalog>();
            var inProcessBackend = backendCatalog.GetRequiredBackend(WorkflowRuntimeBackendKind.InProcess);
            Assert.True(inProcessBackend.IsRegistered);
            Assert.True(inProcessBackend.IsRunnable);
        }
        finally
        {
            if (Directory.Exists(workspaceRoot))
            {
                Directory.Delete(workspaceRoot, recursive: true);
            }
        }
    }

    [Fact]
    public async Task AddAgentFrameworkCore_catalog_service_rejects_unknown_executor()
    {
        var workspaceRoot = Path.Combine(
            Path.GetTempPath(),
            $"candoitall-agent-framework-hosting-{Guid.NewGuid():N}");
        Directory.CreateDirectory(workspaceRoot);

        try
        {
            var services = new ServiceCollection();
            services.AddAgentFrameworkCore(workspaceRoot);

            await using var provider = services.BuildServiceProvider(new ServiceProviderOptions
            {
                ValidateOnBuild = true,
                ValidateScopes = true
            });
            await using var scope = provider.CreateAsyncScope();
            var catalog = scope.ServiceProvider.GetRequiredService<IWorkflowCatalogService>();

            var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => catalog.SaveDefinitionAsync(
                new WorkflowDefinitionSaveRequest(
                    Id: null,
                    ExpectedVersionId: null,
                    "Unknown executor workflow",
                    "Should fail before runtime dispatch.",
                    WorkflowLifecycleStatus.Active,
                    CreateExecutorWorkflowGraph(new WorkflowExecutorId("missing.executor")),
                    WorkflowSettings.Default.DefaultRuntimePolicy)));

            Assert.Contains("missing.executor", exception.Message, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            if (Directory.Exists(workspaceRoot))
            {
                Directory.Delete(workspaceRoot, recursive: true);
            }
        }
    }

    private static WorkflowGraph CreateExecutorWorkflowGraph(WorkflowExecutorId executorId)
    {
        var start = new WorkflowNodeId("start");
        var tool = new WorkflowNodeId("tool");
        var end = new WorkflowNodeId("end");
        return new WorkflowGraph(
            start,
            [
                CreateNode(start, WorkflowNodeKind.Start, resultShape: WorkflowValueShape.Text),
                new(
                    tool,
                    WorkflowNodeKind.Executor,
                    "Tool",
                    [],
                    new WorkflowNodeSettings(
                        ComponentId: null,
                        AgentId: null,
                        SubworkflowId: null,
                        ExternalRequestKind: null,
                        Instructions: string.Empty,
                        InputShape: WorkflowValueShape.Text,
                        ResultShape: new WorkflowValueShape(WorkflowValueShapeKind.Json, "{}", "JSON")) with
                    {
                        ExecutorId = executorId,
                        ExecutorSettingsJson = "{}",
                        ExecutionPolicy = WorkflowExecutorExecutionPolicy.Default
                    }),
                CreateNode(end, WorkflowNodeKind.End, inputShape: new WorkflowValueShape(WorkflowValueShapeKind.Json, "{}", "JSON"))
            ],
            [
                CreateEdge("start-tool", start, tool),
                CreateEdge("tool-end", tool, end)
            ]);
    }

    private static WorkflowNode CreateNode(
        WorkflowNodeId id,
        WorkflowNodeKind kind,
        WorkflowValueShape? inputShape = null,
        WorkflowValueShape? resultShape = null)
        => new(
            id,
            kind,
            id.Value,
            [],
            new WorkflowNodeSettings(
                ComponentId: null,
                AgentId: null,
                SubworkflowId: null,
                ExternalRequestKind: null,
                Instructions: string.Empty,
                InputShape: inputShape,
                ResultShape: resultShape));

    private static WorkflowEdge CreateEdge(
        string id,
        WorkflowNodeId source,
        WorkflowNodeId target)
        => new(
            new WorkflowEdgeId(id),
            source,
            SourcePortId: null,
            target,
            TargetPortId: null,
            WorkflowEdgeKind.Direct,
            ConditionExpression: string.Empty)
        {
            Routing = WorkflowEdgeRouting.Always
        };
}
