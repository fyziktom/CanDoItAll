using System.Security.Cryptography;
using System.Text.Json;
using CanDoItAll.Infrastructure.ControlPlane;
using CanDoItAll.Modules.Workspace.ApiAccess;
using CanDoItAll.SharedKernel;
using Microsoft.Extensions.Options;

namespace CanDoItAll.Tests.Unit;

public sealed class ApiSessionAuthoritySnapshotTests {
    private static readonly string Password = Convert.ToBase64String(RandomNumberGenerator.GetBytes(24));
    private static readonly string Hash = new ApiPasswordService().Hash(Password);

    [Fact]
    public async Task Prepared_snapshot_selects_one_validated_identity_with_one_full_read_and_preserves_fresh_identity_entrypoint() {
        using var fixture = new Fixture();
        var fresh = await fixture.Sessions.ResolveIdentityAsync(fixture.Token);
        var selected = await fixture.Sessions.ResolveAuthoritySnapshotAsync(fixture.Token);

        Assert.NotNull(selected);
        Assert.Equal(JsonSerializer.Serialize(fresh), JsonSerializer.Serialize(selected.Identity));
        Assert.Equal(fixture.Users.Generation, selected.Generation);
        Assert.Equal(fixture.User.NormalizedUserName, selected.NormalizedUserName);
        Assert.Equal(1, fixture.Users.Reads);
        Assert.Equal(1, fixture.Users.SnapshotReads);
        Assert.Equal(0, fixture.Users.Observations);
        Assert.True(fixture.Sessions.IsAuthoritySnapshotCurrent(selected, fixture.Token));
    }

    [Fact]
    public async Task Shared_validation_rejects_disabled_revision_scope_reserved_name_hash_and_credential_changes() {
        using var fixture = new Fixture();
        var original = fixture.User;
        foreach (var user in new[] {
            original with { Enabled = false },
            original with { AuthenticationRevision = original.AuthenticationRevision + 1 },
            original with { AllowedScopes = [ApiAccessScopeNames.ReadProjects] },
            original with { UserName = "admin", NormalizedUserName = "ADMIN" },
            original with { PasswordHash = "invalid" }
        }) {
            fixture.Users.Users = [user];
            Assert.Null(await fixture.Sessions.ResolveIdentityAsync(fixture.Token));
            Assert.Null(await fixture.Sessions.ResolveAuthoritySnapshotAsync(fixture.Token));
        }
        fixture.Users.Users = [original];
        foreach (var token in new[] {
            fixture.Token with { Subject = "different" },
            fixture.Token with { ExpiresAtUtc = fixture.Clock.Now },
            fixture.Token with { RevokedAtUtc = fixture.Clock.Now }
        }) {
            Assert.Null(await fixture.Sessions.ResolveIdentityAsync(token));
            Assert.Null(await fixture.Sessions.ResolveAuthoritySnapshotAsync(token));
        }
        fixture.Options.UserAuthentication.Enabled = false;
        Assert.Null(await fixture.Sessions.ResolveIdentityAsync(fixture.Token));
        Assert.Null(await fixture.Sessions.ResolveAuthoritySnapshotAsync(fixture.Token));
    }

    [Fact]
    public async Task Administrator_snapshot_uses_current_configuration_without_account_reads() {
        using var fixture = new Fixture();
        Assert.NotNull(await fixture.Sessions.LoginAsync(new("admin", Password)));
        var token = Assert.IsType<ApiTokenRecord>(fixture.Registry.Registered);
        var selected = await fixture.Sessions.ResolveAuthoritySnapshotAsync(token);

        Assert.NotNull(selected);
        Assert.True(selected.Identity.IsAdministrator);
        Assert.Null(selected.Generation);
        Assert.Equal(0, fixture.Users.Reads);
        Assert.Equal(0, fixture.Users.SnapshotReads);
        Assert.True(fixture.Sessions.IsAuthoritySnapshotCurrent(selected, token));
        fixture.Options.Authorization.SigningKey += "changed";
        Assert.False(fixture.Sessions.IsAuthoritySnapshotCurrent(selected, token));
        Assert.Null(await fixture.Sessions.ResolveAuthoritySnapshotAsync(token));
    }

    [Fact]
    public async Task Selected_snapshot_detaches_scope_aliases_and_does_not_retain_other_accounts_or_password_material() {
        using var fixture = new Fixture();
        var other = fixture.User with { Id = Guid.NewGuid(), UserName = "other", NormalizedUserName = "OTHER", DisplayName = "Unselected account" };
        fixture.Users.Users = [fixture.User, other];
        var scopes = fixture.Token.Scopes.ToArray();
        var token = fixture.Token with { Scopes = scopes };
        var selected = Assert.IsType<ApiSessionAuthoritySnapshot>(await fixture.Sessions.ResolveAuthoritySnapshotAsync(token));
        scopes[0] = "altered";
        fixture.Users.Users = [];

        Assert.DoesNotContain("altered", selected.Identity.Scopes);
        Assert.Equal(fixture.User.Id, selected.Identity.UserId);
        var text = JsonSerializer.Serialize(selected);
        Assert.DoesNotContain(Hash, text, StringComparison.Ordinal);
        Assert.DoesNotContain(other.DisplayName, text, StringComparison.Ordinal);
        Assert.DoesNotContain("PasswordHash", text, StringComparison.Ordinal);
    }

    private sealed class Fixture : IDisposable {
        public AuthorityClock Clock { get; } = new();
        public UserStore Users { get; } = new();
        public TokenRegistry Registry { get; } = new();
        public ApiAccessOptions Options { get; } = new() {
            Authorization = new() { Enabled = true, SigningKey = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)) },
            UserAuthentication = new() { Enabled = true },
            BootstrapAdmin = new() { UserName = "admin", PasswordHash = Hash }
        };
        public ApiUserRecord User { get; }
        public ApiTokenRecord Token { get; }
        public ApiSessionService Sessions { get; }

        public Fixture() {
            User = new(Guid.NewGuid(), "author", "AUTHOR", "Author", true, Hash,
                [ApiAccessScopeNames.ReadProjects, ApiAccessScopeNames.WriteProjects], 1, 1, Clock.Now, Clock.Now);
            Users.Users = [User];
            Token = new(Guid.NewGuid(), $"api-user:{User.Id:N}", User.DisplayName, Clock.Now, Clock.Now.AddHours(1),
                User.AllowedScopes.Append(ApiAccessScopeNames.Session).Order(StringComparer.Ordinal).ToArray(),
                Kind: ApiCredentialKind.UserSession, UserId: User.Id, AuthenticationRevision: 1);
            Sessions = new(Microsoft.Extensions.Options.Options.Create(Options), Users, Registry, new ApiPasswordService(), Clock);
        }

        public void Dispose() => Sessions.Dispose();
    }

    private sealed class AuthorityClock : IClock {
        public DateTimeOffset Now { get; } = new(2026, 10, 16, 12, 0, 0, TimeSpan.Zero);
        public DateTimeOffset GetUtcNow() => Now;
    }

    private sealed class UserStore : IApiUserStore {
        public ApiUserAuthorityGeneration Generation { get; } = new(Guid.NewGuid());
        public IReadOnlyList<ApiUserRecord> Users { get; set; } = [];
        public int Reads { get; private set; }
        public int SnapshotReads { get; private set; }
        public int Observations { get; private set; }
        public Task<IReadOnlyList<ApiUserRecord>> ReadAsync(CancellationToken cancellationToken = default) {
            Reads++;
            return Task.FromResult(Users);
        }
        public Task<ApiUserStoreSnapshot> ReadSnapshotAsync(CancellationToken cancellationToken = default) {
            SnapshotReads++;
            return Task.FromResult(new ApiUserStoreSnapshot(Generation, Users));
        }
        public ValueTask<ApiUserAuthorityObservation> ObserveAuthorityAsync(ApiUserAuthorityGeneration expected, CancellationToken cancellationToken = default) {
            Observations++;
            return ValueTask.FromResult<ApiUserAuthorityObservation>(new ApiUserAuthorityObservation.Current());
        }
        public Task EnsureAuthorityFormatAsync(CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task SaveAsync(ApiUserRecord user, long? expectedVersion, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task DeleteAsync(Guid id, long expectedVersion, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }

    private sealed class TokenRegistry : IApiTokenRegistry {
        public ApiTokenRecord? Registered { get; private set; }
        public void Register(ApiTokenRecord token) => Registered = token;
        public Task PrepareCurrentReadAsync(CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public ValueTask<ApiTokenCurrentObservation> ObserveCurrentAsync(Guid id, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<ApiTokenRecord?> FindAsync(Guid id, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<ApiTokenPage> SearchAsync(ApiTokenQuery query, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task RevokeAsync(Guid id, DateTimeOffset revokedAtUtc, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task DeleteAsync(Guid id, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }
}
