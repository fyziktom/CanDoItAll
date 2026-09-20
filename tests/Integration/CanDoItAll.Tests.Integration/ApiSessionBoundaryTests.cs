using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;
using CanDoItAll.Infrastructure.ControlPlane;
using CanDoItAll.Modules.Workspace.ApiAccess;
using CanDoItAll.SharedKernel;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using static CanDoItAll.Tests.Integration.Api.ApiUserSessionIntegrationTests;

namespace CanDoItAll.Tests.Integration.Api;

public sealed class ApiSessionBoundaryTests {
    [Fact]
    public async Task Real_handler_rejects_invalid_crypto_duplicate_claims_and_managed_metadata_drift() {
        var password = Password();
        await using var host = await CreateHostAsync(password);
        var admin = await LoginAsync(host.Client, "admin", password);
        var key = host.App.Services.GetRequiredService<IOptions<ApiAccessOptions>>().Value.Authorization.SigningKey;
        var payload = JsonNode.Parse(Decode(admin.Token.Split('.')[1]))!.AsObject();
        var changes = new Dictionary<string, Action<JsonObject>> {
            ["wrong issuer"] = body => body["iss"] = "different-issuer",
            ["wrong audience"] = body => body["aud"] = "different-audience",
            ["expired"] = body => body["exp"] = DateTimeOffset.UtcNow.AddMinutes(-5).ToUnixTimeSeconds(),
            ["future"] = body => body["nbf"] = DateTimeOffset.UtcNow.AddMinutes(5).ToUnixTimeSeconds(),
            ["wrong subject"] = body => body["sub"] = "other-admin",
            ["wrong name"] = body => body["name"] = "other-name",
            ["wrong scopes"] = body => body["scope"] = ApiAccessScopeNames.Api,
            ["wrong kind"] = body => body[ApiManagedTokenClaims.Kind] = ApiCredentialKind.Machine.ToString(),
            ["unknown version"] = body => body[ApiManagedTokenClaims.Version] = "99",
            ["null version"] = body => body[ApiManagedTokenClaims.Version] = null,
            ["unregistered"] = body => body[ApiManagedTokenClaims.TokenId] = Guid.NewGuid().ToString("N"),
            ["wrong issued time"] = body => body["iat"] = 0,
            ["missing registration with kind"] = body => body.Remove(ApiManagedTokenClaims.Version),
            ["admin with user metadata"] = body => body[ApiManagedTokenClaims.UserId] = Guid.NewGuid().ToString("N")
        };
        foreach (var (name, change) in changes) {
            var changed = payload.DeepClone().AsObject();
            change(changed);
            SetToken(host.Client, Sign(changed.ToJsonString(), key));
            using var response = await host.Client.GetAsync("/api/access/users");
            Assert.True(response.StatusCode == HttpStatusCode.Unauthorized, $"{name}: {response.StatusCode}");
            Assert.Equal("application/json", response.Content.Headers.ContentType?.MediaType);
        }
        var bodyJson = payload.ToJsonString();
        foreach (var invalid in new[] {
            Sign(bodyJson, Password()),
            Sign(bodyJson[..^1] + ",\"sub\":\"duplicate\"}", key),
            Sign(bodyJson, key, "{\"alg\":\"HS512\",\"typ\":\"JWT\"}"),
            Encode("{\"alg\":\"none\"}") + "." + Encode(bodyJson) + "."
        }) {
            await AssertRejectedAsync(host.Client, invalid);
        }
        Assert.Empty(await host.App.Services.GetRequiredService<IApiUserStore>().ReadAsync());
    }

    [Fact]
    public async Task Legacy_admin_looking_claims_do_not_establish_typed_administration_or_self_sessions() {
        var password = Password();
        await using var host = await CreateHostAsync(password);
        var token = host.App.Services.GetRequiredService<IApiTokenService>().IssueToken(new() { Scopes = [ApiAccessScopeNames.Api] });
        var body = JsonNode.Parse(Decode(token.Token.Split('.')[1]))!.AsObject();
        body.Remove(ApiManagedTokenClaims.Version);
        body["sub"] = ApiBootstrapAdminOptions.Subject;
        body["role"] = "Administrator";
        body["scope"] = string.Join(' ', ApiAccessScopeNames.Api, ApiAccessScopeNames.ManageAccess, ApiAccessScopeNames.IssueTokens, ApiAccessScopeNames.Session);
        body.Remove("scopes");
        SetToken(host.Client, Sign(body.ToJsonString(), host.App.Services.GetRequiredService<IOptions<ApiAccessOptions>>().Value.Authorization.SigningKey));
        using var data = await host.Client.GetAsync("/api/workflows/contract");
        Assert.Equal(HttpStatusCode.OK, data.StatusCode);
        foreach (var path in new[] { "/api/access/users", "/api/access/tokens", "/api/access/me" }) {
            using var response = await host.Client.GetAsync(path);
            Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        }
        await using var scope = host.App.Services.CreateAsyncScope();
        Assert.False(await scope.ServiceProvider.GetRequiredService<ApiUserAdministrationService>().CanManageAsync());
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => scope.ServiceProvider.GetRequiredService<ApiUserAdministrationService>().SearchAsync());
    }

    [Fact]
    public async Task Rotating_unknown_names_cannot_bypass_bounded_client_throttle() {
        var password = Password();
        var clock = new SessionClock { Now = DateTimeOffset.UtcNow };
        await using var host = await CreateHostAsync(password, configureServices: services => services.AddSingleton<IClock>(clock));
        for (var index = 0; index < 31; index++) {
            using var response = await host.Client.PostAsJsonAsync("/api/access/login", new ApiLoginRequest($"unknown-{index}", password));
            Assert.Equal(index < 30 ? HttpStatusCode.Unauthorized : HttpStatusCode.TooManyRequests, response.StatusCode);
            if (index == 30) {
                Assert.NotNull(response.Headers.RetryAfter);
            }
        }
        Assert.Empty(await host.App.Services.GetRequiredService<IApiUserStore>().ReadAsync());
        clock.Now = clock.Now.AddMinutes(1);
        using var retry = await host.Client.PostAsJsonAsync("/api/access/login", new ApiLoginRequest("unknown-retry", password));
        Assert.Equal(HttpStatusCode.Unauthorized, retry.StatusCode);
    }

    [Fact]
    public async Task Concurrent_duplicate_creation_and_stale_reset_do_not_restore_superseded_state() {
        var password = Password();
        await using var host = await CreateHostAsync(password);
        var admin = await LoginAsync(host.Client, "admin", password);
        SetToken(host.Client, admin.Token);
        var results = await Task.WhenAll(new[] { "race", "RACE" }.Select(name => host.Client.PostAsJsonAsync(
            "/api/access/users", new ApiUserCreateRequest(name, name, password, true, [ApiAccessScopeNames.ReadWorkflows]))));
        using var created = Assert.Single(results, response => response.StatusCode == HttpStatusCode.OK);
        using var duplicate = Assert.Single(results, response => response.StatusCode == HttpStatusCode.Conflict);
        var original = (await created.Content.ReadFromJsonAsync<ApiUserDetails>())!;
        var current = await UpdateAsync(host.Client, original, false, []);
        using var staleReset = await host.Client.PostAsJsonAsync($"/api/access/users/{original.Id}/reset-password", new ApiUserPasswordResetRequest(Password(), original.Version));
        Assert.Equal(HttpStatusCode.Conflict, staleReset.StatusCode);
        var stored = Assert.Single(await host.App.Services.GetRequiredService<IApiUserStore>().ReadAsync());
        Assert.Equal(current.Version, stored.Version);
        Assert.False(stored.Enabled);
        Assert.Empty(stored.AllowedScopes);
    }

    [Fact]
    public async Task Concurrent_profile_reset_and_login_never_restore_superseded_authority() {
        var password = Password();
        var replacement = Password();
        await using var host = await CreateHostAsync(password);
        var admin = await LoginAsync(host.Client, "admin", password);
        SetToken(host.Client, admin.Token);
        var user = await CreateUserAsync(host.Client, "concurrent-session", password, [ApiAccessScopeNames.ReadWorkflows]);
        var originalSession = await LoginAsync(host.Client, user.UserName, password);
        var responses = await Task.WhenAll(
            host.Client.PutAsJsonAsync($"/api/access/users/{user.Id}", new ApiUserUpdateRequest(user.UserName, "Updated concurrently", true, [], user.Version)),
            host.Client.PostAsJsonAsync($"/api/access/users/{user.Id}/reset-password", new ApiUserPasswordResetRequest(replacement, user.Version)),
            host.Client.PostAsJsonAsync("/api/access/login", new ApiLoginRequest(user.UserName, password)));
        using var profile = responses[0];
        using var reset = responses[1];
        using var racingLogin = responses[2];
        Assert.Single(new[] { profile, reset }, response => response.StatusCode == HttpStatusCode.OK);
        Assert.Single(new[] { profile, reset }, response => response.StatusCode == HttpStatusCode.Conflict);
        var current = Assert.Single(await host.App.Services.GetRequiredService<IApiUserStore>().ReadAsync());
        Assert.Equal(user.Version + 1, current.Version);
        Assert.Equal(2, current.AuthenticationRevision);
        await AssertRejectedAsync(host.Client, originalSession.Token);
        var expectedScopes = current.AllowedScopes.Append(ApiAccessScopeNames.Session).Order(StringComparer.Ordinal).ToArray();
        var fresh = await LoginAsync(host.Client, user.UserName, reset.IsSuccessStatusCode ? replacement : password);
        Assert.Equal(expectedScopes, fresh.Scopes.Order(StringComparer.Ordinal));
        Assert.True(racingLogin.StatusCode is HttpStatusCode.OK or HttpStatusCode.Unauthorized);
        if (racingLogin.IsSuccessStatusCode) {
            var raced = (await racingLogin.Content.ReadFromJsonAsync<ApiLoginResult>())!;
            SetToken(host.Client, raced.Token);
            using var currentSession = await host.Client.GetAsync("/api/access/me");
            Assert.True(currentSession.StatusCode is HttpStatusCode.OK or HttpStatusCode.Unauthorized);
            if (currentSession.IsSuccessStatusCode) {
                var identity = (await currentSession.Content.ReadFromJsonAsync<ApiSessionIdentity>())!;
                Assert.Equal(expectedScopes, identity.Scopes.Order(StringComparer.Ordinal));
            }
        }
    }

    [Fact]
    public async Task Expired_user_can_login_again_without_administrator_issuance() {
        var password = Password();
        var clock = new SessionClock { Now = DateTimeOffset.UtcNow.AddMinutes(-61) };
        await using var host = await ApiTestHost.CreateAsync(true, services => services.AddSingleton<IClock>(clock),
            useInMemoryDatabase: true, apiConfiguration: new Dictionary<string, string?> {
                ["Api:UserAuthentication:Enabled"] = "true", ["Api:UserAuthentication:AllowLoopbackHttp"] = "true",
                ["Api:BootstrapAdmin:PasswordHash"] = new ApiPasswordService().Hash(password)
            });
        var now = clock.Now;
        var user = new ApiUserRecord(Guid.NewGuid(), "expiry", "EXPIRY", "Expiry", true, new ApiPasswordService().Hash(password), [], 1, 1, now, now);
        await host.App.Services.GetRequiredService<IApiUserStore>().SaveAsync(user, null);
        var expired = await LoginAsync(host.Client, user.UserName, password);
        clock.Now = DateTimeOffset.UtcNow;
        await AssertRejectedAsync(host.Client, expired.Token);
        var fresh = await LoginAsync(host.Client, user.UserName, password);
        Assert.False(string.Equals(expired.Token, fresh.Token, StringComparison.Ordinal), "A fresh login reused the expired token.");
        SetToken(host.Client, fresh.Token);
        using var me = await host.Client.GetAsync("/api/access/me");
        Assert.Equal(HttpStatusCode.OK, me.StatusCode);
        await AssertRejectedAsync(host.Client, expired.Token);
    }

    [Fact]
    public async Task Invalid_login_inputs_never_reflect_credentials_or_change_accounts_and_registrations() {
        var password = Password();
        await using var host = await CreateHostAsync(password);
        var admin = await LoginAsync(host.Client, "admin", password);
        SetToken(host.Client, admin.Token);
        await CreateUserAsync(host.Client, "enabled-account", password, []);
        var disabled = await CreateUserAsync(host.Client, "disabled-account", password, []);
        await UpdateAsync(host.Client, disabled, false, []);
        var store = host.App.Services.GetRequiredService<IApiUserStore>();
        var before = await store.ReadAsync();
        var registry = host.App.Services.GetRequiredService<IApiTokenRegistry>();
        var beforeSessions = await registry.SearchAsync(new(Kind: ApiCredentialKind.UserSession));
        host.Client.DefaultRequestHeaders.Authorization = null;
        string? invalidCredentials = null;
        foreach (var request in new[] {
            new ApiLoginRequest("enabled-account", Password()),
            new ApiLoginRequest("disabled-account", password),
            new ApiLoginRequest("unknown-account", password)
        }) {
            using var response = await host.Client.PostAsJsonAsync("/api/access/login", request);
            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
            Assert.True(response.Headers.CacheControl?.NoStore);
            var body = await response.Content.ReadAsStringAsync();
            invalidCredentials ??= body;
            Assert.Equal(invalidCredentials, body);
            Assert.False(body.Contains(request.Password, StringComparison.Ordinal), "The login error disclosed a password.");
            Assert.DoesNotContain(request.UserName, body, StringComparison.Ordinal);
        }
        foreach (var body in new JsonObject[] {
            new() { ["userName"] = null, ["password"] = password },
            new() { ["userName"] = "enabled-account", ["password"] = null },
            new() { ["userName"] = new string('a', 65), ["password"] = password },
            new() { ["userName"] = "enabled-account", ["password"] = new string('x', 257) }
        }) {
            await AssertInvalidBodyAsync(body);
        }
        foreach (var property in new[] { "role", "sub", "scopes", "passwordHash", "kind", "authenticationRevision" }) {
            await AssertInvalidBodyAsync(new() { ["userName"] = "enabled-account", ["password"] = password, [property] = "caller-selected" });
        }
        Assert.True(string.Equals(System.Text.Json.JsonSerializer.Serialize(before),
            System.Text.Json.JsonSerializer.Serialize(await store.ReadAsync()), StringComparison.Ordinal), "Rejected login requests changed an account.");
        Assert.Equal(beforeSessions.TotalCount, (await registry.SearchAsync(new(Kind: ApiCredentialKind.UserSession))).TotalCount);

        async Task AssertInvalidBodyAsync(JsonObject body) {
            using var response = await host.Client.PostAsJsonAsync("/api/access/login", body);
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            Assert.Equal("application/json", response.Content.Headers.ContentType?.MediaType);
            Assert.True(response.Headers.CacheControl?.NoStore);
            Assert.False((await response.Content.ReadAsStringAsync()).Contains(password, StringComparison.Ordinal), "The invalid-request response disclosed a password.");
        }
    }

    [Fact]
    public async Task Registered_session_stops_at_its_exact_expiry_without_inheriting_JWT_clock_skew() {
        var password = Password();
        var clock = new SessionClock { Now = DateTimeOffset.UtcNow };
        await using var host = await ApiTestHost.CreateAsync(true, services => services.AddSingleton<IClock>(clock),
            useInMemoryDatabase: true, apiConfiguration: new Dictionary<string, string?> {
                ["Api:UserAuthentication:Enabled"] = "true", ["Api:UserAuthentication:AllowLoopbackHttp"] = "true",
                ["Api:BootstrapAdmin:PasswordHash"] = new ApiPasswordService().Hash(password)
            });
        var session = await LoginAsync(host.Client, "admin", password);
        SetToken(host.Client, session.Token);
        clock.Now = session.ExpiresAtUtc.AddTicks(-1);
        using var beforeExpiry = await host.Client.GetAsync("/api/access/me");
        Assert.Equal(HttpStatusCode.OK, beforeExpiry.StatusCode);
        clock.Now = session.ExpiresAtUtc;
        await AssertRejectedAsync(host.Client, session.Token);
        clock.Now = session.ExpiresAtUtc.AddSeconds(29);
        await AssertRejectedAsync(host.Client, session.Token);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Main_API_off_removes_new_business_and_identity_routes_with_or_without_JWT(bool authorizationEnabled) {
        await using var host = await ApiTestHost.CreateAsync(authorizationEnabled, useInMemoryDatabase: true,
            apiConfiguration: new Dictionary<string, string?> { ["Api:Enabled"] = "false" });
        var reads = new[] { "/api/processes/definitions", "/api/processes/definitions/fixture", "/api/processes/definitions/fixture/roles",
            "/api/processes/definitions/fixture/steps", "/api/workflows/templates", "/api/settings/workspace", "/api/access/me",
            "/api/access/users", "/api/access/tokens", "/api/access/scopes" };
        foreach (var path in reads) {
            using var response = await host.Client.GetAsync(path);
            Assert.True(response.StatusCode == HttpStatusCode.NotFound, $"Disabled API unexpectedly exposed GET {path}.");
        }
        var settings = new { workspaceName = "Must remain absent", defaultProviderProfileId = (Guid?)null,
            defaultPromptOutputFormat = "Markdown", currencyCode = "USD", currencyCultureName = "en-US", notes = "No write" };
        using var save = await host.Client.PutAsJsonAsync("/api/settings/workspace", settings);
        using var draft = await host.Client.PostAsync("/api/workflows/templates/fixture/drafts", null);
        using var login = await host.Client.PostAsJsonAsync("/api/access/login", new ApiLoginRequest("admin", Password()));
        using var issue = await host.Client.PostAsJsonAsync("/api/access/tokens", new ApiTokenIssueRequest());
        using var logout = await host.Client.PostAsync("/api/access/logout", null);
        foreach (var response in new[] { save, draft, login, issue, logout }) {
            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
            Assert.Equal("application/json", response.Content.Headers.ContentType?.MediaType);
        }
        Assert.Empty(await host.App.Services.GetRequiredService<IApiUserStore>().ReadAsync());
        Assert.Equal(0, (await host.App.Services.GetRequiredService<IApiTokenRegistry>().SearchAsync(new(Kind: null))).TotalCount);
    }

    private static string Sign(string payload, string key, string header = "{\"alg\":\"HS256\",\"typ\":\"JWT\"}") {
        var unsigned = Encode(header) + "." + Encode(payload);
        return unsigned + "." + Convert.ToBase64String(HMACSHA256.HashData(Encoding.UTF8.GetBytes(key), Encoding.UTF8.GetBytes(unsigned)))
            .TrimEnd('=').Replace('+', '-').Replace('/', '_');
    }

    private static string Encode(string value) => Convert.ToBase64String(Encoding.UTF8.GetBytes(value)).TrimEnd('=').Replace('+', '-').Replace('/', '_');
    private static string Decode(string value) => Encoding.UTF8.GetString(Convert.FromBase64String(value.Replace('-', '+').Replace('_', '/').PadRight((value.Length + 3) / 4 * 4, '=')));
    private sealed class SessionClock : IClock {
        public DateTimeOffset Now { get; set; }
        public DateTimeOffset GetUtcNow() => Now;
    }
}
