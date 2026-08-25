using System.Net;
using System.Net.Http.Json;
using System.Net.Mime;
using System.Text.Json;
using CanDoItAll.Modules.Workspace;
using CanDoItAll.SharedProviders.Abstractions;
using CanDoItAll.SharedProviders.Http;
using CanDoItAll.Web.Api;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Net.Http.Headers;

namespace CanDoItAll.Tests.Integration;

public sealed class SharedProviderCatalogApiIntegrationTests(
    SharedProviderCatalogApiFixture fixture) : IClassFixture<SharedProviderCatalogApiFixture>
{
    private const string NativeInvalidIfNoneMatchCode = "shared-provider.catalog.if-none-match-invalid";
    private const string OpenAiInvalidIfNoneMatchCode = "shared_provider_invalid_if_none_match";
    private const string OpenAiInvalidAccessContextCode = "shared_provider_access_context_invalid";
    private const string NativeCatalogUnavailableCode = "shared-provider.catalog.unavailable";
    private const string OpenAiCatalogUnavailableCode = "shared_provider_catalog_unavailable";
    private const string CallerSuppliedRequestId = "caller-controlled-request-id";

    [Fact]
    public async Task NativeCatalog_ReturnsCanonicalSanitizedCatalog()
    {
        using var response = await fixture.Host.Client.GetAsync(SharedProviderRoutes.Catalog);
        string body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(MediaTypeNames.Application.Json, response.Content.Headers.ContentType?.MediaType);
        Assert.Equal(
            SharedProviderProtocolJson.SerializeCatalog(SharedProviderApiTestData.Catalog),
            body);
        var parsed = SharedProviderProtocolJson.DeserializeCatalog(body);
        Assert.Equal(SharedProviderApiTestData.Catalog.SchemaVersion, parsed.SchemaVersion);
        Assert.Equal(SharedProviderApiTestData.Catalog.SourceInstanceId, parsed.SourceInstanceId);
        Assert.Equal(SharedProviderApiTestData.Catalog.CatalogRevision, parsed.CatalogRevision);
        Assert.Equal(
            SharedProviderApiTestData.RoutingModelId,
            Assert.Single(Assert.Single(parsed.Providers).Models).Id);

        using var failedResponse = await SendControlledCatalogFailureAsync(
            SharedProviderRoutes.Catalog);
        Assert.Equal(HttpStatusCode.ServiceUnavailable, failedResponse.StatusCode);
        var failure = await failedResponse.Content.ReadFromJsonAsync<ApiErrorResponse>();
        Assert.Equal(NativeCatalogUnavailableCode, Assert.Single(failure!.Errors).Code);
        await AssertSafeCatalogFailureAsync(failedResponse);
    }

    [Fact]
    public async Task OpenAiModels_ReturnsOnlyPublicRoutingModels()
    {
        var relaySupportCatalog = fixture.Host.App.Services
            .GetRequiredService<ISharedProviderRelaySupportCatalog>();
        Assert.IsType<SharedProviderRelaySupportCatalog>(relaySupportCatalog);
        var actualDescriptors = relaySupportCatalog.List()
            .Select(descriptor => (descriptor.ConnectorPluginKey, descriptor.Purpose))
            .OrderBy(item => item.ConnectorPluginKey, StringComparer.Ordinal)
            .ThenBy(item => item.Purpose)
            .ToArray();
        (string ConnectorPluginKey, SharedProviderPurpose Purpose)[] expectedDescriptors =
        [
            ("provider.comfyui.local", SharedProviderPurpose.ImageGeneration),
            ("provider.ollama.local", SharedProviderPurpose.Chat),
            ("provider.ollama.remote", SharedProviderPurpose.Chat),
            ("provider.openai", SharedProviderPurpose.Chat),
            ("provider.openai", SharedProviderPurpose.ImageGeneration)
        ];
        Assert.Equal(expectedDescriptors, actualDescriptors);
        foreach (string forbidden in new[]
        {
            "scenario",
            "process",
            "candoitall-shared",
            "fallback",
            "audio",
            "azure"
        })
        {
            Assert.DoesNotContain(
                actualDescriptors,
                descriptor => descriptor.ConnectorPluginKey.Contains(
                    forbidden,
                    StringComparison.OrdinalIgnoreCase));
        }

        using var response = await fixture.Host.Client.GetAsync(SharedProviderRoutes.Models);
        var payload = await ReadOpenAiModelsAsync(response);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(SharedProviderOpenAiConstants.ListObject, payload.Object);
        var model = Assert.Single(payload.Data);
        Assert.Equal(SharedProviderApiTestData.RoutingModelId, model.Id);
        Assert.Equal(SharedProviderOpenAiConstants.ModelObject, model.Object);
        Assert.Equal(0, model.Created);
        Assert.Equal(SharedProviderOpenAiConstants.OwnedBy, model.OwnedBy);

        using var failedResponse = await SendControlledCatalogFailureAsync(
            SharedProviderRoutes.Models);
        Assert.Equal(HttpStatusCode.ServiceUnavailable, failedResponse.StatusCode);
        var failure = await ReadOpenAiErrorAsync(failedResponse);
        Assert.Equal(OpenAiCatalogUnavailableCode, failure.Error.Code);
        Assert.Equal(SharedProviderOpenAiConstants.ApiErrorType, failure.Error.Type);
        Assert.Null(failure.Error.Param);
        await AssertSafeCatalogFailureAsync(failedResponse);
    }

    [Fact]
    public async Task NativeCatalog_EmitsStrongCatalogEntityTag()
    {
        using var response = await fixture.Host.Client.GetAsync(SharedProviderRoutes.Catalog);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(
            SharedProviderApiTestData.EntityTag.Value,
            Assert.Single(response.Headers.GetValues(HeaderNames.ETag)));
        Assert.False(response.Headers.ETag!.IsWeak);
    }

    [Fact]
    public async Task MatchingWeakStrongListAndWildcardIfNoneMatch_NativeCatalog_ReturnNotModified()
    {
        foreach (string[] headerValues in MatchingConditionalHeaders())
        {
            using var response = await SendConditionalAsync(
                SharedProviderRoutes.Catalog,
                headerValues);

            Assert.Equal(HttpStatusCode.NotModified, response.StatusCode);
            Assert.Empty(await response.Content.ReadAsByteArrayAsync());
            Assert.Equal(
                SharedProviderApiTestData.EntityTag.Value,
                Assert.Single(response.Headers.GetValues(HeaderNames.ETag)));
            AssertSharedProviderResponseHeaders(response);
        }
    }

    [Fact]
    public async Task MatchingWeakStrongListAndWildcardIfNoneMatch_OpenAiModels_ReturnNotModified()
    {
        foreach (string[] headerValues in MatchingConditionalHeaders())
        {
            using var response = await SendConditionalAsync(
                SharedProviderRoutes.Models,
                headerValues);

            Assert.Equal(HttpStatusCode.NotModified, response.StatusCode);
            Assert.Empty(await response.Content.ReadAsByteArrayAsync());
            Assert.Equal(
                SharedProviderApiTestData.EntityTag.Value,
                Assert.Single(response.Headers.GetValues(HeaderNames.ETag)));
            AssertSharedProviderResponseHeaders(response);
        }
    }

    [Fact]
    public async Task NonMatchingStrongIfNoneMatch_ReturnsCurrentCatalog()
    {
        const string differentEntityTag = "\"different-public-representation\"";

        using var response = await SendConditionalAsync(
            SharedProviderRoutes.Catalog,
            differentEntityTag);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(
            SharedProviderProtocolJson.SerializeCatalog(SharedProviderApiTestData.Catalog),
            await response.Content.ReadAsStringAsync());
        Assert.Equal(
            SharedProviderApiTestData.EntityTag.Value,
            Assert.Single(response.Headers.GetValues(HeaderNames.ETag)));
    }

    [Fact]
    public async Task MalformedIfNoneMatch_NativeCatalog_ReturnsNativeBadRequest()
    {
        foreach (string[] headerValues in MalformedConditionalHeaders())
        {
            using var response = await SendConditionalAsync(
                SharedProviderRoutes.Catalog,
                headerValues);

            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            var payload = await response.Content.ReadFromJsonAsync<ApiErrorResponse>();
            var error = Assert.Single(payload!.Errors);
            Assert.Equal(NativeInvalidIfNoneMatchCode, error.Code);
            Assert.Contains(HeaderNames.IfNoneMatch, error.Message, StringComparison.Ordinal);
            AssertSharedProviderResponseHeaders(response);
        }
    }

    [Fact]
    public async Task MalformedIfNoneMatch_OpenAiModels_ReturnsOpenAiBadRequest()
    {
        foreach (string[] headerValues in MalformedConditionalHeaders())
        {
            using var response = await SendConditionalAsync(
                SharedProviderRoutes.Models,
                headerValues);

            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            var payload = await ReadOpenAiErrorAsync(response);
            Assert.Equal(OpenAiInvalidIfNoneMatchCode, payload.Error.Code);
            Assert.Equal(SharedProviderOpenAiConstants.InvalidRequestErrorType, payload.Error.Type);
            Assert.Equal(HeaderNames.IfNoneMatch, payload.Error.Param);
            AssertSharedProviderResponseHeaders(response);
        }
    }

    [Fact]
    public async Task NativeCatalog_EmitsPrivateNoCacheAndServerRequestId()
    {
        using var response = await SendWithCallerRequestIdAsync(SharedProviderRoutes.Catalog);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        AssertSharedProviderResponseHeaders(response);
        Assert.NotEqual(
            CallerSuppliedRequestId,
            Assert.Single(response.Headers.GetValues(SharedProviderHeaders.RequestId)));
    }

    [Fact]
    public async Task OpenAiModels_EmitsPrivateNoCacheAndServerRequestId()
    {
        using var response = await SendWithCallerRequestIdAsync(SharedProviderRoutes.Models);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        AssertSharedProviderResponseHeaders(response);
        Assert.NotEqual(
            CallerSuppliedRequestId,
            Assert.Single(response.Headers.GetValues(SharedProviderHeaders.RequestId)));
    }

    [Fact]
    public async Task MalformedAccessContext_NativeCatalog_ReturnsNativeBadRequest()
    {
        using var response = await SendMalformedAccessContextAsync(SharedProviderRoutes.Catalog);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var payload = await response.Content.ReadFromJsonAsync<ApiErrorResponse>();
        var error = Assert.Single(payload!.Errors);
        Assert.Equal(AccessContextReferenceMiddleware.InvalidAccessContextErrorCode, error.Code);
        Assert.Contains(SharedProviderHeaders.AccessContextReference, error.Message, StringComparison.Ordinal);
        AssertSharedProviderResponseHeaders(response);
    }

    [Fact]
    public async Task MalformedAccessContext_OpenAiModels_ReturnsOpenAiBadRequest()
    {
        using var response = await SendMalformedAccessContextAsync(SharedProviderRoutes.Models);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var payload = await ReadOpenAiErrorAsync(response);
        Assert.Equal(OpenAiInvalidAccessContextCode, payload.Error.Code);
        Assert.Equal(SharedProviderOpenAiConstants.InvalidRequestErrorType, payload.Error.Type);
        Assert.Equal(SharedProviderHeaders.AccessContextReference, payload.Error.Param);
        AssertSharedProviderResponseHeaders(response);
    }

    [Fact]
    public async Task CatalogSurfaces_DoNotExposePrivateFieldsOrSentinelValues()
    {
        using var nativeResponse = await fixture.Host.Client.GetAsync(SharedProviderRoutes.Catalog);
        using var openAiResponse = await fixture.Host.Client.GetAsync(SharedProviderRoutes.Models);
        string bodies = string.Concat(
            await nativeResponse.Content.ReadAsStringAsync(),
            await openAiResponse.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.OK, nativeResponse.StatusCode);
        Assert.Equal(HttpStatusCode.OK, openAiResponse.StatusCode);
        foreach (string forbidden in SharedProviderApiTestData.ForbiddenPublicContent)
        {
            Assert.DoesNotContain(forbidden, bodies, StringComparison.OrdinalIgnoreCase);
        }
    }

    [Fact]
    public async Task OpenApi_DescribesCatalogAndModelsOperationsAndResponses()
    {
        using var document = JsonDocument.Parse(
            await fixture.Host.Client.GetStringAsync("/openapi/v1.json"));
        var paths = document.RootElement.GetProperty("paths");

        AssertOpenApiOperation(
            paths,
            SharedProviderRoutes.Catalog,
            "GetSharedProviderCatalog");
        AssertOpenApiOperation(
            paths,
            SharedProviderRoutes.Models,
            "GetSharedProviderOpenAiModels");
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

    private async Task<HttpResponseMessage> SendConditionalAsync(
        string route,
        params string[] headerValues)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, route);
        Assert.True(request.Headers.TryAddWithoutValidation(HeaderNames.IfNoneMatch, headerValues));
        return await fixture.Host.Client.SendAsync(request);
    }

    private async Task<HttpResponseMessage> SendWithCallerRequestIdAsync(string route)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, route);
        Assert.True(request.Headers.TryAddWithoutValidation(
            SharedProviderHeaders.RequestId,
            CallerSuppliedRequestId));
        return await fixture.Host.Client.SendAsync(request);
    }

    private async Task<HttpResponseMessage> SendMalformedAccessContextAsync(string route)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, route);
        Assert.True(request.Headers.TryAddWithoutValidation(
            SharedProviderHeaders.AccessContextReference,
            ["tenant-a", "tenant-b"]));
        return await fixture.Host.Client.SendAsync(request);
    }

    private async Task<HttpResponseMessage> SendControlledCatalogFailureAsync(string route)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, route);
        Assert.True(request.Headers.TryAddWithoutValidation(
            StubSharedProviderCatalogQueryService.ControlledFailureHeader,
            "true"));
        return await fixture.Host.Client.SendAsync(request);
    }

    private static IEnumerable<string[]> MalformedConditionalHeaders()
    {
        string current = SharedProviderApiTestData.EntityTag.Value;
        yield return [current[1..^1]];
        yield return [$"W/{current[1..^1]}"];
        yield return [current[..^1]];
        yield return [$"*, {current}"];
        yield return [$"{current}, *"];
        yield return [$"\"{new string('a', 8_192)}\""];
        yield return
        [
            string.Join(
                ", ",
                Enumerable.Range(0, 33).Select(index => $"\"tag-{index}\""))
        ];
    }

    private static IEnumerable<string[]> MatchingConditionalHeaders()
    {
        string current = SharedProviderApiTestData.EntityTag.Value;
        const string different = "\"different-public-representation\"";
        yield return [current];
        yield return [$"W/{current}"];
        yield return ["*"];
        yield return [$"{different}, W/{current}"];
        yield return [different, current];
    }

    private static void AssertSharedProviderResponseHeaders(HttpResponseMessage response)
    {
        Assert.True(response.Headers.CacheControl?.Private);
        Assert.True(response.Headers.CacheControl?.NoCache);
        string requestId = Assert.Single(response.Headers.GetValues(SharedProviderHeaders.RequestId));
        Assert.False(string.IsNullOrWhiteSpace(requestId));
    }

    private static async Task AssertSafeCatalogFailureAsync(HttpResponseMessage response)
    {
        AssertSharedProviderResponseHeaders(response);
        string body = await response.Content.ReadAsStringAsync();
        Assert.DoesNotContain("InvalidOperationException", body, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("database connection", body, StringComparison.OrdinalIgnoreCase);
        foreach (string forbidden in SharedProviderApiTestData.ForbiddenPublicContent)
        {
            Assert.DoesNotContain(forbidden, body, StringComparison.OrdinalIgnoreCase);
        }
    }

    private static void AssertOpenApiOperation(
        JsonElement paths,
        string route,
        string operationId)
    {
        var operation = paths.GetProperty(route).GetProperty("get");
        Assert.Equal(operationId, operation.GetProperty("operationId").GetString());
        Assert.Contains(
            operation.GetProperty("tags").EnumerateArray(),
            tag => tag.GetString() == "Shared Providers");

        var responses = operation.GetProperty("responses");
        foreach (string statusCode in new[] { "200", "304", "400", "401", "403", "503" })
        {
            Assert.True(responses.TryGetProperty(statusCode, out _), $"Missing {statusCode} metadata for {route}.");
            AssertOpenApiResponseHeaders(
                responses.GetProperty(statusCode),
                includesEntityTag: statusCode is "200" or "304");
        }

        AssertOpenApiHeaderParameter(
            operation,
            HeaderNames.IfNoneMatch,
            SharedProviderCatalogApi.MaximumIfNoneMatchLength,
            expectedPattern: null);
        AssertOpenApiHeaderParameter(
            operation,
            SharedProviderHeaders.AccessContextReference,
            AccessContextReference.MaximumLength,
            SharedProviderCatalogOpenApiContract.AccessContextPattern);
    }

    private static void AssertOpenApiHeaderParameter(
        JsonElement operation,
        string name,
        int maximumLength,
        string? expectedPattern)
    {
        var parameter = Assert.Single(
            operation.GetProperty("parameters").EnumerateArray(),
            candidate => string.Equals(
                candidate.GetProperty("name").GetString(),
                name,
                StringComparison.Ordinal));
        Assert.Equal("header", parameter.GetProperty("in").GetString());
        Assert.False(
            parameter.TryGetProperty("required", out var required) &&
            required.GetBoolean());

        var schema = parameter.GetProperty("schema");
        Assert.Equal("string", schema.GetProperty("type").GetString());
        Assert.Equal(1, schema.GetProperty("minLength").GetInt32());
        Assert.Equal(maximumLength, schema.GetProperty("maxLength").GetInt32());
        if (expectedPattern is null)
        {
            Assert.False(schema.TryGetProperty("pattern", out _));
        }
        else
        {
            Assert.Equal(expectedPattern, schema.GetProperty("pattern").GetString());
        }
    }

    private static void AssertOpenApiResponseHeaders(
        JsonElement response,
        bool includesEntityTag)
    {
        var headers = response.GetProperty("headers");
        AssertOpenApiResponseHeader(
            headers,
            HeaderNames.CacheControl,
            SharedProviderCatalogOpenApiContract.PrivateNoCachePattern,
            expectedMinimumLength: null);
        AssertOpenApiResponseHeader(
            headers,
            SharedProviderHeaders.RequestId,
            expectedPattern: null,
            expectedMinimumLength: 1);

        if (includesEntityTag)
        {
            AssertOpenApiResponseHeader(
                headers,
                HeaderNames.ETag,
                SharedProviderCatalogOpenApiContract.CatalogEntityTagPattern,
                expectedMinimumLength: null);
        }
        else
        {
            Assert.False(headers.TryGetProperty(HeaderNames.ETag, out _));
        }
    }

    private static void AssertOpenApiResponseHeader(
        JsonElement headers,
        string name,
        string? expectedPattern,
        int? expectedMinimumLength)
    {
        var header = headers.GetProperty(name);
        Assert.True(header.GetProperty("required").GetBoolean());
        var schema = header.GetProperty("schema");
        Assert.Equal("string", schema.GetProperty("type").GetString());

        if (expectedPattern is null)
        {
            Assert.False(schema.TryGetProperty("pattern", out _));
        }
        else
        {
            Assert.Equal(expectedPattern, schema.GetProperty("pattern").GetString());
        }

        if (expectedMinimumLength is null)
        {
            Assert.False(schema.TryGetProperty("minLength", out _));
        }
        else
        {
            Assert.Equal(
                expectedMinimumLength,
                schema.GetProperty("minLength").GetInt32());
        }
    }
}

public sealed class SharedProviderCatalogApiFixture : IAsyncLifetime
{
    private ApiTestHost? host;

    internal ApiTestHost Host
        => host ?? throw new InvalidOperationException("The shared-provider API host is not initialized.");

    public async Task InitializeAsync()
    {
        host = await ApiTestHost.CreateAsync(
            jwtEnabled: false,
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

internal sealed class StubSharedProviderCatalogQueryService(
    SharedProviderCatalogSnapshot snapshot,
    IHttpContextAccessor httpContextAccessor) : ISharedProviderCatalogQueryService
{
    internal const string ControlledFailureHeader = "CanDoItAll-Test-Catalog-Failure";

    public Task<SharedProviderCatalogSnapshot> GetSnapshotAsync(
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (httpContextAccessor.HttpContext?.Request.Headers.ContainsKey(
                ControlledFailureHeader) == true)
        {
            throw new InvalidOperationException(
                "InvalidOperationException: database connection failed for central-secret-value-sentinel at https://private-upstream.example.test.");
        }

        return Task.FromResult(snapshot);
    }
}

internal static class SharedProviderApiTestData
{
    private static readonly SharedProviderPublicRevision PlaceholderRevision = new(
        $"{SharedProviderPublicRevision.Prefix}{new string('0', SharedProviderPublicRevision.HashLength)}");

    public static SharedProviderCatalogDocument Catalog { get; } = CreateCatalog();

    public static SharedProviderRoutingModelId RoutingModelId
        => Catalog.Providers.Single().Models.Single().Id;

    public static SharedProviderCatalogEntityTag EntityTag { get; } =
        SharedProviderCatalogEntityTag.FromRevision(Catalog.CatalogRevision);

    public static SharedProviderCatalogSnapshot Snapshot { get; } = new(Catalog, EntityTag);

    public static IReadOnlyList<string> ForbiddenPublicContent { get; } =
    [
        "providerProfileId",
        "configurationJson",
        "baseUri",
        "apiToken",
        "secretId",
        "secretName",
        "secretValue",
        "environmentVariable",
        "internalNotes",
        "rawHealthError",
        "private-provider-profile-sentinel",
        "central-secret-value-sentinel",
        "https://private-upstream.example.test"
    ];

    private static SharedProviderCatalogDocument CreateCatalog()
    {
        var publicationId = new SharedProviderPublicationId(
            Guid.Parse("10000000-0000-0000-0000-000000000001"));
        var routingModelId = SharedProviderRoutingModelIdCodec.Create(
            publicationId,
            "private-upstream-model-name");
        var draftPublication = new SharedProviderCatalogPublication(
            publicationId,
            PlaceholderRevision,
            "Public Chat Provider",
            SharedProviderPurpose.Chat,
            SharedProviderTransport.OpenAiCompatible,
            routingModelId,
            [
                new SharedProviderCatalogModel(
                    routingModelId,
                    "Public Chat Model",
                    [
                        SharedProviderCapability.ChatCompletions,
                        SharedProviderCapability.Responses,
                        SharedProviderCapability.Streaming
                    ])
            ],
            new SharedProviderCatalogHealth(SharedProviderHealthState.Available));
        var publication = draftPublication with
        {
            Revision = SharedProviderCanonicalRevision.ComputePublication(draftPublication)
        };
        var draftCatalog = new SharedProviderCatalogDocument(
            SharedProviderProtocolVersion.Current,
            new SharedProviderSourceInstanceId(
                Guid.Parse("20000000-0000-0000-0000-000000000001")),
            PlaceholderRevision,
            new SharedProviderProtocolDescriptor(SharedProviderRoutes.OpenAiBase),
            [publication]);
        var catalog = draftCatalog with
        {
            CatalogRevision = SharedProviderCanonicalRevision.ComputeCatalog(draftCatalog)
        };

        SharedProviderProtocolJson.ValidateCatalog(catalog);
        return catalog;
    }
}
