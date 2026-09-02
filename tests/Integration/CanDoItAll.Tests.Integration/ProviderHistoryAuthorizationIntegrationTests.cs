using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using CanDoItAll.AgentFramework.ProviderHistory;
using CanDoItAll.Modules.Workspace.ApiAccess;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace CanDoItAll.Tests.Integration;

public sealed class ProviderHistoryAuthorizationIntegrationTests(ProviderHistoryAuthorizationHost fixture)
    : IClassFixture<ProviderHistoryAuthorizationHost> {
    private const string Route = ProviderHistoryAuthorizationHost.Route;

    [Theory]
    [InlineData(ApiAccessScopeNames.InvokeSharedProviders)]
    [InlineData(ApiAccessScopeNames.Api)]
    [InlineData(ApiAccessScopeNames.ReadSharedProviderCatalog)]
    public async Task Invoke_and_general_api_grants_do_not_authorize_history(string scope) {
        fixture.Reset();
        await fixture.SetTokenAsync(scope);
        using var response = await fixture.Host.Client.GetAsync(Route + "/search");
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Equal(0, fixture.Reads.Calls);
    }

    [Theory]
    [InlineData(ApiAccessScopeNames.ReadProviderHistory, HistoryPermission.ReadMetadata)]
    [InlineData(ApiAccessScopeNames.ReadProviderHistoryContent, HistoryPermission.ReadContent)]
    [InlineData(ApiAccessScopeNames.ManageProviderHistory, HistoryPermission.Manage)]
    public async Task Metadata_content_and_manage_require_separate_explicit_permissions(string grant, HistoryPermission granted) {
        fixture.Reset();
        await fixture.SetTokenAsync(grant);
        foreach (var permission in Enum.GetValues<HistoryPermission>()) {
            var permitted = permission == granted && granted != HistoryPermission.ReadContent;
            await fixture.AssertPermissionAsync(permission, permitted ? HttpStatusCode.OK : HttpStatusCode.Forbidden);
        }
        if (granted == HistoryPermission.ReadContent) {
            await fixture.SetTokenAsync(grant, ApiAccessScopeNames.ReadProviderHistory);
            await fixture.AssertPermissionAsync(granted, HttpStatusCode.OK);
        }
    }

    [Theory]
    [InlineData("revoked")]
    [InlineData("deleted")]
    [InlineData("expired")]
    [InlineData("scope")]
    public async Task Managed_credential_changes_before_read_deny_without_query(string change) {
        fixture.Reset();
        var token = await fixture.SetTokenAsync(ApiAccessScopeNames.ReadProviderHistory);
        await ChangeCredentialAsync(change, token);
        using var response = await fixture.Host.Client.GetAsync(Route + "/search");
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Equal(0, fixture.Reads.Calls);
    }

    [Theory]
    [InlineData("revoked", HistoryFailure.Denied)]
    [InlineData("deleted", HistoryFailure.Denied)]
    [InlineData("expired", HistoryFailure.Denied)]
    [InlineData("scope", HistoryFailure.Denied)]
    [InlineData("partition", HistoryFailure.StaleContext)]
    [InlineData("generation", HistoryFailure.StaleContext)]
    public async Task Revocation_or_partition_change_after_query_denies_publication(string change, HistoryFailure expected) {
        fixture.Reset();
        var token = await fixture.SetTokenAsync(ApiAccessScopeNames.ReadProviderHistory);
        fixture.Reads.AfterRead = async () => {
            if (change == "partition") {
                fixture.Partitions.Value = fixture.Partitions.Value with { StorageLineageId = Guid.NewGuid() };
            } else if (change == "generation") {
                var runtime = Assert.IsType<CanDoItAll.Infrastructure.Persistence.DatabaseRuntimeState>(
                    fixture.Host.App.Services.GetRequiredService<CanDoItAll.Infrastructure.Persistence.IDatabaseRuntimeState>());
                var profile = fixture.Host.App.Services.GetRequiredService<CanDoItAll.Infrastructure.ControlPlane.IActiveDatabaseProfileResolver>()
                    .ResolveCurrentProfile();
                runtime.PublishRestartObserved(runtime.GetSnapshot(), profile);
            } else {
                await ChangeCredentialAsync(change, token);
            }
        };
        using var response = await fixture.Host.Client.GetAsync(Route + "/search");
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Equal(expected, (await response.Content.ReadFromJsonAsync<FailurePayload>())!.Failure);
        Assert.Equal(1, fixture.Reads.Calls);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Simple_chat_content_requires_its_owner_permission(bool ownerGrant) {
        fixture.Reset();
        await fixture.SetTokenAsync(ownerGrant
            ? [ApiAccessScopeNames.ReadProviderHistoryContent, ApiAccessScopeNames.ReadLlmChats]
            : [ApiAccessScopeNames.ReadProviderHistoryContent]);
        using var response = await fixture.Host.Client.GetAsync(Route + "/owner/SimpleChat");
        Assert.Equal(ownerGrant ? HttpStatusCode.OK : HttpStatusCode.Forbidden, response.StatusCode);
        using var agent = await fixture.Host.Client.GetAsync(Route + "/owner/AgentConversation");
        Assert.Equal(HttpStatusCode.Forbidden, agent.StatusCode);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Validated_legacy_identity_remains_explicit_and_does_not_invent_a_key(bool corruptSignature) {
        fixture.Reset();
        var options = fixture.Host.App.Services.GetRequiredService<IOptions<ApiAccessOptions>>().Value.Authorization;
        var now = DateTimeOffset.UtcNow;
        var header = Base64UrlEncoder.Encode(JsonSerializer.Serialize(new { alg = "HS256", typ = "JWT" }));
        var payload = Base64UrlEncoder.Encode(JsonSerializer.Serialize(new {
            iss = options.Issuer, aud = options.Audience, sub = "legacy-history-client",
            nbf = now.ToUnixTimeSeconds(), exp = now.AddMinutes(5).ToUnixTimeSeconds(),
            scope = ApiAccessScopeNames.ReadProviderHistory
        }));
        var unsigned = $"{header}.{payload}";
        var signature = corruptSignature ? "invalid" : Base64UrlEncoder.Encode(
            HMACSHA256.HashData(Encoding.UTF8.GetBytes(options.SigningKey), Encoding.UTF8.GetBytes(unsigned)));
        fixture.Host.Client.DefaultRequestHeaders.Authorization = new("Bearer", $"{unsigned}.{signature}");
        using var response = await fixture.Host.Client.GetAsync(Route + "/permission/ReadMetadata");
        Assert.Equal(corruptSignature ? HttpStatusCode.Forbidden : HttpStatusCode.OK, response.StatusCode);
        if (!corruptSignature) {
            var caller = await response.Content.ReadFromJsonAsync<HistoryCaller>();
            Assert.Equal(HistoryAuthenticationKind.LegacyAuthenticated, caller!.Kind);
            Assert.Null(caller.CredentialId);
        }
    }

    [Fact]
    public async Task Missing_authority_and_auth_disabled_http_do_not_become_local_operator() {
        fixture.Reset();
        fixture.Host.Client.DefaultRequestHeaders.Authorization = null;
        await fixture.AssertPermissionAsync(HistoryPermission.ReadMetadata, HttpStatusCode.Forbidden);
        await using var disabled = new ProviderHistoryAuthorizationHost();
        await disabled.StartAsync(false);
        await disabled.AssertPermissionAsync(HistoryPermission.ReadMetadata, HttpStatusCode.Forbidden);
        using var untrusted = await disabled.Host.Client.GetAsync(Route + "/local/false");
        Assert.Equal(HttpStatusCode.Forbidden, untrusted.StatusCode);
        using var local = await disabled.Host.Client.GetAsync(Route + "/local/true");
        Assert.Equal(HttpStatusCode.OK, local.StatusCode);
        Assert.Equal(HistoryAuthenticationKind.TrustedLocalOperator,
            (await local.Content.ReadFromJsonAsync<HistoryCaller>())!.Kind);
        await disabled.AssertPermissionAsync(HistoryPermission.ReadMetadata, HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Authenticated_bearer_in_trusted_circuit_is_not_elevated() {
        fixture.Reset();
        await fixture.SetTokenAsync(ApiAccessScopeNames.InvokeSharedProviders);
        using var response = await fixture.Host.Client.GetAsync(Route + "/local/true");
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    private async Task ChangeCredentialAsync(string change, CanDoItAll.Infrastructure.ControlPlane.ApiTokenRecord token) {
        switch (change) {
            case "revoked":
                await fixture.Registry.RevokeAsync(token.Id, DateTimeOffset.UtcNow);
                break;
            case "deleted":
                await fixture.Registry.DeleteAsync(token.Id);
                break;
            case "expired":
                await fixture.RewriteTokenAsync(token with { ExpiresAtUtc = DateTimeOffset.UtcNow.AddTicks(-1) });
                break;
            case "scope":
                await fixture.RewriteTokenAsync(token with { Scopes = [ApiAccessScopeNames.InvokeSharedProviders] });
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(change));
        }
    }

    private sealed record FailurePayload(HistoryFailure Failure);
}
