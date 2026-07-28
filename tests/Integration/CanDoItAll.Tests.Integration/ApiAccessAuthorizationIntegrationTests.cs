using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using CanDoItAll.Modules.Workspace.ApiAccess;
using Microsoft.Extensions.DependencyInjection;

namespace CanDoItAll.Tests.Integration;

public sealed class ApiAccessAuthorizationIntegrationTests
{
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

    private static void SetBearerToken(
        ApiTestHost host,
        ApiTokenIssueResult token)
    {
        host.Client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue(token.TokenType, token.Token);
    }
}
