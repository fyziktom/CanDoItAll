using System.Text.Json;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.Workflows.Abstractions;

namespace CanDoItAll.AgentFramework.Core;

public enum WorkflowExternalResponseAuthorizationOutcome
{
    Authorized,
    LinkageMismatch,
    AuthorizationContextUnavailable,
    OriginScopeMismatch,
    AuthorizationPolicyMismatch,
    FingerprintMismatch,
    InvalidLifetime,
    Expired
}

public sealed record WorkflowExternalResponseAuthorizationResult(
    WorkflowExternalResponseAuthorizationOutcome Outcome,
    WorkflowExternalResponseAuthorization? Authorization,
    string SafeMessage)
{
    public bool Succeeded =>
        Outcome == WorkflowExternalResponseAuthorizationOutcome.Authorized &&
        Authorization is not null;
}

public static class WorkflowExternalResponseAuthorizationFactory
{
    public static WorkflowExternalResponseAuthorizationResult Create(
        WorkflowExternalResponseOperationRecord operation,
        WorkflowRunSnapshot run,
        WorkflowExternalRequestRecord request,
        WorkflowExternalRequestBoundaryRecord boundary,
        WorkflowExternalResponseAction action,
        DateTimeOffset asOfUtc)
    {
        ArgumentNullException.ThrowIfNull(operation);
        ArgumentNullException.ThrowIfNull(run);
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(boundary);

        if (!HasValidLinkage(operation, run, request, boundary))
        {
            return Failure(
                WorkflowExternalResponseAuthorizationOutcome.LinkageMismatch,
                "The workflow response authorization evidence does not match the persisted request boundary.");
        }

        var origin = run.Origin;
        var requestPolicy = request.AuthorizationPolicy;
        var boundaryPolicy = boundary.AuthorizationPolicy;
        if (origin is null ||
            origin.AuthorizationScope is null ||
            string.IsNullOrWhiteSpace(origin.AuthorizationPolicyFingerprint) ||
            requestPolicy is null ||
            boundaryPolicy is null ||
            requestPolicy.AuthorizationScope is null ||
            boundaryPolicy.AuthorizationScope is null ||
            string.IsNullOrWhiteSpace(requestPolicy.AuthorizationPolicyFingerprint) ||
            string.IsNullOrWhiteSpace(boundaryPolicy.AuthorizationPolicyFingerprint))
        {
            return Failure(
                WorkflowExternalResponseAuthorizationOutcome.AuthorizationContextUnavailable,
                "The workflow response authorization context is unavailable.");
        }

        if (origin.AuthorizationScope != boundaryPolicy.AuthorizationScope ||
            requestPolicy.AuthorizationScope != boundaryPolicy.AuthorizationScope)
        {
            return Failure(
                WorkflowExternalResponseAuthorizationOutcome.OriginScopeMismatch,
                "The workflow response authorization scope does not match the persisted launch scope.");
        }

        if (requestPolicy != boundaryPolicy ||
            !string.Equals(
                origin.AuthorizationPolicyFingerprint,
                WorkflowExternalResponseAuthorizationPolicy.CurrentFingerprint,
                StringComparison.Ordinal) ||
            !string.Equals(
                boundaryPolicy.AuthorizationPolicyFingerprint,
                WorkflowExternalResponseAuthorizationPolicy.CurrentFingerprint,
                StringComparison.Ordinal) ||
            !string.Equals(
                origin.AuthorizationPolicyFingerprint,
                boundaryPolicy.AuthorizationPolicyFingerprint,
                StringComparison.Ordinal) ||
            ResolveOriginActor(origin) != boundaryPolicy.OriginActor)
        {
            return Failure(
                WorkflowExternalResponseAuthorizationOutcome.AuthorizationPolicyMismatch,
                "The workflow response authorization policy does not match its persisted launch evidence.");
        }

        if (asOfUtc == default ||
            operation.AcceptedAtUtc == default ||
            operation.AcceptedAtUtc > asOfUtc ||
            boundaryPolicy.ResponseAuthorizationLifetimeSeconds !=
                WorkflowExternalResponseAuthorizationPolicy.ResponseLifetimeSeconds)
        {
            return Failure(
                WorkflowExternalResponseAuthorizationOutcome.InvalidLifetime,
                "The workflow response authorization lifetime is invalid.");
        }

        DateTimeOffset expiresAtUtc;
        try
        {
            expiresAtUtc = operation.AcceptedAtUtc.AddSeconds(
                boundaryPolicy.ResponseAuthorizationLifetimeSeconds);
        }
        catch (ArgumentOutOfRangeException)
        {
            return Failure(
                WorkflowExternalResponseAuthorizationOutcome.InvalidLifetime,
                "The workflow response authorization lifetime is invalid.");
        }

        if (asOfUtc >= expiresAtUtc)
        {
            return Failure(
                WorkflowExternalResponseAuthorizationOutcome.Expired,
                "The workflow response authorization has expired.");
        }

        var expectedActorScopeFingerprint =
            WorkflowExternalResponseFingerprintFactory.CreateActorScopeFingerprint(
                operation.Actor,
                boundaryPolicy.AuthorizationScope,
                boundaryPolicy.AuthorizationPolicyFingerprint);
        if (operation.ActorScopeFingerprint != expectedActorScopeFingerprint ||
            !HasValidProtectedPayload(operation, request.Kind, action))
        {
            return Failure(
                WorkflowExternalResponseAuthorizationOutcome.FingerprintMismatch,
                "The workflow response authorization fingerprint is invalid.");
        }

        return new WorkflowExternalResponseAuthorizationResult(
            WorkflowExternalResponseAuthorizationOutcome.Authorized,
            new WorkflowExternalResponseAuthorization(
                operation.Id,
                request.Id,
                request.Version,
                run.RunId,
                run.WorkflowId,
                run.VersionId,
                request.Kind,
                action,
                operation.Actor,
                boundaryPolicy.AuthorizationScope,
                boundaryPolicy.OriginActor,
                boundaryPolicy.AuthorizationPolicyFingerprint,
                operation.AcceptedAtUtc,
                expiresAtUtc),
            "The workflow response authorization is valid.");
    }

    private static bool HasValidLinkage(
        WorkflowExternalResponseOperationRecord operation,
        WorkflowRunSnapshot run,
        WorkflowExternalRequestRecord request,
        WorkflowExternalRequestBoundaryRecord boundary)
    {
        return operation.RequestId == request.Id &&
               operation.RunId == run.RunId &&
               request.RunId == run.RunId &&
               operation.ExpectedRequestVersion == request.Version &&
               boundary.RequestId == request.Id &&
               boundary.RequestVersion == request.Version &&
               boundary.Continuation is not null &&
               request.Continuation is not null &&
               boundary.Continuation == request.Continuation &&
               boundary.Continuation.Request.ExternalRequestId == request.Id &&
               request.ResponseContract is not null &&
               request.ResponseContract == boundary.ResponseContract &&
               operation.AcceptedAtUtc >= boundary.CreatedAtUtc &&
               operation.AcceptedAtUtc >= run.CreatedAtUtc;
    }

    private static bool HasValidProtectedPayload(
        WorkflowExternalResponseOperationRecord operation,
        WorkflowExternalRequestKind requestKind,
        WorkflowExternalResponseAction action)
    {
        string canonicalJson;
        try
        {
            canonicalJson = WorkflowExternalResponseFingerprintFactory.CanonicalizeJson(
                operation.ResponsePayload.Json);
        }
        catch (Exception exception) when (exception is ArgumentException or JsonException)
        {
            return false;
        }

        if (!string.Equals(canonicalJson, operation.ResponsePayload.Json, StringComparison.Ordinal) ||
            operation.ResponsePayloadHash !=
                WorkflowExternalResponseFingerprintFactory.CreatePayloadHash(operation.ResponsePayload))
        {
            return false;
        }

        using var document = JsonDocument.Parse(canonicalJson);
        return WorkflowExternalResponseValidator.TryDeriveAction(
                   requestKind,
                   document.RootElement,
                   out var protectedAction) &&
               protectedAction == action;
    }

    private static WorkflowLaunchActor? ResolveOriginActor(WorkflowLaunchOrigin origin)
        => origin switch
        {
            WorkflowLaunchOrigin.Api api => api.Actor,
            WorkflowLaunchOrigin.Preview preview => preview.Actor,
            WorkflowLaunchOrigin.ProjectStructureNode project => project.RequestingActor,
            WorkflowLaunchOrigin.AgentRuntimeInvocation agent => agent.Agent,
            _ => null
        };

    private static WorkflowExternalResponseAuthorizationResult Failure(
        WorkflowExternalResponseAuthorizationOutcome outcome,
        string safeMessage)
        => new(outcome, Authorization: null, safeMessage);
}
