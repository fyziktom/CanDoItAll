using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.Workflows.Abstractions;

namespace CanDoItAll.AgentFramework.Maf;

internal sealed class MafWorkflowRehydrationVerifier
{
    public async Task<WorkflowBackendCheckpointPayloadRecord> VerifyAsync(
        WorkflowBackendResumeRequest resume,
        WorkflowDefinition definition,
        MafWorkflowBuildResult build,
        IWorkflowBackendCheckpointPayloadStore store,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(resume);
        ArgumentNullException.ThrowIfNull(definition);
        ArgumentNullException.ThrowIfNull(build);
        ArgumentNullException.ThrowIfNull(store);

        var continuation = resume.ExternalRequest.Continuation
            ?? throw Failure(
                WorkflowBackendResumeFailureKind.CheckpointMissing,
                "Workflow external request does not contain a native checkpoint continuation.");
        if (resume.Run.RunId != resume.ExternalRequest.RunId ||
            resume.Run.WorkflowId != definition.Id)
        {
            throw Failure(
                WorkflowBackendResumeFailureKind.RequestMismatch,
                "Workflow run, request, and exact catalog definition identities do not match.");
        }

        if (resume.Run.VersionId != definition.VersionId)
        {
            throw Failure(
                WorkflowBackendResumeFailureKind.ExactWorkflowVersionMismatch,
                "Workflow run version does not match the exact catalog definition version.");
        }

        if (continuation.Request.ExternalRequestId != resume.ExternalRequest.Id)
        {
            throw Failure(
                WorkflowBackendResumeFailureKind.RequestMismatch,
                "Workflow external request id does not match its persisted backend request link.");
        }

        if (build.Workflow is null || !build.Compilation.Succeeded)
        {
            throw Failure(
                WorkflowBackendResumeFailureKind.CompilationFailed,
                "The exact workflow version could not be compiled for checkpoint rehydration.");
        }

        var compilerContractVersion = build.CompilerContractVersion
            ?? throw Failure(
                WorkflowBackendResumeFailureKind.CompilerContractMismatch,
                "Compiled MAF workflow is missing its compiler contract version.");
        var topologyFingerprint = build.TopologyFingerprint
            ?? throw Failure(
                WorkflowBackendResumeFailureKind.TopologyMismatch,
                "Compiled MAF workflow is missing its topology fingerprint.");
        if (continuation.CompilerContractVersion != compilerContractVersion)
        {
            throw Failure(
                WorkflowBackendResumeFailureKind.CompilerContractMismatch,
                "Workflow compiler contract version does not match the persisted continuation.");
        }

        if (continuation.TopologyFingerprint != topologyFingerprint)
        {
            throw Failure(
                WorkflowBackendResumeFailureKind.TopologyMismatch,
                "Workflow topology fingerprint does not match the persisted continuation.");
        }

        var read = await store.ReadAsync(continuation.Checkpoint, cancellationToken);
        if (!read.Succeeded || read.Checkpoint is null)
        {
            throw Failure(
                MapReadFailure(read.Outcome),
                $"Workflow checkpoint payload is unavailable with outcome '{read.Outcome}'.");
        }

        var checkpoint = read.Checkpoint;
        VerifySession(resume, checkpoint, compilerContractVersion, topologyFingerprint);
        if (checkpoint.Payload.Sha256 != continuation.CheckpointPayloadHash ||
            !checkpoint.Payload.HasValidHash)
        {
            throw Failure(
                WorkflowBackendResumeFailureKind.CheckpointCorrupt,
                "Workflow checkpoint payload hash does not match the persisted continuation.");
        }

        if (checkpoint.ExternalRequestLink is not { } storedRequestLink ||
            storedRequestLink.ExternalRequestId != continuation.Request.ExternalRequestId ||
            storedRequestLink.BackendRequestId != continuation.Request.BackendRequestId)
        {
            throw Failure(
                WorkflowBackendResumeFailureKind.RequestMismatch,
                "Workflow checkpoint request link does not match the persisted continuation.");
        }

        if (storedRequestLink.BackendRequestPortId != continuation.Request.BackendRequestPortId)
        {
            throw Failure(
                WorkflowBackendResumeFailureKind.PortMismatch,
                "Workflow checkpoint request port does not match the persisted continuation.");
        }

        return checkpoint;
    }

    private static void VerifySession(
        WorkflowBackendResumeRequest resume,
        WorkflowBackendCheckpointPayloadRecord checkpoint,
        WorkflowCompilerContractVersion compilerContractVersion,
        WorkflowTopologyFingerprint topologyFingerprint)
    {
        var continuation = resume.ExternalRequest.Continuation!;
        var session = checkpoint.Session;
        if (checkpoint.Index.Link != continuation.Checkpoint ||
            session.Id != continuation.Checkpoint.SessionId ||
            session.RunId != resume.Run.RunId ||
            session.WorkflowId != resume.Run.WorkflowId ||
            session.WorkflowVersionId != resume.Run.VersionId ||
            session.Backend != resume.Run.Backend ||
            session.Format != MafWorkflowCheckpointProtocol.Format ||
            session.FormatVersion != MafWorkflowCheckpointProtocol.FormatVersion ||
            session.CompilerContractVersion != compilerContractVersion ||
            session.TopologyFingerprint != topologyFingerprint ||
            !string.Equals(resume.Run.BackendRunId, session.Id.Value, StringComparison.Ordinal))
        {
            throw Failure(
                WorkflowBackendResumeFailureKind.CheckpointIncompatible,
                "Workflow checkpoint session metadata does not match the exact run and compiled workflow.");
        }
    }

    private static WorkflowBackendResumeFailureKind MapReadFailure(
        WorkflowBackendCheckpointReadOutcome outcome)
        => outcome switch
        {
            WorkflowBackendCheckpointReadOutcome.NotFound =>
                WorkflowBackendResumeFailureKind.CheckpointMissing,
            WorkflowBackendCheckpointReadOutcome.PayloadCorrupt =>
                WorkflowBackendResumeFailureKind.CheckpointCorrupt,
            _ => WorkflowBackendResumeFailureKind.CheckpointIncompatible
        };

    private static WorkflowBackendResumeException Failure(
        WorkflowBackendResumeFailureKind kind,
        string safeMessage)
        => new(kind, safeMessage);
}
