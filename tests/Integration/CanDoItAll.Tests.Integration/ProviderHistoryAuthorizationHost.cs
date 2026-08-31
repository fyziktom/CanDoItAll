using System.Net;
using System.Net.Http.Headers;
using System.Security.Claims;
using CanDoItAll.AgentFramework.ProviderHistory;
using CanDoItAll.Infrastructure.ControlPlane;
using CanDoItAll.Modules.Workspace.ApiAccess;
using CanDoItAll.Web;
using CanDoItAll.Web.Api;
using CanDoItAll.Web.Infrastructure;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

namespace CanDoItAll.Tests.Integration;

public sealed class ProviderHistoryAuthorizationHost : IAsyncLifetime, IAsyncDisposable {
    internal const string Route = "/_tests/provider-history";
    internal ApiTestHost Host { get; private set; } = null!;
    internal ReadProbe Reads { get; } = new();
    internal PartitionProbe Partitions { get; } = new();
    internal IApiTokenRegistry Registry => Host.App.Services.GetRequiredService<IApiTokenRegistry>();

    public Task InitializeAsync() => StartAsync(true);
    internal async Task StartAsync(bool jwtEnabled) {
        Host = await ApiTestHost.CreateAsync(jwtEnabled, services => {
            services.Replace(ServiceDescriptor.Singleton<IHistoryReadStore>(Reads));
            services.Replace(ServiceDescriptor.Singleton<IProviderHistoryPartition>(Partitions));
            services.AddScoped<LocalOperatorAuthenticationStateProvider>();
            services.Replace(ServiceDescriptor.Scoped<IInteractiveAccessPrincipalProvider>(
                provider => provider.GetRequiredService<LocalOperatorAuthenticationStateProvider>()));
        }, useInMemoryDatabase: true, configureApplication: MapProbes);
    }

    internal void Reset() {
        Reads.AfterRead = null;
        Reads.Calls = 0;
        Partitions.Value = new(Guid.NewGuid(), Guid.NewGuid(), "authorization-test");
    }

    internal async Task<ApiTokenRecord> SetTokenAsync(params string[] scopes) {
        var issued = Host.App.Services.GetRequiredService<IApiTokenService>().IssueToken(new() {
            Subject = Guid.NewGuid().ToString("N"), DisplayName = "History authorization fixture", Scopes = [.. scopes]
        });
        Host.Client.DefaultRequestHeaders.Authorization = new(issued.TokenType, issued.Token);
        return Assert.Single((await Registry.SearchAsync(new(issued.Subject))).Items);
    }

    internal async Task RewriteTokenAsync(ApiTokenRecord record) {
        await Registry.DeleteAsync(record.Id);
        Registry.Register(record);
    }

    internal async Task AssertPermissionAsync(HistoryPermission permission, HttpStatusCode expected) {
        using var response = await Host.Client.GetAsync($"{Route}/permission/{permission}");
        Assert.Equal(expected, response.StatusCode);
    }

    private static void MapProbes(WebApplication app) {
        app.MapGet(Route + "/search", async (IProviderRequestHistory history, HttpContext http) =>
            await ResultAsync(async () => {
                var now = DateTimeOffset.UtcNow;
                return await history.SearchAsync(new(new HistoryProviderScope.AllAuthorized(), now.AddHours(-1), now), http.RequestAborted);
            }));
        app.MapGet(Route + "/permission/{permission}", async (HistoryPermission permission, HistoryAuthorizedOperation operation, HttpContext http) =>
            await ResultAsync(() => operation.RunAsync(permission, (context, _) => Task.FromResult(context.Caller), http.RequestAborted)));
        app.MapGet(Route + "/owner/{kind}", async (HistorySourceKind kind, IProviderHistoryAccess access, HttpContext http) =>
            await ResultAsync(async () => {
                var context = await access.AuthorizeAsync(HistoryPermission.ReadContent, http.RequestAborted);
                await access.AuthorizeOwnerAsync(context, new(context.Partition, kind, new("owner"), new("item")), http.RequestAborted);
                return context.Caller;
            }));
        app.MapGet(Route + "/local/{trusted}", async (bool trusted, LocalOperatorAuthenticationStateProvider interactive,
            HistoryAuthorizedOperation operation, HttpContext http) => {
            var address = IPAddress.Parse(trusted ? "127.0.0.1" : "192.0.2.10");
            http.Connection.RemoteIpAddress = address;
            http.Items[DevelopmentEndpointAccess.OriginalRemoteIpItemKey] = address;
            interactive.SetAuthenticationState(Task.FromResult(new AuthenticationState(http.User)));
            return await ResultAsync(() => operation.RunAsync(HistoryPermission.Manage, (context, _) => Task.FromResult(context.Caller), http.RequestAborted));
        });
    }

    private static async Task<IResult> ResultAsync<T>(Func<Task<T>> action) {
        try {
            return Results.Ok(await action());
        } catch (ProviderHistoryException exception) {
            return Results.Json(new { exception.Failure }, statusCode: StatusCodes.Status403Forbidden);
        }
    }

    public async Task DisposeAsync() {
        await Host.DisposeAsync();
    }

    ValueTask IAsyncDisposable.DisposeAsync() => new(DisposeAsync());

    internal sealed class PartitionProbe : IProviderHistoryPartition {
        internal HistoryPartition Value { get; set; } = new(Guid.NewGuid(), Guid.NewGuid(), "authorization-test");
        public Task<HistoryPartition> GetAsync(CancellationToken cancellationToken) => Task.FromResult(Value);
    }

    internal sealed class ReadProbe : IHistoryReadStore {
        internal Func<Task>? AfterRead { get; set; }
        internal int Calls { get; set; }
        public async Task<HistoryIndexPage> SearchAsync(HistoryAccessContext context, ProviderRequestHistoryQuery query,
            HistoryPagePosition? position, CancellationToken cancellationToken) {
            Calls++;
            if (AfterRead is not null) {
                await AfterRead();
            }
            return new([], new(HistoryCoverageState.Current, query.ToUtc), query.ToUtc);
        }

        public Task<HistoryMetadata?> GetMetadataAsync(HistoryAccessContext context, HistoryEntryId entryId, CancellationToken cancellationToken) =>
            throw new InvalidOperationException("This fixture only permits the metadata page operation.");
        public Task<HistoryDetail> ReadDetailAsync(HistoryAccessContext context, HistoryEntryId entryId, CancellationToken cancellationToken) =>
            throw new InvalidOperationException("This fixture must not read content.");
        public Task<bool> IsCurrentAsync(HistoryAccessContext context, HistoryMetadata metadata, CanonicalEvidenceReference? owner, CancellationToken cancellationToken) =>
            throw new InvalidOperationException("This fixture must not resolve owners.");
    }
}
