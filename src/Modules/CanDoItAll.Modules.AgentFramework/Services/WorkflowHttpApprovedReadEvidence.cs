using System.Text.Json;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.WorkflowExecutors.Standard.Network;

namespace CanDoItAll.Modules.AgentFramework;

internal sealed record WorkflowHttpApprovedReadEvidence(Guid DatabaseProfileId, Guid SecretId,
    Guid RequestId, long RequestVersion, Guid OperationId, DateTimeOffset ApprovedAtUtc, DateTimeOffset ExpiresAtUtc,
    WorkflowExecutionContentHash DestinationHash, WorkflowExecutionContentHash InputHash) {
    public const int SchemaVersion = 1;

    public static WorkflowProviderReadEvidence? Capture(WorkflowExecutorExecutionContext context, WorkflowNodeInput input,
        WorkflowRunSnapshot run, WorkflowExecutorApprovalAdmission approval, Guid profileId, Guid secretId, Uri destination) {
        var actual = WorkflowExecutorExecutionAuditScope.CurrentInvocation;
        if (context.ExecutionOccurrence is null || actual?.CompilerVersion == WorkflowProviderDisclosureProtocol.Legacy) {
            return null;
        }
        if (actual is null || actual.CompilerVersion != WorkflowProviderDisclosureProtocol.Current ||
                actual.WorkflowId != run.WorkflowId || actual.VersionId != run.VersionId || actual.NodeId != context.Node.Id ||
                actual.Occurrence != context.ExecutionOccurrence || actual.Occurrence.RunId != run.RunId ||
                actual.InputHash != WorkflowExecutionContentHash.Compute(input.PayloadJson) ||
                actual.SourceHash != WorkflowProviderDisclosureContent.Source(run.Origin)) {
            throw new InvalidOperationException("The approved HTTP read has no exact actual Workflow invocation binding.");
        }
        var authorized = approval.Authorization.ExternalResponseAuthorization;
        var payload = new WorkflowHttpApprovedReadEvidence(profileId, secretId, authorized.RequestId.Value,
            authorized.RequestVersion.Value, authorized.OperationId.Value, authorized.AuthorizedAtUtc, authorized.ExpiresAtUtc,
            WorkflowExecutionContentHash.Compute(destination.AbsoluteUri), actual.InputHash);
        return new(WorkflowHttpSecretUse.DisclosureOwner, SchemaVersion, actual.Occurrence, actual.VersionId, actual.NodeId,
            JsonSerializer.Serialize(payload, WorkflowProviderDisclosureContent.JsonOptions));
    }
}
