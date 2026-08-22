using System.Globalization;
using System.Security.Claims;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.Tooling;
using CanDoItAll.AgentFramework.Workflows.Abstractions;
using CanDoItAll.Infrastructure.ControlPlane;
using CanDoItAll.Modules.Workspace.ApiAccess;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.Extensions.Options;

namespace CanDoItAll.Modules.AgentFramework;

public interface IWorkflowExternalResponseActorContextFactory
{
    WorkflowExternalResponseActorContext CreateLocalOperator();

    Task<WorkflowExternalResponseActorContext> CreateAgentAsync(
        AgentRuntimeToolProviderContext context,
        CancellationToken cancellationToken = default);
}

public interface IWorkflowExternalResponsePageActorContextProvider
{
    ValueTask<WorkflowExternalResponseActorContext> GetCurrentAsync(
        CancellationToken cancellationToken = default);
}

internal sealed class WorkflowExternalResponseActorContextFactory(
    IDatabaseProfileRuntimeAccessor databaseProfileRuntimeAccessor,
    IAgentExecutionProfileGenerationSource profileGenerationSource,
    WorkflowAgentRuntimeAuthorizationService agentAuthorizationService,
    TimeProvider timeProvider) : IWorkflowExternalResponseActorContextFactory
{
    private const string LocalOperatorSubjectId = "local-workflow-operator";

    public WorkflowExternalResponseActorContext CreateLocalOperator()
    {
        var currentProfile = databaseProfileRuntimeAccessor.ResolveCurrentProfile().Profile;
        var generation = profileGenerationSource.GetGeneration();
        var organizationScope = WorkspaceScopeDescriptor.Organization(currentProfile.Id.ToString("N"));
        return new WorkflowExternalResponseActorContext.Authenticated(
            new WorkflowLaunchActor(WorkflowLaunchActorKind.User, LocalOperatorSubjectId),
            WorkflowExternalResponseTrustedChannel.LocalOperator,
            new WorkflowExternalResponseCallerAccess(
                currentProfile.Id,
                generation,
                organizationScope,
                [organizationScope],
                WorkflowExternalResponseCallerCapabilities.SubmitHumanInput |
                WorkflowExternalResponseCallerCapabilities.SubmitApprovalDecision,
                WorkflowExternalResponseAuthorizationPolicy.CurrentFingerprint,
                timeProvider.GetUtcNow()));
    }

    public async Task<WorkflowExternalResponseActorContext> CreateAgentAsync(
        AgentRuntimeToolProviderContext context,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentException.ThrowIfNullOrWhiteSpace(context.RuntimeSessionKey);
        var governance = context.Governance
            ?? throw new UnauthorizedAccessException(
                "Workflow response submission requires admitted agent governance.");

        await agentAuthorizationService.EnsureToolInvocationAuthorizedAsync(
            context.Agent.Id,
            AgentToolInvocationPolicyMetadata.WorkflowsExternalResponseSubmit,
            cancellationToken);

        if (governance.AgentId != context.Agent.Id ||
            !governance.MutationAllowed ||
            governance.AllowedCapabilityKeys.Count > 0 &&
            !governance.AllowedCapabilityKeys.Contains(WorkflowRuntimeCapabilityKeys.ExternalResponseSubmit))
        {
            throw new UnauthorizedAccessException(
                "The admitted agent governance does not authorize workflow response submission.");
        }

        var currentProfile = databaseProfileRuntimeAccessor.ResolveCurrentProfile().Profile;
        var currentGeneration = profileGenerationSource.GetGeneration();
        if (governance.DatabaseProfileId != currentProfile.Id ||
            governance.DatabaseProfileGeneration != currentGeneration)
        {
            throw new UnauthorizedAccessException(
                "The admitted agent governance does not match the current database profile.");
        }

        var organizationScope = WorkspaceScopeDescriptor.Organization(currentProfile.Id.ToString("N"));
        return new WorkflowExternalResponseActorContext.Authenticated(
            new WorkflowLaunchActor(
                WorkflowLaunchActorKind.Agent,
                context.Agent.Id.ToString("D")),
            WorkflowExternalResponseTrustedChannel.AgentTool,
            new WorkflowExternalResponseCallerAccess(
                currentProfile.Id,
                currentGeneration,
                organizationScope,
                [governance.WorkspaceScope],
                WorkflowExternalResponseCallerCapabilities.SubmitHumanInput,
                WorkflowExternalResponseAuthorizationPolicy.CurrentFingerprint,
                timeProvider.GetUtcNow()));
    }
}

internal sealed class WorkflowExternalResponsePageActorContextProvider(
    AuthenticationStateProvider authenticationStateProvider,
    IOptions<ApiAccessOptions> apiAccessOptions,
    IWorkflowExternalResponseActorContextFactory actorContextFactory,
    IDatabaseProfileRuntimeAccessor databaseProfileRuntimeAccessor,
    IAgentExecutionProfileGenerationSource profileGenerationSource,
    TimeProvider timeProvider) : IWorkflowExternalResponsePageActorContextProvider
{
    public async ValueTask<WorkflowExternalResponseActorContext> GetCurrentAsync(
        CancellationToken cancellationToken = default)
    {
        if (!apiAccessOptions.Value.Authorization.Enabled)
        {
            return actorContextFactory.CreateLocalOperator();
        }

        var authenticationState = await authenticationStateProvider
            .GetAuthenticationStateAsync()
            .WaitAsync(cancellationToken);
        var principal = authenticationState.User;
        if (principal.Identity?.IsAuthenticated != true)
        {
            return new WorkflowExternalResponseActorContext.Unauthenticated();
        }

        var subjectId = principal.FindFirst("sub")?.Value ??
            principal.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrWhiteSpace(subjectId))
        {
            return new WorkflowExternalResponseActorContext.Unauthenticated(
                "The authenticated workflow response session has no stable subject identifier.");
        }

        var now = timeProvider.GetUtcNow();
        if (!TryReadUnixTime(principal, "auth_time", out var authenticationTime) ||
            !TryReadUnixTime(principal, "iat", out var issuedAtTime) ||
            !TryReadUnixTime(principal, "exp", out var expiresAtUtc))
        {
            return new WorkflowExternalResponseActorContext.Unauthenticated(
                "The workflow response session timestamps are invalid.");
        }

        var authenticatedAtUtc = authenticationTime ??
            issuedAtTime ??
            now;
        if (authenticatedAtUtc > now ||
            expiresAtUtc.HasValue && expiresAtUtc.Value <= authenticatedAtUtc)
        {
            return new WorkflowExternalResponseActorContext.Unauthenticated(
                "The workflow response session timestamps are invalid.");
        }

        var currentProfile = databaseProfileRuntimeAccessor.ResolveCurrentProfile().Profile;
        var organizationScope = WorkspaceScopeDescriptor.Organization(currentProfile.Id.ToString("N"));
        return new WorkflowExternalResponseActorContext.Authenticated(
            new WorkflowLaunchActor(WorkflowLaunchActorKind.User, subjectId),
            WorkflowExternalResponseTrustedChannel.Api,
            new WorkflowExternalResponseCallerAccess(
                currentProfile.Id,
                profileGenerationSource.GetGeneration(),
                organizationScope,
                [organizationScope],
                WorkflowExternalResponseCallerCapabilities.SubmitHumanInput |
                WorkflowExternalResponseCallerCapabilities.SubmitApprovalDecision,
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

internal sealed class WorkflowLaunchAuthorizationScopeResolver(
    IDatabaseProfileRuntimeAccessor databaseProfileRuntimeAccessor) :
    IWorkflowLaunchAuthorizationScopeResolver
{
    public WorkflowLaunchAuthorizationScope Resolve(WorkflowLaunchOrigin origin)
    {
        ArgumentNullException.ThrowIfNull(origin);
        if (!string.IsNullOrWhiteSpace(origin.AuthorizationPolicyFingerprint) &&
            !string.Equals(
                origin.AuthorizationPolicyFingerprint,
                WorkflowExternalResponseAuthorizationPolicy.CurrentFingerprint,
                StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                "The workflow launch authorization policy is not supported by this host.");
        }

        var currentProfile = databaseProfileRuntimeAccessor.ResolveCurrentProfile().Profile;
        var organizationScope = WorkspaceScopeDescriptor.Organization(currentProfile.Id.ToString("N"));
        var scope = ResolveScope(origin, organizationScope);
        if (scope.Kind == WorkspaceScopeKind.Organization && scope != organizationScope)
        {
            throw new InvalidOperationException(
                "The workflow launch authorization scope does not match the current database profile.");
        }

        return new WorkflowLaunchAuthorizationScope(
            scope,
            WorkflowExternalResponseAuthorizationPolicy.CurrentFingerprint);
    }

    private static WorkspaceScopeDescriptor ResolveScope(
        WorkflowLaunchOrigin origin,
        WorkspaceScopeDescriptor organizationScope)
    {
        return origin switch
        {
            WorkflowLaunchOrigin.AgentRuntimeInvocation =>
                origin.AuthorizationScope ?? throw new InvalidOperationException(
                    "An agent workflow launch requires its admitted governance scope."),
            WorkflowLaunchOrigin.ProjectStructureNode project =>
                ResolveExactScope(
                    origin.AuthorizationScope,
                    WorkspaceScopeDescriptor.Project(project.ProjectId.ToString("D"))),
            WorkflowLaunchOrigin.ProcessAssignment process =>
                ResolveExactScope(
                    origin.AuthorizationScope,
                    WorkspaceScopeDescriptor.Process(process.ProcessRunId.ToString("D"))),
            _ => origin.AuthorizationScope ?? organizationScope
        };
    }

    private static WorkspaceScopeDescriptor ResolveExactScope(
        WorkspaceScopeDescriptor? supplied,
        WorkspaceScopeDescriptor expected)
    {
        if (supplied is not null && supplied != expected)
        {
            throw new InvalidOperationException(
                "The workflow launch authorization scope does not match its persisted origin.");
        }

        return expected;
    }
}

internal static class WorkflowExternalResponseCallerRequestFactory
{
    private const string UiIdentityPrefix = "workflow-ui";

    public static WorkflowExternalResponseIdempotencyKey CreateUiIdempotencyKey(
        WorkflowExternalRequestId requestId,
        WorkflowExternalRequestVersion requestVersion)
        => new($"{UiIdentityPrefix}:{requestId.Value:D}:{requestVersion.Value}");

    public static WorkflowLaunchCorrelationId CreateUiCorrelationId(
        WorkflowExternalRequestId requestId,
        WorkflowExternalRequestVersion requestVersion)
        => new($"{UiIdentityPrefix}:{requestId.Value:D}:{requestVersion.Value}");
}
