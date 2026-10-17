using System.Text;
using System.Text.Json;
using CanDoItAll.Infrastructure;
using CanDoItAll.Infrastructure.Configuration;
using CanDoItAll.Infrastructure.ControlPlane;
using CanDoItAll.Infrastructure.FileSystem;
using CanDoItAll.Tests.Support;
using Microsoft.Extensions.Options;

namespace CanDoItAll.Tests.Unit;

[Trait("Category", "HostPlatform")]
public sealed class ApiAuthorityFileSnapshotTests {
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    [Fact]
    public async Task V2_snapshots_and_token_observations_detach_collections_and_preserve_canonical_header() {
        await using var environment = CanDoItAllTestEnvironment.Create("authority-snapshot");
        var stores = CreateStores(environment);
        var scopes = new List<string> { "read", "write" };
        var user = User() with { AllowedScopes = scopes };
        await stores.Users.SaveAsync(user, null);
        await stores.Users.EnsureAuthorityFormatAsync();
        var snapshot = await stores.Users.ReadSnapshotAsync();
        scopes.Clear();
        Assert.Equal(new[] { "read", "write" }, Assert.Single(snapshot.Users).AllowedScopes);
        Assert.Throws<NotSupportedException>(() => ((IList<ApiUserRecord>)snapshot.Users).Clear());
        Assert.Throws<NotSupportedException>(() => ((IList<string>)snapshot.Users[0].AllowedScopes).Clear());
        Assert.Throws<ArgumentException>(() => new ApiUserAuthorityGeneration(Guid.Empty));
        using var json = JsonDocument.Parse(await File.ReadAllBytesAsync(stores.AccountPath));
        Assert.Equal(new[] { "schemaVersion", "authorityGeneration", "users" }, json.RootElement.EnumerateObject().Select(property => property.Name));
        Assert.Equal(2, json.RootElement.GetProperty("schemaVersion").GetInt32());
        Assert.Equal(snapshot.Generation.Value, json.RootElement.GetProperty("authorityGeneration").GetGuid());
        Assert.IsType<ApiUserAuthorityObservation.Current>(await stores.Users.ObserveAuthorityAsync(snapshot.Generation));
        var tokenScopes = new List<string> { "read" };
        var token = Token() with { Scopes = tokenScopes };
        var captured = new ApiTokenCurrentObservation.Found(token);
        tokenScopes.Clear();
        Assert.Equal(new[] { "read" }, captured.Record.Scopes);
        Assert.Throws<NotSupportedException>(() => ((IList<string>)captured.Record.Scopes).Clear());
        stores.Tokens.Register(token with { Scopes = ["read"] });
        await stores.Tokens.PrepareCurrentReadAsync();
        Assert.Equal(token.Id, Assert.IsType<ApiTokenCurrentObservation.Found>(await stores.Tokens.ObserveCurrentAsync(token.Id)).Record.Id);
    }

    [Fact]
    public async Task Every_authority_field_rotates_and_mutable_submission_is_captured_before_waiting() {
        await using var environment = CanDoItAllTestEnvironment.Create("authority-fields");
        var stores = CreateStores(environment);
        await stores.Users.SaveAsync(User(), null);
        await stores.Users.EnsureAuthorityFormatAsync();
        Func<ApiUserRecord, ApiUserRecord>[] changes = [
            user => user with { UserName = "OTHER", NormalizedUserName = "OTHER" },
            user => user with { UserName = "other" },
            user => user with { Enabled = !user.Enabled },
            user => user with { AuthenticationRevision = user.AuthenticationRevision + 1 },
            user => user with { PasswordHash = "different-fixture-hash" },
            user => user with { AllowedScopes = ["read", "write"] },
            user => user with { AllowedScopes = ["read", "WRITE"] },
            user => user with { AllowedScopes = ["read", "WRITE", "WRITE"] }
        ];
        foreach (var change in changes) {
            var before = await stores.Users.ReadSnapshotAsync();
            var current = Assert.Single(before.Users);
            await stores.Users.SaveAsync(change(current) with { Version = current.Version + 1 }, current.Version);
            Assert.NotEqual(before.Generation, (await stores.Users.ReadSnapshotAsync()).Generation);
        }
        var captured = await stores.Users.ReadSnapshotAsync();
        var original = Assert.Single(captured.Users);
        var mutableScopes = original.AllowedScopes.ToList();
        Task saving;
        await using (var held = await stores.Writer.AcquireCoordinationAsync(stores.AccountDirectory,
            stores.AccountPath + ".mutation.lock", TimeSpan.FromSeconds(15), requirePrivateUnixMode: true)) {
            saving = stores.Users.SaveAsync(original with { Version = original.Version + 1, AllowedScopes = mutableScopes }, original.Version);
            mutableScopes.Clear();
            mutableScopes.Add("uncaptured-authority");
            Assert.False(saving.IsCompleted);
        }
        await saving.WaitAsync(TimeSpan.FromSeconds(15));
        var after = await stores.Users.ReadSnapshotAsync();
        Assert.Equal(captured.Generation, after.Generation);
        Assert.Equal(original.AllowedScopes, Assert.Single(after.Users).AllowedScopes);
    }

    [Fact]
    public async Task Cosmetic_and_scope_order_changes_preserve_generation_without_weakening_account_CAS() {
        await using var environment = CanDoItAllTestEnvironment.Create("authority-cosmetic");
        var stores = CreateStores(environment);
        var user = User() with { AllowedScopes = ["read", "write"] };
        await stores.Users.SaveAsync(user, null);
        await stores.Users.EnsureAuthorityFormatAsync();
        var before = await stores.Users.ReadSnapshotAsync();
        var changed = user with { DisplayName = "New label", Version = 2, UpdatedAtUtc = user.UpdatedAtUtc.AddMinutes(1), AllowedScopes = ["write", "read"] };
        await stores.Users.SaveAsync(changed, 1);
        var after = await stores.Users.ReadSnapshotAsync();
        Assert.Equal(before.Generation, after.Generation);
        Assert.Equal("New label", Assert.Single(after.Users).DisplayName);
        await Assert.ThrowsAsync<ApiUserConflictException>(() => stores.Users.SaveAsync(changed with { Version = 3, CreatedAtUtc = user.CreatedAtUtc.AddSeconds(1) }, 2));
        Assert.Equal(2, Assert.Single((await stores.Users.ReadSnapshotAsync()).Users).Version);
    }

    [Fact]
    public async Task Authority_ABA_and_delete_recreate_never_reuse_an_earlier_generation() {
        await using var environment = CanDoItAllTestEnvironment.Create("authority-aba");
        var stores = CreateStores(environment);
        var user = User();
        await stores.Users.SaveAsync(user, null);
        await stores.Users.EnsureAuthorityFormatAsync();
        var generations = new HashSet<ApiUserAuthorityGeneration> { (await stores.Users.ReadSnapshotAsync()).Generation };
        await stores.Users.SaveAsync(user with { Enabled = false, Version = 2 }, 1);
        Assert.True(generations.Add((await stores.Users.ReadSnapshotAsync()).Generation));
        await stores.Users.SaveAsync(user with { Version = 3 }, 2);
        Assert.True(generations.Add((await stores.Users.ReadSnapshotAsync()).Generation));
        await stores.Users.DeleteAsync(user.Id, 3);
        Assert.True(generations.Add((await stores.Users.ReadSnapshotAsync()).Generation));
        await stores.Users.SaveAsync(user, null);
        Assert.True(generations.Add((await stores.Users.ReadSnapshotAsync()).Generation));
        await stores.Users.SaveAsync(User("second"), null);
        Assert.True(generations.Add((await stores.Users.ReadSnapshotAsync()).Generation));
    }

    [Fact]
    public async Task Stale_or_invalid_account_writes_preserve_exact_bytes_and_generation() {
        await using var environment = CanDoItAllTestEnvironment.Create("authority-cas");
        var stores = CreateStores(environment);
        var user = User();
        await stores.Users.SaveAsync(user, null);
        await stores.Users.EnsureAuthorityFormatAsync();
        var before = await stores.Users.ReadSnapshotAsync();
        var bytes = await File.ReadAllBytesAsync(stores.AccountPath);
        await Assert.ThrowsAsync<ApiUserConflictException>(() => stores.Users.SaveAsync(user with { Version = 3 }, 2));
        await Assert.ThrowsAsync<ApiUserConflictException>(() => stores.Users.DeleteAsync(user.Id, 2));
        await Assert.ThrowsAsync<ApiUserConflictException>(() => stores.Users.SaveAsync(User(user.UserName), null));
        await Assert.ThrowsAsync<InvalidDataException>(() => stores.Users.SaveAsync(user with { AllowedScopes = [" "], Version = 2 }, 1));
        Assert.Equal(bytes, await File.ReadAllBytesAsync(stores.AccountPath));
        Assert.Equal(before.Generation, (await stores.Users.ReadSnapshotAsync()).Generation);
    }

    [Fact]
    public async Task Another_accounts_authority_change_is_a_generation_conflict_not_a_subject_denial() {
        await using var environment = CanDoItAllTestEnvironment.Create("authority-independent");
        var stores = CreateStores(environment);
        var first = User();
        var second = User("second");
        await stores.Users.SaveAsync(first, null);
        await stores.Users.SaveAsync(second, null);
        await stores.Users.EnsureAuthorityFormatAsync();
        var before = await stores.Users.ReadSnapshotAsync();
        await stores.Users.SaveAsync(second with { Enabled = false, Version = 2 }, 1);
        Assert.IsType<ApiUserAuthorityObservation.Changed>(await stores.Users.ObserveAuthorityAsync(before.Generation));
        var unchanged = Assert.Single((await stores.Users.ReadSnapshotAsync()).Users, user => user.Id == first.Id);
        Assert.True(unchanged.Enabled);
        Assert.Equal(first.Version, unchanged.Version);
        Assert.Equal(first.AuthenticationRevision, unchanged.AuthenticationRevision);
    }

    [Fact]
    public async Task Legacy_upgrade_preserves_users_and_restart_keeps_the_same_published_generation() {
        var user = User();
        byte[][] legacyForms = [
            Legacy([user]),
            JsonSerializer.SerializeToUtf8Bytes(new { UsErS = new[] { user }, ScHeMaVeRsIoN = "1" }, JsonOptions)
        ];
        foreach (var legacy in legacyForms) {
            await using var environment = CanDoItAllTestEnvironment.Create("authority-upgrade");
            var stores = CreateStores(environment);
            await stores.WriteAccountsAsync(legacy);
            Assert.Equal(user.Id, Assert.Single(await stores.Users.ReadAsync()).Id);
            await stores.Users.EnsureAuthorityFormatAsync();
            var before = await stores.Users.ReadSnapshotAsync();
            var bytes = await File.ReadAllBytesAsync(stores.AccountPath);
            var reopened = CreateStores(environment);
            Assert.Equal(ApiAuthorityReadFailure.NotReady, Assert.IsType<ApiUserAuthorityObservation.Unavailable>(await reopened.Users.ObserveAuthorityAsync(before.Generation)).Failure);
            await reopened.Users.EnsureAuthorityFormatAsync();
            var after = await reopened.Users.ReadSnapshotAsync();
            Assert.Equal(before.Generation, after.Generation);
            Assert.Equal(bytes, await File.ReadAllBytesAsync(stores.AccountPath));
            Assert.Equal(user with { AllowedScopes = Assert.Single(after.Users).AllowedScopes }, Assert.Single(after.Users));
        }
    }

    [Fact]
    public async Task Only_explicit_readiness_provisions_an_absent_authority_store() {
        await using var environment = CanDoItAllTestEnvironment.Create("authority-readiness");
        var stores = CreateStores(environment);
        var generation = new ApiUserAuthorityGeneration(Guid.NewGuid());
        Assert.Equal(ApiAuthorityReadFailure.NotReady, Assert.IsType<ApiUserAuthorityObservation.Unavailable>(await stores.Users.ObserveAuthorityAsync(generation)).Failure);
        Assert.Equal(ApiAuthorityReadFailure.NotReady, (await Assert.ThrowsAsync<ApiAuthorityReadException>(() => stores.Users.ReadSnapshotAsync())).Failure);
        Assert.Equal(ApiAuthorityReadFailure.NotReady, Assert.IsType<ApiTokenCurrentObservation.Unavailable>(await stores.Tokens.ObserveCurrentAsync(Guid.NewGuid())).Failure);
        Assert.False(Directory.Exists(stores.AccountDirectory));
        Assert.False(Directory.Exists(stores.TokenDirectory));
        Assert.Equal(0, stores.Paths.RootCalls);
        Assert.Equal(0, stores.Policies.Calls);
        await stores.Users.EnsureAuthorityFormatAsync();
        await stores.Tokens.PrepareCurrentReadAsync();
        Assert.Empty((await stores.Users.ReadSnapshotAsync()).Users);
        Assert.IsType<ApiTokenCurrentObservation.Absent>(await stores.Tokens.ObserveCurrentAsync(Guid.NewGuid()));
        Assert.Equal(new[] { "accounts.json", "accounts.json.candoitall.lock", "accounts.json.mutation.lock" }, Directory.EnumerateFiles(stores.AccountDirectory).Select(Path.GetFileName).Order(StringComparer.Ordinal));
        Assert.Empty(Directory.EnumerateFiles(stores.TokenDirectory));
        if (!OperatingSystem.IsWindows()) {
            Assert.Equal(UnixFileMode.UserRead | UnixFileMode.UserWrite, File.GetUnixFileMode(stores.AccountPath));
        }
    }

    [Fact]
    public async Task Unsupported_or_malformed_readiness_never_overwrites_or_defaults_to_empty() {
        await using var environment = CanDoItAllTestEnvironment.Create("authority-invalid-ready");
        var stores = CreateStores(environment);
        var generation = Guid.NewGuid();
        string[] invalid = [
            "{}", "null", "{", "{\"schemaVersion\":99,\"users\":[]}",
            "{\"schemaVersion\":1,\"users\":null}",
            $"{{\"schemaVersion\":2,\"authorityGeneration\":\"{Guid.Empty:D}\",\"users\":[]}}",
            $"{{\"authorityGeneration\":\"{generation:D}\",\"schemaVersion\":2,\"users\":[]}}",
            $"{{\"schemaVersion\":2,\"authorityGeneration\":\"{generation:D}\",\"authorityGeneration\":\"{generation:D}\",\"users\":[]}}",
            $"{{\"schemaVersion\":2,\"authorityGeneration\":\"{generation:D}\",\"users\":[],\"users\":[]}}"
        ];
        foreach (var json in invalid) {
            await stores.WriteAccountsAsync(Encoding.UTF8.GetBytes(json));
            var bytes = await File.ReadAllBytesAsync(stores.AccountPath);
            await Assert.ThrowsAsync<InvalidDataException>(() => stores.Users.EnsureAuthorityFormatAsync());
            Assert.Equal(bytes, await File.ReadAllBytesAsync(stores.AccountPath));
            Assert.Equal(ApiAuthorityReadFailure.NotReady, (await Assert.ThrowsAsync<ApiAuthorityReadException>(() => stores.Users.ReadSnapshotAsync())).Failure);
        }
    }

    [Fact]
    public async Task Readiness_failure_before_publication_preserves_legacy_bytes_and_does_not_enable_reads() {
        await using var environment = CanDoItAllTestEnvironment.Create("authority-before-publication");
        var armed = false;
        var stores = CreateStores(environment, stage => {
            if (armed && stage == DurableFileWriteStage.BeforeCommit) {
                throw new IOException("Injected publication failure.");
            }
        });
        var bytes = Legacy([User()]);
        await stores.WriteAccountsAsync(bytes);
        armed = true;
        await Assert.ThrowsAsync<IOException>(() => stores.Users.EnsureAuthorityFormatAsync());
        Assert.Equal(bytes, await File.ReadAllBytesAsync(stores.AccountPath));
        Assert.Equal(ApiAuthorityReadFailure.NotReady, (await Assert.ThrowsAsync<ApiAuthorityReadException>(() => stores.Users.ReadSnapshotAsync())).Failure);
        Assert.Single(await stores.Users.ReadAsync());
    }

    [Fact]
    public async Task Lost_readiness_acknowledgement_is_recovered_by_validating_the_same_generation_without_republishing() {
        await using var environment = CanDoItAllTestEnvironment.Create("authority-lost-ack");
        var armed = false;
        var commits = 0;
        var stores = CreateStores(environment, stage => {
            if (stage == DurableFileWriteStage.Committed) {
                commits++;
                if (armed) {
                    throw new IOException("Injected acknowledgement failure.");
                }
            }
        });
        await stores.WriteAccountsAsync(Legacy([User()]));
        commits = 0;
        armed = true;
        await Assert.ThrowsAsync<IOException>(() => stores.Users.EnsureAuthorityFormatAsync());
        var bytes = await File.ReadAllBytesAsync(stores.AccountPath);
        using var document = JsonDocument.Parse(bytes);
        var generation = document.RootElement.GetProperty("authorityGeneration").GetGuid();
        Assert.NotEqual(Guid.Empty, generation);
        Assert.Equal(ApiAuthorityReadFailure.NotReady, (await Assert.ThrowsAsync<ApiAuthorityReadException>(() => stores.Users.ReadSnapshotAsync())).Failure);
        armed = false;
        await stores.Users.EnsureAuthorityFormatAsync();
        Assert.Equal(generation, (await stores.Users.ReadSnapshotAsync()).Generation.Value);
        Assert.Equal(bytes, await File.ReadAllBytesAsync(stores.AccountPath));
        Assert.Equal(1, commits);
    }

    [Fact]
    public async Task Current_changed_missing_invalid_unsafe_and_io_results_remain_distinct_from_absent_token() {
        await using var environment = CanDoItAllTestEnvironment.Create("authority-results");
        var stores = CreateStores(environment);
        await stores.Users.EnsureAuthorityFormatAsync();
        await stores.Tokens.PrepareCurrentReadAsync();
        var snapshot = await stores.Users.ReadSnapshotAsync();
        Assert.IsType<ApiUserAuthorityObservation.Current>(await stores.Users.ObserveAuthorityAsync(snapshot.Generation));
        Assert.IsType<ApiUserAuthorityObservation.Changed>(await stores.Users.ObserveAuthorityAsync(new(Guid.NewGuid())));
        var bytes = await File.ReadAllBytesAsync(stores.AccountPath);
        File.Move(stores.AccountPath, stores.AccountPath + ".held");
        Assert.Equal(ApiAuthorityReadFailure.MissingPath, Assert.IsType<ApiUserAuthorityObservation.Unavailable>(await stores.Users.ObserveAuthorityAsync(snapshot.Generation)).Failure);
        Assert.Equal(ApiAuthorityReadFailure.MissingPath, (await Assert.ThrowsAsync<ApiAuthorityReadException>(() => stores.Users.ReadSnapshotAsync())).Failure);
        File.Move(stores.AccountPath + ".held", stores.AccountPath);
        await using (var held = new FileStream(stores.AccountPath, FileMode.Open, FileAccess.ReadWrite, FileShare.None)) {
            Assert.Equal(ApiAuthorityReadFailure.IoFailure, Assert.IsType<ApiUserAuthorityObservation.Unavailable>(await stores.Users.ObserveAuthorityAsync(snapshot.Generation)).Failure);
        }
        string[] invalid = [
            "{\"schemaVersion\":1,\"users\":[]}",
            "{\"schemaVersion\":2,\"authorityGeneration\":\"invalid\",\"users\":[]}",
            $"{{\"schemaVersion\":2,\"authorityGeneration\":\"{snapshot.Generation.Value:D}\",\"authorityGeneration\":\"{Guid.NewGuid():D}\",\"users\":[]}}",
            $"{{\"schemaVersion\":2,\"users\":[],\"authorityGeneration\":\"{snapshot.Generation.Value:D}\"}}"
        ];
        foreach (var json in invalid) {
            await stores.WriteAccountsAsync(Encoding.UTF8.GetBytes(json));
            Assert.Equal(ApiAuthorityReadFailure.InvalidFormat, Assert.IsType<ApiUserAuthorityObservation.Unavailable>(await stores.Users.ObserveAuthorityAsync(snapshot.Generation)).Failure);
        }
        await stores.WriteAccountsAsync(bytes);
        if (!OperatingSystem.IsWindows()) {
            File.SetUnixFileMode(stores.AccountPath, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.GroupRead);
            Assert.Equal(ApiAuthorityReadFailure.UnsafePath, Assert.IsType<ApiUserAuthorityObservation.Unavailable>(await stores.Users.ObserveAuthorityAsync(snapshot.Generation)).Failure);
            File.SetUnixFileMode(stores.AccountPath, UnixFileMode.UserRead | UnixFileMode.UserWrite);
        }
        var token = Token();
        stores.Tokens.Register(token);
        Assert.IsType<ApiTokenCurrentObservation.Found>(await stores.Tokens.ObserveCurrentAsync(token.Id));
        await stores.Tokens.DeleteAsync(token.Id);
        Assert.IsType<ApiTokenCurrentObservation.Absent>(await stores.Tokens.ObserveCurrentAsync(token.Id));
        Directory.Move(stores.TokenDirectory, Path.Combine(environment.RootPath, "removed-tokens"));
        Assert.Equal(ApiAuthorityReadFailure.MissingPath, Assert.IsType<ApiTokenCurrentObservation.Unavailable>(await stores.Tokens.ObserveCurrentAsync(token.Id)).Failure);
        Assert.False(Directory.Exists(stores.TokenDirectory));
    }

    [Fact]
    public async Task Prepared_observations_never_resolve_probe_write_harden_or_recreate_directories() {
        await using var environment = CanDoItAllTestEnvironment.Create("authority-read-only");
        var stores = CreateStores(environment);
        await stores.Users.SaveAsync(User(), null);
        await stores.Users.EnsureAuthorityFormatAsync();
        var token = Token();
        stores.Tokens.Register(token);
        await stores.Tokens.PrepareCurrentReadAsync();
        var generation = (await stores.Users.ReadSnapshotAsync()).Generation;
        Assert.True(stores.Policies.Calls > 0);
        Assert.True(stores.Paths.RootCalls > 0);
        var calls = (stores.Paths.RootCalls, stores.Policies.Calls);
        var files = Directory.EnumerateFiles(environment.ControlPlaneRootPath, "*", SearchOption.AllDirectories)
            .ToDictionary(path => path, path => (Bytes: File.ReadAllBytes(path), Written: File.GetLastWriteTimeUtc(path)), StringComparer.Ordinal);
        var directories = Directory.EnumerateDirectories(environment.ControlPlaneRootPath).ToArray();
        for (var index = 0; index < 3; index++) {
            Assert.IsType<ApiUserAuthorityObservation.Current>(await stores.Users.ObserveAuthorityAsync(generation));
            Assert.Single((await stores.Users.ReadSnapshotAsync()).Users);
            Assert.IsType<ApiTokenCurrentObservation.Found>(await stores.Tokens.ObserveCurrentAsync(token.Id));
            Assert.IsType<ApiTokenCurrentObservation.Absent>(await stores.Tokens.ObserveCurrentAsync(Guid.NewGuid()));
        }
        Assert.Equal(calls, (stores.Paths.RootCalls, stores.Policies.Calls));
        Assert.Equal(files.Keys.Order(StringComparer.Ordinal), Directory.EnumerateFiles(environment.ControlPlaneRootPath, "*", SearchOption.AllDirectories).Order(StringComparer.Ordinal));
        Assert.Equal(directories.Order(StringComparer.Ordinal), Directory.EnumerateDirectories(environment.ControlPlaneRootPath).Order(StringComparer.Ordinal));
        foreach (var (path, before) in files) {
            Assert.Equal(before.Bytes, await File.ReadAllBytesAsync(path));
            Assert.Equal(before.Written, File.GetLastWriteTimeUtc(path));
        }
        Directory.Move(environment.ControlPlaneRootPath, Path.Combine(environment.RootPath, "removed-control-plane"));
        Assert.Equal(ApiAuthorityReadFailure.MissingPath, Assert.IsType<ApiUserAuthorityObservation.Unavailable>(await stores.Users.ObserveAuthorityAsync(generation)).Failure);
        Assert.Equal(ApiAuthorityReadFailure.MissingPath, Assert.IsType<ApiTokenCurrentObservation.Unavailable>(await stores.Tokens.ObserveCurrentAsync(token.Id)).Failure);
        Assert.False(Directory.Exists(environment.ControlPlaneRootPath));
        Assert.Equal(calls, (stores.Paths.RootCalls, stores.Policies.Calls));
    }

    [Fact]
    public async Task Token_bytes_are_bounded_including_growth_and_invalid_content_has_only_sanitized_failures() {
        await using var environment = CanDoItAllTestEnvironment.Create("authority-token-limits");
        var stores = CreateStores(environment);
        var token = Token();
        stores.Tokens.Register(token);
        await stores.Tokens.PrepareCurrentReadAsync();
        var path = stores.TokenPath(token.Id);
        var bytes = await File.ReadAllBytesAsync(path);
        var exact = Enumerable.Repeat((byte)' ', ApiAuthorityFileRead.MaximumTokenBytes).ToArray();
        bytes.CopyTo(exact, 0);
        await stores.Writer.WriteBytesAsync(stores.TokenDirectory, path, exact, DurableFileWriteOptions.Private);
        Assert.IsType<ApiTokenCurrentObservation.Found>(await stores.Tokens.ObserveCurrentAsync(token.Id));
        await using (var stream = new FileStream(path, FileMode.Open, FileAccess.Write, FileShare.None)) {
            stream.SetLength(ApiAuthorityFileRead.MaximumTokenBytes + 1);
        }
        Assert.Equal(ApiAuthorityReadFailure.SizeLimit, Assert.IsType<ApiTokenCurrentObservation.Unavailable>(await stores.Tokens.ObserveCurrentAsync(token.Id)).Failure);
        await using (var growing = new GrowingStream(new byte[ApiAuthorityFileRead.MaximumTokenBytes + 1])) {
            Assert.Equal(ApiAuthorityReadFailure.SizeLimit, (await Assert.ThrowsAsync<ApiAuthorityReadException>(() =>
                ApiAuthorityFileRead.ReadBoundedAsync(growing, ApiAuthorityFileRead.MaximumTokenBytes, CancellationToken.None).AsTask())).Failure);
        }
        await stores.Writer.WriteTextAsync(stores.TokenDirectory, path, "{private-marker-not-a-record", DurableFileWriteOptions.Private);
        var malformed = Assert.IsType<ApiTokenCurrentObservation.Unavailable>(await stores.Tokens.ObserveCurrentAsync(token.Id));
        Assert.Equal(ApiAuthorityReadFailure.InvalidFormat, malformed.Failure);
        Assert.DoesNotContain("private-marker", malformed.ToString(), StringComparison.Ordinal);
        var tooLarge = Token() with { DisplayName = new string('x', ApiAuthorityFileRead.MaximumTokenBytes) };
        Assert.Equal(ApiAuthorityReadFailure.SizeLimit, Assert.Throws<ApiAuthorityReadException>(() => stores.Tokens.Register(tooLarge)).Failure);
        Assert.False(File.Exists(stores.TokenPath(tooLarge.Id)));
    }

    [Fact]
    public async Task Full_account_reads_bound_bytes_and_count_and_validate_every_record_before_publication() {
        await using var environment = CanDoItAllTestEnvironment.Create("authority-account-limits");
        var stores = CreateStores(environment);
        await stores.Users.EnsureAuthorityFormatAsync();
        var generation = (await stores.Users.ReadSnapshotAsync()).Generation;
        var invalid = User("unrelated") with { NormalizedUserName = "not-normalized" };
        var duplicateId = User("first");
        ApiUserRecord[][] invalidSets = [
            [User(), invalid],
            [duplicateId, duplicateId with { UserName = "second", NormalizedUserName = "SECOND" }],
            [User("duplicate"), User("duplicate")]
        ];
        foreach (var users in invalidSets) {
            await stores.WriteAccountsAsync(Canonical(generation, users));
            Assert.Equal(ApiAuthorityReadFailure.InvalidFormat, (await Assert.ThrowsAsync<ApiAuthorityReadException>(() => stores.Users.ReadSnapshotAsync())).Failure);
        }
        await stores.WriteAccountsAsync(Encoding.UTF8.GetBytes(Prefix(generation) + string.Join(',', Enumerable.Repeat("null", 10001)) + "]}"));
        Assert.Equal(ApiAuthorityReadFailure.SizeLimit, (await Assert.ThrowsAsync<ApiAuthorityReadException>(() => stores.Users.ReadSnapshotAsync())).Failure);
        await using (var stream = new FileStream(stores.AccountPath, FileMode.Open, FileAccess.Write, FileShare.None)) {
            stream.SetLength(ApiAuthorityFileRead.MaximumAccountBytes + 1L);
        }
        Assert.Equal(ApiAuthorityReadFailure.SizeLimit, (await Assert.ThrowsAsync<ApiAuthorityReadException>(() => stores.Users.ReadSnapshotAsync())).Failure);
        var usersAtLimit = Enumerable.Range(0, 10000).Select(index => User($"{index:D5}" + new string('u', 59)) with {
            DisplayName = new string('d', 128), PasswordHash = new string('h', 512), AllowedScopes = ["read", "write"]
        }).ToArray();
        await stores.WriteAccountsAsync(Legacy(usersAtLimit));
        await stores.Users.EnsureAuthorityFormatAsync();
        Assert.Equal(10000, (await stores.Users.ReadSnapshotAsync()).Users.Count);
        var bytesBefore = await File.ReadAllBytesAsync(stores.AccountPath);
        var tooLarge = usersAtLimit[0] with { Version = 2, DisplayName = new string('x', ApiAuthorityFileRead.MaximumAccountBytes) };
        Assert.Equal(ApiAuthorityReadFailure.SizeLimit, (await Assert.ThrowsAsync<ApiAuthorityReadException>(() => stores.Users.SaveAsync(tooLarge, 1))).Failure);
        Assert.Equal(bytesBefore, await File.ReadAllBytesAsync(stores.AccountPath));
    }

    [Fact]
    public async Task Prefix_observation_is_bounded_assumes_a_canonical_body_and_propagates_cancellation_without_held_handles() {
        await using var environment = CanDoItAllTestEnvironment.Create("authority-prefix");
        var stores = CreateStores(environment);
        await stores.Users.EnsureAuthorityFormatAsync();
        await stores.Tokens.PrepareCurrentReadAsync();
        var generation = (await stores.Users.ReadSnapshotAsync()).Generation;
        await stores.WriteAccountsAsync(Encoding.UTF8.GetBytes(Prefix(generation) + "malformed-private-tail"));
        Assert.IsType<ApiUserAuthorityObservation.Current>(await stores.Users.ObserveAuthorityAsync(generation));
        Assert.Equal(ApiAuthorityReadFailure.InvalidFormat, (await Assert.ThrowsAsync<ApiAuthorityReadException>(() => stores.Users.ReadSnapshotAsync())).Failure);
        await using (var stream = new FileStream(stores.AccountPath, FileMode.Open, FileAccess.Write, FileShare.None)) {
            stream.SetLength(ApiAuthorityFileRead.MaximumAccountBytes + 1L);
        }
        Assert.IsType<ApiUserAuthorityObservation.Current>(await stores.Users.ObserveAuthorityAsync(generation));
        Assert.Equal(ApiAuthorityReadFailure.SizeLimit, (await Assert.ThrowsAsync<ApiAuthorityReadException>(() => stores.Users.ReadSnapshotAsync())).Failure);
        await stores.WriteAccountsAsync(Encoding.UTF8.GetBytes(new string(' ', ApiAuthorityFileRead.MaximumHeaderBytes) + Prefix(generation) + "]}"));
        Assert.Equal(ApiAuthorityReadFailure.SizeLimit, Assert.IsType<ApiUserAuthorityObservation.Unavailable>(await stores.Users.ObserveAuthorityAsync(generation)).Failure);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => stores.Users.ObserveAuthorityAsync(generation, cancellation.Token).AsTask());
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => stores.Users.ReadSnapshotAsync(cancellation.Token));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => stores.Tokens.ObserveCurrentAsync(Guid.NewGuid(), cancellation.Token).AsTask());
        await using var exclusive = new FileStream(stores.AccountPath, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
        Assert.True(exclusive.CanWrite);
    }

    private static ApiUserRecord User(string name = "example") {
        var created = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
        return new(Guid.NewGuid(), name, name.ToUpperInvariant(), "Fixture account", true, "fixture-hash", ["read"], 1, 1, created, created);
    }

    private static ApiTokenRecord Token() {
        var issued = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
        return new(Guid.NewGuid(), "fixture-subject", "Fixture token", issued, issued.AddHours(1), ["read"]);
    }

    private static byte[] Legacy(IReadOnlyList<ApiUserRecord> users) => JsonSerializer.SerializeToUtf8Bytes(new { schemaVersion = 1, users }, JsonOptions);
    private static byte[] Canonical(ApiUserAuthorityGeneration generation, IReadOnlyList<ApiUserRecord> users) =>
        JsonSerializer.SerializeToUtf8Bytes(new { schemaVersion = 2, authorityGeneration = generation.Value, users }, JsonOptions);
    private static string Prefix(ApiUserAuthorityGeneration generation) => $"{{\"schemaVersion\":2,\"authorityGeneration\":\"{generation.Value:D}\",\"users\":[";

    private static TestStores CreateStores(CanDoItAllTestEnvironment environment, Action<DurableFileWriteStage>? observer = null) {
        var policies = new RecordingPolicies();
        var writer = new DurableFileWriter(policies, observer);
        var paths = new RecordingPaths(new ControlPlanePathResolver(Options.Create(new ControlPlaneOptions { RootPath = environment.ControlPlaneRootPath }),
            environment.CreateHostEnvironment(nameof(ApiAuthorityFileSnapshotTests)), writer));
        return new(environment.ControlPlaneRootPath, writer, paths, policies, new(paths, writer, policies), new(paths, writer, policies));
    }

    private sealed record TestStores(string Root, DurableFileWriter Writer, RecordingPaths Paths, RecordingPolicies Policies,
        FileApiUserStore Users, FileApiTokenRegistry Tokens) {
        public string AccountDirectory => Path.Combine(Root, "api-users");
        public string AccountPath => Path.Combine(AccountDirectory, "accounts.json");
        public string TokenDirectory => Path.Combine(Root, "api-tokens");
        public string TokenPath(Guid id) => Path.Combine(TokenDirectory, $"{id:N}.json");
        public Task WriteAccountsAsync(byte[] bytes) => Writer.WriteBytesAsync(Root, AccountPath, bytes, DurableFileWriteOptions.Private);
    }

    private sealed class GrowingStream(byte[] bytes) : MemoryStream(bytes, writable: false) {
        public override long Length => 1;
    }

    private sealed class RecordingPolicies : IPhysicalFileSystemPathPolicyFactory {
        public int Calls { get; private set; }
        public IPhysicalFileSystemPathPolicy Create(string managedRoot) {
            Calls++;
            return new PhysicalFileSystemPathPolicyFactory().Create(managedRoot);
        }
    }

    private sealed class RecordingPaths(IControlPlanePathResolver inner) : IControlPlanePathResolver {
        public int RootCalls { get; private set; }
        public string ResolveRootPath() {
            RootCalls++;
            return inner.ResolveRootPath();
        }
        public string ResolveDatabaseProfilesRootPath() => inner.ResolveDatabaseProfilesRootPath();
        public string ResolveCatalogFilePath() => inner.ResolveCatalogFilePath();
        public string ResolveActiveProfileStateFilePath() => inner.ResolveActiveProfileStateFilePath();
        public string ResolveFileApplicationPreferencesFilePath() => inner.ResolveFileApplicationPreferencesFilePath();
        public string ResolveDataProtectionKeysPath() => inner.ResolveDataProtectionKeysPath();
        public string ResolveStateRootPath() => inner.ResolveStateRootPath();
        public string ResolveLogsRootPath() => inner.ResolveLogsRootPath();
        public string ResolveRuntimeTemporaryRootPath() => inner.ResolveRuntimeTemporaryRootPath();
    }
}
