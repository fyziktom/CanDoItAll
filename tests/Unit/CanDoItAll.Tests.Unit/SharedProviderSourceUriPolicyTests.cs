using System.Net;
using System.Net.Http.Headers;
using System.Text;
using CanDoItAll.SharedProviders.Abstractions;
using CanDoItAll.SharedProviders.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace CanDoItAll.Tests.Unit;

public sealed class SharedProviderSourceUriPolicyTests
{
    private static readonly SharedProviderSourceInstanceId SourceInstanceId = new(
        Guid.Parse("a157823e-ad47-43e7-9b52-e9cd43f89820"));
    private static readonly SharedProviderPublicationId PublicationId = new(
        Guid.Parse("62fb875c-dc08-4e4b-a182-9bedcd814928"));

    [Fact]
    public void Normalize_CanonicalizesHostPortAndReverseProxyRoot()
    {
        var actual = Normalize("HTTPS://CENTRAL.EXAMPLE.TEST:443/proxy/root");

        Assert.Equal("https://central.example.test/proxy/root/", actual.AbsoluteUri);
        Assert.Equal(
            "https://central.example.test/proxy/root/api/shared-providers/v1/catalog",
            SharedProviderRoutes.ResolveCatalog(actual).AbsoluteUri);
    }

    [Fact]
    public void Normalize_RejectsRelativeAndNonHttpSchemes()
    {
        var policy = new SharedProviderSourceUriPolicy();

        Assert.Throws<ArgumentException>(() => policy.Normalize(
            new Uri("relative", UriKind.Relative),
            SharedProviderSourceNetworkPolicy.PublicOnly));
        Assert.Throws<ArgumentException>(() => Normalize("file:///tmp/catalog"));
        Assert.Throws<ArgumentException>(() => Normalize("ftp://central.example.test/catalog"));
    }

    [Fact]
    public void Normalize_RejectsUserInfoQueryAndFragment()
    {
        foreach (var value in new[]
        {
            "https://user:pass@central.example.test/root",
            "https://central.example.test/root?tenant=a",
            "https://central.example.test/root#catalog"
        })
        {
            Assert.Throws<ArgumentException>(() => Normalize(value));
        }
    }

    [Fact]
    public void Normalize_RejectsAmbiguousAndOverlongAddresses()
    {
        foreach (var value in new[]
        {
            "https://central.example.test./root",
            "https://central.example.test/root%2fescape",
            $"https://central.example.test/{new string('a', SharedProviderSourceUriPolicy.MaximumUriCharacters)}"
        })
        {
            Assert.Throws<ArgumentException>(() => Normalize(value));
        }
    }

    [Fact]
    public void Normalize_RejectsPlainHttpPublicDestinationsEvenWhenPrivateAccessIsApproved()
    {
        Assert.Throws<ArgumentException>(() => Normalize(
            "http://8.8.8.8/source",
            SharedProviderSourceNetworkPolicy.AllowPrivateNetwork));
        Assert.Throws<ArgumentException>(() => Normalize(
            "http://central.example.test/source",
            SharedProviderSourceNetworkPolicy.PublicOnly));
    }

    [Fact]
    public void Normalize_AllowsLoopbackHttpWithoutBroadPrivateApproval()
    {
        Assert.Equal(
            "http://localhost:5100/source/",
            Normalize("http://LOCALHOST:5100/source").AbsoluteUri);
        Assert.Equal(
            "http://127.0.0.1:5100/",
            Normalize("http://127.0.0.1:5100").AbsoluteUri);
    }

    [Fact]
    public void Normalize_PrivateHttpRequiresExplicitApproval()
    {
        Assert.Throws<ArgumentException>(() => Normalize("http://10.2.3.4/source"));

        Assert.Equal(
            "http://10.2.3.4/source/",
            Normalize(
                "http://10.2.3.4/source",
                SharedProviderSourceNetworkPolicy.AllowPrivateNetwork).AbsoluteUri);
    }

    [Fact]
    public void Normalize_PrivateAndLoopbackHttpsRequireExplicitApproval()
    {
        foreach (var value in new[]
        {
            "https://127.0.0.1/source",
            "https://10.2.3.4/source",
            "https://localhost/source"
        })
        {
            Assert.Throws<ArgumentException>(() => Normalize(value));
            Assert.StartsWith(
                "https://",
                Normalize(value, SharedProviderSourceNetworkPolicy.AllowPrivateNetwork).AbsoluteUri,
                StringComparison.Ordinal);
        }

        Assert.Equal("https://8.8.8.8/", Normalize("https://8.8.8.8").AbsoluteUri);
    }

    [Fact]
    public void PublicAddressPolicy_AcceptsOnlyGlobalUnicastDestinations()
    {
        Assert.True(IsAllowed("8.8.8.8", SharedProviderDestinationAccess.PublicOnly));
        Assert.True(IsAllowed("2606:4700:4700::1111", SharedProviderDestinationAccess.PublicOnly));

        foreach (var value in new[]
        {
            "0.0.0.0",
            "10.0.0.1",
            "100.64.0.1",
            "127.0.0.1",
            "169.254.169.254",
            "172.16.0.1",
            "192.168.0.1",
            "192.88.99.2",
            "198.51.100.1",
            "203.0.113.1",
            "::1",
            "fe80::1",
            "fc00::1",
            "2001::1",
            "2001:2::1",
            "2001:db8::1",
            "2002::1",
            "3fff::1"
        })
        {
            Assert.False(IsAllowed(value, SharedProviderDestinationAccess.PublicOnly));
        }
    }

    [Fact]
    public void TrustedAddressPolicy_AllowsPrivateButNeverLinkLocalOrMulticast()
    {
        foreach (var value in new[]
        {
            "10.0.0.1",
            "172.31.0.1",
            "192.168.0.1",
            "127.0.0.1",
            "fc00::1",
            "::1"
        })
        {
            Assert.True(IsAllowed(value, SharedProviderDestinationAccess.TrustedNetwork));
        }

        foreach (var value in new[]
        {
            "169.254.169.254",
            "224.0.0.1",
            "fe80::1",
            "ff02::1"
        })
        {
            Assert.False(IsAllowed(value, SharedProviderDestinationAccess.TrustedNetwork));
        }
    }

    [Fact]
    public async Task PrivateHttpConnectionPolicy_RejectsPublicDnsAndAllowsApprovedPrivateDns()
    {
        var publicConnector = new RecordingSocketConnector();
        var publicResolutionPolicy = CreateConnectionPolicy(
            SharedProviderDestinationAccess.ApprovedPrivateOnly,
            publicConnector,
            [IPAddress.Parse("8.8.8.8")]);

        await Assert.ThrowsAsync<HttpRequestException>(() =>
            publicResolutionPolicy.ConnectAsync(CreateConnectionEndpoint("source.test", 80), default).AsTask());
        Assert.Empty(publicConnector.Calls);

        var privateConnector = new RecordingSocketConnector();
        var privateResolutionPolicy = CreateConnectionPolicy(
            SharedProviderDestinationAccess.ApprovedPrivateOnly,
            privateConnector,
            [IPAddress.Parse("172.20.0.10")]);

        await using var stream = await privateResolutionPolicy.ConnectAsync(
            CreateConnectionEndpoint("source.test", 80),
            default);
        Assert.Single(privateConnector.Calls);
    }

    [Fact]
    public async Task PublicConnectionPolicy_RejectsPrivateAndMixedDnsAnswers()
    {
        foreach (var addresses in new[]
        {
            new[] { IPAddress.Parse("10.0.0.1") },
            new[] { IPAddress.Parse("8.8.8.8"), IPAddress.Parse("10.0.0.1") }
        })
        {
            var connector = new RecordingSocketConnector();
            var policy = CreateConnectionPolicy(
                SharedProviderDestinationAccess.PublicOnly,
                connector,
                addresses);

            await Assert.ThrowsAsync<HttpRequestException>(() =>
                policy.ConnectAsync(CreateConnectionEndpoint("source.test", 443), default).AsTask());
            Assert.Empty(connector.Calls);
        }
    }

    [Fact]
    public async Task ConnectionPolicy_RevalidatesEveryNewConnectionAgainstDnsRebinding()
    {
        var resolver = new SequenceAddressResolver(
            [IPAddress.Parse("8.8.8.8")],
            [IPAddress.Parse("10.0.0.5")]);
        var connector = new RecordingSocketConnector();
        var policy = new SharedProviderSourceConnectionPolicy(
            SharedProviderDestinationAccess.PublicOnly,
            resolver,
            connector);
        var context = CreateConnectionEndpoint("source.test", 443);

        await using var first = await policy.ConnectAsync(context, default);
        await Assert.ThrowsAsync<HttpRequestException>(() => policy.ConnectAsync(context, default).AsTask());

        Assert.Equal(2, resolver.CallCount);
        Assert.Single(connector.Calls);
    }

    [Fact]
    public async Task HandlerAndDependencyInjection_DisableRedirectsProxyCookiesAndUriLogging()
    {
        using var handler = SharedProviderSourceHttpHandlerFactory.Create(
            SharedProviderDestinationAccess.PublicOnly,
            new SequenceAddressResolver([IPAddress.Parse("8.8.8.8")]),
            new RecordingSocketConnector());

        Assert.False(handler.AllowAutoRedirect);
        Assert.False(handler.UseProxy);
        Assert.False(handler.UseCookies);
        Assert.NotNull(handler.ConnectCallback);
        Assert.Equal(TimeSpan.FromSeconds(10), handler.ConnectTimeout);
        Assert.Equal(TimeSpan.FromMinutes(5), handler.PooledConnectionLifetime);
        Assert.Null(handler.SslOptions.RemoteCertificateValidationCallback);

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSharedProviderHttpDescriptors();
        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();
        Assert.IsType<SharedProviderSourceUriPolicy>(
            scope.ServiceProvider.GetRequiredService<ISharedProviderSourceUriPolicy>());
        Assert.IsType<SharedProviderCatalogClient>(
            scope.ServiceProvider.GetRequiredService<ISharedProviderCatalogClient>());
        var handlerFactory = provider.GetRequiredService<IHttpMessageHandlerFactory>();
        foreach (var clientName in new[]
        {
            SharedProviderCatalogClient.PublicClientName,
            SharedProviderCatalogClient.TrustedNetworkClientName,
            SharedProviderCatalogClient.PrivateHttpClientName
        })
        {
            var registeredHandler = Assert.IsType<SocketsHttpHandler>(
                UnwrapPrimaryHandler(handlerFactory.CreateHandler(clientName)));
            Assert.False(registeredHandler.AllowAutoRedirect);
            Assert.False(registeredHandler.UseProxy);
            Assert.False(registeredHandler.UseCookies);
            Assert.NotNull(registeredHandler.ConnectCallback);
            Assert.Equal(TimeSpan.FromSeconds(10), registeredHandler.ConnectTimeout);
            Assert.Equal(TimeSpan.FromMinutes(5), registeredHandler.PooledConnectionLifetime);
            Assert.Null(registeredHandler.SslOptions.RemoteCertificateValidationCallback);
        }

        var loggerProvider = new RecordingLoggerProvider();
        var loggingServices = new ServiceCollection();
        loggingServices.AddLogging(builder =>
        {
            builder.ClearProviders();
            builder.AddProvider(loggerProvider);
            builder.SetMinimumLevel(LogLevel.Trace);
        });
        loggingServices.AddSharedProviderHttpDescriptors();
        var loggingRequests = new (string ClientName, string RequestUri)[]
        {
            (
                SharedProviderCatalogClient.PublicClientName,
                "https://private-source.example.test/sensitive/catalog/path"),
            (
                SharedProviderHttpRelayClient.ClientName,
                "https://private-relay.example.test/sensitive/relay/path")
        };
        foreach (var request in loggingRequests)
        {
            loggingServices.AddHttpClient(request.ClientName)
                .ConfigurePrimaryHttpMessageHandler(() => new CallbackHandler(
                    (_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK))));
        }

        using var loggingProvider = loggingServices.BuildServiceProvider();
        var loggingClientFactory = loggingProvider.GetRequiredService<IHttpClientFactory>();
        foreach (var request in loggingRequests)
        {
            using var loggingClient = loggingClientFactory.CreateClient(request.ClientName);
            using var response = await loggingClient.GetAsync(request.RequestUri);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }

        Assert.DoesNotContain(
            loggerProvider.Messages,
            message => message.Contains("private-source.example.test", StringComparison.Ordinal) ||
                message.Contains("sensitive/catalog/path", StringComparison.Ordinal) ||
                message.Contains("private-relay.example.test", StringComparison.Ordinal) ||
                message.Contains("sensitive/relay/path", StringComparison.Ordinal));
    }

    [Fact]
    public void CatalogRequest_UsesTypedRedactedCredentialAndPinnedIdentity()
    {
        var token = new SharedProviderCatalogAccessToken("catalog-token_123");
        var entityTag = SharedProviderCatalogEntityTag.FromRevision(CreateCatalog().CatalogRevision);
        var request = new SharedProviderCatalogFetchRequest(
            new Uri("https://central.example.test/root"),
            SharedProviderSourceNetworkPolicy.PublicOnly,
            token,
            entityTag,
            SourceInstanceId);

        Assert.Equal("[REDACTED]", token.ToString());
        Assert.Equal(nameof(SharedProviderCatalogFetchRequest), request.ToString());
        Assert.DoesNotContain("central.example.test", request.ToString(), StringComparison.Ordinal);
        Assert.DoesNotContain("catalog-token_123", request.ToString(), StringComparison.Ordinal);
        Assert.Equal(entityTag, request.IfNoneMatch);
        Assert.Equal(SourceInstanceId, request.ExpectedSourceInstanceId);
        Assert.Throws<ArgumentException>(() => new SharedProviderCatalogAccessToken("unsafe\r\ntoken"));
        Assert.Throws<ArgumentOutOfRangeException>(() => new SharedProviderCatalogFetchRequest(
            new Uri("https://central.example.test/root"),
            (SharedProviderSourceNetworkPolicy)999,
            token));
    }

    [Fact]
    public async Task CatalogClient_SendsCanonicalAuthenticatedRequestAndValidatesSuccess()
    {
        var catalog = CreateCatalog();
        var entityTag = SharedProviderCatalogEntityTag.FromRevision(catalog.CatalogRevision);
        CapturedRequest? captured = null;
        var factory = new RecordingHttpClientFactory((request, _) =>
        {
            captured = CapturedRequest.From(request);
            return Task.FromResult(CatalogResponse(catalog, entityTag));
        });
        var client = CreateClient(
            factory,
            new FixedAccessContextReferenceAccessor(new AccessContextReference("project:alpha")));
        var result = await client.FetchAsync(new SharedProviderCatalogFetchRequest(
            new Uri("HTTPS://CENTRAL.EXAMPLE.TEST:443/proxy"),
            SharedProviderSourceNetworkPolicy.PublicOnly,
            new SharedProviderCatalogAccessToken("catalog-token"),
            expectedSourceInstanceId: SourceInstanceId));

        var succeeded = Assert.IsType<SharedProviderCatalogFetchResult.Succeeded>(result);
        Assert.Equal(catalog.SourceInstanceId, succeeded.Catalog.SourceInstanceId);
        Assert.Equal(entityTag, succeeded.EntityTag);
        Assert.Equal(SharedProviderCatalogClient.PublicClientName, factory.RequestedClientName);
        Assert.NotNull(captured);
        Assert.Equal(
            "https://central.example.test/proxy/api/shared-providers/v1/catalog",
            captured.RequestUri.AbsoluteUri);
        Assert.Equal("Bearer", captured.AuthorizationScheme);
        Assert.Equal("catalog-token", captured.AuthorizationParameter);
        Assert.Equal("project:alpha", captured.AccessContext);
        Assert.Equal("application/json", captured.Accept);
    }

    [Fact]
    public async Task CatalogClient_HandlesConditionalNoOpAndSanitizedStatusFailures()
    {
        var catalog = CreateCatalog();
        var entityTag = SharedProviderCatalogEntityTag.FromRevision(catalog.CatalogRevision);
        var notModifiedFactory = new RecordingHttpClientFactory((request, _) =>
        {
            Assert.Equal(entityTag.Value, Assert.Single(request.Headers.IfNoneMatch).ToString());
            var response = new HttpResponseMessage(HttpStatusCode.NotModified)
            {
                Content = new ByteArrayContent([])
            };
            response.Headers.ETag = EntityTagHeaderValue.Parse(entityTag.Value);
            return Task.FromResult(response);
        });
        var notModified = await CreateClient(notModifiedFactory).FetchAsync(CreateFetchRequest(
            ifNoneMatch: entityTag));
        Assert.Equal(
            entityTag,
            Assert.IsType<SharedProviderCatalogFetchResult.NotModified>(notModified).EntityTag);

        foreach (var (status, category) in new[]
        {
            (HttpStatusCode.Unauthorized, SharedProviderFailureCategory.Unauthorized),
            (HttpStatusCode.Forbidden, SharedProviderFailureCategory.InsufficientScope),
            (HttpStatusCode.TooManyRequests, SharedProviderFailureCategory.RateLimited),
            (HttpStatusCode.ServiceUnavailable, SharedProviderFailureCategory.Unavailable)
        })
        {
            var factory = new RecordingHttpClientFactory((_, _) => Task.FromResult(
                new HttpResponseMessage(status)
                {
                    Content = new StringContent("secret-token raw upstream failure")
                }));
            var failed = Assert.IsType<SharedProviderCatalogFetchResult.Failed>(
                await CreateClient(factory).FetchAsync(CreateFetchRequest()));
            Assert.Equal(category, failed.Failure.Category);
            Assert.DoesNotContain("secret-token", failed.Failure.SanitizedMessage, StringComparison.Ordinal);
            Assert.DoesNotContain("raw upstream", failed.Failure.SanitizedMessage, StringComparison.Ordinal);
        }
    }

    [Fact]
    public async Task CatalogClient_RejectsInvalidBoundsContractIdentityAndMapsTimeouts()
    {
        var catalog = CreateCatalog();
        var entityTag = SharedProviderCatalogEntityTag.FromRevision(catalog.CatalogRevision);
        var otherEntityTag = new SharedProviderCatalogEntityTag($"\"sha256:{new string('f', 64)}\"");
        var invalidResponses = new Func<HttpResponseMessage>[]
        {
            () => CatalogResponse(catalog, entityTag, mediaType: "text/plain"),
            () => CatalogResponse(catalog, otherEntityTag),
            () => CatalogResponse(
                catalog,
                entityTag,
                json: SharedProviderProtocolJson.SerializeCatalog(catalog)
                    .Replace("\"schemaVersion\":\"1.1\"", "\"schemaVersion\":\"2.0\"", StringComparison.Ordinal)),
            () => new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StreamContent(new MemoryStream(new byte[SharedProviderCatalogClient.MaximumResponseBytes + 1]))
            }
        };
        for (var index = 0; index < invalidResponses.Length; index++)
        {
            HttpResponseMessage CreateResponse()
            {
                if (index != invalidResponses.Length - 1)
                {
                    return invalidResponses[index]();
                }

                var response = invalidResponses[index]();
                response.Content.Headers.ContentType = new MediaTypeHeaderValue("application/json");
                response.Headers.ETag = EntityTagHeaderValue.Parse(entityTag.Value);
                return response;
            }

            var factory = new RecordingHttpClientFactory((_, _) => Task.FromResult(CreateResponse()));
            var failed = Assert.IsType<SharedProviderCatalogFetchResult.Failed>(
                await CreateClient(factory).FetchAsync(CreateFetchRequest()));
            Assert.Equal(SharedProviderFailureCategory.VersionUnsupported, failed.Failure.Category);
        }

        var identityFactory = new RecordingHttpClientFactory((_, _) =>
            Task.FromResult(CatalogResponse(catalog, entityTag)));
        var identityMismatch = Assert.IsType<SharedProviderCatalogFetchResult.Failed>(
            await CreateClient(identityFactory).FetchAsync(new SharedProviderCatalogFetchRequest(
                new Uri("https://central.example.test"),
                SharedProviderSourceNetworkPolicy.PublicOnly,
                new SharedProviderCatalogAccessToken("catalog-token"),
                expectedSourceInstanceId: new SharedProviderSourceInstanceId(Guid.NewGuid()))));
        Assert.Equal(SharedProviderFailureCategory.Conflict, identityMismatch.Failure.Category);
        Assert.Equal(SharedProviderCatalogFailureCodes.SourceIdentityMismatch, identityMismatch.Failure.Code);

        var bodyTimeoutFactory = new RecordingHttpClientFactory((_, _) =>
        {
            var response = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StreamContent(new CancellingReadStream())
            };
            response.Content.Headers.ContentType = new MediaTypeHeaderValue("application/json");
            response.Headers.ETag = EntityTagHeaderValue.Parse(entityTag.Value);
            return Task.FromResult(response);
        });
        var bodyTimeout = Assert.IsType<SharedProviderCatalogFetchResult.Failed>(
            await CreateClient(bodyTimeoutFactory).FetchAsync(CreateFetchRequest()));
        Assert.Equal(SharedProviderFailureCategory.Timeout, bodyTimeout.Failure.Category);

        using var callerCancellation = new CancellationTokenSource();
        callerCancellation.Cancel();
        var cancelledFactory = new RecordingHttpClientFactory((_, token) =>
            Task.FromCanceled<HttpResponseMessage>(token));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            CreateClient(cancelledFactory).FetchAsync(
                CreateFetchRequest(),
                callerCancellation.Token).AsTask());
    }

    private static Uri Normalize(
        string value,
        SharedProviderSourceNetworkPolicy networkPolicy = SharedProviderSourceNetworkPolicy.PublicOnly)
        => new SharedProviderSourceUriPolicy().Normalize(new Uri(value, UriKind.Absolute), networkPolicy);

    private static bool IsAllowed(string address, SharedProviderDestinationAccess access)
        => SharedProviderSourceAddressPolicy.IsAllowed(IPAddress.Parse(address), access);

    private static SharedProviderSourceConnectionPolicy CreateConnectionPolicy(
        SharedProviderDestinationAccess access,
        RecordingSocketConnector connector,
        IReadOnlyList<IPAddress> addresses)
        => new(access, new SequenceAddressResolver(addresses), connector);

    private static DnsEndPoint CreateConnectionEndpoint(string host, int port)
        => new(host, port);

    private static HttpMessageHandler UnwrapPrimaryHandler(HttpMessageHandler handler)
    {
        while (handler is DelegatingHandler delegatingHandler)
        {
            handler = delegatingHandler.InnerHandler
                ?? throw new InvalidOperationException("The registered HTTP handler pipeline is incomplete.");
        }

        return handler;
    }

    private static SharedProviderCatalogClient CreateClient(
        IHttpClientFactory factory,
        IAccessContextReferenceAccessor? accessContextAccessor = null)
        => new(
            factory,
            new SharedProviderSourceUriPolicy(),
            NullLogger<SharedProviderCatalogClient>.Instance,
            accessContextAccessor);

    private static SharedProviderCatalogFetchRequest CreateFetchRequest(
        SharedProviderCatalogEntityTag? ifNoneMatch = null)
        => new(
            new Uri("https://central.example.test/root"),
            SharedProviderSourceNetworkPolicy.PublicOnly,
            new SharedProviderCatalogAccessToken("catalog-token"),
            ifNoneMatch);

    private static HttpResponseMessage CatalogResponse(
        SharedProviderCatalogDocument catalog,
        SharedProviderCatalogEntityTag entityTag,
        string mediaType = "application/json",
        string? json = null)
    {
        var response = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(
                json ?? SharedProviderProtocolJson.SerializeCatalog(catalog),
                Encoding.UTF8,
                mediaType)
        };
        response.Headers.ETag = EntityTagHeaderValue.Parse(entityTag.Value);
        return response;
    }

    private static SharedProviderCatalogDocument CreateCatalog()
    {
        var placeholder = new SharedProviderPublicRevision($"sha256:{new string('0', 64)}");
        var modelId = SharedProviderRoutingModelIdCodec.Create(PublicationId, "model-a");
        var publication = new SharedProviderCatalogPublication(
            PublicationId,
            placeholder,
            "Shared model",
            SharedProviderPurpose.Chat,
            SharedProviderTransport.OpenAiCompatible,
            modelId,
            [
                new SharedProviderCatalogModel(
                    modelId,
                    "Model A",
                    [SharedProviderCapability.ChatCompletions])
            ],
            new SharedProviderCatalogHealth(SharedProviderHealthState.Available));
        publication = publication with
        {
            Revision = SharedProviderCanonicalRevision.ComputePublication(publication)
        };
        var catalog = new SharedProviderCatalogDocument(
            SharedProviderProtocolVersion.Current,
            SourceInstanceId,
            placeholder,
            new SharedProviderProtocolDescriptor(SharedProviderRoutes.OpenAiBase),
            [publication]);
        return catalog with
        {
            CatalogRevision = SharedProviderCanonicalRevision.ComputeCatalog(catalog)
        };
    }

    private sealed class SequenceAddressResolver(params IReadOnlyList<IPAddress>[] answers) :
        ISharedProviderHostAddressResolver
    {
        private int index;

        public int CallCount => index;

        public ValueTask<IReadOnlyList<IPAddress>> ResolveAsync(
            string host,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (index >= answers.Length)
            {
                throw new InvalidOperationException("No scripted DNS answer remains.");
            }

            return ValueTask.FromResult(answers[index++]);
        }
    }

    private sealed class RecordingSocketConnector : ISharedProviderSocketConnector
    {
        public List<(IReadOnlyList<IPAddress> Addresses, int Port)> Calls { get; } = [];

        public ValueTask<Stream> ConnectAsync(
            IReadOnlyList<IPAddress> addresses,
            int port,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Calls.Add((addresses, port));
            return ValueTask.FromResult<Stream>(new MemoryStream());
        }
    }

    private sealed class RecordingHttpClientFactory(
        Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> callback) : IHttpClientFactory
    {
        public string? RequestedClientName { get; private set; }

        public HttpClient CreateClient(string name)
        {
            RequestedClientName = name;
            return new HttpClient(new CallbackHandler(callback), disposeHandler: true);
        }
    }

    private sealed class FixedAccessContextReferenceAccessor(AccessContextReference? current) :
        IAccessContextReferenceAccessor
    {
        public AccessContextReference? Current { get; } = current;
    }

    private sealed class CallbackHandler(
        Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> callback) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
            => callback(request, cancellationToken);
    }

    private sealed class RecordingLoggerProvider : ILoggerProvider
    {
        private readonly List<string> messages = [];

        public IReadOnlyList<string> Messages
        {
            get
            {
                lock (messages)
                {
                    return messages.ToArray();
                }
            }
        }

        public ILogger CreateLogger(string categoryName)
            => new RecordingLogger(categoryName, AddMessage);

        public void Dispose()
        {
        }

        private void AddMessage(string message)
        {
            lock (messages)
            {
                messages.Add(message);
            }
        }

        private sealed class RecordingLogger(
            string categoryName,
            Action<string> record) : ILogger
        {
            public IDisposable BeginScope<TState>(TState state)
                where TState : notnull
                => NullScope.Instance;

            public bool IsEnabled(LogLevel logLevel) => true;

            public void Log<TState>(
                LogLevel logLevel,
                EventId eventId,
                TState state,
                Exception? exception,
                Func<TState, Exception?, string> formatter)
                => record($"{categoryName}: {formatter(state, exception)}");
        }

        private sealed class NullScope : IDisposable
        {
            public static NullScope Instance { get; } = new();

            public void Dispose()
            {
            }
        }
    }

    private sealed class CancellingReadStream : Stream
    {
        public override bool CanRead => true;

        public override bool CanSeek => false;

        public override bool CanWrite => false;

        public override long Length => throw new NotSupportedException();

        public override long Position
        {
            get => throw new NotSupportedException();
            set => throw new NotSupportedException();
        }

        public override int Read(byte[] buffer, int offset, int count)
            => throw new OperationCanceledException();

        public override ValueTask<int> ReadAsync(
            Memory<byte> buffer,
            CancellationToken cancellationToken = default)
            => ValueTask.FromException<int>(new OperationCanceledException());

        public override void Flush()
            => throw new NotSupportedException();

        public override long Seek(long offset, SeekOrigin origin)
            => throw new NotSupportedException();

        public override void SetLength(long value)
            => throw new NotSupportedException();

        public override void Write(byte[] buffer, int offset, int count)
            => throw new NotSupportedException();
    }

    private sealed record CapturedRequest(
        Uri RequestUri,
        string? AuthorizationScheme,
        string? AuthorizationParameter,
        string? AccessContext,
        string? Accept)
    {
        public static CapturedRequest From(HttpRequestMessage request)
            => new(
                request.RequestUri!,
                request.Headers.Authorization?.Scheme,
                request.Headers.Authorization?.Parameter,
                request.Headers.TryGetValues(SharedProviderHeaders.AccessContextReference, out var values)
                    ? Assert.Single(values)
                    : null,
                Assert.Single(request.Headers.Accept).MediaType);
    }
}
