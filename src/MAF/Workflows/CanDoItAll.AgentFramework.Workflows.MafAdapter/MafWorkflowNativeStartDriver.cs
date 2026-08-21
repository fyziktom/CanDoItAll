using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.Workflows.Abstractions;

namespace CanDoItAll.AgentFramework.Maf;

internal sealed class MafWorkflowNativeStartDriver(
    IWorkflowBackendCheckpointPayloadStore checkpointPayloadStore,
    MafWorkflowStreamingRunDriver streamingDriver,
    MafWorkflowTurnResultMapper turnResultMapper,
    TimeProvider timeProvider)
{
    private readonly IWorkflowBackendCheckpointPayloadStore payloadStore = checkpointPayloadStore ?? throw new ArgumentNullException(nameof(checkpointPayloadStore));
    private readonly MafWorkflowStreamingRunDriver runDriver = streamingDriver ?? throw new ArgumentNullException(nameof(streamingDriver));
    private readonly MafWorkflowTurnResultMapper resultMapper = turnResultMapper ?? throw new ArgumentNullException(nameof(turnResultMapper));
    private readonly TimeProvider clock = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));

    public async Task<WorkflowBackendStartResult> StartAsync(
        WorkflowDefinition definition,
        WorkflowRunStartRequest request,
        WorkflowRunId runId,
        MafWorkflowBuildResult build,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(definition);
        ArgumentNullException.ThrowIfNull(request);
        ValidateBuild(build);

        var createdAtUtc = clock.GetUtcNow();
        var session = CreateSession(definition, runId, build);
        var checkpointManager = MafWorkflowCheckpointProtocol.CreateManager(payloadStore, session);
        var progressObserver = resultMapper.CreateProgressObserver(
            runId,
            definition,
            request.PreviewSimulationPlan,
            request.Origin);
        MafWorkflowStreamTurn turn;
        using (WorkflowExecutorExecutionAuditScope.Push(runId))
        using (WorkflowNodeExecutionProgressScope.Push(progressObserver))
        {
            turn = await runDriver.StartAsync(
                build.Workflow!,
                new WorkflowNodeInput(request.InputJson),
                checkpointManager,
                session.Id.Value,
                cancellationToken);
        }

        return await resultMapper.MapTurnAsync(
            definition,
            runId,
            request.Origin,
            createdAtUtc,
            turn,
            progressObserver,
            request,
            cancellationToken);
    }

    private static WorkflowBackendCheckpointSession CreateSession(
        WorkflowDefinition definition,
        WorkflowRunId runId,
        MafWorkflowBuildResult build)
    {
        return new WorkflowBackendCheckpointSession(
            new WorkflowBackendSessionId(runId.ToString()),
            runId,
            definition.Id,
            definition.VersionId,
            WorkflowRuntimeBackendKind.InProcess,
            MafWorkflowCheckpointProtocol.Format,
            MafWorkflowCheckpointProtocol.FormatVersion,
            build.CompilerContractVersion!.Value,
            build.TopologyFingerprint!.Value);
    }

    private static void ValidateBuild(MafWorkflowBuildResult build)
    {
        ArgumentNullException.ThrowIfNull(build);
        if (!build.Compilation.Succeeded ||
            build.Workflow is null ||
            !build.HasNativeExternalRequests ||
            build.CompilerContractVersion is null ||
            build.TopologyFingerprint is null)
        {
            throw new InvalidOperationException(
                "Native MAF workflow execution requires a successful build with deterministic compiler and topology metadata.");
        }
    }
}
