using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.Workflows.Abstractions;

namespace CanDoItAll.AgentFramework.Core;

public sealed class WorkflowExternalResponseValidator : IWorkflowExternalResponseValidator
{
    public const int MaximumJsonDepth = 32;
    public const int MaximumApprovalMessageLength = 4_096;

    private const int SupportedSchemaVersion = 1;
    private const string HumanInputSchemaName = "workflow_human_input_response";

    public WorkflowExternalResponseValidationResult Validate(
        WorkflowExternalResponseValidationRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(request.Run);
        ArgumentNullException.ThrowIfNull(request.Request);
        ArgumentNullException.ThrowIfNull(request.Boundary);

        if (request.ExpectedRequestVersion != request.Request.Version)
        {
            return Failure(
                WorkflowExternalResponseValidationOutcome.RequestVersionMismatch,
                "The workflow external request version does not match the submitted version.");
        }

        if (!HasValidBoundaryLinkage(request))
        {
            return Failure(
                WorkflowExternalResponseValidationOutcome.BoundaryMismatch,
                "The workflow external request boundary does not match the active request.");
        }

        if (!HasValidContract(request.Request, request.Boundary))
        {
            return Failure(
                WorkflowExternalResponseValidationOutcome.InvalidContract,
                "The workflow external response contract is unavailable or inconsistent.");
        }

        if (request.Response.ValueKind == JsonValueKind.Undefined)
        {
            return Failure(
                WorkflowExternalResponseValidationOutcome.SchemaMismatch,
                "The workflow external response is not valid JSON.");
        }

        var rawJson = request.Response.GetRawText();
        if (Encoding.UTF8.GetByteCount(rawJson) > request.Boundary.ResponseContract.MaximumPayloadBytes)
        {
            return Failure(
                WorkflowExternalResponseValidationOutcome.PayloadTooLarge,
                "The workflow external response exceeds the persisted payload limit.");
        }

        if (GetMaximumDepth(request.Response) > MaximumJsonDepth)
        {
            return Failure(
                WorkflowExternalResponseValidationOutcome.MaximumDepthExceeded,
                "The workflow external response exceeds the maximum JSON depth.");
        }

        string canonicalJson;
        try
        {
            canonicalJson = WorkflowExternalResponseFingerprintFactory.CanonicalizeJson(rawJson);
        }
        catch (Exception exception) when (exception is ArgumentException or JsonException)
        {
            return Failure(
                WorkflowExternalResponseValidationOutcome.SchemaMismatch,
                "The workflow external response contains invalid or ambiguous JSON.");
        }

        var contract = request.Boundary.ResponseContract;
        if (!TryDeriveAction(contract.Kind, request.Response, out var action))
        {
            return Failure(
                WorkflowExternalResponseValidationOutcome.SchemaMismatch,
                "The workflow external response does not satisfy its persisted response shape.");
        }

        if (contract.Kind == WorkflowExternalRequestKind.HumanInput)
        {
            AgentJsonSchemaOutputResult schemaResult;
            try
            {
                schemaResult = AgentJsonSchemaOutputValidator.Validate(
                    contract.SchemaJson,
                    canonicalJson,
                    HumanInputSchemaName,
                    strict: true);
            }
            catch (AgentJsonSchemaOutputContractException)
            {
                return Failure(
                    WorkflowExternalResponseValidationOutcome.InvalidContract,
                    "The persisted workflow external response schema is invalid.");
            }

            if (schemaResult.ValidationStatus != AgentJsonSchemaOutputValidationStatus.Valid)
            {
                return Failure(
                    WorkflowExternalResponseValidationOutcome.SchemaMismatch,
                    "The workflow external response does not satisfy its persisted JSON schema.");
            }
        }

        return new WorkflowExternalResponseValidationResult(
            WorkflowExternalResponseValidationOutcome.Valid,
            new WorkflowExternalResponsePayload(canonicalJson),
            action,
            "The workflow external response is valid.");
    }

    internal static bool TryDeriveAction(
        WorkflowExternalRequestKind requestKind,
        JsonElement response,
        out WorkflowExternalResponseAction action)
    {
        if (requestKind == WorkflowExternalRequestKind.HumanInput)
        {
            action = WorkflowExternalResponseAction.SubmitInput;
            return true;
        }

        if (requestKind is not (WorkflowExternalRequestKind.Approval or WorkflowExternalRequestKind.ToolApproval) ||
            response.ValueKind != JsonValueKind.Object)
        {
            action = default;
            return false;
        }

        var propertyNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        bool? approved = null;
        foreach (var property in response.EnumerateObject())
        {
            if (!propertyNames.Add(property.Name))
            {
                action = default;
                return false;
            }

            if (string.Equals(property.Name, "approved", StringComparison.OrdinalIgnoreCase))
            {
                if (property.Value.ValueKind is not (JsonValueKind.True or JsonValueKind.False))
                {
                    action = default;
                    return false;
                }

                approved = property.Value.GetBoolean();
                continue;
            }

            if (string.Equals(property.Name, "message", StringComparison.OrdinalIgnoreCase))
            {
                if (property.Value.ValueKind != JsonValueKind.String ||
                    property.Value.GetString()!.Length > MaximumApprovalMessageLength)
                {
                    action = default;
                    return false;
                }

                continue;
            }

            action = default;
            return false;
        }

        if (!approved.HasValue)
        {
            action = default;
            return false;
        }

        action = approved.Value
            ? WorkflowExternalResponseAction.Approve
            : WorkflowExternalResponseAction.Deny;
        return true;
    }

    private static bool HasValidBoundaryLinkage(WorkflowExternalResponseValidationRequest validation)
    {
        var request = validation.Request;
        var boundary = validation.Boundary;
        return request.RunId == validation.Run.RunId &&
               boundary.RequestId == request.Id &&
               boundary.RequestVersion == request.Version &&
               boundary.RequestVersion == validation.ExpectedRequestVersion &&
               boundary.State == request.State &&
               boundary.Continuation is not null &&
               request.Continuation is not null &&
               boundary.Continuation == request.Continuation &&
               boundary.Continuation.Request.ExternalRequestId == request.Id &&
               boundary.RequestPayloadHash == ComputeRequestPayloadHash(request.RequestJson);
    }

    private static bool HasValidContract(
        WorkflowExternalRequestRecord request,
        WorkflowExternalRequestBoundaryRecord boundary)
    {
        var requestContract = request.ResponseContract;
        var boundaryContract = boundary.ResponseContract;
        if (requestContract is null ||
            boundaryContract is null ||
            request.Kind is not (
                WorkflowExternalRequestKind.HumanInput or
                WorkflowExternalRequestKind.Approval or
                WorkflowExternalRequestKind.ToolApproval) ||
            requestContract.Kind != request.Kind ||
            boundaryContract.Kind != request.Kind ||
            requestContract.SchemaVersion != SupportedSchemaVersion ||
            boundaryContract.SchemaVersion != SupportedSchemaVersion ||
            requestContract.MaximumPayloadBytes <= 0 ||
            boundaryContract.MaximumPayloadBytes != requestContract.MaximumPayloadBytes ||
            !string.Equals(requestContract.SchemaId, boundaryContract.SchemaId, StringComparison.Ordinal) ||
            !string.Equals(requestContract.SchemaJson, boundaryContract.SchemaJson, StringComparison.Ordinal) ||
            !string.Equals(requestContract.SchemaHash, boundaryContract.SchemaHash, StringComparison.Ordinal) ||
            !string.Equals(requestContract.SchemaHash, ComputeSha256(requestContract.SchemaJson), StringComparison.Ordinal))
        {
            return false;
        }

        return true;
    }

    private static int GetMaximumDepth(JsonElement element, int depth = 1)
    {
        var maximum = depth;
        if (element.ValueKind == JsonValueKind.Object)
        {
            foreach (var property in element.EnumerateObject())
            {
                maximum = Math.Max(maximum, GetMaximumDepth(property.Value, depth + 1));
            }
        }
        else if (element.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in element.EnumerateArray())
            {
                maximum = Math.Max(maximum, GetMaximumDepth(item, depth + 1));
            }
        }

        return maximum;
    }

    private static WorkflowExternalRequestPayloadHash ComputeRequestPayloadHash(string requestJson)
        => new(ComputeSha256(requestJson));

    private static string ComputeSha256(string value)
        => Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(value)));

    private static WorkflowExternalResponseValidationResult Failure(
        WorkflowExternalResponseValidationOutcome outcome,
        string safeMessage)
        => new(outcome, CanonicalPayload: null, Action: null, safeMessage);
}
