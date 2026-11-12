using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.Infrastructure.ControlPlane;
using CanDoItAll.Modules.Workbench;
using CanDoItAll.Modules.Workspace.ApiAccess;
using CanDoItAll.Processes.Runtime;
using CanDoItAll.Web.Api;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace CanDoItAll.Tests.Integration.Api;

[Trait("Category", "HostPlatform")]
public sealed class ProcessPlanningReviewHttpAccessTests {
    private const string ProofRoute = "/api/process-review-authority-proof";
    private static readonly string[] Grants = [
        ApiAccessScopeNames.ReadProcesses, ApiAccessScopeNames.WriteProcesses, ApiAccessScopeNames.ExecuteProcesses,
        ApiAccessScopeNames.ReadAgents, ApiAccessScopeNames.WriteAgents, ApiAccessScopeNames.ExecuteAgents,
        ApiAccessScopeNames.ReadProjects, ApiAccessScopeNames.WriteProjects
    ];

    [Fact]
    public async Task Managed_credentials_require_every_owner_grant_even_with_general_API_scope() {
        await using var host = await CreateHostAsync(true);
        foreach (var missing in Grants) {
            UseToken(host, [ApiAccessScopeNames.Api, .. Grants.Where(scope => scope != missing)]);
            using var refused = await host.Client.GetAsync(ProofRoute);
            Assert.Equal(HttpStatusCode.Forbidden, refused.StatusCode);
        }
        UseToken(host, Grants);
        using var accepted = await host.Client.GetAsync(ProofRoute);
        Assert.Equal(HttpStatusCode.OK, accepted.StatusCode);
        var result = await accepted.Content.ReadFromJsonAsync<AuthorityResult>();
        Assert.False(result!.Local);
        Assert.Null(result.ProjectAdmission);
        Assert.Equal(ProcessLaunchOperatorSurface.Api, result.Surface);
    }

    [Fact]
    public async Task Captured_authority_refuses_replaced_principal_and_revoked_credential() {
        var probe = new AuthorityProbe();
        await using var host = await CreateHostAsync(true, probe);
        UseToken(host, Grants);
        probe.AfterCapture = (context, _) => {
            ((ClaimsIdentity)context.User.Identity!).AddClaim(new("scope", "changed-after-capture"));
            return Task.CompletedTask;
        };
        using var changed = await host.Client.GetAsync(ProofRoute);
        Assert.Equal(HttpStatusCode.Forbidden, changed.StatusCode);

        probe.AfterCapture = async (context, token) => {
            var original = context.Features.Get<ApiAuthenticationObservation>()!;
            await context.RequestServices.GetRequiredService<IApiTokenRegistry>()
                .RevokeAsync(original.Credential!.Id, DateTimeOffset.UtcNow, token);
        };
        using var revoked = await host.Client.GetAsync(ProofRoute);
        Assert.Equal(HttpStatusCode.Forbidden, revoked.StatusCode);
    }

    [Fact]
    public async Task Missing_validated_observation_cannot_become_a_local_operator() {
        var probe = new AuthorityProbe {
            BeforeCapture = context => context.Features.Set<ApiAuthenticationObservation>(null)
        };
        await using var host = await CreateHostAsync(true, probe);
        UseToken(host, Grants);
        using var refused = await host.Client.GetAsync(ProofRoute);
        Assert.Equal(HttpStatusCode.Forbidden, refused.StatusCode);
    }

    [Fact]
    public async Task Explicit_open_API_mode_captures_local_API_authority_without_Project_admission() {
        await using var host = await CreateHostAsync(false);
        using var accepted = await host.Client.GetAsync(ProofRoute);
        Assert.Equal(HttpStatusCode.OK, accepted.StatusCode);
        var result = await accepted.Content.ReadFromJsonAsync<AuthorityResult>();
        Assert.True(result!.Local);
        Assert.Null(result.ProjectAdmission);
        Assert.Equal(ProcessLaunchOperatorSurface.Api, result.Surface);
    }

    private static Task<ApiTestHost> CreateHostAsync(bool authentication, AuthorityProbe? probe = null)
        => ApiTestHost.CreateAsync(authentication, services => services.AddScoped<ProcessPlanningReviewHttpAccess>(),
            useInMemoryDatabase: true, configureApplication: app => {
                app.MapGet(ProofRoute, async (HttpContext context, ProcessPlanningReviewHttpAccess access) => {
                    try {
                        probe?.BeforeCapture?.Invoke(context);
                        var manager = await access.CaptureAsync(context, context.RequestAborted);
                        if (probe?.AfterCapture is { } afterCapture) {
                            await afterCapture(context, context.RequestAborted);
                        }
                        await manager.RequireCurrentAsync(context.RequestAborted);
                        return Results.Ok(new AuthorityResult(
                            manager.Original.Principal is ProcessLaunchPrincipal.LocalOperator,
                            manager.Original.Principal switch {
                                ProcessLaunchPrincipal.LocalOperator local => local.Surface,
                                ProcessLaunchPrincipal.AuthenticatedOperator => ProcessLaunchOperatorSurface.Api,
                                _ => throw new InvalidOperationException("Only operator authority is expected.")
                            }, manager.Original.ProjectAdmission?.ProjectId));
                    } catch (Exception exception) when (exception is WebCurrentPrincipalDeniedException or AgentToolReceiptAccessDeniedException) {
                        return Results.StatusCode(StatusCodes.Status403Forbidden);
                    }
                });
            });

    internal static void UseToken(ApiTestHost host, IReadOnlyList<string>? grants = null) {
        var issued = host.App.Services.GetRequiredService<IApiTokenService>().IssueToken(new() {
            Subject = "process-review-manager", Scopes = [.. (grants ?? Grants)], LifetimeMinutes = 60
        });
        host.Client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", issued.Token);
    }

    private sealed class AuthorityProbe {
        internal Action<HttpContext>? BeforeCapture { get; init; }
        internal Func<HttpContext, CancellationToken, Task>? AfterCapture { get; set; }
    }

    private sealed record AuthorityResult(bool Local, ProcessLaunchOperatorSurface Surface, Guid? ProjectAdmission);
}
