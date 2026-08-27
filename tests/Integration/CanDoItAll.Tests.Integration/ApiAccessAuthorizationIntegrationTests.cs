using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using CanDoItAll.Modules.Projects;
using CanDoItAll.Modules.Workbench;
using CanDoItAll.Modules.Workspace.ApiAccess;
using CanDoItAll.Web.Infrastructure;
using Microsoft.Extensions.DependencyInjection;

namespace CanDoItAll.Tests.Integration.Api;

public sealed class ApiAccessAuthorizationIntegrationTests
{
    [Fact]
    public async Task API_BOUNDARY_local_operator_ui_identity_does_not_authenticate_http_boundaries() {
        await using var host = await ApiTestHost.CreateAsync(
            jwtEnabled: true,
            configureServices: services => {
                services.AddCanDoItAllLocalOperatorUiAuthentication();
                services.Configure<LocalOperatorUiOptions>(options =>
                    options.TrustedAddresses = ["127.0.0.1", "172.31.0.1"]);
            },
            useInMemoryDatabase: true);

        using var llmChatsResponse = await host.Client.GetAsync("/api/llm-chats");
        using var authorizedFileResponse = await host.Client.GetAsync("/authorized-files/content");

        Assert.Equal(HttpStatusCode.Unauthorized, llmChatsResponse.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, authorizedFileResponse.StatusCode);

        SetBearerToken(host, host.App.Services.GetRequiredService<IApiTokenService>()
            .IssueToken(new ApiTokenIssueRequest {
                Subject = "read-only-ui-boundary-test",
                DisplayName = "Read only UI boundary test",
                Scopes = [ApiAccessScopeNames.ReadLlmChats]
            }));
        using var readResponse = await host.Client.GetAsync("/api/llm-chats");
        using var createResponse = await host.Client.PostAsJsonAsync("/api/llm-chats", new { });

        Assert.Equal(HttpStatusCode.OK, readResponse.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, createResponse.StatusCode);
    }

    [Fact]
    public async Task Token_issuance_requires_explicit_privileged_scope()
    {
        await using var host = await ApiTestHost.CreateAsync(
            jwtEnabled: true,
            useInMemoryDatabase: true);
        var tokenService = host.App.Services.GetRequiredService<IApiTokenService>();
        var request = new ApiTokenIssueRequest
        {
            Subject = "delegated-client",
            DisplayName = "Delegated client"
        };

        SetBearerToken(
            host,
            tokenService.IssueToken(new ApiTokenIssueRequest
            {
                Subject = "ordinary-client",
                DisplayName = "Ordinary client",
                Scopes = [ApiAccessScopeNames.Api]
            }));

        using var forbiddenResponse = await host.Client.PostAsJsonAsync(
            "/api/access/tokens",
            request);

        Assert.Equal(HttpStatusCode.Forbidden, forbiddenResponse.StatusCode);

        SetBearerToken(
            host,
            tokenService.IssueToken(new ApiTokenIssueRequest
            {
                Subject = "token-administrator",
                DisplayName = "Token administrator",
                Scopes =
                [
                    ApiAccessScopeNames.Api,
                    ApiAccessScopeNames.IssueTokens
                ]
            }));

        using var issuedResponse = await host.Client.PostAsJsonAsync(
            "/api/access/tokens",
            request);

        Assert.Equal(HttpStatusCode.OK, issuedResponse.StatusCode);
        var issuedToken = await issuedResponse.Content.ReadFromJsonAsync<ApiTokenIssueResult>();
        Assert.NotNull(issuedToken);
        Assert.Equal(request.Subject, issuedToken.Subject);
    }

    [Fact]
    public async Task Authorization_disabled_token_endpoint_is_not_protected_by_the_scope_policy()
    {
        await using var host = await ApiTestHost.CreateAsync(
            jwtEnabled: false,
            useInMemoryDatabase: true);

        using var response = await host.Client.PostAsJsonAsync(
            "/api/access/tokens",
            new ApiTokenIssueRequest());

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Project_structure_routes_require_write_scope_and_bind_lease_owner_to_token_subject()
    {
        await using var host = await ApiTestHost.CreateAsync(
            jwtEnabled: true,
            useInMemoryDatabase: true);
        Guid projectId;
        await using (var scope = host.App.Services.CreateAsyncScope())
        {
            var savedProject = await scope.ServiceProvider.GetRequiredService<ProjectsService>()
                .SaveAsync(new ProjectEditorModel
                {
                    Name = "Authorized lease project",
                    Objective = "Validate authenticated lease ownership.",
                    CurrentPhase = "Validation"
                });
            Assert.True(savedProject.IsSuccess);
            projectId = savedProject.Value;
        }

        var tokenService = host.App.Services.GetRequiredService<IApiTokenService>();
        SetBearerToken(
            host,
            tokenService.IssueToken(new ApiTokenIssueRequest
            {
                Subject = "memory-only-client",
                DisplayName = "Memory-only client",
                Scopes = [ApiAccessScopeNames.ReadMemoryProviders]
            }));

        using var forbiddenResponse = await host.Client.GetAsync(
            "/api/project-structure/node-catalog");

        Assert.Equal(HttpStatusCode.Forbidden, forbiddenResponse.StatusCode);

        const string authenticatedSubject = "project-structure-operator";
        SetBearerToken(
            host,
            tokenService.IssueToken(new ApiTokenIssueRequest
            {
                Subject = authenticatedSubject,
                DisplayName = "Project Structure Operator",
                Scopes = [ApiAccessScopeNames.WriteProjectStructure]
            }));
        host.Client.DefaultRequestHeaders.Add(
            ProjectStructureAgentHttpHeaders.AgentId,
            "spoofed-agent");
        host.Client.DefaultRequestHeaders.Add(
            ProjectStructureAgentHttpHeaders.MachineName,
            "spoofed-machine");
        host.Client.DefaultRequestHeaders.Add(
            ProjectStructureAgentHttpHeaders.RepositoryRoot,
            "C:/spoofed/repository");
        using var leaseResponse = await host.Client.PostAsJsonAsync(
            "/api/project-structure/leases/acquire",
            new ProjectStructureLeaseAcquireRequest(
                ProjectStructureLeaseScopeKind.Project,
                projectId.ToString("D"),
                "JWT identity boundary acceptance"));

        Assert.Equal(HttpStatusCode.OK, leaseResponse.StatusCode);
        var lease = await leaseResponse.Content.ReadFromJsonAsync<ProjectStructureLeaseSnapshot>();
        Assert.NotNull(lease);
        Assert.Equal(authenticatedSubject, lease.AgentId);
        Assert.Equal(Environment.MachineName, lease.MachineName);
        Assert.Empty(lease.RepositoryRoot);
        Assert.Empty(lease.BranchName);
    }

    private static void SetBearerToken(
        ApiTestHost host,
        ApiTokenIssueResult token)
    {
        host.Client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue(token.TokenType, token.Token);
    }
}
