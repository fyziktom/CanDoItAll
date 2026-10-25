using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text.Json;
using CanDoItAll.Infrastructure;
using CanDoItAll.Infrastructure.ControlPlane;
using CanDoItAll.Infrastructure.FileSystem;
using CanDoItAll.Modules.Projects;
using CanDoItAll.Modules.Workspace.ApiAccess;
using CanDoItAll.SharedKernel;
using CanDoItAll.Web.Api;
using CanDoItAll.Web.Infrastructure;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace CanDoItAll.Tests.Integration.Api;

[Trait("Category", "HostPlatform")]
public sealed class ProjectDefinitionCurrentWriteAccessTests {
    [Fact]
    public async Task Lifecycle_access_rechecks_the_same_original_credential_and_does_not_adopt_a_replacement_caller() {
        using var fixture = await Fixture.CreateAsync();
        IProjectLifecycleCurrentWriteAccess access = fixture.Access;
        Assert.Equal(ProjectLifecycleWriteAccessDecision.Allowed, await access.ObserveAsync(default));
        fixture.Tokens.Current = new ApiTokenCurrentObservation.Absent();
        Assert.Equal(ProjectLifecycleWriteAccessDecision.Denied, await access.ObserveAsync(default));
        fixture.Tokens.Current = new ApiTokenCurrentObservation.Found(fixture.Token with { Subject = "replacement-caller" });
        Assert.Equal(ProjectLifecycleWriteAccessDecision.CallerChanged, await access.ObserveAsync(default));
        Assert.Equal(3, fixture.Tokens.Observations);
    }

    private static readonly TimeSpan TestTimeout = TimeSpan.FromSeconds(10);
    private static readonly string Password = Convert.ToBase64String(RandomNumberGenerator.GetBytes(24));
    private static readonly string Hash = new ApiPasswordService().Hash(Password);

    [Fact]
    public async Task Real_JWT_middleware_publishes_one_selected_snapshot_and_current_checks_read_only_headers() {
        await using var host = await ApiTestHost.CreateAsync(true, services => {
            services.AddSingleton<CountingUsers>(provider => new(new FileApiUserStore(provider.GetRequiredService<IControlPlanePathResolver>(),
                provider.GetRequiredService<DurableFileWriter>(), provider.GetRequiredService<IPhysicalFileSystemPathPolicyFactory>())));
            services.AddSingleton<IApiUserStore>(provider => provider.GetRequiredService<CountingUsers>());
        }, useInMemoryDatabase: true, configureApplication: app => {
            app.MapGet("/api/definition-authority-proof", async (HttpContext context, ProjectDefinitionHttpAccess access) => {
                var invocation = await access.ResolveWriteAsync(context.RequestAborted);
                var first = await invocation.CurrentWrite!.ObserveAsync(context.RequestAborted);
                var second = await invocation.CurrentWrite.ObserveAsync(context.RequestAborted);
                return Results.Ok(new { Published = context.Features.Get<ApiAuthenticationObservation>() is not null, First = first, Second = second });
            }).RequireAuthorization();
        }, apiConfiguration: new Dictionary<string, string?> {
            ["Api:UserAuthentication:Enabled"] = "true",
            ["Api:UserAuthentication:AllowLoopbackHttp"] = "true",
            ["Api:BootstrapAdmin:PasswordHash"] = Hash
        });
        var users = host.App.Services.GetRequiredService<CountingUsers>();
        var now = DateTimeOffset.UtcNow;
        await users.SaveAsync(new(Guid.NewGuid(), "author", "AUTHOR", "Author", true, Hash,
            [ApiAccessScopeNames.ReadProjects, ApiAccessScopeNames.WriteProjects], 1, 1, now, now), null);
        var login = Assert.IsType<ApiLoginResult>(await host.App.Services.GetRequiredService<ApiSessionService>().LoginAsync(new("author", Password)));
        users.Reset();
        host.Client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", login.Token);

        using var response = await host.Client.GetAsync("/api/definition-authority-proof");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(body.GetProperty("published").GetBoolean());
        Assert.Equal((int)ProjectDefinitionWriteAccessDecision.Allowed, body.GetProperty("first").GetInt32());
        Assert.Equal((int)ProjectDefinitionWriteAccessDecision.Allowed, body.GetProperty("second").GetInt32());
        Assert.Equal(1, users.SnapshotReads);
        Assert.Equal(0, users.Reads);
        Assert.Equal(2, users.HeaderReads);
    }

    [Fact]
    public async Task Missing_or_substituted_principal_facts_refuse_without_refetching_or_aliasing_scope_collections() {
        using var fixture = await Fixture.CreateAsync();
        var resolver = new WebCurrentPrincipalResolver(new AnonymousInteractiveAccessPrincipalProvider(),
            new HttpContextAccessor { HttpContext = fixture.Context }, new NoAuthorization(), fixture.Tokens, fixture.Sessions,
            Options.Create(fixture.Options), fixture.Clock);
        await Assert.ThrowsAsync<WebCurrentPrincipalDeniedException>(() => resolver.CaptureDefinitionWriteAuthorityAsync(CancellationToken.None));
        fixture.Context.Features.Set(fixture.Observation);
        Assert.Same(fixture.Observation, await resolver.CaptureDefinitionWriteAuthorityAsync(CancellationToken.None));
        fixture.Scopes[0] = "client-substitution";
        Assert.DoesNotContain("client-substitution", fixture.Observation!.Credential!.Scopes);
        ((ClaimsIdentity)fixture.Context.User.Identity!).AddClaim(new Claim(ClaimTypes.NameIdentifier, "other"));
        await Assert.ThrowsAsync<WebCurrentPrincipalDeniedException>(() => resolver.CaptureDefinitionWriteAuthorityAsync(CancellationToken.None));
        Assert.Equal(ProjectDefinitionWriteAccessDecision.CallerChanged, await fixture.Access.ObserveAsync(CancellationToken.None));
        Assert.Equal(0, fixture.Tokens.Observations);
        Assert.Equal(0, fixture.Users.Reads);
        Assert.Equal(0, fixture.Users.SnapshotReads);
    }

    [Fact]
    public async Task Current_managed_metadata_cannot_change_subject_kind_user_binding_grants_or_original_payload() {
        using var fixture = await Fixture.CreateAsync();
        var original = fixture.Token;
        foreach (var changed in new[] { original with { Subject = "other" }, original with { UserId = Guid.NewGuid() },
                original with { Kind = ApiCredentialKind.UserSession } }) {
            fixture.Tokens.Current = new ApiTokenCurrentObservation.Found(changed);
            Assert.Equal(ProjectDefinitionWriteAccessDecision.CallerChanged, await fixture.Access.ObserveAsync(CancellationToken.None));
        }
        foreach (var changed in new[] { original with { Scopes = [ApiAccessScopeNames.ReadProjects] },
                original with { RevokedAtUtc = fixture.Clock.Now }, original with { DisplayName = "changed" },
                original with { ExpiresAtUtc = fixture.Clock.Now } }) {
            fixture.Tokens.Current = new ApiTokenCurrentObservation.Found(changed);
            Assert.Equal(ProjectDefinitionWriteAccessDecision.Denied, await fixture.Access.ObserveAsync(CancellationToken.None));
        }
        fixture.Tokens.Current = new ApiTokenCurrentObservation.Absent();
        Assert.Equal(ProjectDefinitionWriteAccessDecision.Denied, await fixture.Access.ObserveAsync(CancellationToken.None));
        Assert.Equal(0, fixture.Users.HeaderReads);
    }

    [Fact]
    public async Task Legacy_and_open_requests_keep_their_original_mode_exact_expiry_and_supported_claim_shape() {
        using var fixture = await Fixture.CreateAsync(managed: false);
        Assert.Equal(ProjectDefinitionWriteAccessDecision.Allowed, await fixture.Access.ObserveAsync(CancellationToken.None));
        Assert.Equal(0, fixture.Tokens.Observations);
        fixture.Clock.Now = fixture.Token.ExpiresAtUtc;
        Assert.Equal(ProjectDefinitionWriteAccessDecision.Denied, await fixture.Access.ObserveAsync(CancellationToken.None));
        fixture.Options.Authorization.Enabled = false;
        Assert.Equal(ProjectDefinitionWriteAccessDecision.CallerChanged, await fixture.Access.ObserveAsync(CancellationToken.None));
        var open = fixture.CreateAccess(null);
        Assert.Equal(ProjectDefinitionWriteAccessDecision.Allowed, await open.ObserveAsync(CancellationToken.None));
        fixture.Options.Authorization.Enabled = true;
        Assert.Equal(ProjectDefinitionWriteAccessDecision.CallerChanged, await open.ObserveAsync(CancellationToken.None));
        using var payload = fixture.Payload();
        ((ClaimsIdentity)fixture.Context.User.Identity!).AddClaim(new Claim("sub", "ambiguous"));
        Assert.Null(ApiAuthenticationObservation.TryCapture(fixture.Context.User, fixture.Options.Authorization.Issuer,
            payload.RootElement, null, null));
        using var managed = await Fixture.CreateAsync();
        using var managedPayload = managed.Payload();
        ((ClaimsIdentity)managed.Context.User.Identity!).AddClaim(new Claim(ApiManagedTokenClaims.Version, ApiManagedTokenClaims.CurrentVersion));
        Assert.Null(ApiAuthenticationObservation.TryCapture(managed.Context.User, managed.Options.Authorization.Issuer,
            managedPayload.RootElement, managed.Token, null));
    }

    [Fact]
    public async Task Ordinary_generation_conflict_and_current_configuration_use_header_reads_without_new_full_scans() {
        using var fixture = await Fixture.CreateAsync(user: true);
        Assert.Equal(ProjectDefinitionWriteAccessDecision.Allowed, await fixture.Access.ObserveAsync(CancellationToken.None));
        fixture.Users.Authority = new ApiUserAuthorityObservation.Changed();
        Assert.Equal(ProjectDefinitionWriteAccessDecision.AuthorityChanged, await fixture.Access.ObserveAsync(CancellationToken.None));
        fixture.Users.Authority = new ApiUserAuthorityObservation.Current();
        fixture.Options.BootstrapAdmin.UserName = "author";
        Assert.Equal(ProjectDefinitionWriteAccessDecision.Denied, await fixture.Access.ObserveAsync(CancellationToken.None));
        Assert.Equal(2, fixture.Users.HeaderReads);
        Assert.Equal(0, fixture.Users.Reads);
        Assert.Equal(0, fixture.Users.SnapshotReads);
    }

    [Fact]
    public async Task Unavailable_and_expired_budget_are_sanitized_and_a_late_read_is_drained_before_return() {
        using var fixture = await Fixture.CreateAsync();
        fixture.Tokens.Current = new ApiTokenCurrentObservation.Unavailable(ApiAuthorityReadFailure.InvalidFormat);
        Assert.Equal(ProjectDefinitionWriteAccessDecision.Unavailable, await fixture.Access.ObserveAsync(CancellationToken.None));
        fixture.Tokens.Pending = _ => ValueTask.FromException<ApiTokenCurrentObservation>(new IOException("private-store-detail"));
        Assert.Equal(ProjectDefinitionWriteAccessDecision.Unavailable, await fixture.Access.ObserveAsync(CancellationToken.None));
        var pending = new PendingObservation(fixture.Token);
        fixture.Tokens.Pending = pending.ReadAsync;
        var observing = fixture.Access.ObserveAsync(CancellationToken.None).AsTask();
        try {
            await pending.Entered.Task.WaitAsync(TestTimeout);
            await pending.Canceled.Task.WaitAsync(TestTimeout);
            Assert.False(observing.IsCompleted);
            pending.Release.TrySetResult();
            Assert.Equal(ProjectDefinitionWriteAccessDecision.Unavailable, await observing.WaitAsync(TestTimeout));
            Assert.Equal(0, pending.Active);
        } finally {
            pending.Release.TrySetResult();
            await observing.WaitAsync(TestTimeout);
        }
    }

    [Fact]
    public async Task Caller_cancellation_wins_over_the_observation_budget_and_never_becomes_unavailable() {
        using var fixture = await Fixture.CreateAsync();
        foreach (var alsoExpireBudget in new[] { false, true }) {
            using var cancellation = new CancellationTokenSource();
            var pending = new PendingObservation(fixture.Token);
            fixture.Tokens.Pending = pending.ReadAsync;
            var observing = fixture.Access.ObserveAsync(cancellation.Token).AsTask();
            try {
                await pending.Entered.Task.WaitAsync(TestTimeout);
                if (alsoExpireBudget) {
                    await pending.Canceled.Task.WaitAsync(TestTimeout);
                }
                cancellation.Cancel();
                pending.Release.TrySetResult();
                var error = await Assert.ThrowsAnyAsync<OperationCanceledException>(() => observing.WaitAsync(TestTimeout));
                Assert.Equal(cancellation.Token, error.CancellationToken);
                Assert.Equal(0, pending.Active);
            } finally {
                cancellation.Cancel();
                pending.Release.TrySetResult();
                try {
                    await observing.WaitAsync(TestTimeout);
                } catch (OperationCanceledException) when (cancellation.IsCancellationRequested) {
                }
            }
        }
    }

    [Fact]
    public async Task Actual_HTTP_result_mapping_distinguishes_known_refusal_from_unconfirmed_with_the_same_503() {
        var ns = new DatasetNamespaceId(Guid.NewGuid());
        var operation = new ProjectDefinitionOperation(ns, new(1), Guid.NewGuid(), ProjectMutationCallerScope.OpenApi.Bind(ns));
        ProjectDefinitionOutcome<ProjectEditorAcknowledgement>[] outcomes = [
            new ProjectDefinitionOutcome<ProjectEditorAcknowledgement>.Rejected(operation,
                [ProjectDefinitionCurrentWriteGuard.ToError(ProjectDefinitionWriteAccessDecision.Unavailable)]),
            new ProjectDefinitionOutcome<ProjectEditorAcknowledgement>.Unconfirmed(operation, ProjectDefinitionRecoveryReason.CommitUnconfirmed)
        ];
        await using var services = new ServiceCollection().AddLogging().AddOptions().BuildServiceProvider();
        foreach (var outcome in outcomes) {
            var context = new DefaultHttpContext { RequestServices = services };
            await using var body = new MemoryStream();
            context.Response.Body = body;
            await ProjectDefinitionApi.Result(outcome).ExecuteAsync(context);
            Assert.Equal(StatusCodes.Status503ServiceUnavailable, context.Response.StatusCode);
            body.Position = 0;
            using var document = await JsonDocument.ParseAsync(body);
            var root = document.RootElement;
            Assert.Equal(operation.Id, root.GetProperty("operation").GetProperty("id").GetGuid());
            if (outcome is ProjectDefinitionOutcome<ProjectEditorAcknowledgement>.Rejected) {
                Assert.Equal("rejected", root.GetProperty("status").GetString());
                Assert.Equal(ProjectErrorCodes.DefinitionAuthorityUnavailable, root.GetProperty("errors")[0].GetProperty("code").GetString());
                Assert.False(root.TryGetProperty("reason", out _));
            } else {
                Assert.Equal("unconfirmed", root.GetProperty("status").GetString());
                Assert.True(root.TryGetProperty("reason", out _));
                Assert.False(root.TryGetProperty("errors", out _));
            }
        }
    }

    private sealed class Fixture : IDisposable {
        public AuthorityClock Clock { get; } = new();
        public DefaultHttpContext Context { get; } = new();
        public TestUsers Users { get; } = new();
        public TestTokens Tokens { get; } = new();
        public ApiAccessOptions Options { get; } = new() {
            Authorization = new() { Enabled = true, Issuer = "authority-fixture" },
            UserAuthentication = new() { Enabled = true }
        };
        public string[] Scopes { get; private set; } = [];
        public ApiTokenRecord Token { get; private set; } = null!;
        public ApiSessionService Sessions { get; private set; } = null!;
        public ApiAuthenticationObservation? Observation { get; private set; }
        public ProjectDefinitionCurrentWriteAccess Access { get; private set; } = null!;

        public static async Task<Fixture> CreateAsync(bool user = false, bool managed = true) {
            var fixture = new Fixture();
            var id = Guid.NewGuid();
            fixture.Scopes = user
                ? [ApiAccessScopeNames.ReadProjects, ApiAccessScopeNames.Session, ApiAccessScopeNames.WriteProjects]
                : [ApiAccessScopeNames.ReadProjects, ApiAccessScopeNames.WriteProjects];
            fixture.Token = new(Guid.NewGuid(), user ? $"api-user:{id:N}" : "machine", "Caller", fixture.Clock.Now,
                fixture.Clock.Now.AddHours(1), fixture.Scopes, Kind: user ? ApiCredentialKind.UserSession : ApiCredentialKind.Machine,
                UserId: user ? id : null, AuthenticationRevision: user ? 1 : null);
            fixture.Tokens.Current = new ApiTokenCurrentObservation.Found(fixture.Token);
            fixture.Users.User = new(id, "author", "AUTHOR", "Author", true, Hash,
                [ApiAccessScopeNames.ReadProjects, ApiAccessScopeNames.WriteProjects], 1, 1, fixture.Clock.Now, fixture.Clock.Now);
            fixture.Sessions = new(Microsoft.Extensions.Options.Options.Create(fixture.Options), fixture.Users, fixture.Tokens,
                new ApiPasswordService(), fixture.Clock);
            var identity = new ClaimsIdentity([
                new("iss", fixture.Options.Authorization.Issuer), new(ClaimTypes.NameIdentifier, fixture.Token.Subject),
                new("exp", fixture.Token.ExpiresAtUtc.ToUnixTimeSeconds().ToString(CultureInfo.InvariantCulture)),
                new("scope", string.Join(' ', fixture.Scopes)), .. fixture.Scopes.Select(scope => new Claim("scopes", scope))
            ], "Bearer");
            if (managed) {
                identity.AddClaim(new(ApiManagedTokenClaims.Version, user ? ApiManagedTokenClaims.SessionVersion : ApiManagedTokenClaims.CurrentVersion));
                identity.AddClaim(new(ApiManagedTokenClaims.TokenId, fixture.Token.Id.ToString("N")));
            }
            fixture.Context.User = new(identity);
            using var payload = fixture.Payload();
            var session = user ? await fixture.Sessions.ResolveAuthoritySnapshotAsync(fixture.Token) : null;
            fixture.Observation = Assert.IsType<ApiAuthenticationObservation>(ApiAuthenticationObservation.TryCapture(fixture.Context.User,
                fixture.Options.Authorization.Issuer, payload.RootElement, managed ? fixture.Token : null, session));
            fixture.Access = fixture.CreateAccess(fixture.Observation);
            fixture.Users.SnapshotReads = 0;
            return fixture;
        }

        public JsonDocument Payload() => JsonDocument.Parse(JsonSerializer.Serialize(new {
            iss = Options.Authorization.Issuer, sub = Token.Subject, exp = Token.ExpiresAtUtc.ToUnixTimeSeconds()
        }));
        public ProjectDefinitionCurrentWriteAccess CreateAccess(ApiAuthenticationObservation? observation)
            => new(Context, observation, Tokens, Users, Sessions, Microsoft.Extensions.Options.Options.Create(Options), Clock);
        public void Dispose() => Sessions.Dispose();
    }

    private sealed class AuthorityClock : TimeProvider, IClock {
        public DateTimeOffset Now { get; set; } = DateTimeOffset.FromUnixTimeSeconds(DateTimeOffset.UtcNow.ToUnixTimeSeconds());
        public override DateTimeOffset GetUtcNow() => Now;
    }

    private sealed class PendingObservation(ApiTokenRecord token) {
        public TaskCompletionSource Entered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource Canceled { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public int Active { get; private set; }
        public async ValueTask<ApiTokenCurrentObservation> ReadAsync(CancellationToken cancellationToken) {
            Active++;
            try {
                using var registration = cancellationToken.Register(() => Canceled.TrySetResult());
                Entered.TrySetResult();
                await Release.Task;
                return new ApiTokenCurrentObservation.Found(token);
            } finally {
                Active--;
            }
        }
    }

    private sealed class TestTokens : IApiTokenRegistry {
        public ApiTokenCurrentObservation Current { get; set; } = new ApiTokenCurrentObservation.Absent();
        public Func<CancellationToken, ValueTask<ApiTokenCurrentObservation>>? Pending { get; set; }
        public int Observations { get; private set; }
        public ValueTask<ApiTokenCurrentObservation> ObserveCurrentAsync(Guid id, CancellationToken cancellationToken = default) {
            Observations++;
            return Pending?.Invoke(cancellationToken) ?? ValueTask.FromResult(Current);
        }
        public void Register(ApiTokenRecord token) => throw new NotSupportedException();
        public Task PrepareCurrentReadAsync(CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<ApiTokenRecord?> FindAsync(Guid id, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<ApiTokenPage> SearchAsync(ApiTokenQuery query, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task RevokeAsync(Guid id, DateTimeOffset revokedAtUtc, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task DeleteAsync(Guid id, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }

    private sealed class TestUsers : IApiUserStore {
        public ApiUserRecord User { get; set; } = null!;
        public ApiUserAuthorityGeneration Generation { get; } = new(Guid.NewGuid());
        public ApiUserAuthorityObservation Authority { get; set; } = new ApiUserAuthorityObservation.Current();
        public int Reads { get; private set; }
        public int SnapshotReads { get; set; }
        public int HeaderReads { get; private set; }
        public Task<IReadOnlyList<ApiUserRecord>> ReadAsync(CancellationToken cancellationToken = default) {
            Reads++;
            throw new NotSupportedException();
        }
        public Task<ApiUserStoreSnapshot> ReadSnapshotAsync(CancellationToken cancellationToken = default) {
            SnapshotReads++;
            return Task.FromResult(new ApiUserStoreSnapshot(Generation, [User]));
        }
        public ValueTask<ApiUserAuthorityObservation> ObserveAuthorityAsync(ApiUserAuthorityGeneration expected, CancellationToken cancellationToken = default) {
            Assert.Equal(Generation, expected);
            HeaderReads++;
            return ValueTask.FromResult(Authority);
        }
        public Task EnsureAuthorityFormatAsync(CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task SaveAsync(ApiUserRecord user, long? expectedVersion, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task DeleteAsync(Guid id, long expectedVersion, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }

    private sealed class NoAuthorization : IAuthorizationService {
        public Task<AuthorizationResult> AuthorizeAsync(ClaimsPrincipal user, object? resource, IEnumerable<IAuthorizationRequirement> requirements)
            => throw new NotSupportedException();
        public Task<AuthorizationResult> AuthorizeAsync(ClaimsPrincipal user, object? resource, string policyName) => throw new NotSupportedException();
    }

    private sealed class CountingUsers(FileApiUserStore inner) : IApiUserStore {
        public int Reads { get; private set; }
        public int SnapshotReads { get; private set; }
        public int HeaderReads { get; private set; }
        public void Reset() {
            Reads = 0;
            SnapshotReads = 0;
            HeaderReads = 0;
        }
        public Task<IReadOnlyList<ApiUserRecord>> ReadAsync(CancellationToken cancellationToken = default) {
            Reads++;
            return inner.ReadAsync(cancellationToken);
        }
        public Task<ApiUserStoreSnapshot> ReadSnapshotAsync(CancellationToken cancellationToken = default) {
            SnapshotReads++;
            return inner.ReadSnapshotAsync(cancellationToken);
        }
        public ValueTask<ApiUserAuthorityObservation> ObserveAuthorityAsync(ApiUserAuthorityGeneration expected, CancellationToken cancellationToken = default) {
            HeaderReads++;
            return inner.ObserveAuthorityAsync(expected, cancellationToken);
        }
        public Task EnsureAuthorityFormatAsync(CancellationToken cancellationToken = default) => inner.EnsureAuthorityFormatAsync(cancellationToken);
        public Task SaveAsync(ApiUserRecord user, long? expectedVersion, CancellationToken cancellationToken = default) => inner.SaveAsync(user, expectedVersion, cancellationToken);
        public Task DeleteAsync(Guid id, long expectedVersion, CancellationToken cancellationToken = default) => inner.DeleteAsync(id, expectedVersion, cancellationToken);
    }
}
