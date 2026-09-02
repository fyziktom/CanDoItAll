using System.Text.Json;
using System.Text;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.Workflows.Abstractions;
using Microsoft.Agents.AI.Workflows;

namespace CanDoItAll.AgentFramework.Maf;

internal sealed record MafWorkflowMappedExternalRequest(
    WorkflowExternalRequestRecord Request,
    WorkflowCheckpointRecord Checkpoint);

internal sealed class MafWorkflowExternalRequestMapper(TimeProvider timeProvider)
{
    private const int MaximumResponsePayloadBytes = 64 * 1024;
    private const string HumanResponseSchemaId = "CanDoItAll.WorkflowHumanInputResponse/v1";
    private const string ApprovalResponseSchemaId = "CanDoItAll.WorkflowApprovalResponse/v1";
    private const string DefaultResponseSchemaJson = "{}";
    private const string ApprovalResponseSchemaJson = """
        {
          "type": "object",
          "properties": {
            "approved": { "type": "boolean" },
            "message": { "type": "string" }
          },
          "required": ["approved"],
          "additionalProperties": false
        }
        """;
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly TimeProvider clock = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));

    public MafWorkflowMappedExternalRequest Map(
        WorkflowRunId runId,
        WorkflowLaunchOrigin? origin,
        ExternalRequest nativeRequest,
        WorkflowBackendCheckpointPayloadRecord checkpoint)
    {
        ArgumentNullException.ThrowIfNull(nativeRequest);
        ArgumentNullException.ThrowIfNull(checkpoint);

        var externalRequestId = WorkflowExternalRequestId.New();
        var backendRequest = new WorkflowBackendExternalRequestLink(
            externalRequestId,
            new WorkflowBackendRequestId(nativeRequest.RequestId),
            new WorkflowBackendRequestPortId(nativeRequest.PortInfo.PortId));
        var now = clock.GetUtcNow();

        WorkflowExternalRequestRecord request;
        if (nativeRequest.PortInfo.RequestType.IsMatch<MafWorkflowHumanInputRequest>() &&
            nativeRequest.TryGetDataAs<MafWorkflowHumanInputRequest>(out var humanRequest) &&
            humanRequest is not null)
        {
            request = MapHumanRequest(
                runId,
                origin,
                backendRequest,
                checkpoint,
                humanRequest,
                now);
        }
        else if (nativeRequest.PortInfo.RequestType.IsMatch<MafWorkflowApprovalRequest>() &&
                 nativeRequest.TryGetDataAs<MafWorkflowApprovalRequest>(out var approvalRequest) &&
                 approvalRequest is not null)
        {
            request = MapApprovalRequest(
                runId,
                origin,
                backendRequest,
                checkpoint,
                approvalRequest,
                now);
        }
        else
        {
            throw new InvalidOperationException(
                "MAF emitted an unsupported external-request payload type for the workflow HITL boundary.");
        }

        var summary = request.Kind is WorkflowExternalRequestKind.Approval or WorkflowExternalRequestKind.ToolApproval
            ? $"Workflow is waiting for approval at node '{request.NodeId}'."
            : $"Workflow is waiting for input at node '{request.NodeId}'.";
        var checkpointRecord = new WorkflowCheckpointRecord(
            WorkflowCheckpointId.New(),
            runId,
            checkpoint.Session.WorkflowId,
            checkpoint.Session.WorkflowVersionId,
            checkpoint.Session.Backend,
            WorkflowCheckpointKind.WaitingForInput,
            WorkflowCheckpointTrustBoundary.TrustedRuntimeState,
            WorkflowResumeAvailability.Available,
            request.NodeId,
            request.Id,
            checkpoint.Index.Link.CheckpointId.Value,
            CreatePayloadReference(checkpoint.Index.Link),
            checkpoint.Payload.Sha256.Value,
            summary,
            ResumeUnavailableReason: string.Empty,
            checkpoint.Index.CreatedAtUtc,
            ResumedAtUtc: null);

        return new MafWorkflowMappedExternalRequest(request, checkpointRecord);
    }

    public ExternalResponse CreateResponse(
        ExternalRequest restoredRequest,
        WorkflowExternalRequestRecord persistedRequest,
        JsonElement response,
        WorkflowExternalResponseAuthorization authorization)
    {
        ArgumentNullException.ThrowIfNull(restoredRequest);
        ArgumentNullException.ThrowIfNull(persistedRequest);
        ArgumentNullException.ThrowIfNull(authorization);
        ValidateRestoredBoundary(restoredRequest, persistedRequest);
        ValidateResponseAuthorization(persistedRequest, authorization, clock.GetUtcNow());
        try
        {
            var responseJson = response.GetRawText();
            if (persistedRequest.ResponseContract is not { } contract ||
                Encoding.UTF8.GetByteCount(responseJson) > contract.MaximumPayloadBytes)
            {
                throw Failure(
                    WorkflowBackendResumeFailureKind.ResponseMismatch,
                    "Workflow external response does not satisfy the persisted response contract size boundary.");
            }

            return persistedRequest.Kind switch
            {
                WorkflowExternalRequestKind.HumanInput => CreateHumanResponse(
                    restoredRequest,
                    persistedRequest,
                    responseJson),
                WorkflowExternalRequestKind.Approval or WorkflowExternalRequestKind.ToolApproval => CreateApprovalResponse(
                    restoredRequest,
                    persistedRequest,
                    response,
                    authorization),
                _ => throw Failure(
                    WorkflowBackendResumeFailureKind.RequestMismatch,
                    "The persisted workflow external-request kind is not supported by this workflow HITL driver.")
            };
        }
        catch (WorkflowBackendResumeException)
        {
            throw;
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            throw Failure(
                WorkflowBackendResumeFailureKind.ResponseMismatch,
                "The workflow external response could not be restored against the persisted request contract.",
                exception);
        }
    }

    private static ExternalResponse CreateHumanResponse(
        ExternalRequest restoredRequest,
        WorkflowExternalRequestRecord persistedRequest,
        string responseJson)
    {
        if (!restoredRequest.PortInfo.RequestType.IsMatch<MafWorkflowHumanInputRequest>() ||
            !restoredRequest.TryGetDataAs<MafWorkflowHumanInputRequest>(out var humanRequest) ||
            humanRequest is null)
        {
            throw Failure(
                WorkflowBackendResumeFailureKind.RequestMismatch,
                "The restored MAF external request does not contain the expected human-input payload.");
        }

        ValidateHumanRequest(persistedRequest, humanRequest);
        return restoredRequest.CreateResponse(
            new MafWorkflowHumanInputResponse(responseJson));
    }

    private static ExternalResponse CreateApprovalResponse(
        ExternalRequest restoredRequest,
        WorkflowExternalRequestRecord persistedRequest,
        JsonElement response,
        WorkflowExternalResponseAuthorization authorization)
    {
        if (!restoredRequest.PortInfo.RequestType.IsMatch<MafWorkflowApprovalRequest>() ||
            !restoredRequest.TryGetDataAs<MafWorkflowApprovalRequest>(out var approvalRequest) ||
            approvalRequest is null)
        {
            throw Failure(
                WorkflowBackendResumeFailureKind.RequestMismatch,
                "The restored MAF external request does not contain the expected approval payload.");
        }

        ValidateApprovalRequest(persistedRequest, approvalRequest);
        var approvalResponse = ParseApprovalResponse(response);
        var expectedAction = approvalResponse.Approved
            ? WorkflowExternalResponseAction.Approve
            : WorkflowExternalResponseAction.Deny;
        if (authorization.Action != expectedAction)
        {
            throw Failure(
                WorkflowBackendResumeFailureKind.ResponseMismatch,
                "The reconstructed workflow authorization action does not match the validated approval response.");
        }

        return restoredRequest.CreateResponse(MafWorkflowApprovalContinuation.Create(
            approvalRequest,
            authorization,
            approvalResponse.Approved,
            approvalResponse.Message ?? string.Empty));
    }

    private static WorkflowExternalRequestRecord MapHumanRequest(
        WorkflowRunId runId,
        WorkflowLaunchOrigin? origin,
        WorkflowBackendExternalRequestLink backendRequest,
        WorkflowBackendCheckpointPayloadRecord checkpoint,
        MafWorkflowHumanInputRequest request,
        DateTimeOffset now)
    {
        ValidateNativeIdentity(runId, checkpoint, request.WorkflowId, request.WorkflowVersionId);
        using var context = JsonDocument.Parse(request.Context.PayloadJson);
        var requestJson = JsonSerializer.Serialize(
            new MafWorkflowHumanInputProjection(
                request.Prompt,
                request.ResponseShape,
                context.RootElement.Clone()),
            JsonOptions);
        var schemaJson = string.IsNullOrWhiteSpace(request.ResponseShape?.SchemaJson)
            ? DefaultResponseSchemaJson
            : request.ResponseShape.SchemaJson;

        return CreateRequest(
            runId,
            request.Kind,
            request.NodeId,
            eventName: "MafHumanInputRequested",
            requestJson,
            new WorkflowExternalResponseContract(
                request.Kind,
                HumanResponseSchemaId,
                schemaVersion: 1,
                schemaJson,
                MaximumResponsePayloadBytes),
            backendRequest,
            checkpoint,
            new WorkflowExternalRequestAuthorizationPolicySnapshot(
                ResolveOriginActor(origin),
                ExecutorId: null,
                WorkflowExecutorCapabilityFlags.None,
                WorkflowExecutorApprovalRequirement.NotRequired,
                IntendedApproverSubjectId: string.Empty)
            {
                AuthorizationScope = origin?.AuthorizationScope,
                AuthorizationPolicyFingerprint = origin?.AuthorizationPolicyFingerprint ?? string.Empty,
                ResponseAuthorizationLifetimeSeconds = WorkflowExternalResponseAuthorizationPolicy.ResponseLifetimeSeconds
            },
            now);
    }

    private static WorkflowExternalRequestRecord MapApprovalRequest(
        WorkflowRunId runId,
        WorkflowLaunchOrigin? origin,
        WorkflowBackendExternalRequestLink backendRequest,
        WorkflowBackendCheckpointPayloadRecord checkpoint,
        MafWorkflowApprovalRequest request,
        DateTimeOffset now)
    {
        ValidateNativeIdentity(runId, checkpoint, request.WorkflowId, request.WorkflowVersionId);
        if (request.RunId != runId)
        {
            throw new InvalidOperationException("MAF approval request run identity does not match the active workflow run.");
        }

        var requestJson = JsonSerializer.Serialize(
            new MafWorkflowApprovalProjection(
                request.Prompt,
                request.ExecutorId,
                request.RequiredCapabilities,
                request.ApprovalRequirement,
                request.RedactedSettingsSummary),
            JsonOptions);
        return CreateRequest(
            runId,
            WorkflowExternalRequestKind.Approval,
            request.NodeId,
            eventName: "MafApprovalRequested",
            requestJson,
            new WorkflowExternalResponseContract(
                WorkflowExternalRequestKind.Approval,
                ApprovalResponseSchemaId,
                schemaVersion: 1,
                ApprovalResponseSchemaJson,
                MaximumResponsePayloadBytes),
            backendRequest,
            checkpoint,
            new WorkflowExternalRequestAuthorizationPolicySnapshot(
                ResolveOriginActor(origin),
                request.ExecutorId,
                request.RequiredCapabilities,
                request.ApprovalRequirement,
                IntendedApproverSubjectId: string.Empty)
            {
                AuthorizationScope = origin?.AuthorizationScope,
                AuthorizationPolicyFingerprint = origin?.AuthorizationPolicyFingerprint ?? string.Empty,
                ResponseAuthorizationLifetimeSeconds = WorkflowExternalResponseAuthorizationPolicy.ResponseLifetimeSeconds
            },
            now);
    }

    private static WorkflowExternalRequestRecord CreateRequest(
        WorkflowRunId runId,
        WorkflowExternalRequestKind kind,
        WorkflowNodeId nodeId,
        string eventName,
        string requestJson,
        WorkflowExternalResponseContract responseContract,
        WorkflowBackendExternalRequestLink backendRequest,
        WorkflowBackendCheckpointPayloadRecord checkpoint,
        WorkflowExternalRequestAuthorizationPolicySnapshot authorizationPolicy,
        DateTimeOffset now)
    {
        return new WorkflowExternalRequestRecord(
            backendRequest.ExternalRequestId,
            runId,
            kind,
            nodeId,
            eventName,
            requestJson,
            ResponseJson: string.Empty,
            now,
            RespondedAtUtc: null)
        {
            Version = WorkflowExternalRequestVersion.Initial,
            State = WorkflowExternalRequestState.Pending,
            ResponseContract = responseContract,
            Continuation = new WorkflowExternalRequestContinuation(
                backendRequest,
                checkpoint.Index.Link,
                checkpoint.Session.CompilerContractVersion,
                checkpoint.Session.TopologyFingerprint,
                checkpoint.Payload.Sha256),
            AuthorizationPolicy = authorizationPolicy
        };
    }

    private static void ValidateHumanRequest(
        WorkflowExternalRequestRecord persisted,
        MafWorkflowHumanInputRequest restored)
    {
        if (persisted.Kind != restored.Kind ||
            persisted.NodeId != restored.NodeId ||
            persisted.ResponseContract?.Kind != restored.Kind ||
            persisted.Continuation is null)
        {
            throw Failure(
                WorkflowBackendResumeFailureKind.RequestMismatch,
                "Restored MAF human-input request metadata does not match the persisted external request.");
        }
    }

    private static void ValidateApprovalRequest(
        WorkflowExternalRequestRecord persisted,
        MafWorkflowApprovalRequest restored)
    {
        var policy = persisted.AuthorizationPolicy;
        if (persisted.Kind is not (WorkflowExternalRequestKind.Approval or WorkflowExternalRequestKind.ToolApproval) ||
            persisted.RunId != restored.RunId ||
            persisted.NodeId != restored.NodeId ||
            persisted.ResponseContract?.Kind != persisted.Kind ||
            persisted.Continuation is null ||
            policy is null ||
            policy.ExecutorId != restored.ExecutorId ||
            policy.RequiredCapabilities != restored.RequiredCapabilities ||
            policy.ApprovalRequirement != restored.ApprovalRequirement)
        {
            throw Failure(
                WorkflowBackendResumeFailureKind.RequestMismatch,
                "Restored MAF approval request metadata does not match the persisted authorization policy.");
        }
    }

    private static MafWorkflowApprovalResponse ParseApprovalResponse(JsonElement response)
    {
        if (response.ValueKind != JsonValueKind.Object)
        {
            throw new ArgumentException("Workflow approval response must be a JSON object.", nameof(response));
        }

        bool? approved = null;
        var message = string.Empty;
        var propertyNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var property in response.EnumerateObject())
        {
            if (!propertyNames.Add(property.Name))
            {
                throw Failure(
                    WorkflowBackendResumeFailureKind.ResponseMismatch,
                    $"Workflow approval response contains duplicate property '{property.Name}'.");
            }

            if (string.Equals(property.Name, "approved", StringComparison.OrdinalIgnoreCase))
            {
                if (property.Value.ValueKind is not (JsonValueKind.True or JsonValueKind.False))
                {
                    throw Failure(
                        WorkflowBackendResumeFailureKind.ResponseMismatch,
                        "Workflow approval response property 'approved' must be boolean.");
                }

                approved = property.Value.GetBoolean();
                continue;
            }

            if (string.Equals(property.Name, "message", StringComparison.OrdinalIgnoreCase))
            {
                if (property.Value.ValueKind is not (JsonValueKind.String or JsonValueKind.Null))
                {
                    throw Failure(
                        WorkflowBackendResumeFailureKind.ResponseMismatch,
                        "Workflow approval response property 'message' must be a string.");
                }

                message = property.Value.GetString() ?? string.Empty;
                continue;
            }

            throw Failure(
                WorkflowBackendResumeFailureKind.ResponseMismatch,
                $"Workflow approval response property '{property.Name}' is not allowed.");
        }

        if (!approved.HasValue)
        {
            throw Failure(
                WorkflowBackendResumeFailureKind.ResponseMismatch,
                "Workflow approval response must contain an approved boolean property.");
        }

        return new MafWorkflowApprovalResponse(
            approved.Value,
            WorkflowExecutorRedaction.RedactText(message));
    }

    private static void ValidateNativeIdentity(
        WorkflowRunId runId,
        WorkflowBackendCheckpointPayloadRecord checkpoint,
        WorkflowId workflowId,
        WorkflowVersionId workflowVersionId)
    {
        if (checkpoint.Session.RunId != runId ||
            checkpoint.Session.WorkflowId != workflowId ||
            checkpoint.Session.WorkflowVersionId != workflowVersionId)
        {
            throw new InvalidOperationException(
                "MAF external request identity does not match the checkpoint session metadata.");
        }
    }

    private static void ValidateRestoredBoundary(
        ExternalRequest restoredRequest,
        WorkflowExternalRequestRecord persistedRequest)
    {
        var continuation = persistedRequest.Continuation ?? throw Failure(
            WorkflowBackendResumeFailureKind.RequestMismatch,
            "The persisted workflow external request has no native continuation linkage.");
        if (continuation.Request.ExternalRequestId != persistedRequest.Id ||
            !string.Equals(
                restoredRequest.RequestId,
                continuation.Request.BackendRequestId.Value,
                StringComparison.Ordinal))
        {
            throw Failure(
                WorkflowBackendResumeFailureKind.RequestMismatch,
                "The restored MAF request identity does not match the persisted external request.");
        }

        if (!string.Equals(
                restoredRequest.PortInfo.PortId,
                continuation.Request.BackendRequestPortId.Value,
                StringComparison.Ordinal))
        {
            throw Failure(
                WorkflowBackendResumeFailureKind.PortMismatch,
                "The restored MAF request port does not match the persisted external request.");
        }
    }

    private static void ValidateResponseAuthorization(
        WorkflowExternalRequestRecord persistedRequest,
        WorkflowExternalResponseAuthorization authorization,
        DateTimeOffset nowUtc)
    {
        var policy = persistedRequest.AuthorizationPolicy;
        var expectedActionKind = persistedRequest.Kind switch
        {
            WorkflowExternalRequestKind.HumanInput => authorization.Action == WorkflowExternalResponseAction.SubmitInput,
            WorkflowExternalRequestKind.Approval or WorkflowExternalRequestKind.ToolApproval =>
                authorization.Action is WorkflowExternalResponseAction.Approve or WorkflowExternalResponseAction.Deny,
            _ => false
        };
        if (policy is null ||
            policy.AuthorizationScope is null ||
            !string.Equals(
                policy.AuthorizationPolicyFingerprint,
                WorkflowExternalResponseAuthorizationPolicy.CurrentFingerprint,
                StringComparison.Ordinal) ||
            policy.ResponseAuthorizationLifetimeSeconds != WorkflowExternalResponseAuthorizationPolicy.ResponseLifetimeSeconds ||
            authorization.RequestId != persistedRequest.Id ||
            authorization.RequestVersion != persistedRequest.Version ||
            authorization.RunId != persistedRequest.RunId ||
            authorization.RequestKind != persistedRequest.Kind ||
            authorization.AuthorizationScope != policy.AuthorizationScope ||
            !string.Equals(
             authorization.AuthorizationPolicyFingerprint,
                 policy.AuthorizationPolicyFingerprint,
                 StringComparison.Ordinal) ||
            authorization.OriginActor != policy.OriginActor ||
            authorization.Actor is null ||
            authorization.Actor.Kind == WorkflowLaunchActorKind.Agent &&
                persistedRequest.Kind is WorkflowExternalRequestKind.Approval or WorkflowExternalRequestKind.ToolApproval ||
            authorization.OriginActor is { Kind: WorkflowLaunchActorKind.Agent or WorkflowLaunchActorKind.Service } origin &&
                origin == authorization.Actor &&
                persistedRequest.Kind is WorkflowExternalRequestKind.Approval or WorkflowExternalRequestKind.ToolApproval ||
            authorization.AuthorizedAtUtc == default ||
            authorization.AuthorizedAtUtc > nowUtc ||
            authorization.ExpiresAtUtc <= authorization.AuthorizedAtUtc ||
            authorization.ExpiresAtUtc != authorization.AuthorizedAtUtc.AddSeconds(
                WorkflowExternalResponseAuthorizationPolicy.ResponseLifetimeSeconds) ||
            authorization.IsExpired(nowUtc) ||
            !expectedActionKind)
        {
            throw Failure(
                WorkflowBackendResumeFailureKind.RequestMismatch,
                "The reconstructed workflow external-response authorization does not match the persisted request boundary.");
        }
    }

    private static WorkflowBackendResumeException Failure(
        WorkflowBackendResumeFailureKind kind,
        string safeMessage,
        Exception? innerException = null)
        => new(kind, safeMessage, innerException);

    private static WorkflowLaunchActor? ResolveOriginActor(WorkflowLaunchOrigin? origin)
    {
        return origin switch
        {
            WorkflowLaunchOrigin.Api api => api.Actor,
            WorkflowLaunchOrigin.Preview preview => preview.Actor,
            WorkflowLaunchOrigin.ProjectStructureNode projectStructure => projectStructure.RequestingActor,
            WorkflowLaunchOrigin.AgentRuntimeInvocation agentRuntime => agentRuntime.Agent,
            _ => null
        };
    }

    private static string CreatePayloadReference(WorkflowBackendCheckpointLink link)
        => $"workflow-checkpoint://{link.SessionId.Value}/{link.CheckpointId.Value}";

    private sealed record MafWorkflowHumanInputProjection(
        string Prompt,
        WorkflowValueShape? ResponseShape,
        JsonElement Context);

    private sealed record MafWorkflowApprovalProjection(
        string Prompt,
        WorkflowExecutorId ExecutorId,
        WorkflowExecutorCapabilityFlags RequiredCapabilities,
        WorkflowExecutorApprovalRequirement ApprovalRequirement,
        string RedactedSettingsSummary);

    private sealed record MafWorkflowApprovalResponse(bool Approved, string? Message);
}
