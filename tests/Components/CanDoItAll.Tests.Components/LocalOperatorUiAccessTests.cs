using System.Net;
using System.Security.Claims;
using CanDoItAll.Composition;
using CanDoItAll.Modules.Workspace.ApiAccess;
using CanDoItAll.Web;
using CanDoItAll.Web.Api;
using CanDoItAll.Web.Infrastructure;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace CanDoItAll.Tests.Components.Shell;

public sealed class LocalOperatorUiAccessTests {
    private const string TrustedAddressKey = LocalOperatorUiOptions.SectionName + ":TrustedAddresses:0";

    [Theory]
    [InlineData(false, "127.0.0.1", "127.0.0.1", null)]
    [InlineData(false, "172.31.0.1", "172.31.0.1", "172.31.0.1")]
    [InlineData(true, "172.31.0.1", "172.31.0.1", "172.31.0.1")]
    [InlineData(false, "::ffff:172.31.0.1", "::ffff:172.31.0.1", "172.31.0.1")]
    public async Task LOCAL_UI_ACCESS_local_browser_gets_chat_scopes_independently_of_os_profile(
        bool interactiveOs, string original, string effective, string? trustedAddress) {
        using var services = CreateServices(interactiveOs, trustedAddress);
        using var scope = services.CreateScope();
        var context = new DefaultHttpContext();
        context.Items[DevelopmentEndpointAccess.OriginalRemoteIpItemKey] = IPAddress.Parse(original);
        context.Connection.RemoteIpAddress = IPAddress.Parse(effective);
        scope.ServiceProvider.GetRequiredService<IHttpContextAccessor>().HttpContext = context;
        var host = scope.ServiceProvider.GetRequiredService<IHostEnvironmentAuthenticationStateProvider>();
        host.SetAuthenticationState(Task.FromResult(new AuthenticationState(new ClaimsPrincipal(new ClaimsIdentity()))));

        var principal = await scope.ServiceProvider.GetRequiredService<IInteractiveAccessPrincipalProvider>().GetCurrentAsync();

        Assert.True(principal.Identity?.IsAuthenticated);
        Assert.Equal(LocalOperatorAuthenticationStateProvider.ActorId, principal.FindFirstValue("sub"));
        Assert.True(ApiAuthorizationPolicies.HasScope(principal, ApiAccessScopeNames.ReadLlmChats));
        Assert.True(ApiAuthorizationPolicies.HasScope(principal, ApiAccessScopeNames.ManageLlmChats));
        Assert.True(ApiAuthorizationPolicies.HasScope(principal, ApiAccessScopeNames.ExecuteLlmChats));
        Assert.False(ApiAuthorizationPolicies.HasScope(principal, ApiAccessScopeNames.Api));
        Assert.False(context.User.Identity?.IsAuthenticated);
    }

    [Theory]
    [InlineData("192.0.2.10", "127.0.0.1", "172.31.0.1")]
    [InlineData("192.0.2.10", "172.31.0.1", "172.31.0.1")]
    [InlineData("172.31.0.1", "192.0.2.10", "172.31.0.1")]
    [InlineData("172.31.0.2", "127.0.0.1", "172.31.0.1")]
    [InlineData("172.31.0.1", "172.31.0.1", null)]
    [InlineData(null, "127.0.0.1", "172.31.0.1")]
    [InlineData("172.31.0.1", null, "172.31.0.1")]
    public async Task API_BOUNDARY_untrusted_or_missing_transport_cannot_gain_local_scopes(
        string? original, string? effective, string? trustedAddress) {
        using var services = CreateServices(false, trustedAddress);
        using var scope = services.CreateScope();
        var context = new DefaultHttpContext();
        if (original is not null) {
            context.Items[DevelopmentEndpointAccess.OriginalRemoteIpItemKey] = IPAddress.Parse(original);
        }
        context.Connection.RemoteIpAddress = effective is null ? null : IPAddress.Parse(effective);
        context.Request.Host = new HostString("localhost");
        context.Request.Headers["X-Forwarded-For"] = "127.0.0.1";
        scope.ServiceProvider.GetRequiredService<IHttpContextAccessor>().HttpContext = context;
        var host = scope.ServiceProvider.GetRequiredService<IHostEnvironmentAuthenticationStateProvider>();
        host.SetAuthenticationState(Task.FromResult(new AuthenticationState(new ClaimsPrincipal(new ClaimsIdentity()))));

        var principal = await scope.ServiceProvider.GetRequiredService<IInteractiveAccessPrincipalProvider>().GetCurrentAsync();

        Assert.False(principal.Identity?.IsAuthenticated);
        Assert.False(ApiAuthorizationPolicies.HasScope(principal, ApiAccessScopeNames.ReadLlmChats));
    }

    [Theory]
    [InlineData("")]
    [InlineData("docker-gateway")]
    [InlineData("172.31.0.0/16")]
    [InlineData("*")]
    [InlineData("0.0.0.0")]
    [InlineData("::")]
    [InlineData("::ffff:0.0.0.0")]
    [InlineData("255.255.255.255")]
    public void API_BOUNDARY_invalid_trust_configuration_fails_startup(string trustedAddress) {
        using var services = CreateServices(false, trustedAddress);

        var error = Assert.Throws<OptionsValidationException>(() =>
            services.GetRequiredService<IStartupValidator>().Validate());

        Assert.Contains("explicit IP addresses", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task API_BOUNDARY_trusted_ingress_does_not_expand_authenticated_read_only_access() {
        using var services = CreateServices(false, "172.31.0.1");
        using var scope = services.CreateScope();
        var context = new DefaultHttpContext();
        context.Items[DevelopmentEndpointAccess.OriginalRemoteIpItemKey] = IPAddress.Parse("172.31.0.1");
        context.Connection.RemoteIpAddress = IPAddress.Parse("172.31.0.1");
        scope.ServiceProvider.GetRequiredService<IHttpContextAccessor>().HttpContext = context;
        var bearer = new ClaimsPrincipal(new ClaimsIdentity(
            [new Claim("scope", ApiAccessScopeNames.ReadLlmChats)], "Bearer"));
        scope.ServiceProvider.GetRequiredService<IHostEnvironmentAuthenticationStateProvider>()
            .SetAuthenticationState(Task.FromResult(new AuthenticationState(bearer)));

        var principal = await scope.ServiceProvider.GetRequiredService<IInteractiveAccessPrincipalProvider>().GetCurrentAsync();

        Assert.Same(bearer, principal);
        Assert.False(ApiAuthorizationPolicies.HasScope(principal, ApiAccessScopeNames.ManageLlmChats));
        Assert.False(ApiAuthorizationPolicies.HasScope(principal, ApiAccessScopeNames.ExecuteLlmChats));
    }

    [Fact]
    public async Task LOCAL_UI_ACCESS_identity_is_stable_after_http_context_is_gone_and_isolated_per_circuit() {
        using var services = CreateServices(false, "172.31.0.1");
        using var firstScope = services.CreateScope();
        using var secondScope = services.CreateScope();
        var context = new DefaultHttpContext();
        context.Items[DevelopmentEndpointAccess.OriginalRemoteIpItemKey] = IPAddress.Parse("172.31.0.1");
        context.Connection.RemoteIpAddress = IPAddress.Parse("172.31.0.1");
        var accessor = services.GetRequiredService<IHttpContextAccessor>();
        accessor.HttpContext = context;
        var anonymous = Task.FromResult(new AuthenticationState(new ClaimsPrincipal(new ClaimsIdentity())));
        firstScope.ServiceProvider.GetRequiredService<IHostEnvironmentAuthenticationStateProvider>().SetAuthenticationState(anonymous);
        secondScope.ServiceProvider.GetRequiredService<IHostEnvironmentAuthenticationStateProvider>().SetAuthenticationState(anonymous);
        accessor.HttpContext = null;
        firstScope.ServiceProvider.GetRequiredService<IHostEnvironmentAuthenticationStateProvider>().SetAuthenticationState(anonymous);
        var firstProvider = firstScope.ServiceProvider.GetRequiredService<IInteractiveAccessPrincipalProvider>();

        var first = await firstProvider.GetCurrentAsync();
        var repeated = await firstProvider.GetCurrentAsync();
        var second = await secondScope.ServiceProvider.GetRequiredService<IInteractiveAccessPrincipalProvider>().GetCurrentAsync();

        Assert.True(first.Identity?.IsAuthenticated);
        Assert.Same(first, repeated);
        Assert.NotEqual(first.FindFirstValue("jti"), second.FindFirstValue("jti"));
    }

    private static ServiceProvider CreateServices(bool interactiveOs, string? trustedAddress) {
        var services = new ServiceCollection();
        var settings = new Dictionary<string, string?>();
        if (trustedAddress is not null) {
            settings[TrustedAddressKey] = trustedAddress;
        }
        services.AddSingleton<IConfiguration>(new ConfigurationBuilder()
            .AddInMemoryCollection(settings)
            .Build());
        services.AddSingleton(Options.Create(new ApiAccessOptions {
            Authorization = new ApiAuthorizationOptions { Enabled = true }
        }));
        services.AddSingleton(new ResolvedRuntimeHostProfile(
            interactiveOs ? RuntimeHostProfileKind.LinuxInteractive : RuntimeHostProfileKind.LinuxHeadless,
            RuntimeHostOperatingSystem.Linux,
            IsInteractive: interactiveOs,
            IsTest: false,
            ActualHostSupportVerified: true));
        services.AddCanDoItAllLocalOperatorUiAuthentication();
        return services.BuildServiceProvider();
    }
}
