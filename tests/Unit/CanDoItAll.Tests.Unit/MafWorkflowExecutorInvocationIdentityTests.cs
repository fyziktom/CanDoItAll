using System.Text.Json;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Maf;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.Workflows.Abstractions;
using Microsoft.Agents.AI.Workflows;

namespace CanDoItAll.Tests.Unit.AgentFramework;

public sealed class MafWorkflowExecutorInvocationIdentityTests
{
    [Fact]
    public async Task ExactVersionRecompileCarriesResponseOperationIdentityIntoApprovalContinuation()
    {
        var executor = new DescriptorExecutor();
        var catalog = new WorkflowExecutorCatalog([executor]);
        var capturingInvoker = new CapturingInvoker();
        var compiler = new MafWorkflowCompiler(
            new WorkflowDefinitionValidator(catalog),
            capturingInvoker,
            executorCatalog: catalog);
        var definition = CreateDefinition(executor.Descriptor);
        var original = compiler.Compile(definition, []);
        var requestId = WorkflowExternalRequestId.New();
        var requestVersion = new WorkflowExternalRequestVersion(4);
        var operationId = WorkflowExternalResponseOperationId.New();
        var runId = WorkflowRunId.New();
        var now = TimeProvider.System.GetUtcNow();
        var authorization = new WorkflowExternalResponseAuthorization(
            operationId,
            requestId,
            requestVersion,
            runId,
            definition.Id,
            definition.VersionId,
            WorkflowExternalRequestKind.Approval,
            WorkflowExternalResponseAction.Approve,
            new WorkflowLaunchActor(WorkflowLaunchActorKind.User, "identity-approver"),
            WorkspaceScopeDescriptor.Organization("identity-profile"),
            new WorkflowLaunchActor(WorkflowLaunchActorKind.Agent, "identity-origin-agent"),
            WorkflowExternalResponseAuthorizationPolicy.CurrentFingerprint,
            now,
            now.AddSeconds(WorkflowExternalResponseAuthorizationPolicy.ResponseLifetimeSeconds));
        var invocationContext = new WorkflowExecutorInvocationContext
        {
            ExternalResponseAuthorization = authorization,
            CausationRequestId = requestId,
            CausationRequestVersion = requestVersion,
            CausationOperationId = operationId,
            InvocationGeneration = new WorkflowExecutorInvocationGeneration(requestVersion.Value)
        };
        var recompiled = compiler.Compile(
            definition,
            [],
            WorkflowPreviewSimulationPlan.Empty,
            invocationContext);

        Assert.True(original.Compilation.Succeeded, original.Compilation.ErrorMessage);
        Assert.True(recompiled.Compilation.Succeeded, recompiled.Compilation.ErrorMessage);
        Assert.Equal(original.TopologyFingerprint, recompiled.TopologyFingerprint);
        var workflow = Assert.IsType<Workflow>(recompiled.Workflow);
        using var auditScope = WorkflowExecutorExecutionAuditScope.Push(runId);
        using var cancellationSource = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        await using var run = await InProcessExecution.RunStreamingAsync(
            workflow,
            new WorkflowNodeInput("{\"immutable\":true}") { ExecutionOccurrence = WorkflowExecutionOccurrence.Start(runId) },
            cancellationToken: cancellationSource.Token);
        ExternalRequest? externalRequest = null;
        await foreach (var workflowEvent in run.WatchStreamAsync(
            blockOnPendingRequest: false,
            cancellationSource.Token))
        {
            if (workflowEvent is RequestInfoEvent requestInfoEvent)
            {
                externalRequest = requestInfoEvent.Request;
            }
        }

        Assert.NotNull(externalRequest);
        Assert.True(externalRequest.TryGetDataAs<MafWorkflowApprovalRequest>(out var approvalRequest));
        Assert.NotNull(approvalRequest);
        await run.SendResponseAsync(externalRequest.CreateResponse(
            MafWorkflowApprovalContinuation.Create(
                approvalRequest,
                authorization,
                approved: true,
                "approved")));
        await foreach (var _ in run.WatchStreamAsync(
            blockOnPendingRequest: false,
            cancellationSource.Token))
        {
        }

        var captured = Assert.Single(capturingInvoker.Contexts);
        Assert.Equal(WorkflowExecutionOccurrence.Start(runId).Advance(definition.VersionId, new("start"))
            .Advance(definition.VersionId, new("effect")), captured.ExecutionOccurrence);
        Assert.Equal(requestId, captured.CausationRequestId);
        Assert.Equal(requestVersion, captured.CausationRequestVersion);
        Assert.Equal(operationId, captured.CausationOperationId);
        Assert.Equal(requestVersion.Value, captured.InvocationGeneration.Value);
        Assert.NotNull(captured.ApprovalAuthorization);
        Assert.Equal(runId, captured.ApprovalAuthorization.RunId);
        Assert.Equal(authorization, captured.ExternalResponseAuthorization);
        Assert.Equal(authorization, captured.ApprovalAuthorization.ExternalResponseAuthorization);
    }

    [Fact]
    public async Task FanOutJoinRetainsEachPredecessorOccurrenceAcrossRecompile() {
        var executor = new DescriptorExecutor(approvalRequired: false);
        var start = CreateNode("start", WorkflowNodeKind.Start);
        var left = CreateNode("left", WorkflowNodeKind.End);
        var right = CreateNode("right", WorkflowNodeKind.End);
        var join = CreateNode("join", WorkflowNodeKind.Executor) with {
            Settings = CreateNode("join", WorkflowNodeKind.Executor).Settings with { ExecutorId = executor.Descriptor.Id, ExecutorSettingsJson = "{}" }
        };
        var end = CreateNode("end", WorkflowNodeKind.End);
        var definition = CreateDefinition(executor.Descriptor) with { Graph = new(start.Id, [start, left, right, join, end], [
            CreateEdge("start-left", start.Id, left.Id) with { Kind = WorkflowEdgeKind.FanOut },
            CreateEdge("start-right", start.Id, right.Id) with { Kind = WorkflowEdgeKind.FanOut },
            CreateEdge("left-join", left.Id, join.Id) with { Kind = WorkflowEdgeKind.FanIn },
            CreateEdge("right-join", right.Id, join.Id) with { Kind = WorkflowEdgeKind.FanIn },
            CreateEdge("join-end", join.Id, end.Id)
        ]) };
        var runId = WorkflowRunId.New();
        var input = new WorkflowNodeInput("{}") { ExecutionOccurrence = WorkflowExecutionOccurrence.Start(runId) };
        var first = await ExecuteOccurrencesAsync(definition, executor, input, generation: 1);
        var persisted = JsonSerializer.Deserialize<WorkflowNodeInput>(JsonSerializer.Serialize(input))!;
        var retry = await ExecuteOccurrencesAsync(definition, executor, persisted, generation: 7);

        var common = WorkflowExecutionOccurrence.Start(runId).Advance(definition.VersionId, start.Id);
        var expected = new[] { common.Advance(definition.VersionId, left.Id).Advance(definition.VersionId, join.Id).Path,
            common.Advance(definition.VersionId, right.Id).Advance(definition.VersionId, join.Id).Path }.Order().ToArray();
        Assert.Equal(expected, first.Select(context => context.ExecutionOccurrence!.Path).Order());
        Assert.Equal(expected, retry.Select(context => context.ExecutionOccurrence!.Path).Order());
        Assert.All(retry, context => Assert.Equal(7, context.InvocationGeneration.Value));
    }

    [Fact]
    public async Task LoopVisitsHaveDistinctOccurrencesButRetryAndGenerationDoNotChangeTheirIdentities() {
        var executor = new DescriptorExecutor(approvalRequired: false);
        var definition = CreateDefinition(executor.Descriptor);
        var start = definition.Graph.Nodes.Single(node => node.Id.Value == "start");
        var effect = definition.Graph.Nodes.Single(node => node.Id.Value == "effect");
        var end = definition.Graph.Nodes.Single(node => node.Id.Value == "end");
        definition = definition with { Graph = new(start.Id, [start, effect, end], [
            CreateEdge("start-effect", start.Id, effect.Id),
            CreateEdge("loop", effect.Id, effect.Id) with { Kind = WorkflowEdgeKind.Conditional,
                Routing = WorkflowEdgeRouting.Predicate("$.iteration", WorkflowRouteOperator.LessThan, "2", WorkflowRouteValueKind.Number) },
            CreateEdge("finish", effect.Id, end.Id) with { Kind = WorkflowEdgeKind.Conditional,
                Routing = WorkflowEdgeRouting.Predicate("$.iteration", WorkflowRouteOperator.GreaterThanOrEqual, "2", WorkflowRouteValueKind.Number) }
        ]) };
        var runId = WorkflowRunId.New();
        var input = new WorkflowNodeInput("{\"iteration\":0}") { ExecutionOccurrence = WorkflowExecutionOccurrence.Start(runId) };
        static string Advance(WorkflowNode _, WorkflowNodeInput value) {
            using var document = JsonDocument.Parse(value.PayloadJson);
            return JsonSerializer.Serialize(new { iteration = document.RootElement.GetProperty("iteration").GetInt32() + 1 });
        }
        var first = await ExecuteOccurrencesAsync(definition, executor, input, 1, Advance);
        var retry = await ExecuteOccurrencesAsync(definition, executor,
            JsonSerializer.Deserialize<WorkflowNodeInput>(JsonSerializer.Serialize(input))!, 9, Advance);
        var expectedFirst = WorkflowExecutionOccurrence.Start(runId).Advance(definition.VersionId, start.Id).Advance(definition.VersionId, effect.Id);
        Assert.Equal(new[] { expectedFirst, expectedFirst.Advance(definition.VersionId, effect.Id) }, first.Select(context => context.ExecutionOccurrence));
        Assert.Equal(first.Select(context => context.ExecutionOccurrence), retry.Select(context => context.ExecutionOccurrence));
        Assert.Equal(2, first.Select(context => context.ExecutionOccurrence).Distinct().Count());
    }

    private static async Task<IReadOnlyList<WorkflowExecutorInvocationContext>> ExecuteOccurrencesAsync(
        WorkflowDefinition definition, DescriptorExecutor executor, WorkflowNodeInput input, int generation,
        Func<WorkflowNode, WorkflowNodeInput, string>? transform = null) {
        var catalog = new WorkflowExecutorCatalog([executor]);
        var invoker = new CapturingInvoker(transform);
        var compiler = new MafWorkflowCompiler(new WorkflowDefinitionValidator(catalog), invoker, executorCatalog: catalog);
        var compiled = compiler.Compile(definition, [], WorkflowPreviewSimulationPlan.Empty,
            new() { InvocationGeneration = new(generation) });
        Assert.True(compiled.Compilation.Succeeded, compiled.Compilation.ErrorMessage);
        using var scope = WorkflowExecutorExecutionAuditScope.Push(input.ExecutionOccurrence!.RunId);
        using var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        await using var run = await InProcessExecution.RunStreamingAsync(Assert.IsType<Workflow>(compiled.Workflow), input,
            cancellationToken: cancellation.Token);
        await foreach (var _ in run.WatchStreamAsync(blockOnPendingRequest: false, cancellation.Token)) {
        }

        return invoker.Contexts;
    }

    private static WorkflowDefinition CreateDefinition(WorkflowExecutorDescriptor descriptor)
    {
        var start = CreateNode("start", WorkflowNodeKind.Start);
        var effect = CreateNode("effect", WorkflowNodeKind.Executor) with
        {
            Settings = CreateNode("effect", WorkflowNodeKind.Executor).Settings with
            {
                ExecutorId = descriptor.Id,
                ExecutorSettingsJson = "{}",
                ExecutionPolicy = WorkflowExecutorExecutionPolicy.Default
            }
        };
        var end = CreateNode("end", WorkflowNodeKind.End);
        var now = DateTimeOffset.UtcNow;
        return new WorkflowDefinition(
            WorkflowId.New(),
            WorkflowVersionId.New(),
            "Invocation identity",
            "Invocation identity propagation test.",
            WorkflowLifecycleStatus.Draft,
            new WorkflowGraph(
                start.Id,
                [start, effect, end],
                [
                    CreateEdge("start-effect", start.Id, effect.Id),
                    CreateEdge("effect-end", effect.Id, end.Id)
                ]),
            new WorkflowRuntimePolicy(
                WorkflowRuntimeBackendKind.InProcess,
                AllowInProcessPreviewRuns: true,
                RequireDurableProductionRuns: false,
                ExposeAzureFunctionsStatusEndpoint: false,
                ExposeAzureFunctionsMcpTool: false),
            now,
            now);
    }

    private static WorkflowNode CreateNode(string id, WorkflowNodeKind kind)
        => new(
            new WorkflowNodeId(id),
            kind,
            id,
            [],
            new WorkflowNodeSettings(
                ComponentId: null,
                AgentId: null,
                SubworkflowId: null,
                ExternalRequestKind: null,
                Instructions: string.Empty,
                InputShape: WorkflowValueShape.Text,
                ResultShape: WorkflowValueShape.Text));

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
            ConditionExpression: string.Empty);

    private sealed class DescriptorExecutor(bool approvalRequired = true) : IWorkflowExecutor
    {
        public WorkflowExecutorDescriptor Descriptor { get; } =
            BuiltInWorkflowExecutorDescriptors.JsonTransform with
            {
                Id = new WorkflowExecutorId("test.identity-propagation"),
                Name = "Identity propagation",
                PermissionPolicy = new WorkflowExecutorPermissionPolicy(
                    WorkflowExecutorCapabilityFlags.WritesExternalData,
                    approvalRequired ? WorkflowExecutorApprovalRequirement.AlwaysRequired : WorkflowExecutorApprovalRequirement.NotRequired)
            };

        public ValueTask<WorkflowNodeExecutionResult> ExecuteAsync(
            WorkflowExecutorExecutionContext context,
            WorkflowNodeInput input,
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException();
    }

    private sealed class CapturingInvoker(Func<WorkflowNode, WorkflowNodeInput, string>? transform = null) : IWorkflowExecutorInvoker
    {
        public List<WorkflowExecutorInvocationContext> Contexts { get; } = [];

        public ValueTask<WorkflowNodeExecutionResult> ExecuteAsync(
            WorkflowDefinition definition,
            WorkflowNode node,
            WorkflowNodeInput input,
            CancellationToken cancellationToken = default)
            => ExecuteAsync(
                definition,
                node,
                input,
                WorkflowExecutorInvocationContext.Empty,
                cancellationToken);

        public ValueTask<WorkflowNodeExecutionResult> ExecuteAsync(
            WorkflowDefinition definition,
            WorkflowNode node,
            WorkflowNodeInput input,
            WorkflowExecutorInvocationContext invocationContext,
            CancellationToken cancellationToken = default)
        {
            lock (Contexts) {
                Contexts.Add(invocationContext);
            }
            return ValueTask.FromResult(new WorkflowNodeExecutionResult(
                node.Id,
                transform?.Invoke(node, input) ?? input.PayloadJson,
                node.Settings.ResultShape ?? WorkflowValueShape.Text));
        }
    }
}
