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
            new WorkflowNodeInput("{\"immutable\":true}"),
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
        Assert.Equal(requestId, captured.CausationRequestId);
        Assert.Equal(requestVersion, captured.CausationRequestVersion);
        Assert.Equal(operationId, captured.CausationOperationId);
        Assert.Equal(requestVersion.Value, captured.InvocationGeneration.Value);
        Assert.NotNull(captured.ApprovalAuthorization);
        Assert.Equal(runId, captured.ApprovalAuthorization.RunId);
        Assert.Equal(authorization, captured.ExternalResponseAuthorization);
        Assert.Equal(authorization, captured.ApprovalAuthorization.ExternalResponseAuthorization);
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

    private sealed class DescriptorExecutor : IWorkflowExecutor
    {
        public WorkflowExecutorDescriptor Descriptor { get; } =
            BuiltInWorkflowExecutorDescriptors.JsonTransform with
            {
                Id = new WorkflowExecutorId("test.identity-propagation"),
                Name = "Identity propagation",
                PermissionPolicy = new WorkflowExecutorPermissionPolicy(
                    WorkflowExecutorCapabilityFlags.WritesExternalData,
                    WorkflowExecutorApprovalRequirement.AlwaysRequired)
            };

        public ValueTask<WorkflowNodeExecutionResult> ExecuteAsync(
            WorkflowExecutorExecutionContext context,
            WorkflowNodeInput input,
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException();
    }

    private sealed class CapturingInvoker : IWorkflowExecutorInvoker
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
            Contexts.Add(invocationContext);
            return ValueTask.FromResult(new WorkflowNodeExecutionResult(
                node.Id,
                input.PayloadJson,
                node.Settings.ResultShape ?? WorkflowValueShape.Text));
        }
    }
}
