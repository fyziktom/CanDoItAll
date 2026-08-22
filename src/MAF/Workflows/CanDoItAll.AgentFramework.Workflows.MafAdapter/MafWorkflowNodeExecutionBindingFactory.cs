using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.Workflows.Abstractions;
using Microsoft.Agents.AI.Workflows;

namespace CanDoItAll.AgentFramework.Maf;

internal sealed class MafWorkflowNodeExecutionBindingFactory(
    IWorkflowExecutorInvoker? executorInvoker = null,
    IWorkflowLlmComponentInvoker? llmComponentInvoker = null,
    TimeProvider? timeProvider = null)
{
    private readonly TimeProvider clock = timeProvider ?? TimeProvider.System;

    public MafCompiledNodeBinding Create(
        WorkflowDefinition definition,
        WorkflowNode node,
        IReadOnlyDictionary<WorkflowComponentId, LlmCallComponent> componentsById,
        IReadOnlyDictionary<WorkflowNodeId, WorkflowPreviewSimulationStep> simulationSteps,
        WorkflowExecutorInvocationContext? invocationContext = null)
    {
        ArgumentNullException.ThrowIfNull(definition);
        ArgumentNullException.ThrowIfNull(node);
        ArgumentNullException.ThrowIfNull(componentsById);
        ArgumentNullException.ThrowIfNull(simulationSteps);
        var resolvedInvocationContext = invocationContext ?? WorkflowExecutorInvocationContext.Empty;

        async ValueTask<WorkflowNodeInput> ExecuteBindingAsync(
            WorkflowNodeInput input,
            IWorkflowContext context,
            CancellationToken cancellationToken)
            => await ExecuteAsync(
                definition,
                node,
                input,
                componentsById,
                simulationSteps,
                resolvedInvocationContext,
                cancellationToken);

        var binding = ((Func<WorkflowNodeInput, IWorkflowContext, CancellationToken, ValueTask<WorkflowNodeInput>>)ExecuteBindingAsync)
            .BindAsExecutor(node.Id.Value, threadsafe: true);
        return new MafCompiledNodeBinding(
            node.Id,
            binding,
            binding,
            [new MafWorkflowBindingComponent(MafWorkflowBindingRole.Execute, binding)],
            []);
    }

    public async ValueTask<WorkflowNodeInput> ExecuteAsync(
        WorkflowDefinition definition,
        WorkflowNode node,
        WorkflowNodeInput input,
        IReadOnlyDictionary<WorkflowComponentId, LlmCallComponent> componentsById,
        IReadOnlyDictionary<WorkflowNodeId, WorkflowPreviewSimulationStep> simulationSteps,
        WorkflowExecutorInvocationContext invocationContext,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(definition);
        ArgumentNullException.ThrowIfNull(node);
        ArgumentNullException.ThrowIfNull(input);
        ArgumentNullException.ThrowIfNull(componentsById);
        ArgumentNullException.ThrowIfNull(simulationSteps);
        ArgumentNullException.ThrowIfNull(invocationContext);

        var progressObserver = WorkflowNodeExecutionProgressScope.Current;
        var startedAtUtc = clock.GetUtcNow();
        var invocationId = Guid.NewGuid();
        WorkflowUsageMetrics? usage = null;
        IReadOnlyList<WorkflowUsageObservation> usageObservations = [];
        await RecordProgressAsync(
            progressObserver,
            definition,
            node,
            WorkflowNodeExecutionProgressState.Started,
            cancellationToken,
            occurredAtUtc: startedAtUtc);

        try
        {
            var output = await ExecuteCoreAsync(input, cancellationToken);
            await RecordProgressAsync(
                progressObserver,
                definition,
                node,
                WorkflowNodeExecutionProgressState.Completed,
                cancellationToken,
                payloadJson: output.PayloadJson,
                usage: usage,
                usageObservations: usageObservations,
                occurredAtUtc: clock.GetUtcNow());
            return output;
        }
        catch (Exception exception)
        {
            if (exception is WorkflowUsageObservationException usageException)
            {
                usageObservations = usageException.Observations;
                usage = WorkflowUsageCompatibilityProjection.Project(
                    usageObservations,
                    fallbackProviderName: "workflow-provider",
                    fallbackModel: "workflow-model");
            }

            await RecordProgressAsync(
                progressObserver,
                definition,
                node,
                WorkflowNodeExecutionProgressState.Failed,
                CancellationToken.None,
                errorMessage: MafWorkflowFailureDetails.CreateDetailedMessage(exception),
                usage: usage,
                usageObservations: usageObservations,
                occurredAtUtc: clock.GetUtcNow());
            throw;
        }

        async ValueTask<WorkflowNodeInput> ExecuteCoreAsync(
            WorkflowNodeInput nodeInput,
            CancellationToken nodeCancellationToken)
        {
            if (simulationSteps.TryGetValue(node.Id, out var simulationStep))
            {
                if (node.Settings.ExecutorId != simulationStep.SourceExecutorId)
                {
                    var actualExecutorId = node.Settings.ExecutorId?.Value ?? "<none>";
                    var requestedExecutorId = simulationStep.SourceExecutorId?.Value ?? "<none>";
                    throw new InvalidOperationException(
                        $"Preview simulation for workflow node '{node.Id}' targets executor '{requestedExecutorId}', but the node uses executor '{actualExecutorId}'.");
                }

                return new WorkflowNodeInput(WorkflowPreviewSimulationRenderer.Render(
                    simulationStep,
                    definition,
                    node,
                    nodeInput));
            }

            if (node.Kind == WorkflowNodeKind.LlmCall)
            {
                return await ExecuteLlmAsync(nodeInput, nodeCancellationToken);
            }

            if (node.Kind == WorkflowNodeKind.Executor || node.Settings.ExecutorId is not null)
            {
                return await ExecuteExecutorAsync(nodeInput, nodeCancellationToken);
            }

            return nodeInput;
        }

        async ValueTask<WorkflowNodeInput> ExecuteLlmAsync(
            WorkflowNodeInput nodeInput,
            CancellationToken nodeCancellationToken)
        {
            if (llmComponentInvoker is null)
            {
                throw new InvalidOperationException($"LLM workflow node '{node.Id}' requires a registered LLM component invoker.");
            }

            if (node.Settings.ComponentId is not { } componentId ||
                !componentsById.TryGetValue(componentId, out var component))
            {
                throw new InvalidOperationException($"LLM workflow node '{node.Id}' references component '{node.Settings.ComponentId}', but it was not supplied to the compiler.");
            }

            var result = await llmComponentInvoker.ExecuteAsync(definition, node, component, nodeInput, nodeCancellationToken);
            if (result.NodeId != node.Id)
            {
                throw new InvalidOperationException($"LLM workflow node '{node.Id}' returned result for node '{result.NodeId}'.");
            }

            usage = result.Usage;
            usageObservations = result.UsageObservations;
            if (usageObservations.Count == 0 && usage is not null)
            {
                usageObservations = WorkflowUsageObservationFactory.FromLegacyMetrics(
                    new WorkflowUsageObservationContext(
                        WorkflowExecutorExecutionAuditScope.CurrentRunId,
                        definition.Id,
                        definition.VersionId,
                        node.Id,
                        ExecutorId: null,
                        component.Id,
                        WorkflowUsageProducerKind.LlmComponent,
                        invocationId,
                        Attempt: 1,
                        startedAtUtc,
                        clock.GetUtcNow()),
                    usage,
                    clock.GetUtcNow());
            }

            usage ??= WorkflowUsageCompatibilityProjection.Project(
                usageObservations,
                fallbackProviderName: "workflow-provider",
                fallbackModel: string.IsNullOrWhiteSpace(node.Settings.Model)
                    ? component.Model
                    : node.Settings.Model.Trim());
            return new WorkflowNodeInput(result.PayloadJson);
        }

        async ValueTask<WorkflowNodeInput> ExecuteExecutorAsync(
            WorkflowNodeInput nodeInput,
            CancellationToken nodeCancellationToken)
        {
            if (executorInvoker is null)
            {
                throw new InvalidOperationException($"Workflow executor node '{node.Id}' requires a registered executor invoker.");
            }

            var result = await executorInvoker.ExecuteAsync(
                definition,
                node,
                nodeInput,
                invocationContext,
                nodeCancellationToken);
            usage = result.Usage;
            usageObservations = result.UsageObservations;
            if (usageObservations.Count == 0 && usage is not null)
            {
                var recordedAtUtc = clock.GetUtcNow();
                usageObservations = WorkflowUsageObservationFactory.FromLegacyMetrics(
                    new WorkflowUsageObservationContext(
                        WorkflowExecutorExecutionAuditScope.CurrentRunId,
                        definition.Id,
                        definition.VersionId,
                        node.Id,
                        node.Settings.ExecutorId,
                        ComponentId: null,
                        WorkflowUsageProducerKind.Executor,
                        invocationId,
                        Attempt: 1,
                        startedAtUtc,
                        recordedAtUtc),
                    usage,
                    recordedAtUtc);
            }

            usage ??= WorkflowUsageCompatibilityProjection.Project(
                usageObservations,
                fallbackProviderName: "workflow-provider",
                fallbackModel: "workflow-executor");
            return new WorkflowNodeInput(result.PayloadJson);
        }
    }

    private static ValueTask RecordProgressAsync(
        IWorkflowNodeExecutionProgressObserver? observer,
        WorkflowDefinition definition,
        WorkflowNode node,
        WorkflowNodeExecutionProgressState state,
        CancellationToken cancellationToken,
        string payloadJson = "",
        string errorMessage = "",
        WorkflowUsageMetrics? usage = null,
        IReadOnlyList<WorkflowUsageObservation>? usageObservations = null,
        DateTimeOffset? occurredAtUtc = null)
    {
        return observer is null
            ? ValueTask.CompletedTask
            : observer.RecordAsync(
                new WorkflowNodeExecutionProgress(
                    definition.Id,
                    definition.VersionId,
                    WorkflowExecutorExecutionAuditScope.CurrentRunId,
                    node.Id,
                    state,
                    occurredAtUtc ?? DateTimeOffset.UtcNow)
                {
                    ExecutorId = node.Settings.ExecutorId,
                    PayloadJson = payloadJson,
                    ErrorMessage = errorMessage,
                    Usage = usage,
                    UsageObservations = usageObservations ?? []
                },
                cancellationToken);
    }
}
