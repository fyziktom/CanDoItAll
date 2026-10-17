using System.Data.Common;
using System.Collections.Concurrent;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using CanDoItAll.Infrastructure.ControlPlane;
using CanDoItAll.Infrastructure.Search;
using CanDoItAll.Modules.Projects;
using CanDoItAll.Modules.Workspace.ApiAccess;
using CanDoItAll.SharedKernel;
using CanDoItAll.Web.Api;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Logging;
using Npgsql;

namespace CanDoItAll.Tests.Integration.Api;

internal sealed class ProjectDefinitionHttpCurrentWriteFixture : IAsyncDisposable {
    internal static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) {
        Converters = { new ProjectOperationEpochHttpJsonConverter() }
    };
    internal static readonly string[] Grants = [ApiAccessScopeNames.ReadProjects, ApiAccessScopeNames.WriteProjects];
    private ProjectDefinitionHttpCurrentWriteFixture(ApiTestHost host, AuthorityProbe probe, AuthorityClock clock,
        ObservedUsers users, ObservedTokens tokens) {
        Host = host;
        Probe = probe;
        Clock = clock;
        Users = users;
        Tokens = tokens;
    }

    internal ApiTestHost Host { get; }
    internal AuthorityProbe Probe { get; }
    internal AuthorityClock Clock { get; }
    internal ObservedUsers Users { get; }
    internal ObservedTokens Tokens { get; }
    internal ApiUserRecord? User { get; private set; }
    internal ApiTokenRecord? TokenRecord { get; private set; }
    internal string Password { get; private set; } = string.Empty;
    internal string Token { get; private set; } = string.Empty;
    internal ProjectDefinitionWriteContext Context { get; private set; } = null!;
    internal ProjectEditorAcknowledgement Original { get; private set; } = null!;
    internal ApiAccessOptions Options => Host.App.Services.GetRequiredService<IOptions<ApiAccessOptions>>().Value;
    internal string AccountsPath => Path.Combine(Host.App.Services.GetRequiredService<IControlPlanePathResolver>().ResolveRootPath(), "api-users", "accounts.json");

    internal static async Task<ProjectDefinitionHttpCurrentWriteFixture> CreateAsync(bool ordinary = false, bool open = false) {
        var probe = new AuthorityProbe();
        var clock = new AuthorityClock();
        ObservedUsers? users = null;
        ObservedTokens? tokens = null;
        var configuration = ordinary ? new Dictionary<string, string?> {
            ["Api:UserAuthentication:Enabled"] = "true",
            ["Api:UserAuthentication:AllowLoopbackHttp"] = "true",
            ["Api:BootstrapAdmin:PasswordHash"] = new ApiPasswordService().Hash(ApiUserSessionIntegrationTests.Password())
        } : null;
        var host = await ApiTestHost.CreateAsync(!open, services => {
            services.AddSingleton<TimeProvider>(clock);
            services.AddSingleton<IClock>(clock);
            services.AddLogging(logging => logging.AddProvider(new AuthorityLogProbe(probe)));
            services.Replace(ServiceDescriptor.Singleton<IApiUserStore>(provider => users =
                new(ActivatorUtilities.CreateInstance<FileApiUserStore>(provider), probe)));
            services.Replace(ServiceDescriptor.Singleton<IApiTokenRegistry>(provider => tokens =
                new(ActivatorUtilities.CreateInstance<FileApiTokenRegistry>(provider))));
            services.ConfigureDbContext<ProjectsDbContext>(options => options.AddInterceptors(
                probe, new AuthorityCommitProbe(probe), new AuthorityReleaseProbe(probe)), ServiceLifetime.Singleton);
            services.ConfigureDbContext<SearchDbContext>(options => options.AddInterceptors(probe), ServiceLifetime.Singleton);
        }, apiConfiguration: configuration);
        try {
            var fixture = new ProjectDefinitionHttpCurrentWriteFixture(host, probe, clock,
                users ?? (ObservedUsers)host.App.Services.GetRequiredService<IApiUserStore>(),
                tokens ?? (ObservedTokens)host.App.Services.GetRequiredService<IApiTokenRegistry>());
            clock.Set(DateTimeOffset.FromUnixTimeSeconds(DateTimeOffset.UtcNow.ToUnixTimeSeconds()));
            if (ordinary) {
                fixture.Password = ApiUserSessionIntegrationTests.Password();
                fixture.User = await fixture.AddUserAsync("writer", fixture.Password);
                await fixture.LoginAsync();
            } else if (!open) {
                fixture.UseMachineToken(Grants, lifetimeMinutes: 1);
                var summary = Assert.Single((await fixture.Tokens.Inner.SearchAsync(new("current-write-fixture"))).Items);
                fixture.TokenRecord = await fixture.Tokens.Inner.FindAsync(summary.Id);
            }
            fixture.Context = (await host.Client.GetFromJsonAsync<ProjectDefinitionWriteContext>("/api/projects/definition-write-context", Json))!;
            var create = fixture.Request(new CreateProjectDefinitionCommand(
                new("Original HTTP project", "Original description", "Original objective", ProjectStatus.Draft, "Planning", null),
                [new(null, "Original phase", "Original goal", ProjectPhaseStatus.Planned, null, null)],
                [new(null, ProjectOptionCategory.Language, "Original option", "Original notes")], null));
            using var response = await host.Client.PostAsJsonAsync("/api/projects/definitions", create, Json);
            fixture.Original = (await AcceptedAsync(response, HttpStatusCode.Created)).Receipt.Acknowledgement;
            fixture.Reset();
            return fixture;
        } catch {
            await host.DisposeAsync();
            throw;
        }
    }

    internal async Task<ApiUserRecord> AddUserAsync(string prefix, string? password = null) {
        var name = $"{prefix}-{Guid.NewGuid():N}";
        var now = Clock.GetUtcNow();
        var user = new ApiUserRecord(Guid.NewGuid(), name, ApiIdentityRules.NormalizeUserName(name), name, true,
            new ApiPasswordService().Hash(password ?? ApiUserSessionIntegrationTests.Password()), Grants, 1, 1, now, now);
        await Users.Inner.SaveAsync(user, null);
        return user;
    }

    internal async Task SaveUserAsync(ApiUserRecord updated) {
        var original = User ?? throw new InvalidOperationException("This fixture has no ordinary account.");
        await Users.Inner.SaveAsync(updated, original.Version);
        User = updated;
    }

    internal async Task LoginAsync() {
        var user = User ?? throw new InvalidOperationException("This fixture has no ordinary account.");
        var prior = (await Tokens.Inner.SearchAsync(new(Kind: ApiCredentialKind.UserSession))).Items.Select(item => item.Id).ToHashSet();
        var login = await ApiUserSessionIntegrationTests.LoginAsync(Host.Client, user.UserName, Password);
        Token = login.Token;
        Host.Client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", Token);
        var summary = Assert.Single((await Tokens.Inner.SearchAsync(new(Kind: ApiCredentialKind.UserSession))).Items,
            item => !prior.Contains(item.Id));
        TokenRecord = await Tokens.Inner.FindAsync(summary.Id);
        Assert.Equal(user.Id, TokenRecord!.UserId);
    }

    internal void UseMachineToken(IReadOnlyList<string> grants, int lifetimeMinutes = 1) {
        var issued = Host.App.Services.GetRequiredService<IApiTokenService>().IssueToken(new() {
            Subject = "current-write-fixture", Scopes = [.. grants], LifetimeMinutes = lifetimeMinutes
        });
        Token = issued.Token;
        Host.Client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", Token);
    }

    internal ProjectDefinitionRequest<T> Request<T>(T command) where T : class
        => new(new(Context.Namespace, Context.Epoch, Guid.NewGuid(), Context.CallerBinding), ProjectDefinitionRequestVersion.VersionOne, command);

    internal ProjectDefinitionRequest<ReplaceProjectDefinitionCommand> Replacement(string name = "Proposed HTTP replacement",
        ProjectEditorAcknowledgement? basis = null) {
        basis ??= Original;
        return Request(new ReplaceProjectDefinitionCommand(basis.Definition,
            new(name, "Changed description", "Changed objective", ProjectStatus.Draft, "Delivery", null),
            [new(basis.Phases[0].Id, "Changed phase", "Changed goal", ProjectPhaseStatus.Planned, null, null),
                new(null, "Additional phase", "Additional goal", ProjectPhaseStatus.Planned, null, null)],
            [new(basis.Options[0].Id, ProjectOptionCategory.Language, "Changed option", "Changed notes"),
                new(null, ProjectOptionCategory.Language, "Additional option", "Additional notes")], null));
    }

    internal Task<HttpResponseMessage> SendAsync(ProjectDefinitionRequest<ReplaceProjectDefinitionCommand> request,
        CancellationToken cancellationToken = default)
        => Host.Client.PutAsJsonAsync($"/api/projects/{request.Command.Expected.Project.ProjectId:D}/definition", request, Json, cancellationToken);

    internal void Reset(AuthorityPause pause = AuthorityPause.None) {
        Probe.Arm(Original.Project.ProjectId, pause);
        Users.Reset();
        Tokens.Reset();
    }

    internal async Task<string> StateAsync() {
        await using var scope = Host.App.Services.CreateAsyncScope();
        await using var database = await scope.ServiceProvider.GetRequiredService<IDbContextFactory<ProjectsDbContext>>().CreateDbContextAsync();
        await using var search = await scope.ServiceProvider.GetRequiredService<IDbContextFactory<SearchDbContext>>().CreateDbContextAsync();
        return JsonSerializer.Serialize(new {
            Roots = await database.Set<Project>().AsNoTracking().OrderBy(row => row.Id).Select(row => new {
                row.Id, row.LifetimeId, row.ConcurrencyToken, row.Name, row.Description, row.Objective,
                row.Status, row.CurrentPhase, row.TargetDateUtc, row.UpdatedAtUtc
            }).ToArrayAsync(),
            Phases = await database.Set<ProjectPhase>().AsNoTracking().OrderBy(row => row.Id).ToArrayAsync(),
            Options = await database.Set<ProjectOptionSelection>().AsNoTracking().OrderBy(row => row.Id).ToArrayAsync(),
            Search = await search.Set<SearchDocument>().AsNoTracking().OrderBy(row => row.Id).ToArrayAsync(),
            Receipts = await database.Set<ProjectDefinitionReceiptRecord>().AsNoTracking().OrderBy(row => row.OperationId).ToArrayAsync()
        }, Json);
    }

    internal async Task<OwnedProjectGuard> HoldProjectAsync() {
        await using var scope = Host.App.Services.CreateAsyncScope();
        await using var database = await scope.ServiceProvider.GetRequiredService<IDbContextFactory<ProjectsDbContext>>().CreateDbContextAsync();
        return await OwnedProjectGuard.AcquireAsync(database.Database.GetConnectionString()!, Original.Project.ProjectId);
    }

    internal static async Task<ProjectDefinitionAcceptedApiResponse<ProjectEditorAcknowledgement>> AcceptedAsync(HttpResponseMessage response,
        HttpStatusCode status = HttpStatusCode.OK) {
        Assert.Equal(status, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(Json);
        Assert.Equal("accepted", body.GetProperty("status").GetString());
        return body.Deserialize<ProjectDefinitionAcceptedApiResponse<ProjectEditorAcknowledgement>>(Json)!;
    }

    internal async Task RejectedAsync(HttpResponseMessage response, ProjectDefinitionOperation operation, HttpStatusCode status, string code) {
        Assert.Equal(status, response.StatusCode);
        var text = await response.Content.ReadAsStringAsync();
        var body = JsonSerializer.Deserialize<JsonElement>(text, Json);
        Assert.Equal("rejected", body.GetProperty("status").GetString());
        Assert.Equal(operation, body.GetProperty("operation").Deserialize<ProjectDefinitionOperation>(Json));
        Assert.Contains(body.GetProperty("errors").EnumerateArray(), error => error.GetProperty("code").GetString() == code);
        Assert.DoesNotContain(Host.RootPath, text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Injected", text, StringComparison.OrdinalIgnoreCase);
        if (Token.Length > 0) {
            Assert.DoesNotContain(Token, text, StringComparison.Ordinal);
        }
        if (User is { } user) {
            Assert.DoesNotContain(user.PasswordHash, text, StringComparison.Ordinal);
        }
    }

    public async ValueTask DisposeAsync() {
        Probe.Release.TrySetResult();
        Users.ReleasePending.TrySetResult();
        await Host.DisposeAsync();
    }
}

internal sealed class AuthorityClock : TimeProvider, IClock {
    private long ticks = DateTimeOffset.FromUnixTimeSeconds(DateTimeOffset.UtcNow.ToUnixTimeSeconds()).Ticks;
    public override DateTimeOffset GetUtcNow() => new(Interlocked.Read(ref ticks), TimeSpan.Zero);
    internal void Set(DateTimeOffset value) => Interlocked.Exchange(ref ticks, value.UtcTicks);
}

internal enum AuthorityPause { None, FinalLifetime, ReceiptObservation }

internal sealed class AuthorityProbe : DbCommandInterceptor {
    private string projectKey = string.Empty;
    private int lifetimeReads;
    internal AuthorityPause Pause { get; private set; }
    internal DbConnection? OwnerConnection { get; private set; }
    internal Guid? OwnerContext { get; private set; }
    internal bool GuardHeld { get; private set; }
    internal bool RootFlushed { get; private set; }
    internal bool SearchFlushed { get; private set; }
    internal bool ReceiptFlushed { get; private set; }
    internal bool FinalLifetimeExecuted { get; private set; }
    internal int CommitAttempts;
    internal int Commits;
    internal bool FailDisposal { get; set; }
    internal bool DisposalFailed { get; set; }
    internal bool AuthorityUnavailable { get; set; }
    internal bool FailRecoveryAfterUnavailable { get; set; }
    internal int RecoveryQueries;
    internal ConcurrentQueue<string> CleanupFailureTypes { get; } = new();
    internal TaskCompletionSource<int> ProjectCommand { get; private set; } = NewSource<int>();
    internal TaskCompletionSource Paused { get; private set; } = NewSource();
    internal TaskCompletionSource Release { get; private set; } = NewSource();
    internal TaskCompletionSource Released { get; private set; } = NewSource();

    internal void Arm(Guid projectId, AuthorityPause pause) {
        projectKey = ProjectMutationScopeKeys.ForProject(projectId);
        Pause = pause;
        lifetimeReads = 0;
        OwnerConnection = null;
        OwnerContext = null;
        GuardHeld = false;
        RootFlushed = false;
        SearchFlushed = false;
        ReceiptFlushed = false;
        FinalLifetimeExecuted = false;
        CommitAttempts = 0;
        Commits = 0;
        FailDisposal = false;
        DisposalFailed = false;
        AuthorityUnavailable = false;
        FailRecoveryAfterUnavailable = false;
        RecoveryQueries = 0;
        CleanupFailureTypes.Clear();
        ProjectCommand = NewSource<int>();
        Paused = NewSource();
        Release = NewSource();
        Released = NewSource();
    }

    public override ValueTask<InterceptionResult<int>> NonQueryExecutingAsync(DbCommand command, CommandEventData eventData,
        InterceptionResult<int> result, CancellationToken cancellationToken = default) {
        if (IsProjectGuard(command)) {
            OwnerConnection = command.Connection;
            OwnerContext = eventData.Context!.ContextId.InstanceId;
            ProjectCommand.TrySetResult(((NpgsqlConnection)command.Connection!).ProcessID);
        }
        return ValueTask.FromResult(result);
    }

    public override ValueTask<int> NonQueryExecutedAsync(DbCommand command, CommandExecutedEventData eventData,
        int result, CancellationToken cancellationToken = default) {
        if (IsProjectGuard(command)) {
            GuardHeld = true;
        }
        ObserveFlush(command);
        return ValueTask.FromResult(result);
    }

    public override async ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(DbCommand command, CommandEventData eventData,
        InterceptionResult<DbDataReader> result, CancellationToken cancellationToken = default) {
        if (AuthorityUnavailable && FailRecoveryAfterUnavailable && command.CommandText.Contains("AS \"ActiveNamespaceId\"", StringComparison.Ordinal)) {
            Interlocked.Increment(ref RecoveryQueries);
            throw new IOException("Injected independent receipt-read outage after known authority refusal.");
        }
        if (ReferenceEquals(command.Connection, OwnerConnection) && command.CommandText.Contains("FOR KEY SHARE", StringComparison.Ordinal) &&
            Interlocked.Increment(ref lifetimeReads) == 2 && Pause == AuthorityPause.FinalLifetime) {
            Assert.NotNull(command.Transaction);
            Assert.Equal(System.Data.IsolationLevel.ReadCommitted, command.Transaction.IsolationLevel);
            Assert.True(RootFlushed && SearchFlushed && ReceiptFlushed, "The final lifetime command must follow all three real SQL flushes.");
            Paused.TrySetResult();
            await Release.Task.WaitAsync(cancellationToken);
        }
        return result;
    }

    public override async ValueTask<DbDataReader> ReaderExecutedAsync(DbCommand command, CommandExecutedEventData eventData,
        DbDataReader result, CancellationToken cancellationToken = default) {
        ObserveFlush(command);
        if (ReferenceEquals(command.Connection, OwnerConnection) && lifetimeReads == 2 &&
            command.CommandText.Contains("FOR KEY SHARE", StringComparison.Ordinal)) {
            FinalLifetimeExecuted = true;
        }
        if (Pause == AuthorityPause.ReceiptObservation && command.Transaction is null &&
            command.CommandText.Contains("AS \"ActiveNamespaceId\"", StringComparison.Ordinal)) {
            Paused.TrySetResult();
            await Release.Task.WaitAsync(cancellationToken);
        }
        return result;
    }

    internal bool Closed(DbConnection connection, Guid? context) {
        if (context != OwnerContext || OwnerContext is null || !ReferenceEquals(connection, OwnerConnection) || !GuardHeld) {
            return false;
        }
        Assert.Equal(System.Data.ConnectionState.Closed, connection.State);
        GuardHeld = false;
        Released.TrySetResult();
        return true;
    }

    private bool IsProjectGuard(DbCommand command) => projectKey.Length > 0 &&
        command.CommandText.Contains("pg_advisory_xact_lock(", StringComparison.Ordinal) &&
        command.Parameters.Cast<DbParameter>().Any(parameter => parameter.Value is string value && value == projectKey);

    private void ObserveFlush(DbCommand command) {
        if (!ReferenceEquals(command.Connection, OwnerConnection) || command.Transaction is null) {
            return;
        }
        RootFlushed |= command.CommandText.Contains("UPDATE \"Projects_Projects\"", StringComparison.Ordinal);
        SearchFlushed |= command.CommandText.Contains("UPDATE \"Infrastructure_SearchDocuments\"", StringComparison.Ordinal);
        ReceiptFlushed |= command.CommandText.Contains("INSERT INTO \"Projects_DefinitionReceipts\"", StringComparison.Ordinal);
    }

    private static TaskCompletionSource NewSource() => new(TaskCreationOptions.RunContinuationsAsynchronously);
    private static TaskCompletionSource<T> NewSource<T>() => new(TaskCreationOptions.RunContinuationsAsynchronously);
}

internal sealed class AuthorityCommitProbe(AuthorityProbe probe) : DbTransactionInterceptor {
    public override ValueTask<InterceptionResult> TransactionCommittingAsync(DbTransaction transaction, TransactionEventData eventData,
        InterceptionResult result, CancellationToken cancellationToken = default) {
        if (eventData.Context?.ContextId.InstanceId == probe.OwnerContext && probe.OwnerContext is not null) {
            Interlocked.Increment(ref probe.CommitAttempts);
        }
        return ValueTask.FromResult(result);
    }
    public override Task TransactionCommittedAsync(DbTransaction transaction, TransactionEndEventData eventData,
        CancellationToken cancellationToken = default) {
        if (eventData.Context?.ContextId.InstanceId == probe.OwnerContext && probe.OwnerContext is not null) {
            Interlocked.Increment(ref probe.Commits);
        }
        return Task.CompletedTask;
    }
}

internal sealed class AuthorityReleaseProbe(AuthorityProbe probe) : DbConnectionInterceptor {
    public override Task ConnectionClosedAsync(DbConnection connection, ConnectionEndEventData eventData) {
        Released(connection, eventData);
        return Task.CompletedTask;
    }

    public override void ConnectionClosed(DbConnection connection, ConnectionEndEventData eventData) => Released(connection, eventData);

    private void Released(DbConnection connection, ConnectionEndEventData eventData) {
        if (probe.Closed(connection, eventData.Context?.ContextId.InstanceId) && probe.FailDisposal &&
            probe.AuthorityUnavailable && probe.CommitAttempts == 0 && !probe.DisposalFailed) {
            probe.DisposalFailed = true;
            throw new IOException("Injected cleanup failure after actual precommit owner connection release.");
        }
    }
}

internal sealed class AuthorityLogProbe(AuthorityProbe probe) : ILoggerProvider {
    public ILogger CreateLogger(string categoryName) => new Logger(probe, categoryName);
    public void Dispose() {
    }

    private sealed class Logger(AuthorityProbe probe, string category) : ILogger {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => category == typeof(ProjectDefinitionWriter).FullName && logLevel == LogLevel.Warning;
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) {
            if (IsEnabled(logLevel) && state is IEnumerable<KeyValuePair<string, object?>> properties) {
                var failure = properties.FirstOrDefault(item => item.Key == "FailureType").Value as string;
                if (failure is not null) {
                    probe.CleanupFailureTypes.Enqueue(failure);
                }
            }
        }
    }
}

internal sealed class OwnedProjectGuard : IAsyncDisposable {
    private readonly string connectionString;
    private readonly NpgsqlConnection connection;
    private readonly NpgsqlTransaction transaction;
    private int disposed;
    private OwnedProjectGuard(string connectionString, NpgsqlConnection connection, NpgsqlTransaction transaction) {
        this.connectionString = connectionString;
        this.connection = connection;
        this.transaction = transaction;
    }

    internal static async Task<OwnedProjectGuard> AcquireAsync(string connectionString, Guid projectId) {
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var connection = new NpgsqlConnection(connectionString);
        try {
            await connection.OpenAsync(deadline.Token);
            var transaction = await connection.BeginTransactionAsync(deadline.Token);
            try {
                await using var command = new NpgsqlCommand("SELECT pg_advisory_xact_lock(hashtextextended(@key, 0))", connection, transaction);
                command.Parameters.AddWithValue("key", ProjectMutationScopeKeys.ForProject(projectId));
                await command.ExecuteNonQueryAsync(deadline.Token);
                return new(connectionString, connection, transaction);
            } catch {
                await transaction.DisposeAsync();
                throw;
            }
        } catch {
            await connection.DisposeAsync();
            throw;
        }
    }

    internal async Task RequireBlockedAsync(AuthorityProbe probe) {
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var pid = await probe.ProjectCommand.Task.WaitAsync(deadline.Token);
        await using var observer = new NpgsqlConnection(connectionString);
        await observer.OpenAsync(deadline.Token);
        await using var command = new NpgsqlCommand("""
            SELECT EXISTS (SELECT 1 FROM pg_stat_activity
                WHERE pid = @pid AND datname = current_database() AND wait_event_type = 'Lock' AND wait_event = 'advisory')
            """, observer);
        command.Parameters.AddWithValue("pid", pid);
        while (!(bool)(await command.ExecuteScalarAsync(deadline.Token))!) {
            await Task.Delay(TimeSpan.FromMilliseconds(10), deadline.Token);
        }
        Assert.False(probe.GuardHeld);
    }

    public async ValueTask DisposeAsync() {
        if (Interlocked.Exchange(ref disposed, 1) != 0) {
            return;
        }
        try {
            await transaction.DisposeAsync();
        } finally {
            await connection.DisposeAsync();
        }
    }
}

internal sealed class ObservedUsers(IApiUserStore inner, AuthorityProbe probe) : IApiUserStore {
    internal IApiUserStore Inner => inner;
    internal int FullReads;
    internal int SnapshotReads;
    internal int HeaderReads;
    internal int ReadsUnderGuard;
    internal int Preparations;
    internal bool PauseFinalRead { get; set; }
    internal TaskCompletionSource Pending { get; private set; } = NewSource();
    internal TaskCompletionSource Drained { get; private set; } = NewSource();
    internal TaskCompletionSource ReleasePending { get; private set; } = NewSource();
    internal bool PendingCancelled { get; private set; }

    internal void Reset() {
        FullReads = 0;
        SnapshotReads = 0;
        HeaderReads = 0;
        ReadsUnderGuard = 0;
        Preparations = 0;
        PauseFinalRead = false;
        PendingCancelled = false;
        Pending = NewSource();
        Drained = NewSource();
        ReleasePending = NewSource();
    }

    public Task<IReadOnlyList<ApiUserRecord>> ReadAsync(CancellationToken cancellationToken = default) {
        Interlocked.Increment(ref FullReads);
        CountGuardRead();
        return inner.ReadAsync(cancellationToken);
    }
    public Task<ApiUserStoreSnapshot> ReadSnapshotAsync(CancellationToken cancellationToken = default) {
        Interlocked.Increment(ref SnapshotReads);
        CountGuardRead();
        return inner.ReadSnapshotAsync(cancellationToken);
    }
    public Task EnsureAuthorityFormatAsync(CancellationToken cancellationToken = default) {
        Interlocked.Increment(ref Preparations);
        return inner.EnsureAuthorityFormatAsync(cancellationToken);
    }
    public async ValueTask<ApiUserAuthorityObservation> ObserveAuthorityAsync(ApiUserAuthorityGeneration expected, CancellationToken cancellationToken = default) {
        var count = Interlocked.Increment(ref HeaderReads);
        if (PauseFinalRead && count == 2) {
            Pending.TrySetResult();
            try {
                await ReleasePending.Task.WaitAsync(cancellationToken);
            } catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) {
                PendingCancelled = true;
                throw;
            } finally {
                Drained.TrySetResult();
            }
        }
        var observed = await inner.ObserveAuthorityAsync(expected, cancellationToken);
        probe.AuthorityUnavailable |= observed is ApiUserAuthorityObservation.Unavailable;
        return observed;
    }
    public Task SaveAsync(ApiUserRecord user, long? expectedVersion, CancellationToken cancellationToken = default)
        => inner.SaveAsync(user, expectedVersion, cancellationToken);
    public Task DeleteAsync(Guid id, long expectedVersion, CancellationToken cancellationToken = default)
        => inner.DeleteAsync(id, expectedVersion, cancellationToken);
    private void CountGuardRead() {
        if (probe.GuardHeld) {
            Interlocked.Increment(ref ReadsUnderGuard);
        }
    }
    private static TaskCompletionSource NewSource() => new(TaskCreationOptions.RunContinuationsAsynchronously);
}

internal sealed class ObservedTokens(IApiTokenRegistry inner) : IApiTokenRegistry {
    internal IApiTokenRegistry Inner => inner;
    internal int CurrentReads;
    internal int Preparations;
    internal void Reset() {
        CurrentReads = 0;
        Preparations = 0;
    }
    public void Register(ApiTokenRecord token) => inner.Register(token);
    public Task PrepareCurrentReadAsync(CancellationToken cancellationToken = default) {
        Interlocked.Increment(ref Preparations);
        return inner.PrepareCurrentReadAsync(cancellationToken);
    }
    public ValueTask<ApiTokenCurrentObservation> ObserveCurrentAsync(Guid id, CancellationToken cancellationToken = default) {
        Interlocked.Increment(ref CurrentReads);
        return inner.ObserveCurrentAsync(id, cancellationToken);
    }
    public Task<ApiTokenRecord?> FindAsync(Guid id, CancellationToken cancellationToken = default) => inner.FindAsync(id, cancellationToken);
    public Task<ApiTokenPage> SearchAsync(ApiTokenQuery query, CancellationToken cancellationToken = default) => inner.SearchAsync(query, cancellationToken);
    public Task RevokeAsync(Guid id, DateTimeOffset revokedAtUtc, CancellationToken cancellationToken = default) => inner.RevokeAsync(id, revokedAtUtc, cancellationToken);
    public Task DeleteAsync(Guid id, CancellationToken cancellationToken = default) => inner.DeleteAsync(id, cancellationToken);
}
