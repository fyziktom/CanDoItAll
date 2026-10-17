using CanDoItAll.Infrastructure.ControlPlane;
using CanDoItAll.Modules.Projects;
using CanDoItAll.Modules.Workspace.ApiAccess;
using Microsoft.Extensions.Options;

namespace CanDoItAll.Web.Api;

internal sealed class ProjectDefinitionCurrentWriteAccess(HttpContext context, ApiAuthenticationObservation? original,
    IApiTokenRegistry tokens, IApiUserStore users, ApiSessionService sessions, IOptions<ApiAccessOptions> options,
    TimeProvider clock) : IProjectDefinitionCurrentWriteAccess {
    internal static readonly TimeSpan ObservationBudget = TimeSpan.FromSeconds(1);

    public async ValueTask<ProjectDefinitionWriteAccessDecision> ObserveAsync(CancellationToken cancellationToken) {
        cancellationToken.ThrowIfCancellationRequested();
        using var budget = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        budget.CancelAfter(ObservationBudget);
        try {
            var configuration = ObserveConfiguration();
            if (configuration != ProjectDefinitionWriteAccessDecision.Allowed) {
                return configuration;
            }
            if (original?.Credential is { } expected) {
                var observation = await tokens.ObserveCurrentAsync(expected.Id, budget.Token);
                RequireWithinBudget();
                if (observation is ApiTokenCurrentObservation.Unavailable) {
                    return ProjectDefinitionWriteAccessDecision.Unavailable;
                }
                if (observation is not ApiTokenCurrentObservation.Found found) {
                    return ProjectDefinitionWriteAccessDecision.Denied;
                }
                var current = found.Record;
                if (current.Id != expected.Id || current.Subject != expected.Subject || current.Kind != expected.Kind || current.UserId != expected.UserId) {
                    return ProjectDefinitionWriteAccessDecision.CallerChanged;
                }
                if (current.GetStatus(clock.GetUtcNow()) != ApiTokenStatus.Active ||
                        !ApiAuthenticationObservation.HasRequiredGrants(current) || !ApiAuthenticationObservation.MatchesCredential(expected, current)) {
                    return ProjectDefinitionWriteAccessDecision.Denied;
                }
                if (original.Session?.Generation is { } generation) {
                    var authority = await users.ObserveAuthorityAsync(generation, budget.Token);
                    RequireWithinBudget();
                    if (authority is not ApiUserAuthorityObservation.Current) {
                        return authority is ApiUserAuthorityObservation.Changed
                            ? ProjectDefinitionWriteAccessDecision.AuthorityChanged : ProjectDefinitionWriteAccessDecision.Unavailable;
                    }
                }
            }
            RequireWithinBudget();
            return ObserveConfiguration();
        } catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested && budget.IsCancellationRequested) {
            return ProjectDefinitionWriteAccessDecision.Unavailable;
        } catch (Exception exception) when (exception is IOException or UnauthorizedAccessException) {
            cancellationToken.ThrowIfCancellationRequested();
            return ProjectDefinitionWriteAccessDecision.Unavailable;
        }

        void RequireWithinBudget() {
            cancellationToken.ThrowIfCancellationRequested();
            budget.Token.ThrowIfCancellationRequested();
        }
    }

    private ProjectDefinitionWriteAccessDecision ObserveConfiguration() {
        var configured = options.Value;
        if (!configured.Enabled) {
            return ProjectDefinitionWriteAccessDecision.Denied;
        }
        if (original is null) {
            return configured.Authorization.Enabled ? ProjectDefinitionWriteAccessDecision.CallerChanged : ProjectDefinitionWriteAccessDecision.Allowed;
        }
        if (!configured.Authorization.Enabled || configured.Authorization.Issuer != original.Issuer || !original.MatchesPrincipal(context.User)) {
            return ProjectDefinitionWriteAccessDecision.CallerChanged;
        }
        if (original.ExpiresAt <= clock.GetUtcNow().ToUnixTimeSeconds() || !original.HasProjectsAccess ||
                original.Session is { } session && !sessions.IsAuthoritySnapshotCurrent(session, original.Credential!)) {
            return ProjectDefinitionWriteAccessDecision.Denied;
        }
        return ProjectDefinitionWriteAccessDecision.Allowed;
    }
}
