using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using CanDoItAll.Infrastructure;
using CanDoItAll.Infrastructure.ControlPlane;
using CanDoItAll.Infrastructure.FileSystem;
using CanDoItAll.Modules.Workspace.ApiAccess;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using static CanDoItAll.Tests.Integration.Api.ApiUserSessionIntegrationTests;

namespace CanDoItAll.Tests.Integration.Api;

[Trait("Category", "HostPlatform")]
public sealed class ApiAdministrationOutcomeHttpTests {
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Ambiguous_token_registration_returns_redacted_503_and_preserves_machine_wire_defaults(bool afterCommit) {
        var password = Password();
        var armed = false;
        var writer = new DurableFileWriter(new PhysicalFileSystemPathPolicyFactory(), stage => {
            if (armed && stage == (afterCommit ? DurableFileWriteStage.Committed : DurableFileWriteStage.BeforeCommit)) {
                throw new IOException("Injected token acknowledgement failure.");
            }
        });
        await using var host = await CreateHostAsync(password, configureServices: services =>
            services.AddSingleton<IApiTokenRegistry>(provider => new FileApiTokenRegistry(provider.GetRequiredService<IControlPlanePathResolver>(), writer)));
        var administrator = await LoginAsync(host.Client, "admin", password);
        SetToken(host.Client, administrator.Token);
        using var acknowledged = await host.Client.PostAsJsonAsync("/api/access/tokens", new ApiTokenIssueRequest { Subject = "acknowledged", Scopes = [ApiAccessScopeNames.ReadWorkflows] });
        Assert.Equal(HttpStatusCode.OK, acknowledged.StatusCode);
        Assert.True(acknowledged.Headers.CacheControl?.NoStore);
        using (var json = JsonDocument.Parse(await acknowledged.Content.ReadAsStringAsync())) {
            Assert.Equal(new[] { "displayName", "expiresAtUtc", "scopes", "subject", "token", "tokenType" }, json.RootElement.EnumerateObject().Select(property => property.Name).Order(StringComparer.Ordinal));
        }
        armed = true;
        using var response = await host.Client.PostAsJsonAsync("/api/access/tokens", new ApiTokenIssueRequest { Subject = "ambiguous", Scopes = [ApiAccessScopeNames.ReadWorkflows] });
        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        Assert.True(response.Headers.CacheControl?.NoStore);
        var body = await response.Content.ReadAsStringAsync();
        Assert.True(!body.Contains("eyJ", StringComparison.Ordinal) && !body.Contains(password, StringComparison.Ordinal), "The failed response disclosed credential material.");
        Assert.DoesNotContain("CandidateId", body, StringComparison.OrdinalIgnoreCase);
        using var listed = await host.Client.GetAsync("/api/access/tokens?search=ambiguous");
        Assert.Equal(HttpStatusCode.OK, listed.StatusCode);
        var page = (await listed.Content.ReadFromJsonAsync<ApiTokenPage>())!;
        Assert.Equal(afterCommit ? 1 : 0, page.TotalCount);
        Assert.All(page.Items, token => Assert.Equal(ApiCredentialKind.Machine, token.Kind));
    }

    [Fact]
    public async Task Durable_account_logging_faults_keep_success_wire_shapes_no_store_and_session_invalidation() {
        var password = Password();
        var logger = new FaultLogger();
        await using var host = await CreateHostAsync(password, configureServices: services => {
            services.AddSingleton<ILogger<ApiUserAdministrationService>>(logger);
            services.AddSingleton<ReadBackFailureStore>(provider => new(ActivatorUtilities.CreateInstance<FileApiUserStore>(provider)));
            services.AddSingleton<IApiUserStore>(provider => provider.GetRequiredService<ReadBackFailureStore>());
        });
        var administrator = await LoginAsync(host.Client, "admin", password);
        SetToken(host.Client, administrator.Token);
        using var create = await host.Client.PostAsJsonAsync("/api/access/users", new ApiUserCreateRequest("http-outcome", "Original", password, true, []));
        var user = await ReadKnownAsync(create, password);
        Assert.Equal(1, user.Version);
        Assert.Single(await host.App.Services.GetRequiredService<IApiUserStore>().ReadAsync());
        var store = host.App.Services.GetRequiredService<ReadBackFailureStore>();
        store.FailNextRead = true;
        using var failedList = await host.Client.GetAsync("/api/access/users");
        Assert.Equal(HttpStatusCode.ServiceUnavailable, failedList.StatusCode);
        Assert.True(failedList.Headers.CacheControl?.NoStore);
        using var retryList = await host.Client.GetAsync("/api/access/users");
        Assert.Equal(HttpStatusCode.OK, retryList.StatusCode);
        var oldSession = await LoginAsync(host.Client, user.UserName, password);
        using var update = await host.Client.PutAsJsonAsync($"/api/access/users/{user.Id}", new ApiUserUpdateRequest(user.UserName, "Changed", true, [], user.Version));
        user = await ReadKnownAsync(update, password);
        Assert.Equal(2, user.Version);
        await AssertRejectedAsync(host.Client, oldSession.Token);
        SetToken(host.Client, administrator.Token);
        var current = await LoginAsync(host.Client, user.UserName, password);
        var replacement = Password();
        using var reset = await host.Client.PostAsJsonAsync($"/api/access/users/{user.Id}/reset-password", new ApiUserPasswordResetRequest(replacement, user.Version));
        user = await ReadKnownAsync(reset, replacement);
        Assert.Equal(3, user.Version);
        await AssertRejectedAsync(host.Client, current.Token);
        SetToken(host.Client, administrator.Token);
        var afterReset = await LoginAsync(host.Client, user.UserName, replacement);
        using var delete = await host.Client.DeleteAsync($"/api/access/users/{user.Id}?expectedVersion={user.Version}");
        Assert.Equal(HttpStatusCode.NoContent, delete.StatusCode);
        Assert.True(delete.Headers.CacheControl?.NoStore);
        Assert.Empty(await delete.Content.ReadAsByteArrayAsync());
        await AssertRejectedAsync(host.Client, afterReset.Token);
        Assert.Empty(await store.ReadAsync());
        Assert.Equal(4, logger.Failures);
        Assert.Equal(4, store.Writes);
    }

    private static async Task<ApiUserDetails> ReadKnownAsync(HttpResponseMessage response, string password) {
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.True(response.Headers.CacheControl?.NoStore);
        var body = await response.Content.ReadAsStringAsync();
        Assert.True(!body.Contains(password, StringComparison.Ordinal), "The safe response leaked credential material.");
        using var json = JsonDocument.Parse(body);
        Assert.Equal(new[] { "createdAtUtc", "displayName", "enabled", "id", "scopes", "updatedAtUtc", "userName", "version" },
            json.RootElement.EnumerateObject().Select(property => property.Name).Order(StringComparer.Ordinal));
        return JsonSerializer.Deserialize<ApiUserDetails>(body, new JsonSerializerOptions(JsonSerializerDefaults.Web))!;
    }
    private sealed class FaultLogger : ILogger<ApiUserAdministrationService> {
        public int Failures { get; private set; }
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => true;
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) {
            Failures++;
            throw new IOException("Injected account diagnostic failure.");
        }
    }
    private sealed class ReadBackFailureStore(FileApiUserStore inner) : IApiUserStore {
        public bool FailNextRead { get; set; }
        public int Writes { get; private set; }
        public Task<IReadOnlyList<ApiUserRecord>> ReadAsync(CancellationToken cancellationToken = default) {
            if (FailNextRead) {
                FailNextRead = false;
                throw new IOException("Injected read-back failure.");
            }
            return inner.ReadAsync(cancellationToken);
        }
        public async Task SaveAsync(ApiUserRecord user, long? expectedVersion, CancellationToken cancellationToken = default) {
            await inner.SaveAsync(user, expectedVersion, cancellationToken);
            Writes++;
        }
        public async Task DeleteAsync(Guid id, long expectedVersion, CancellationToken cancellationToken = default) {
            await inner.DeleteAsync(id, expectedVersion, cancellationToken);
            Writes++;
        }
    }
}
