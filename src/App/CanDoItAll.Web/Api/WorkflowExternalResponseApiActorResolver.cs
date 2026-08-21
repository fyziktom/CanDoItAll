using System.Globalization;
using System.Security.Claims;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.Workflows.Abstractions;
using CanDoItAll.Infrastructure.ControlPlane;
using CanDoItAll.Modules.Workspace.ApiAccess;

namespace CanDoItAll.Web.Api;

internal sealed class WorkflowExternalResponseApiActorResolver(
    IDatabaseProfileRuntimeAccessor databaseProfileRuntimeAccessor,
    IAgentExecutionProfileGenerationSource profileGenerationSource,
    TimeProvider timeProvider)
{
    public WorkflowExternalResponseActorContext Resolve(ClaimsPrincipal principal)
    {
        ArgumentNullException.ThrowIfNull(principal);
        if (principal.Identity?.IsAuthenticated != true)
        {
            return new WorkflowExternalResponseActorContext.Unauthenticated();
        }

        var subjectId = principal.FindFirst("sub")?.Value ??
                        principal.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrWhiteSpace(subjectId))
        {
            return new WorkflowExternalResponseActorContext.Unauthenticated(
                "An authenticated principal with a stable subject is required.");
        }

        var nowUtc = timeProvider.GetUtcNow();
        if (!TryReadUnixTime(principal, "auth_time", out var authenticationTime) ||
            !TryReadUnixTime(principal, "iat", out var issuedAtTime) ||
            !TryReadUnixTime(principal, "exp", out var expiresAtUtc))
        {
            return new WorkflowExternalResponseActorContext.Unauthenticated(
                "The authenticated principal has invalid timestamps.");
        }

        var authenticatedAtUtc = authenticationTime ?? issuedAtTime ?? nowUtc;
        if (authenticatedAtUtc > nowUtc ||
            expiresAtUtc.HasValue &&
            (expiresAtUtc.Value <= nowUtc || expiresAtUtc.Value <= authenticatedAtUtc))
        {
            return new WorkflowExternalResponseActorContext.Unauthenticated(
                "The authenticated principal has invalid or expired timestamps.");
        }

        var profile = databaseProfileRuntimeAccessor.ResolveCurrentProfile().Profile;
        var scope = WorkspaceScopeDescriptor.Organization(profile.Id.ToString("N"));
        var capabilities = ApiAuthorizationPolicies.HasScope(
            principal,
            ApiAccessScopeNames.RespondWorkflows)
                ? WorkflowExternalResponseCallerCapabilities.SubmitHumanInput |
                  WorkflowExternalResponseCallerCapabilities.SubmitApprovalDecision
                : WorkflowExternalResponseCallerCapabilities.None;
        return new WorkflowExternalResponseActorContext.Authenticated(
            new WorkflowLaunchActor(WorkflowLaunchActorKind.User, subjectId),
            WorkflowExternalResponseTrustedChannel.Api,
            new WorkflowExternalResponseCallerAccess(
                profile.Id,
                profileGenerationSource.GetGeneration(),
                scope,
                [scope],
                capabilities,
                WorkflowExternalResponseAuthorizationPolicy.CurrentFingerprint,
                authenticatedAtUtc,
                expiresAtUtc));
    }

    private static bool TryReadUnixTime(
        ClaimsPrincipal principal,
        string claimType,
        out DateTimeOffset? timestamp)
    {
        var value = principal.FindFirst(claimType)?.Value;
        if (value is null)
        {
            timestamp = null;
            return true;
        }

        if (!long.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out var seconds))
        {
            timestamp = null;
            return false;
        }

        try
        {
            timestamp = DateTimeOffset.FromUnixTimeSeconds(seconds);
            return true;
        }
        catch (ArgumentOutOfRangeException)
        {
            timestamp = null;
            return false;
        }
    }
}
