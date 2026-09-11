using System.Text.Json;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.WorkflowExecutors.Standard.ProjectStructure;

namespace CanDoItAll.Tests.Unit.AgentFramework;

public sealed class WorkflowStructureReadContextTests {
    [Theory]
    [InlineData(WorkflowProjectStructureOperation.ListProjects)]
    [InlineData(WorkflowProjectStructureOperation.ReadTree)]
    [InlineData(WorkflowProjectStructureOperation.ReadNode)]
    public async Task Reads_pass_the_actual_occurrence_without_using_manual_discovery(WorkflowProjectStructureOperation operation) {
        var gateway = new ReadGateway();
        var executor = new ProjectStructureWorkflowExecutor(gateway);
        var context = Context(executor, operation);
        var result = await executor.ExecuteAsync(context, new("{\"project\":{\"id\":\"ffffffff-ffff-ffff-ffff-ffffffffffff\"}}"));
        Assert.Equal(new WorkflowStructureReadContext(context.ExecutionOccurrence!, context.Definition.VersionId, context.Node.Id), gateway.Context);
        Assert.Equal(context.Node.Id, result.NodeId);
        if (operation != WorkflowProjectStructureOperation.ListProjects) {
            Assert.Equal(TargetProject, gateway.ProjectId);
            Assert.True(gateway.Request!.IncludeMetadata);
            Assert.True(gateway.Request.IncludeNotes);
            Assert.True(gateway.Request.IncludeAssets);
            Assert.Equal<int?>(operation == WorkflowProjectStructureOperation.ReadTree ? 250 : null, gateway.Request.Take);
            if (operation == WorkflowProjectStructureOperation.ReadNode) {
                Assert.Equal(new[] { "selected-node" }, gateway.Request.NodeIds);
            } else {
                Assert.Null(gateway.Request.NodeIds);
            }
        }
        using var json = JsonDocument.Parse(result.PayloadJson);
        Assert.True(json.RootElement.TryGetProperty("result", out _));
        Assert.True(json.RootElement.TryGetProperty("inputPayload", out _));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Missing_or_mismatched_occurrence_never_calls_the_owner(bool mismatch) {
        var gateway = new ReadGateway();
        var executor = new ProjectStructureWorkflowExecutor(gateway);
        var context = Context(executor, WorkflowProjectStructureOperation.ListProjects);
        context = mismatch ? context with { RunId = WorkflowRunId.New() } : context with { ExecutionOccurrence = null };
        await Assert.ThrowsAsync<InvalidOperationException>(() => executor.ExecuteAsync(context, new("{}")).AsTask());
        Assert.Null(gateway.Context);
    }

    [Fact]
    public async Task Legacy_gateway_cannot_silently_serve_a_governed_read() {
        IProjectStructureRuntimeGateway gateway = new UnavailableProjectStructureRuntimeGateway();
        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => gateway.ListWorkflowProjectsAsync(
            new(WorkflowExecutionOccurrence.Start(WorkflowRunId.New()), WorkflowVersionId.New(), new("read"))));
        Assert.Contains("source-authorized", error.Message, StringComparison.Ordinal);
    }

    private static readonly Guid TargetProject = Guid.Parse("11111111-1111-1111-1111-111111111111");

    private static WorkflowExecutorExecutionContext Context(ProjectStructureWorkflowExecutor executor, WorkflowProjectStructureOperation operation) {
        var node = new WorkflowNode(new("read"), WorkflowNodeKind.Executor, "Read", [],
            new(null, null, null, null, "", WorkflowValueShape.Text, WorkflowValueShape.Text) {
                ExecutorId = WorkflowExecutorIds.ProjectStructure,
                ExecutorSettingsJson = WorkflowExecutorJson.Serialize(new WorkflowProjectStructureExecutorSettings {
                    Operation = operation, ProjectId = TargetProject, NodeId = "selected-node", IncludeInputPayload = true
                })
            });
        var now = DateTimeOffset.UtcNow;
        var definition = new WorkflowDefinition(WorkflowId.New(), WorkflowVersionId.New(), "Read", "Read", WorkflowLifecycleStatus.Active,
            new(node.Id, [node], []), new(WorkflowRuntimeBackendKind.InProcess, true, false, false, false), now, now);
        var runId = WorkflowRunId.New();
        return new(definition, node, executor.Descriptor, node.Settings.ExecutorSettingsJson, WorkflowExecutorExecutionPolicy.Default) {
            RunId = runId, ExecutionOccurrence = WorkflowExecutionOccurrence.Start(runId).Advance(definition.VersionId, node.Id)
        };
    }

    private sealed class ReadGateway : IProjectStructureRuntimeGateway {
        public WorkflowStructureReadContext? Context { get; private set; }
        public Guid? ProjectId { get; private set; }
        public ProjectStructureRuntimeReadRequest? Request { get; private set; }
        public Task<IReadOnlyList<ProjectStructureRuntimeProjectSummary>> ListWorkflowProjectsAsync(WorkflowStructureReadContext context,
            CancellationToken cancellationToken = default) {
            Context = context;
            return Task.FromResult<IReadOnlyList<ProjectStructureRuntimeProjectSummary>>([]);
        }
        public Task<ProjectStructureRuntimeReadResponse> ReadWorkflowStructureAsync(Guid projectId, ProjectStructureRuntimeReadRequest request,
            WorkflowStructureReadContext context, CancellationToken cancellationToken = default) {
            Context = context;
            ProjectId = projectId;
            Request = request;
            return Task.FromResult(new ProjectStructureRuntimeReadResponse(projectId, "Project", [], [], []));
        }
        public Task<IReadOnlyList<ProjectStructureRuntimeProjectSummary>> ListProjectsAsync(CancellationToken cancellationToken = default)
            => throw new InvalidOperationException("Manual discovery must not handle an admitted Workflow read.");
        public Task<ProjectStructureRuntimeReadResponse> ReadStructureAsync(Guid projectId, ProjectStructureRuntimeReadRequest request,
            CancellationToken cancellationToken = default) => throw new InvalidOperationException("Manual read must not handle an admitted Workflow read.");
        public Task<ProjectStructureRuntimeNodeSummary> CreateNodeAsync(Guid projectId, ProjectStructureRuntimeNodeCreateRequest request,
            ProjectStructureRuntimeAgentContext agent, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<ProjectStructureRuntimeNodeSummary> CreateAssetAsync(Guid projectId, ProjectStructureRuntimeAssetCreateRequest request,
            ProjectStructureRuntimeAgentContext agent, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }
}
