using System.Net;
using System.Net.Http.Json;
using CanDoItAll.SharedProviders.Abstractions;
using CanDoItAll.Web.Api;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;

namespace CanDoItAll.Tests.Integration;

public sealed class SharedProviderAccessContextTests : IAsyncLifetime
{
    private const string ContextRoute = "/_tests/shared-providers/access-context";
    private const string ProtectedRoute = "/_tests/shared-providers/access-context/protected";
    private const string ReexecutionRoute = "/_tests/shared-providers/access-context/not-found";
    private ApiTestHost? host;

    [Fact]
    public void Parser_AcceptsConservativeGrammar()
    {
        const string expected = "ABCxyz019._~:-";

        bool parsed = AccessContextReference.TryParse(expected, out var reference);

        Assert.True(parsed);
        Assert.Equal(expected, reference.Value);
    }

    [Fact]
    public void Parser_AcceptsMinimumAndMaximumLengths()
    {
        string maximum = new('a', AccessContextReference.MaximumLength);

        Assert.True(AccessContextReference.TryParse("a", out var minimumReference));
        Assert.True(AccessContextReference.TryParse(maximum, out var maximumReference));
        Assert.Equal("a", minimumReference.Value);
        Assert.Equal(maximum, maximumReference.Value);
    }

    [Fact]
    public void Parser_RejectsNullEmptyAndWhitespace()
    {
        string?[] malformed = [null, string.Empty, " ", " tenant", "tenant "];

        foreach (string? value in malformed)
        {
            Assert.False(AccessContextReference.TryParse(value, out _));
        }

        var state = new AccessContextReferenceState();
        Assert.Throws<InvalidOperationException>(() => state.Set(default));
    }

    [Fact]
    public void Parser_RejectsControlsUnicodeDisallowedAndOversizedValues()
    {
        string[] malformed =
        [
            "tenant\0context",
            "tenant\rcontext",
            "ténant",
            "tenant/context",
            new string('a', AccessContextReference.MaximumLength + 1)
        ];

        foreach (string value in malformed)
        {
            Assert.False(AccessContextReference.TryParse(value, out _));
        }
    }

    [Fact]
    public async Task AbsentHeader_LeavesRequestContextEmpty()
    {
        using var response = await SendAsync(ContextRoute);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Null((await ReadContextAsync(response)).Value);
    }

    [Fact]
    public async Task ValidHeader_BindsExactOpaqueValue()
    {
        const string expected = "Tenant_42~Session:primary.stage-1";

        using var response = await SendAsync(ContextRoute, expected);
        using var reexecutedResponse = await SendAsync(
            "/_tests/shared-providers/access-context/missing",
            expected);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(expected, (await ReadContextAsync(response)).Value);
        Assert.Equal(HttpStatusCode.OK, reexecutedResponse.StatusCode);
        Assert.Equal(expected, (await ReadContextAsync(reexecutedResponse)).Value);
    }

    [Fact]
    public async Task MalformedHeaders_ReturnNativeBadRequest()
    {
        string[] malformed =
        [
            string.Empty,
            "tenant context",
            "tenant/context",
            new string('a', AccessContextReference.MaximumLength + 1)
        ];

        foreach (string value in malformed)
        {
            using var response = await SendAsync(ContextRoute, value);
            await AssertInvalidAsync(response);
        }
    }

    [Fact]
    public async Task RepeatedAndCommaCombinedHeaders_ReturnNativeBadRequest()
    {
        using var repeatedIdenticalResponse = await SendAsync(ContextRoute, "tenant-a", "tenant-a");
        using var repeatedConflictingResponse = await SendAsync(ContextRoute, "tenant-a", "tenant-b");
        using var combinedResponse = await SendAsync(ContextRoute, "tenant-a,tenant-b");

        await AssertInvalidAsync(repeatedIdenticalResponse);
        await AssertInvalidAsync(repeatedConflictingResponse);
        await AssertInvalidAsync(combinedResponse);
    }

    [Fact]
    public async Task ConcurrentRequests_KeepAccessContextsIsolated()
    {
        Task<HttpResponseMessage> firstRequest = SendAsync(
            $"{ContextRoute}?delayMilliseconds=100",
            "tenant-a");
        Task<HttpResponseMessage> secondRequest = SendAsync(
            $"{ContextRoute}?delayMilliseconds=100",
            "tenant-b");

        HttpResponseMessage[] responses = await Task.WhenAll(firstRequest, secondRequest);
        using var firstResponse = responses[0];
        using var secondResponse = responses[1];

        Assert.Equal("tenant-a", (await ReadContextAsync(firstResponse)).Value);
        Assert.Equal("tenant-b", (await ReadContextAsync(secondResponse)).Value);
    }

    [Fact]
    public async Task ForgedAccessContext_DoesNotSatisfyAuthentication()
    {
        using var response = await SendAsync(ProtectedRoute, "forged-admin-context");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        var failure = await response.Content.ReadFromJsonAsync<ApiErrorResponse>();
        Assert.Equal("api.authorization-required", Assert.Single(failure!.Errors).Code);
    }

    public async Task InitializeAsync()
    {
        host = await ApiTestHost.CreateAsync(
            jwtEnabled: true,
            useInMemoryDatabase: true,
            configureApplication: MapTestEndpoints);
    }

    public async Task DisposeAsync()
    {
        if (host is not null)
        {
            await host.DisposeAsync();
        }
    }

    private ApiTestHost Host
        => host ?? throw new InvalidOperationException("The API test host is not initialized.");

    private async Task<HttpResponseMessage> SendAsync(
        string route,
        params string[] headerValues)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, route);
        if (headerValues.Length > 0 &&
            !request.Headers.TryAddWithoutValidation(
                SharedProviderHeaders.AccessContextReference,
                headerValues))
        {
            throw new InvalidOperationException("The access-context test header could not be added.");
        }

        return await Host.Client.SendAsync(request);
    }

    private static async Task AssertInvalidAsync(HttpResponseMessage response)
    {
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var failure = await response.Content.ReadFromJsonAsync<ApiErrorResponse>();
        var error = Assert.Single(failure!.Errors);
        Assert.Equal(AccessContextReferenceMiddleware.InvalidAccessContextErrorCode, error.Code);
        Assert.Contains(SharedProviderHeaders.AccessContextReference, error.Message, StringComparison.Ordinal);
    }

    private static async Task<AccessContextProbeResponse> ReadContextAsync(
        HttpResponseMessage response)
        => await response.Content.ReadFromJsonAsync<AccessContextProbeResponse>() ??
            throw new InvalidOperationException("The access-context probe response was empty.");

    private static void MapTestEndpoints(WebApplication app)
    {
        app.UseStatusCodePagesWithReExecute(ReexecutionRoute);
        app.MapGet(ContextRoute, async (
            HttpContext context,
            IAccessContextReferenceAccessor accessor) =>
        {
            if (int.TryParse(
                    context.Request.Query["delayMilliseconds"],
                    out int delayMilliseconds) &&
                delayMilliseconds > 0)
            {
                await Task.Delay(delayMilliseconds, context.RequestAborted);
            }

            return Results.Ok(new AccessContextProbeResponse(accessor.Current?.Value));
        });
        app.MapGet(ReexecutionRoute, (IAccessContextReferenceAccessor accessor) =>
            Results.Ok(new AccessContextProbeResponse(accessor.Current?.Value)));
        app.MapGet(ProtectedRoute, () => Results.NoContent())
            .RequireAuthorization();
    }

    private sealed record AccessContextProbeResponse(string? Value);
}
