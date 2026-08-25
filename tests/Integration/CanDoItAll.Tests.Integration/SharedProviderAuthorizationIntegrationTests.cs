using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Json;
using CanDoItAll.Modules.Workspace;
using CanDoItAll.Modules.Workspace.ApiAccess;
using CanDoItAll.SharedKernel;
using CanDoItAll.SharedProviders.Abstractions;
using CanDoItAll.Web.Api;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

namespace CanDoItAll.Tests.Integration;

public sealed class SharedProviderAuthorizationIntegrationTests(
    SharedProviderAuthorizationFixture fixture) : IClassFixture<SharedProviderAuthorizationFixture>
{
    private const string NativeUnauthorizedCode = "shared-provider.catalog.unauthorized";
    private const string NativeForbiddenCode = "shared-provider.catalog.forbidden";
    private const string OpenAiUnauthorizedCode = "shared_provider_unauthorized";
    private const string OpenAiInsufficientScopeCode = "shared_provider_insufficient_scope";
    private const string CatalogReadPolicy = "Api.SharedProviders.Catalog.Read";
    private const string InvokePolicy = "Api.SharedProviders.Invoke";

    [Fact]
    public async Task GranularCatalogReadScope_AllowsNativeCatalog()
    {
        using var response = await SendWithScopeAsync(
            SharedProviderRoutes.Catalog,
            ApiAccessScopeNames.ReadSharedProviderCatalog);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        await AssertPolicyMatrixAsync(
            ApiAccessScopeNames.ReadSharedProviderCatalog,
            expectedCatalogRead: true,
            expectedInvoke: false);
    }

    [Fact]
    public async Task GranularCatalogReadScope_AllowsOpenAiModels()
    {
        using var response = await SendWithScopeAsync(
            SharedProviderRoutes.Models,
            ApiAccessScopeNames.ReadSharedProviderCatalog);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var payload = await ReadOpenAiModelsAsync(response);
        Assert.Single(payload.Data);
    }

    [Fact]
    public async Task UmbrellaApiScope_AllowsNativeCatalogAndBothSharedProviderPolicies()
    {
        using var response = await SendWithScopeAsync(
            SharedProviderRoutes.Catalog,
            ApiAccessScopeNames.Api);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        await AssertPolicyMatrixAsync(
            ApiAccessScopeNames.Api,
            expectedCatalogRead: true,
            expectedInvoke: true);
    }

    [Fact]
    public async Task UmbrellaApiScope_AllowsOpenAiModels()
    {
        using var response = await SendWithScopeAsync(
            SharedProviderRoutes.Models,
            ApiAccessScopeNames.Api);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var payload = await ReadOpenAiModelsAsync(response);
        Assert.Single(payload.Data);
    }

    [Fact]
    public async Task MissingBearerToken_NativeCatalog_ReturnsNativeUnauthorized()
    {
        using var response = await fixture.Host.Client.GetAsync(SharedProviderRoutes.Catalog);

        await AssertNativeAuthorizationErrorAsync(
            response,
            HttpStatusCode.Unauthorized,
            NativeUnauthorizedCode);
    }

    [Fact]
    public async Task MissingBearerToken_OpenAiModels_ReturnsOpenAiUnauthorized()
    {
        using var response = await fixture.Host.Client.GetAsync(SharedProviderRoutes.Models);

        await AssertOpenAiAuthorizationErrorAsync(
            response,
            HttpStatusCode.Unauthorized,
            OpenAiUnauthorizedCode,
            SharedProviderOpenAiConstants.AuthenticationErrorType);
    }

    [Fact]
    public async Task MalformedOrExpiredBearerToken_NativeCatalog_ReturnsNativeUnauthorized()
    {
        foreach (string token in new[] { "not-a-valid-jwt", CreateExpiredToken() })
        {
            using var response = await SendWithRawTokenAsync(
                SharedProviderRoutes.Catalog,
                token);

            await AssertNativeAuthorizationErrorAsync(
                response,
                HttpStatusCode.Unauthorized,
                NativeUnauthorizedCode);
        }
    }

    [Fact]
    public async Task MalformedOrExpiredBearerToken_OpenAiModels_ReturnsOpenAiUnauthorized()
    {
        foreach (string token in new[] { "not-a-valid-jwt", CreateExpiredToken() })
        {
            using var response = await SendWithRawTokenAsync(
                SharedProviderRoutes.Models,
                token);

            await AssertOpenAiAuthorizationErrorAsync(
                response,
                HttpStatusCode.Unauthorized,
                OpenAiUnauthorizedCode,
                SharedProviderOpenAiConstants.AuthenticationErrorType);
        }
    }

    [Fact]
    public async Task InvokeOnlyScope_NativeCatalog_ReturnsNativeForbidden()
    {
        using var response = await SendWithScopeAsync(
            SharedProviderRoutes.Catalog,
            ApiAccessScopeNames.InvokeSharedProviders);

        await AssertNativeAuthorizationErrorAsync(
            response,
            HttpStatusCode.Forbidden,
            NativeForbiddenCode);
        await AssertPolicyMatrixAsync(
            ApiAccessScopeNames.InvokeSharedProviders,
            expectedCatalogRead: false,
            expectedInvoke: true);
    }

    [Fact]
    public async Task InvokeOnlyScope_OpenAiModels_ReturnsOpenAiForbidden()
    {
        using var response = await SendWithScopeAsync(
            SharedProviderRoutes.Models,
            ApiAccessScopeNames.InvokeSharedProviders);

        await AssertOpenAiAuthorizationErrorAsync(
            response,
            HttpStatusCode.Forbidden,
            OpenAiInsufficientScopeCode,
            SharedProviderOpenAiConstants.PermissionErrorType);
    }

    private async Task<HttpResponseMessage> SendWithScopeAsync(string route, string scope)
    {
        var token = fixture.Host.App.Services
            .GetRequiredService<IApiTokenService>()
            .IssueToken(new ApiTokenIssueRequest
            {
                Subject = $"shared-provider-{scope}",
                DisplayName = "Shared-provider API test client",
                Scopes = [scope]
            });

        using var request = new HttpRequestMessage(HttpMethod.Get, route);
        request.Headers.Authorization = new AuthenticationHeaderValue(token.TokenType, token.Token);
        return await fixture.Host.Client.SendAsync(request);
    }

    private async Task<HttpResponseMessage> SendWithRawTokenAsync(string route, string token)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, route);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return await fixture.Host.Client.SendAsync(request);
    }

    private string CreateExpiredToken()
    {
        var hostClock = fixture.Host.App.Services.GetRequiredService<IClock>();
        var tokenService = new ApiTokenService(
            fixture.Host.App.Services.GetRequiredService<IOptions<ApiAccessOptions>>(),
            new FixedClock(hostClock.GetUtcNow().AddHours(-2)));
        return tokenService.IssueToken(new ApiTokenIssueRequest
        {
            Subject = "expired-shared-provider-client",
            DisplayName = "Expired shared-provider API test client",
            LifetimeMinutes = 1,
            Scopes = [ApiAccessScopeNames.ReadSharedProviderCatalog]
        }).Token;
    }

    private async Task AssertPolicyMatrixAsync(
        string scope,
        bool expectedCatalogRead,
        bool expectedInvoke)
    {
        var identity = new ClaimsIdentity(
            [new Claim("scope", scope)],
            authenticationType: "SharedProviderAuthorizationTests");
        var principal = new ClaimsPrincipal(identity);
        var authorization = fixture.Host.App.Services.GetRequiredService<IAuthorizationService>();

        var catalogRead = await authorization.AuthorizeAsync(
            principal,
            resource: null,
            CatalogReadPolicy);
        var invoke = await authorization.AuthorizeAsync(
            principal,
            resource: null,
            InvokePolicy);

        Assert.Equal(expectedCatalogRead, catalogRead.Succeeded);
        Assert.Equal(expectedInvoke, invoke.Succeeded);
    }

    private static async Task AssertNativeAuthorizationErrorAsync(
        HttpResponseMessage response,
        HttpStatusCode expectedStatus,
        string expectedCode)
    {
        Assert.Equal(expectedStatus, response.StatusCode);
        var payload = await response.Content.ReadFromJsonAsync<ApiErrorResponse>();
        Assert.Equal(expectedCode, Assert.Single(payload!.Errors).Code);
        AssertSharedProviderResponseHeaders(response);
    }

    private static async Task AssertOpenAiAuthorizationErrorAsync(
        HttpResponseMessage response,
        HttpStatusCode expectedStatus,
        string expectedCode,
        string expectedType)
    {
        Assert.Equal(expectedStatus, response.StatusCode);
        var payload = await ReadOpenAiErrorAsync(response);
        Assert.Equal(expectedCode, payload.Error.Code);
        Assert.Equal(expectedType, payload.Error.Type);
        Assert.Null(payload.Error.Param);
        AssertSharedProviderResponseHeaders(response);
    }

    private static async Task<SharedProviderOpenAiModelList> ReadOpenAiModelsAsync(
        HttpResponseMessage response)
    {
        string body = await response.Content.ReadAsStringAsync();
        return JsonSerializer.Deserialize<SharedProviderOpenAiModelList>(
                body,
                SharedProviderProtocolJson.Options) ??
            throw new InvalidOperationException("The OpenAI model-list response was empty.");
    }

    private static async Task<SharedProviderOpenAiErrorEnvelope> ReadOpenAiErrorAsync(
        HttpResponseMessage response)
    {
        string body = await response.Content.ReadAsStringAsync();
        return JsonSerializer.Deserialize<SharedProviderOpenAiErrorEnvelope>(
                body,
                SharedProviderProtocolJson.Options) ??
            throw new InvalidOperationException("The OpenAI error response was empty.");
    }

    private static void AssertSharedProviderResponseHeaders(HttpResponseMessage response)
    {
        Assert.True(response.Headers.CacheControl?.Private);
        Assert.True(response.Headers.CacheControl?.NoCache);
        string requestId = Assert.Single(response.Headers.GetValues(SharedProviderHeaders.RequestId));
        Assert.False(string.IsNullOrWhiteSpace(requestId));
    }

    private sealed class FixedClock(DateTimeOffset now) : IClock
    {
        public DateTimeOffset GetUtcNow() => now;
    }
}

public sealed class SharedProviderAuthorizationFixture : IAsyncLifetime
{
    private ApiTestHost? host;

    internal ApiTestHost Host
        => host ?? throw new InvalidOperationException("The shared-provider authorization host is not initialized.");

    public async Task InitializeAsync()
    {
        host = await ApiTestHost.CreateAsync(
            jwtEnabled: true,
            configureServices: services =>
            {
                services.RemoveAll<ISharedProviderCatalogQueryService>();
                services.AddScoped<ISharedProviderCatalogQueryService>(serviceProvider =>
                    new StubSharedProviderCatalogQueryService(
                        SharedProviderApiTestData.Snapshot,
                        serviceProvider.GetRequiredService<IHttpContextAccessor>()));
            },
            useInMemoryDatabase: true);
    }

    public async Task DisposeAsync()
    {
        if (host is not null)
        {
            await host.DisposeAsync();
        }
    }
}
