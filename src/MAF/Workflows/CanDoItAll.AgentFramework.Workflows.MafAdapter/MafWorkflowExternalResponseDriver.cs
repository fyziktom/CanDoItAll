using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.Workflows.Abstractions;
using Microsoft.Agents.AI.Workflows;

namespace CanDoItAll.AgentFramework.Maf;

internal sealed class MafWorkflowExternalResponseDriver(
    IWorkflowMafCompiler compiler,
    IWorkflowCatalogService catalog,
    IWorkflowBackendCheckpointPayloadStore checkpointPayloadStore,
    MafWorkflowStreamingRunDriver streamingDriver,
    MafWorkflowRehydrationVerifier verifier,
    MafWorkflowExternalRequestMapper requestMapper,
    MafWorkflowTurnResultMapper turnResultMapper)
{
    private readonly IWorkflowMafCompiler workflowCompiler = compiler ?? throw new ArgumentNullException(nameof(compiler));
    private readonly IWorkflowCatalogService workflowCatalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
    private readonly IWorkflowBackendCheckpointPayloadStore payloadStore = checkpointPayloadStore ?? throw new ArgumentNullException(nameof(checkpointPayloadStore));
    private readonly MafWorkflowStreamingRunDriver runDriver = streamingDriver ?? throw new ArgumentNullException(nameof(streamingDriver));
    private readonly MafWorkflowRehydrationVerifier rehydrationVerifier = verifier ?? throw new ArgumentNullException(nameof(verifier));
    private readonly MafWorkflowExternalRequestMapper externalRequestMapper = requestMapper ?? throw new ArgumentNullException(nameof(requestMapper));
    private readonly MafWorkflowTurnResultMapper resultMapper = turnResultMapper ?? throw new ArgumentNullException(nameof(turnResultMapper));

    public async Task<WorkflowBackendStartResult> ResumeAsync(
        WorkflowBackendResumeRequest request,
        IReadOnlyList<LlmCallComponent> resolvedComponents,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(resolvedComponents);

        var detail = await workflowCatalog.GetDefinitionAsync(
            request.Run.WorkflowId,
            request.Run.VersionId,
            cancellationToken) ?? throw Failure(
                WorkflowBackendResumeFailureKind.ExactWorkflowVersionMissing,
                "The exact checkpointed workflow version is unavailable for response recovery.");
        if (detail.Definition.Id != request.Run.WorkflowId ||
            detail.Definition.VersionId != request.Run.VersionId)
        {
            throw Failure(
                WorkflowBackendResumeFailureKind.ExactWorkflowVersionMismatch,
                "Workflow catalog returned a different version than the exact checkpointed workflow version.");
        }

        var components = FilterReferencedComponents(detail.Definition, resolvedComponents);
        var invocationContext = CreateInvocationContext(request);
        MafWorkflowBuildResult build;
        try
        {
            build = workflowCompiler.Compile(
                detail.Definition,
                components,
                WorkflowPreviewSimulationPlan.Empty,
                invocationContext);
        }
        catch (WorkflowBackendResumeException)
        {
            throw;
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            throw Failure(
                WorkflowBackendResumeFailureKind.CompilationFailed,
                "The exact checkpointed workflow could not be compiled for response recovery.",
                exception);
        }
        var checkpoint = await rehydrationVerifier.VerifyAsync(
            request,
            detail.Definition,
            build,
            payloadStore,
            cancellationToken);
        var checkpointManager = MafWorkflowCheckpointProtocol.CreateManager(
            payloadStore,
            checkpoint.Session);
        var checkpointInfo = new CheckpointInfo(
            checkpoint.Index.Link.SessionId.Value,
            checkpoint.Index.Link.CheckpointId.Value);
        var progressObserver = resultMapper.CreateProgressObserver(
            request.Run.RunId,
            detail.Definition,
            WorkflowPreviewSimulationPlan.Empty,
            request.Run.Origin);
        MafWorkflowStreamTurn turn;
        using (WorkflowExecutorExecutionAuditScope.Push(request.Run.RunId, request.Run.Origin))
        using (WorkflowNodeExecutionProgressScope.Push(progressObserver))
        {
            turn = await runDriver.ResumeAndRespondAsync(
                build.Workflow!,
                checkpointInfo,
                checkpointManager,
                request.ExternalRequest.Continuation!.Request,
                restored => externalRequestMapper.CreateResponse(
                    restored,
                    request.ExternalRequest,
                    request.Response,
                    request.Authorization!),
                cancellationToken);
        }

        return await resultMapper.MapTurnAsync(
            detail.Definition,
            request.Run.RunId,
            request.Run.Origin,
            request.Run.CreatedAtUtc,
            turn,
            progressObserver,
            startRequest: null,
            cancellationToken);
    }

    private static WorkflowExecutorInvocationContext CreateInvocationContext(
        WorkflowBackendResumeRequest request)
    {
        if (request.CausationOperationId is not { } operationId ||
            request.Authorization is not { } authorization)
        {
            throw Failure(
                WorkflowBackendResumeFailureKind.RequestMismatch,
                "Workflow backend response recovery requires reconstructed external-response authorization.");
        }

        if (request.InvocationGeneration != request.ExternalRequest.Version.Value ||
            authorization.OperationId != operationId ||
            authorization.RequestId != request.ExternalRequest.Id ||
            authorization.RequestVersion != request.ExternalRequest.Version ||
            authorization.RunId != request.Run.RunId ||
            authorization.RunId != request.ExternalRequest.RunId ||
            authorization.WorkflowId != request.Run.WorkflowId ||
            authorization.WorkflowVersionId != request.Run.VersionId ||
            authorization.RequestKind != request.ExternalRequest.Kind)
        {
            throw Failure(
                WorkflowBackendResumeFailureKind.RequestMismatch,
                "Workflow backend authorization must match the exact response operation, request, run, and workflow version.");
        }

        return new WorkflowExecutorInvocationContext
        {
            ExternalResponseAuthorization = authorization,
            CausationRequestId = request.ExternalRequest.Id,
            CausationRequestVersion = request.ExternalRequest.Version,
            CausationOperationId = operationId,
            InvocationGeneration = new WorkflowExecutorInvocationGeneration(request.InvocationGeneration)
        };
    }

    private static IReadOnlyList<LlmCallComponent> FilterReferencedComponents(
        WorkflowDefinition definition,
        IReadOnlyList<LlmCallComponent> resolvedComponents)
    {
        var referencedComponentIds = definition.Graph.Nodes
            .Where(node => node.Kind == WorkflowNodeKind.LlmCall && node.Settings.ComponentId.HasValue)
            .Select(node => node.Settings.ComponentId!.Value)
            .ToHashSet();
        return referencedComponentIds.Count == 0
            ? []
            : resolvedComponents
                .Where(component => referencedComponentIds.Contains(component.Id))
                .ToArray();
    }

    private static WorkflowBackendResumeException Failure(
        WorkflowBackendResumeFailureKind kind,
        string safeMessage,
        Exception? innerException = null)
        => new(kind, safeMessage, innerException);
}
