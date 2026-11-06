using System.Globalization;
using System.Security.Claims;
using CanDoItAll.Infrastructure.ControlPlane;
using CanDoItAll.Modules.Prompts;
using CanDoItAll.Modules.Workspace.ApiAccess;
using CanDoItAll.Tests.Support;
using CanDoItAll.Web.Api;
using CanDoItAll.Web.Infrastructure;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace CanDoItAll.Tests.Integration.Api;

public sealed class PromptGalleryUiAccessTests {
    [Fact]
    public async Task An_unchanged_principal_refresh_keeps_the_current_grant_authorized() {
        await using var host = await ApiTestHost.CreateAsync(true, useInMemoryDatabase: true);
        using var scope = host.App.Services.CreateScope();
        var principal = new PrincipalSource();
        var identity = new ClaimsPrincipal(new ClaimsIdentity([new Claim("sub", LocalOperatorAuthenticationStateProvider.ActorId)],
            LocalOperatorAuthenticationStateProvider.AuthenticationType));
        principal.Set(identity);
        var changes = PromptGalleryObservationTestSupport.Create();
        using var access = Create(scope.ServiceProvider, changes, principal);
        using var grant = await access.OpenAsync(changes.Scope, default);
        principal.Set(new(identity));
        await access.ValidateAsync(grant, write: true, default);
        Assert.False(grant.Lifetime.IsCancellationRequested);
    }

    [Fact]
    public async Task Anonymous_and_wrong_dataset_cannot_open_an_observation_grant() {
        await using var host = await ApiTestHost.CreateAsync(true, useInMemoryDatabase: true);
        using var scope = host.App.Services.CreateScope();
        var principal = new PrincipalSource();
        var changes = PromptGalleryObservationTestSupport.Create();
        using var access = Create(scope.ServiceProvider, changes, principal);
        await Assert.ThrowsAsync<WebCurrentPrincipalDeniedException>(() => access.OpenAsync(changes.Scope, default));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => access.OpenAsync(changes.Scope with { ActivationGeneration = 1 }, default));
    }

    [Fact]
    public async Task Trusted_local_admission_is_explicit_and_principal_change_retires_the_opening() {
        await using var host = await ApiTestHost.CreateAsync(true, useInMemoryDatabase: true);
        using var scope = host.App.Services.CreateScope();
        var principal = new PrincipalSource();
        principal.Set(new(new ClaimsIdentity([new Claim("sub", LocalOperatorAuthenticationStateProvider.ActorId)],
            LocalOperatorAuthenticationStateProvider.AuthenticationType)));
        var changes = PromptGalleryObservationTestSupport.Create();
        using var access = Create(scope.ServiceProvider, changes, principal);
        using var grant = await access.OpenAsync(changes.Scope, default);
        await access.ValidateAsync(grant, write: true, default);
        principal.Set(new(new ClaimsIdentity()));
        Assert.True(grant.Lifetime.IsCancellationRequested);
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => access.ValidateAsync(grant, write: false, default).AsTask());
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Remote_read_grant_uses_current_native_scopes_before_each_write(bool writeGranted) {
        await using var host = await ApiTestHost.CreateAsync(true, useInMemoryDatabase: true);
        using var scope = host.App.Services.CreateScope();
        var principal = new PrincipalSource();
        var options = scope.ServiceProvider.GetRequiredService<IOptions<ApiAccessOptions>>().Value;
        var identity = new ClaimsIdentity([
            new Claim("sub", "remote-reader"), new Claim("iss", options.Authorization.Issuer),
            new Claim("exp", DateTimeOffset.UtcNow.AddMinutes(5).ToUnixTimeSeconds().ToString(CultureInfo.InvariantCulture)),
            new Claim("scope", ApiAccessScopeNames.ReadPrompts)
        ], "Bearer");
        if (writeGranted) {
            identity.AddClaim(new("scope", ApiAccessScopeNames.WritePrompts));
        }
        principal.Set(new(identity));
        var changes = PromptGalleryObservationTestSupport.Create();
        using var access = Create(scope.ServiceProvider, changes, principal);
        using var grant = await access.OpenAsync(changes.Scope, default);
        await access.ValidateAsync(grant, write: false, default);
        if (writeGranted) {
            await access.ValidateAsync(grant, write: true, default);
            identity.AddClaim(new("auth_rev", "2"));
            await Assert.ThrowsAsync<UnauthorizedAccessException>(() => access.ValidateAsync(grant, write: true, default).AsTask());
        } else {
            await Assert.ThrowsAsync<UnauthorizedAccessException>(() => access.ValidateAsync(grant, write: true, default).AsTask());
        }
        Assert.True(grant.Lifetime.IsCancellationRequested);
    }

    private static WebPromptGalleryUiAccess Create(IServiceProvider services, PromptGalleryChanges changes, PrincipalSource source) {
        var resolver = new WebCurrentPrincipalResolver(source, new HttpContextAccessor(), services.GetRequiredService<IAuthorizationService>(),
            services.GetRequiredService<IApiTokenRegistry>(), services.GetRequiredService<ApiSessionService>(),
            services.GetRequiredService<IOptions<ApiAccessOptions>>(), TimeProvider.System);
        return new(resolver, changes, source, TimeProvider.System);
    }

    private sealed class PrincipalSource : AuthenticationStateProvider, IInteractiveAccessPrincipalProvider {
        private ClaimsPrincipal principal = new(new ClaimsIdentity());
        public bool IsAvailable => true;
        public ValueTask<ClaimsPrincipal> GetCurrentAsync(CancellationToken cancellationToken) => ValueTask.FromResult(principal);
        public override Task<AuthenticationState> GetAuthenticationStateAsync() => Task.FromResult(new AuthenticationState(principal));
        public void Set(ClaimsPrincipal value) {
            principal = value;
            NotifyAuthenticationStateChanged(GetAuthenticationStateAsync());
        }
    }
}
