using System.Globalization;
using System.Net;
using System.Security.Claims;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.Infrastructure.ControlPlane;
using CanDoItAll.Modules.Workspace.ApiAccess;
using CanDoItAll.Web;
using CanDoItAll.Web.Api;
using CanDoItAll.Web.Infrastructure;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Logging.Abstractions;

namespace CanDoItAll.Tests.Components.AgentFramework;

public sealed class AgentChatOwnerProviderTests {
    private const string Issuer = "agent-chat-owner-tests";

    [Fact]
    public async Task Authenticated_owner_survives_session_rotation_but_isolates_subject_and_issuer() {
        var original = await OwnerAsync(Principal("first", "session-a"));
        var refreshed = await OwnerAsync(Principal("first", "session-b"));
        var otherUser = await OwnerAsync(Principal("second", "session-a"));
        var otherIssuer = await OwnerAsync(Principal("first", "session-a", issuer: "other-issuer"), issuer: "other-issuer");

        Assert.False(original.IsEmpty);
        Assert.Equal(original, refreshed);
        Assert.NotEqual(original, otherUser);
        Assert.NotEqual(original, otherIssuer);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Trusted_local_owner_survives_new_circuit_identity(bool authorizationEnabled) {
        var first = LocalCircuit(authorizationEnabled, IPAddress.Loopback);
        var second = LocalCircuit(authorizationEnabled, IPAddress.Loopback);
        var firstPrincipal = await first.Provider.TryGetTrustedLocalOperatorAsync();
        var secondPrincipal = await second.Provider.TryGetTrustedLocalOperatorAsync();

        Assert.NotEqual(firstPrincipal!.FindFirstValue("jti"), secondPrincipal!.FindFirstValue("jti"));
        Assert.Equal(await first.Owner.GetOwnerAsync(), await second.Owner.GetOwnerAsync());
    }

    [Theory]
    [InlineData(true, false)]
    [InlineData(true, true)]
    [InlineData(false, true)]
    public async Task Enabling_either_authentication_mode_denies_a_shared_owner_to_untrusted_anonymous_circuits(
        bool authorizationEnabled, bool userAuthenticationEnabled) {
        var circuit = LocalCircuit(authorizationEnabled, IPAddress.Parse("192.0.2.10"), userAuthenticationEnabled);

        await Assert.ThrowsAsync<WebCurrentPrincipalDeniedException>(async () => await circuit.Owner.GetOwnerAsync());
    }

    [Fact]
    public async Task A_deployment_without_users_shares_a_distinct_workspace_owner_across_browser_addresses() {
        var first = await LocalCircuit(false, IPAddress.Parse("192.0.2.10")).Owner.GetOwnerAsync();
        var second = await LocalCircuit(false, IPAddress.Parse("192.0.2.11")).Owner.GetOwnerAsync();
        var loopback = await LocalCircuit(false, IPAddress.Loopback).Owner.GetOwnerAsync();
        var trustedLocal = await LocalCircuit(true, IPAddress.Loopback).Owner.GetOwnerAsync();
        var authenticated = await OwnerAsync(Principal("local-workspace", "session"));

        Assert.False(first.IsEmpty);
        Assert.Equal(first, second);
        Assert.Equal(first, loopback);
        Assert.NotEqual(first, trustedLocal);
        Assert.NotEqual(first, authenticated);
    }

    [Fact]
    public async Task Authenticated_subject_cannot_collide_with_trusted_local_operator() {
        var local = await LocalCircuit(true, IPAddress.Loopback).Owner.GetOwnerAsync();
        var authenticated = await OwnerAsync(Principal(LocalOperatorAuthenticationStateProvider.ActorId, "session"));

        Assert.NotEqual(local, authenticated);
    }

    [Theory]
    [InlineData(null, Issuer, false)]
    [InlineData("first", "untrusted-issuer", false)]
    [InlineData("first", Issuer, true)]
    public async Task Invalid_authenticated_identity_is_denied(string? subject, string issuer, bool expired) {
        await Assert.ThrowsAsync<WebCurrentPrincipalDeniedException>(async () =>
            await OwnerAsync(Principal(subject, "session", expired, issuer)));
    }

    [Fact]
    public async Task Revoked_managed_credential_cannot_recover_an_owner() {
        var credentialId = Guid.NewGuid();
        var principal = Principal("first", credentialId.ToString("N"));
        ((ClaimsIdentity)principal.Identity!).AddClaim(new(ApiManagedTokenClaims.Version, ApiManagedTokenClaims.CurrentVersion));
        var registry = new CredentialRegistry(new(credentialId, "first", "Revoked", DateTimeOffset.UtcNow.AddHours(-1),
            DateTimeOffset.UtcNow.AddHours(1), [], RevokedAtUtc: DateTimeOffset.UtcNow));

        await Assert.ThrowsAsync<WebCurrentPrincipalDeniedException>(async () => await OwnerAsync(principal, tokens: registry));
    }

    [Fact]
    public async Task Authentication_changes_notify_recovery_and_disposal_detaches_the_observer() {
        var circuit = LocalCircuit(true, IPAddress.Loopback);
        var original = await circuit.Owner.GetOwnerAsync();
        var notifications = 0;
        circuit.Owner.Changed += (_, _) => notifications++;

        circuit.Provider.SetAuthenticationState(Task.FromResult(new AuthenticationState(Principal("replacement", "new-session"))));

        Assert.Equal(1, notifications);
        Assert.NotEqual(original, await circuit.Owner.GetOwnerAsync());
        circuit.Owner.Dispose();
        circuit.Provider.SetAuthenticationState(Task.FromResult(new AuthenticationState(new ClaimsPrincipal(new ClaimsIdentity()))));
        Assert.Equal(1, notifications);
    }

    private static async ValueTask<AgentChatOwnerId> OwnerAsync(ClaimsPrincipal principal, string issuer = Issuer,
        IApiTokenRegistry? tokens = null) {
        var http = new HttpContextAccessor { HttpContext = new DefaultHttpContext { User = principal } };
        return await Create(new AnonymousInteractiveAccessPrincipalProvider(), http, OptionsFor(true, issuer), tokens).GetOwnerAsync();
    }

    private static (LocalOperatorAuthenticationStateProvider Provider, WebAgentChatOwnerProvider Owner) LocalCircuit(
        bool authorizationEnabled, IPAddress remote, bool userAuthenticationEnabled = false) {
        var http = new HttpContextAccessor { HttpContext = new DefaultHttpContext() };
        http.HttpContext.Connection.RemoteIpAddress = remote;
        http.HttpContext.Items[DevelopmentEndpointAccess.OriginalRemoteIpItemKey] = remote;
        var options = OptionsFor(authorizationEnabled, userAuthenticationEnabled: userAuthenticationEnabled);
        var provider = new LocalOperatorAuthenticationStateProvider(http, options, Options.Create(new LocalOperatorUiOptions()));
        provider.SetAuthenticationState(Task.FromResult(new AuthenticationState(new ClaimsPrincipal(new ClaimsIdentity()))));
        return (provider, Create(provider, http, options));
    }

    private static WebAgentChatOwnerProvider Create(IInteractiveAccessPrincipalProvider interactive, IHttpContextAccessor http,
        IOptions<ApiAccessOptions> options, IApiTokenRegistry? tokens = null)
        => new(new WebCurrentPrincipalResolver(interactive, http, authorization: null!, tokens ?? new CredentialRegistry(null),
            sessions: null!, options, TimeProvider.System), options, NullLogger<WebAgentChatOwnerProvider>.Instance,
            interactive as AuthenticationStateProvider);

    private static IOptions<ApiAccessOptions> OptionsFor(bool enabled, string issuer = Issuer, bool userAuthenticationEnabled = false)
        => Options.Create(new ApiAccessOptions {
            Authorization = new ApiAuthorizationOptions { Enabled = enabled, Issuer = issuer },
            UserAuthentication = new ApiUserAuthenticationOptions { Enabled = userAuthenticationEnabled }
        });

    private static ClaimsPrincipal Principal(string? subject, string session, bool expired = false, string issuer = Issuer) {
        List<Claim> claims = [
            new("iss", issuer),
            new("jti", session),
            new("exp", DateTimeOffset.UtcNow.AddHours(expired ? -1 : 1).ToUnixTimeSeconds().ToString(CultureInfo.InvariantCulture))
        ];
        if (subject is not null) {
            claims.Add(new("sub", subject));
        }
        return new(new ClaimsIdentity(claims, "Bearer"));
    }

    private sealed class CredentialRegistry(ApiTokenRecord? record) : IApiTokenRegistry {
        public Task<ApiTokenRecord?> FindAsync(Guid id, CancellationToken cancellationToken = default) => Task.FromResult(record);
        public void Register(ApiTokenRecord token) => throw new NotSupportedException();
        public Task PrepareCurrentReadAsync(CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public ValueTask<ApiTokenCurrentObservation> ObserveCurrentAsync(Guid id, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();
        public Task<ApiTokenPage> SearchAsync(ApiTokenQuery query, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task RevokeAsync(Guid id, DateTimeOffset revokedAtUtc, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task DeleteAsync(Guid id, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }
}
