using System.Net;
using System.Security.Claims;
using CanDoItAll.Modules.Workspace.ApiAccess;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.Extensions.Options;

namespace CanDoItAll.Web.Infrastructure;

internal interface IInteractiveAccessPrincipalProvider
{
    bool IsAvailable { get; }

    ValueTask<ClaimsPrincipal> GetCurrentAsync(CancellationToken cancellationToken = default);
}

internal sealed class AnonymousInteractiveAccessPrincipalProvider : IInteractiveAccessPrincipalProvider
{
    public bool IsAvailable => false;

    public ValueTask<ClaimsPrincipal> GetCurrentAsync(
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return ValueTask.FromResult(new ClaimsPrincipal(new ClaimsIdentity()));
    }
}

internal sealed class LocalOperatorAuthenticationStateProvider(
    IHttpContextAccessor httpContextAccessor,
    IOptions<ApiAccessOptions> apiOptions,
    IOptions<LocalOperatorUiOptions> uiOptions) :
    AuthenticationStateProvider,
    IHostEnvironmentAuthenticationStateProvider,
    IInteractiveAccessPrincipalProvider
{
    internal const string ActorId = "local-operator";
    internal const string AuthenticationType = "CanDoItAll.LocalOperator";

    private readonly ClaimsPrincipal localOperator = CreateLocalOperator();
    private readonly HashSet<IPAddress> trustedAddresses = uiOptions.Value.TrustedAddresses
        .Select(value => LocalOperatorUiOptions.Normalize(IPAddress.Parse(value)))
        .ToHashSet();
    private Task<AuthenticationState>? authenticationStateTask;
    private Task<ClaimsPrincipal>? accessPrincipalTask;
    private bool? isTrustedCircuit;

    public bool IsAvailable => accessPrincipalTask is not null;

    public override Task<AuthenticationState> GetAuthenticationStateAsync() =>
        authenticationStateTask ?? throw new InvalidOperationException(
            "The host has not supplied the interactive authentication state.");

    public async ValueTask<ClaimsPrincipal> GetCurrentAsync(
        CancellationToken cancellationToken = default)
    {
        var currentTask = accessPrincipalTask ?? throw new InvalidOperationException(
            "The host has not supplied the interactive authentication state.");
        return await currentTask.WaitAsync(cancellationToken);
    }

    public void SetAuthenticationState(Task<AuthenticationState> authenticationStateTask)
    {
        ArgumentNullException.ThrowIfNull(authenticationStateTask);
        isTrustedCircuit ??= IsTrustedRequest(httpContextAccessor.HttpContext);
        this.authenticationStateTask = authenticationStateTask;
        accessPrincipalTask = ResolveAccessPrincipalAsync(
            authenticationStateTask,
            apiOptions.Value.Authorization.Enabled,
            isTrustedCircuit.Value,
            localOperator);
        NotifyAuthenticationStateChanged(this.authenticationStateTask);
    }

    private static async Task<ClaimsPrincipal> ResolveAccessPrincipalAsync(
        Task<AuthenticationState> authenticationStateTask,
        bool authorizationEnabled,
        bool isTrustedCircuit,
        ClaimsPrincipal localOperator)
    {
        var authenticationState = await authenticationStateTask.ConfigureAwait(false);
        return authorizationEnabled &&
               isTrustedCircuit &&
               authenticationState.User.Identity?.IsAuthenticated != true
            ? localOperator
            : authenticationState.User;
    }

    private bool IsTrustedRequest(HttpContext? httpContext) {
        if (httpContext is null) {
            return false;
        }

        var originalRemoteIp = httpContext.Items[DevelopmentEndpointAccess.OriginalRemoteIpItemKey]
            as IPAddress;
        return IsTrustedAddress(originalRemoteIp) &&
               IsTrustedAddress(httpContext.Connection.RemoteIpAddress);
    }

    private bool IsTrustedAddress(IPAddress? address) => address is not null &&
        (IPAddress.IsLoopback(address) || trustedAddresses.Contains(LocalOperatorUiOptions.Normalize(address)));

    private static ClaimsPrincipal CreateLocalOperator()
    {
        var sessionId = $"circuit-{Guid.NewGuid():N}";
        var identity = new ClaimsIdentity(
            [
                new Claim("sub", ActorId),
                new Claim(ClaimTypes.NameIdentifier, ActorId),
                new Claim("jti", sessionId),
                new Claim("auth_rev", "0"),
                new Claim(
                    "scope",
                    string.Join(
                        ' ',
                        ApiAccessScopeNames.ReadLlmChats,
                        ApiAccessScopeNames.ManageLlmChats,
                        ApiAccessScopeNames.ExecuteLlmChats))
            ],
            AuthenticationType);
        return new ClaimsPrincipal(identity);
    }
}
