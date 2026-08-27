using System.Net;
using System.Security.Claims;
using CanDoItAll.Composition;
using CanDoItAll.FileTools.Integration;
using CanDoItAll.Infrastructure.ControlPlane;
using CanDoItAll.Modules.Workspace.ApiAccess;
using CanDoItAll.Web;
using CanDoItAll.Web.Api;
using CanDoItAll.Web.Infrastructure;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace CanDoItAll.Tests.Components.Shell;

public sealed class LocalOperatorAuthenticationStateProviderTests
{
    [Fact]
    public async Task Anonymous_loopback_circuit_receives_stable_scoped_local_operator_identity()
    {
        var provider = CreateProvider(
            authorizationEnabled: true,
            IPAddress.Loopback,
            IPAddress.Loopback,
            out _);
        SetAuthenticationState(provider, AnonymousPrincipal());

        var source = await provider.GetAuthenticationStateAsync();
        var first = await provider.GetCurrentAsync();
        var second = await provider.GetCurrentAsync();

        Assert.False(source.User.Identity?.IsAuthenticated);
        Assert.True(first.Identity?.IsAuthenticated);
        Assert.Equal(
            LocalOperatorAuthenticationStateProvider.AuthenticationType,
            first.Identity?.AuthenticationType);
        Assert.Equal(
            LocalOperatorAuthenticationStateProvider.ActorId,
            first.FindFirstValue("sub"));
        Assert.Equal(first.FindFirstValue("jti"), second.FindFirstValue("jti"));
        Assert.Equal("0", first.FindFirstValue("auth_rev"));
        Assert.True(ApiAuthorizationPolicies.HasScope(first, ApiAccessScopeNames.ReadLlmChats));
        Assert.True(ApiAuthorizationPolicies.HasScope(first, ApiAccessScopeNames.ManageLlmChats));
        Assert.True(ApiAuthorizationPolicies.HasScope(first, ApiAccessScopeNames.ExecuteLlmChats));
        Assert.False(ApiAuthorizationPolicies.HasScope(first, ApiAccessScopeNames.Api));
        Assert.False(ApiAuthorizationPolicies.HasScope(first, ApiAccessScopeNames.RespondWorkflows));
    }

    [Theory]
    [InlineData("192.0.2.10", "127.0.0.1")]
    [InlineData("127.0.0.1", "192.0.2.10")]
    public async Task Both_transport_addresses_must_be_loopback_to_elevate_a_circuit(
        string originalRemoteIp,
        string effectiveRemoteIp)
    {
        var provider = CreateProvider(
            authorizationEnabled: true,
            IPAddress.Parse(originalRemoteIp),
            IPAddress.Parse(effectiveRemoteIp),
            out _);
        SetAuthenticationState(provider, AnonymousPrincipal());

        var principal = await provider.GetCurrentAsync();

        Assert.False(principal.Identity?.IsAuthenticated);
    }

    [Fact]
    public async Task Missing_http_context_does_not_elevate_an_anonymous_circuit()
    {
        var provider = CreateProvider(
            authorizationEnabled: true,
            IPAddress.Loopback,
            IPAddress.Loopback,
            out var accessor);
        accessor.HttpContext = null;
        SetAuthenticationState(provider, AnonymousPrincipal());

        var principal = await provider.GetCurrentAsync();

        Assert.False(principal.Identity?.IsAuthenticated);
    }

    [Fact]
    public async Task Authenticated_principal_is_preserved_without_adding_local_scopes()
    {
        var provider = CreateProvider(
            authorizationEnabled: true,
            IPAddress.Loopback,
            IPAddress.Loopback,
            out _);
        var bearer = AuthenticatedPrincipal("api-user", "api-session", ApiAccessScopeNames.Api);
        SetAuthenticationState(provider, bearer);

        var state = await provider.GetAuthenticationStateAsync();
        var accessPrincipal = await provider.GetCurrentAsync();

        Assert.Same(bearer, state.User);
        Assert.Same(bearer, accessPrincipal);
        Assert.False(ApiAuthorizationPolicies.HasScope(accessPrincipal, ApiAccessScopeNames.ReadLlmChats));
    }

    [Fact]
    public async Task Disabled_authorization_preserves_the_anonymous_loopback_principal()
    {
        var provider = CreateProvider(
            authorizationEnabled: false,
            IPAddress.Loopback,
            IPAddress.Loopback,
            out _);
        SetAuthenticationState(provider, AnonymousPrincipal());

        var principal = await provider.GetCurrentAsync();

        Assert.False(principal.Identity?.IsAuthenticated);
    }

    [Fact]
    public async Task Local_operator_identity_satisfies_the_existing_exact_chat_policies()
    {
        var provider = CreateProvider(
            authorizationEnabled: true,
            IPAddress.Loopback,
            IPAddress.Loopback,
            out _);
        SetAuthenticationState(provider, AnonymousPrincipal());
        var principal = await provider.GetCurrentAsync();
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Api:Authorization:Enabled"] = "true",
                ["Api:Authorization:SigningKey"] = "0123456789abcdef0123456789abcdef"
            })
            .Build();
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddCanDoItAllApi(configuration);

        using var serviceProvider = services.BuildServiceProvider();
        var authorization = serviceProvider.GetRequiredService<IAuthorizationService>();

        Assert.True((await authorization.AuthorizeAsync(
            principal,
            ApiAuthorizationPolicies.ReadLlmChats)).Succeeded);
        Assert.True((await authorization.AuthorizeAsync(
            principal,
            ApiAuthorizationPolicies.ManageLlmChats)).Succeeded);
        Assert.True((await authorization.AuthorizeAsync(
            principal,
            ApiAuthorizationPolicies.ExecuteLlmChats)).Succeeded);
    }

    [Fact]
    public void Local_operator_registration_supplies_the_same_host_and_ui_provider()
    {
        var services = new ServiceCollection();
        services.AddCanDoItAllInteractiveServer(detailedErrors: true);
        services.AddSingleton<IConfiguration>(new ConfigurationBuilder().Build());
        services.AddSingleton(Options.Create(new ApiAccessOptions()));
        services.AddSingleton(InteractiveHostProfile());
        services.AddCanDoItAllLocalOperatorUiAuthentication();

        using var serviceProvider = services.BuildServiceProvider();
        using var scope = serviceProvider.CreateScope();
        var authenticationStateProvider = scope.ServiceProvider
            .GetRequiredService<AuthenticationStateProvider>();
        var hostProvider = scope.ServiceProvider
            .GetRequiredService<IHostEnvironmentAuthenticationStateProvider>();
        var accessPrincipalProvider = scope.ServiceProvider
            .GetRequiredService<IInteractiveAccessPrincipalProvider>();

        Assert.IsType<LocalOperatorAuthenticationStateProvider>(authenticationStateProvider);
        Assert.Same(authenticationStateProvider, hostProvider);
        Assert.Same(authenticationStateProvider, accessPrincipalProvider);
    }

    [Fact]
    public async Task File_context_uses_the_stable_circuit_identity_without_an_http_request()
    {
        var authenticationStateProvider = CreateProvider(
            authorizationEnabled: true,
            IPAddress.Loopback,
            IPAddress.Loopback,
            out var httpContextAccessor);
        SetAuthenticationState(authenticationStateProvider, AnonymousPrincipal());
        httpContextAccessor.HttpContext = null;
        var runtimeDatabase = new StubCanonicalRuntimeDatabase();
        var provider = new HttpFileAccessContextProvider(
            httpContextAccessor,
            EnabledApiOptions(),
            runtimeDatabase,
            authenticationStateProvider);

        var first = await provider.GetCurrentAsync();
        var second = await provider.GetCurrentAsync();

        Assert.Equal(LocalOperatorAuthenticationStateProvider.ActorId, first.ActorId.Value);
        Assert.Equal(first.SessionId, second.SessionId);
        Assert.Equal(runtimeDatabase.Profile.Profile.Id, first.RuntimeProfileId);
        Assert.Equal(runtimeDatabase.Generation, first.RuntimeGeneration);
    }

    [Fact]
    public async Task Anonymous_hub_request_uses_the_initialized_circuit_identity()
    {
        var authenticationStateProvider = CreateProvider(
            authorizationEnabled: true,
            IPAddress.Loopback,
            IPAddress.Loopback,
            out var httpContextAccessor);
        SetAuthenticationState(authenticationStateProvider, AnonymousPrincipal());
        httpContextAccessor.HttpContext = new DefaultHttpContext();
        var provider = new HttpFileAccessContextProvider(
            httpContextAccessor,
            EnabledApiOptions(),
            new StubCanonicalRuntimeDatabase(),
            authenticationStateProvider);

        var context = await provider.GetCurrentAsync();

        Assert.Equal(LocalOperatorAuthenticationStateProvider.ActorId, context.ActorId.Value);
        Assert.Equal(
            (await authenticationStateProvider.GetCurrentAsync()).FindFirstValue("jti"),
            context.SessionId.Value);
    }

    [Fact]
    public async Task Anonymous_http_request_without_interactive_state_is_denied()
    {
        var httpContextAccessor = new HttpContextAccessor
        {
            HttpContext = new DefaultHttpContext()
        };
        var provider = new HttpFileAccessContextProvider(
            httpContextAccessor,
            EnabledApiOptions(),
            new StubCanonicalRuntimeDatabase(),
            new AnonymousInteractiveAccessPrincipalProvider());

        var exception = await Assert.ThrowsAsync<FileAccessDeniedException>(async () =>
            await provider.GetCurrentAsync());

        Assert.Equal(FileAccessFailureCode.Forbidden, exception.Code);
        Assert.Equal("An authenticated file access context is required.", exception.Message);
    }

    [Fact]
    public async Task Initialized_circuit_identity_takes_precedence_over_retained_http_identity()
    {
        var authenticationStateProvider = CreateProvider(
            authorizationEnabled: true,
            IPAddress.Loopback,
            IPAddress.Loopback,
            out var httpContextAccessor);
        SetAuthenticationState(authenticationStateProvider, AnonymousPrincipal());
        var httpContext = new DefaultHttpContext
        {
            User = AuthenticatedPrincipal("http-user", "http-session", ApiAccessScopeNames.Api)
        };
        httpContextAccessor.HttpContext = httpContext;
        var provider = new HttpFileAccessContextProvider(
            httpContextAccessor,
            EnabledApiOptions(),
            new StubCanonicalRuntimeDatabase(),
            authenticationStateProvider);

        var context = await provider.GetCurrentAsync();

        Assert.Equal(LocalOperatorAuthenticationStateProvider.ActorId, context.ActorId.Value);
        Assert.Equal(
            (await authenticationStateProvider.GetCurrentAsync()).FindFirstValue("jti"),
            context.SessionId.Value);
    }

    [Fact]
    public async Task Authenticated_http_request_without_interactive_state_uses_http_identity()
    {
        var httpContext = new DefaultHttpContext
        {
            User = AuthenticatedPrincipal("http-user", "http-session", ApiAccessScopeNames.Api)
        };
        var provider = new HttpFileAccessContextProvider(
            new HttpContextAccessor { HttpContext = httpContext },
            EnabledApiOptions(),
            new StubCanonicalRuntimeDatabase(),
            new AnonymousInteractiveAccessPrincipalProvider());

        var context = await provider.GetCurrentAsync();

        Assert.Equal("http-user", context.ActorId.Value);
        Assert.Equal("http-session", context.SessionId.Value);
    }

    private static LocalOperatorAuthenticationStateProvider CreateProvider(
        bool authorizationEnabled,
        IPAddress originalRemoteIp,
        IPAddress effectiveRemoteIp,
        out HttpContextAccessor httpContextAccessor)
    {
        var httpContext = new DefaultHttpContext();
        httpContext.Connection.RemoteIpAddress = effectiveRemoteIp;
        httpContext.Items[DevelopmentEndpointAccess.OriginalRemoteIpItemKey] = originalRemoteIp;
        httpContextAccessor = new HttpContextAccessor { HttpContext = httpContext };
        return new LocalOperatorAuthenticationStateProvider(
            httpContextAccessor,
            Options.Create(new ApiAccessOptions
            {
                Authorization = new ApiAuthorizationOptions
                {
                    Enabled = authorizationEnabled
                }
            }),
            Options.Create(new LocalOperatorUiOptions()));
    }

    private static ResolvedRuntimeHostProfile InteractiveHostProfile() => new(
        RuntimeHostProfileKind.WindowsInteractive,
        RuntimeHostOperatingSystem.Windows,
        IsInteractive: true,
        IsTest: false,
        ActualHostSupportVerified: true);

    private static IOptions<ApiAccessOptions> EnabledApiOptions() =>
        Options.Create(new ApiAccessOptions
        {
            Authorization = new ApiAuthorizationOptions
            {
                Enabled = true
            }
        });

    private static void SetAuthenticationState(
        IHostEnvironmentAuthenticationStateProvider provider,
        ClaimsPrincipal principal) =>
        provider.SetAuthenticationState(Task.FromResult(new AuthenticationState(principal)));

    private static ClaimsPrincipal AnonymousPrincipal() =>
        new(new ClaimsIdentity());

    private static ClaimsPrincipal AuthenticatedPrincipal(
        string actorId,
        string sessionId,
        string scope) =>
        new(new ClaimsIdentity(
            [
                new Claim("sub", actorId),
                new Claim("jti", sessionId),
                new Claim("scope", scope)
            ],
            "Bearer"));

    private sealed class StubCanonicalRuntimeDatabase : ICanonicalRuntimeDatabase
    {
        public ResolvedDatabaseProfile Profile { get; } = new(
            new DatabaseProfileRecord
            {
                Id = Guid.NewGuid(),
                ProviderKind = DatabaseProviderKind.InMemory,
                SourceKind = DatabaseProfileSourceKind.InMemory,
                InMemory = new InMemoryDatabaseProfileConnection()
            },
            DatabaseProfileResolutionSource.ExplicitOverride,
            "in-memory");

        public long Generation => 7;
    }
}
