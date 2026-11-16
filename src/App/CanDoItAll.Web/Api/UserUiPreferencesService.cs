using System.Security.Claims;
using CanDoItAll.Infrastructure.ControlPlane;
using CanDoItAll.Modules.Workspace.ApiAccess;
using CanDoItAll.SharedKernel;
using Microsoft.Extensions.Options;

namespace CanDoItAll.Web.Api;

public sealed record UserUiPreferencesSnapshot(UserUiPreferencesAccount Account, UserUiPreferences Preferences);

public sealed class UserUiPreferencesService(IHttpContextAccessor http, IApiTokenRegistry tokens,
    ApiSessionService sessions, IOptions<ApiAccessOptions> options, IClock clock, IUserUiPreferencesStore store) {
    private ApiAuthenticationObservation? authentication = http.HttpContext?.Features.Get<ApiAuthenticationObservation>();

    public async Task<UserUiPreferencesSnapshot?> ReadAsync(ClaimsPrincipal principal, CancellationToken cancellationToken = default) {
        var authority = await ResolveAsync(principal, cancellationToken);
        if (authority is null) {
            return null;
        }
        var preferences = await store.ReadAsync(authority.Value.Account, cancellationToken);
        await RequireCurrentAsync(principal, authority.Value.Authentication, cancellationToken);
        return new(authority.Value.Account, preferences);
    }

    public async Task<UserUiPreferencesSnapshot?> UpdateAsync(ClaimsPrincipal principal, UserUiPreferencesPatch patch,
        CancellationToken cancellationToken = default) {
        ArgumentNullException.ThrowIfNull(patch);
        patch.Validate();
        var authority = await ResolveAsync(principal, cancellationToken);
        if (authority is null) {
            return null;
        }
        var preferences = await store.UpdateAsync(authority.Value.Account, patch,
            token => RequireCurrentAsync(principal, authority.Value.Authentication, token), cancellationToken);
        await RequireCurrentAsync(principal, authority.Value.Authentication, cancellationToken);
        return new(authority.Value.Account, preferences);
    }

    private async Task<(UserUiPreferencesAccount Account, ApiAuthenticationObservation Authentication)?> ResolveAsync(
        ClaimsPrincipal principal, CancellationToken cancellationToken) {
        ArgumentNullException.ThrowIfNull(principal);
        cancellationToken.ThrowIfCancellationRequested();
        if (!principal.Identities.Any(identity => identity.IsAuthenticated)) {
            return null;
        }
        if (http.HttpContext?.Features.Get<ApiAuthenticationObservation>() is { Session: not null } observed &&
            observed.MatchesPrincipal(principal)) {
            authentication = observed;
        }
        var original = authentication;
        if (original?.Session is null) {
            throw Denied();
        }
        await RequireCurrentAsync(principal, original, cancellationToken);
        var identity = original.Session.Identity;
        var account = identity.IsAdministrator && identity.UserId is null
            ? UserUiPreferencesAccount.ConfiguredAdministrator
            : identity.UserId is { } id && !identity.IsAdministrator
                ? UserUiPreferencesAccount.ForUser(id)
                : throw Denied();
        return (account, original);
    }

    private async Task RequireCurrentAsync(ClaimsPrincipal principal, ApiAuthenticationObservation original,
        CancellationToken cancellationToken) {
        cancellationToken.ThrowIfCancellationRequested();
        var configuration = options.Value;
        if (!configuration.Enabled || !configuration.UserAuthentication.Enabled || !configuration.Authorization.Enabled ||
            configuration.Authorization.Issuer != original.Issuer || !original.MatchesPrincipal(principal) ||
            original.Credential is not { Kind: ApiCredentialKind.UserSession or ApiCredentialKind.AdministratorSession } expected) {
            throw Denied();
        }
        var observation = await tokens.ObserveCurrentAsync(expected.Id, cancellationToken);
        if (observation is ApiTokenCurrentObservation.Unavailable) {
            throw new IOException("The current UI preference account authority is unavailable.");
        }
        if (observation is not ApiTokenCurrentObservation.Found found ||
            found.Record.GetStatus(clock.GetUtcNow()) != ApiTokenStatus.Active ||
            !ApiAuthenticationObservation.MatchesCredential(expected, found.Record) ||
            await sessions.ResolveIdentityAsync(found.Record, cancellationToken) is null ||
            !original.MatchesPrincipal(principal)) {
            throw Denied();
        }
    }

    private static UnauthorizedAccessException Denied() => new("The current session cannot access account UI preferences.");
}
