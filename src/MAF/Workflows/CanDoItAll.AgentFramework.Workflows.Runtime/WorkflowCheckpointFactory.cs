using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.Workflows.Abstractions;

namespace CanDoItAll.AgentFramework.Core;

public sealed class WorkflowCheckpointFactory : IWorkflowCheckpointFactory
{
    public const string MetadataOnlyPayloadReference = "runtime://metadata-only";

    public const string MetadataOnlyResumeUnavailableReason =
        "Resume is not available for metadata-only workflow checkpoints. Use a durable workflow backend with trusted runtime state before enabling resume.";

    public WorkflowCheckpointRecord CreateMetadataCheckpoint(WorkflowCheckpointCreateRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(request.Definition);

        var payloadReference = string.IsNullOrWhiteSpace(request.PayloadReference)
            ? MetadataOnlyPayloadReference
            : request.PayloadReference.Trim();
        var summary = string.IsNullOrWhiteSpace(request.Summary)
            ? $"Workflow checkpoint '{request.Kind}' captured."
            : request.Summary.Trim();

        return new WorkflowCheckpointRecord(
            WorkflowCheckpointId.New(),
            request.RunId,
            request.Definition.Id,
            request.Definition.VersionId,
            request.Backend,
            request.Kind,
            WorkflowCheckpointTrustBoundary.MetadataOnly,
            WorkflowResumeAvailability.NotSupported,
            request.NodeId,
            request.ExternalRequestId,
            request.BackendCheckpointId.Trim(),
            payloadReference,
            PayloadHash: string.Empty,
            summary,
            MetadataOnlyResumeUnavailableReason,
            request.CreatedAtUtc,
            ResumedAtUtc: null);
    }
}
