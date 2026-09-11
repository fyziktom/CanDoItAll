using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Serialization;
using CanDoItAll.AgentFramework.Models;

namespace CanDoItAll.AgentFramework.Core;

public sealed class WorkflowExecutorApprovalAdmission {
    private readonly string settingsHash;
    private readonly WorkflowExecutionOccurrence? incomingOccurrence;

    internal WorkflowExecutorApprovalAdmission(WorkflowExecutorApprovalAuthorization authorization,
        string settingsJson, WorkflowNodeInput originalInput, WorkflowExternalResponseLease? responseLease) {
        ArgumentNullException.ThrowIfNull(authorization);
        ArgumentNullException.ThrowIfNull(settingsJson);
        ArgumentNullException.ThrowIfNull(originalInput);
        if (!authorization.Approved || !authorization.ExpectedToken.FixedTimeEquals(authorization.PresentedToken) ||
                authorization.InputHash != WorkflowExecutorInputHash.Compute(originalInput) ||
                originalInput.ExecutionOccurrence is { } occurrence && occurrence.RunId != authorization.RunId) {
            throw new InvalidOperationException("The restored Workflow approval does not bind this executor input.");
        }
        Authorization = authorization;
        ResponseLease = responseLease;
        settingsHash = Hash(settingsJson);
        incomingOccurrence = originalInput.ExecutionOccurrence;
    }

    [JsonIgnore]
    public WorkflowExecutorApprovalAuthorization Authorization { get; }

    [JsonIgnore]
    public WorkflowExternalResponseLease? ResponseLease { get; }

    public void RequireMatches(WorkflowExecutorExecutionContext context, WorkflowNodeInput input) {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(input);
        var authorization = Authorization;
        var response = authorization.ExternalResponseAuthorization;
        if (!ReferenceEquals(context.ApprovalAdmission, this) ||
                context.RunId != authorization.RunId || context.Definition.Id != authorization.WorkflowId ||
                context.Definition.VersionId != authorization.WorkflowVersionId || context.Node.Id != authorization.NodeId ||
                context.Node.Settings.ExecutorId != authorization.ExecutorId || context.Descriptor.Id != authorization.ExecutorId ||
                context.Descriptor.PermissionPolicy.RequiredCapabilities != authorization.RequiredCapabilities ||
                context.Descriptor.PermissionPolicy.ApprovalRequirement != authorization.ApprovalRequirement ||
                context.CausationRequestId != response.RequestId || context.CausationRequestVersion != response.RequestVersion ||
                context.CausationOperationId != response.OperationId || context.InvocationGeneration.Value != response.RequestVersion.Value ||
                input.ExecutionOccurrence != incomingOccurrence ||
                context.ExecutionOccurrence != incomingOccurrence?.Advance(authorization.WorkflowVersionId, authorization.NodeId) ||
                authorization.InputHash != WorkflowExecutorInputHash.Compute(input) ||
                !string.Equals(settingsHash, Hash(context.SettingsJson), StringComparison.Ordinal)) {
            throw new InvalidOperationException("The Workflow executor invocation differs from its restored approval admission.");
        }
    }

    private static string Hash(string value)
        => Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(value)));
}
