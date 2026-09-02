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
    public async Task TOKEN_SCOPES_empty_selection_never_grants_broad_api_access() {
        await using var host = await ApiTestHost.CreateAsync(jwtEnabled: true, useInMemoryDatabase: true);
        var issuer = host.App.Services.GetRequiredService<IApiTokenService>();

        Assert.Throws<InvalidOperationException>(() => issuer.IssueToken(
            new ApiTokenIssueRequest { Subject = "empty-scope-regression", Scopes = [] }));
    }

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

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task TOKEN_LIFECYCLE_deleted_and_revoked_tokens_fail_real_http_requests(bool revokeFirst) {
        await using var host = await ApiTestHost.CreateAsync(jwtEnabled: true, useInMemoryDatabase: true);
        var issuer = host.App.Services.GetRequiredService<IApiTokenService>();
        var registry = host.App.Services.GetRequiredService<CanDoItAll.Infrastructure.ControlPlane.IApiTokenRegistry>();
        var issued = issuer.IssueToken(new ApiTokenIssueRequest {
            Subject = "token-lifecycle", Scopes = [ApiAccessScopeNames.ReadLlmChats]
        });
        SetBearerToken(host, issued);
        using var before = await host.Client.GetAsync("/api/llm-chats");
        Assert.Equal(HttpStatusCode.OK, before.StatusCode);
        var record = Assert.Single((await registry.SearchAsync(new("token-lifecycle"))).Items);

        if (revokeFirst) {
            await registry.RevokeAsync(record.Id, DateTimeOffset.UtcNow);
            using var revoked = await host.Client.GetAsync("/api/llm-chats");
            Assert.Equal(HttpStatusCode.Unauthorized, revoked.StatusCode);
        }
        await registry.DeleteAsync(record.Id);
        using var deleted = await host.Client.GetAsync("/api/llm-chats");
        Assert.Equal(HttpStatusCode.Unauthorized, deleted.StatusCode);
        Assert.Empty((await registry.SearchAsync(new("token-lifecycle"))).Items);
    }

    [Fact]
    public async Task TOKEN_LIFECYCLE_corrupt_registration_fails_closed_on_http() {
        await using var host = await ApiTestHost.CreateAsync(jwtEnabled: true, useInMemoryDatabase: true);
        var issuer = host.App.Services.GetRequiredService<IApiTokenService>();
        var registry = host.App.Services.GetRequiredService<CanDoItAll.Infrastructure.ControlPlane.IApiTokenRegistry>();
        SetBearerToken(host, issuer.IssueToken(new ApiTokenIssueRequest {
            Subject = "corrupt-registration", Scopes = [ApiAccessScopeNames.ReadLlmChats]
        }));
        var record = Assert.Single((await registry.SearchAsync(new("corrupt-registration"))).Items);
        var root = host.App.Services.GetRequiredService<CanDoItAll.Infrastructure.ControlPlane.IControlPlanePathResolver>().ResolveRootPath();
        await File.WriteAllTextAsync(Path.Combine(root, "api-tokens", $"{record.Id:N}.json"), "{}");

        using var response = await host.Client.GetAsync("/api/llm-chats");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task TOKEN_LIFECYCLE_legacy_tokens_remain_subject_to_signature_and_scope_checks() {
        await using var host = await ApiTestHost.CreateAsync(jwtEnabled: true, useInMemoryDatabase: true);
        var options = host.App.Services.GetRequiredService<Microsoft.Extensions.Options.IOptions<ApiAccessOptions>>().Value.Authorization;
        var now = DateTimeOffset.UtcNow;
        var header = Microsoft.IdentityModel.Tokens.Base64UrlEncoder.Encode(System.Text.Json.JsonSerializer.Serialize(new { alg = "HS256", typ = "JWT" }));
        var payload = Microsoft.IdentityModel.Tokens.Base64UrlEncoder.Encode(System.Text.Json.JsonSerializer.Serialize(new {
            iss = options.Issuer, aud = options.Audience, sub = "legacy-client",
            jti = Guid.NewGuid().ToString("N"), iat = now.ToUnixTimeSeconds(), nbf = now.ToUnixTimeSeconds(),
            exp = now.AddMinutes(5).ToUnixTimeSeconds(), scope = ApiAccessScopeNames.ReadLlmChats
        }));
        var unsigned = $"{header}.{payload}";
        var signature = Microsoft.IdentityModel.Tokens.Base64UrlEncoder.Encode(
            System.Security.Cryptography.HMACSHA256.HashData(System.Text.Encoding.UTF8.GetBytes(options.SigningKey),
                System.Text.Encoding.UTF8.GetBytes(unsigned)));
        host.Client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", $"{unsigned}.{signature}");

        using var permitted = await host.Client.GetAsync("/api/llm-chats");
        using var forbidden = await host.Client.PostAsJsonAsync("/api/llm-chats", new { });
        Assert.Equal(HttpStatusCode.OK, permitted.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, forbidden.StatusCode);
        host.Client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", $"{unsigned}.invalid");
        using var invalid = await host.Client.GetAsync("/api/llm-chats");
        Assert.Equal(HttpStatusCode.Unauthorized, invalid.StatusCode);
    }

    private static void SetBearerToken(
        ApiTestHost host,
        ApiTokenIssueResult token)
    {
        host.Client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue(token.TokenType, token.Token);
    }
}
