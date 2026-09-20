using System.Buffers.Binary;
using System.Security.Cryptography;
using CanDoItAll.Infrastructure;
using CanDoItAll.Infrastructure.Configuration;
using CanDoItAll.Infrastructure.ControlPlane;
using CanDoItAll.Infrastructure.FileSystem;
using CanDoItAll.Modules.Workspace.ApiAccess;
using CanDoItAll.Tests.Support;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;

namespace CanDoItAll.Tests.Unit;

public sealed class ApiCredentialRulesTests {
    [Fact]
    public void Password_hashes_are_salted_verified_and_bounded_before_expensive_work() {
        var password = Convert.ToBase64String(RandomNumberGenerator.GetBytes(24));
        var service = new ApiPasswordService();
        var hash = service.Hash(password);
        Assert.NotEqual(hash, service.Hash(password));
        Assert.True(ApiPasswordService.IsSupportedHash(hash, requireCurrentWorkFactor: true));
        Assert.Equal(PasswordVerificationResult.Success, service.Verify(hash, password));
        Assert.Equal(PasswordVerificationResult.Failed, service.Verify(hash, password + "!"));
        var excessive = Convert.FromBase64String(hash);
        BinaryPrimitives.WriteUInt32BigEndian(excessive.AsSpan(5, 4), uint.MaxValue);
        Assert.False(ApiPasswordService.IsSupportedHash(Convert.ToBase64String(excessive)));
        foreach (var invalid in new[] { "", "invalid", Convert.ToBase64String(new byte[61]), new string('A', 513) }) {
            Assert.Throws<InvalidDataException>(() => service.Verify(invalid, password));
        }
        var oldHasher = new PasswordHasher<object>(Options.Create(new PasswordHasherOptions { IterationCount = 100000 }));
        var older = oldHasher.HashPassword(new(), password);
        Assert.Equal(PasswordVerificationResult.SuccessRehashNeeded, service.Verify(older, password));
        Assert.False(ApiPasswordService.IsSupportedHash(older, requireCurrentWorkFactor: true));
    }

    [Fact]
    public void Usernames_have_one_ASCII_identity_and_passwords_are_not_trimmed() {
        Assert.Equal("A.B_2-@EXAMPLE", ApiIdentityRules.NormalizeUserName("a.B_2-@example"));
        foreach (var invalid in new string?[] { null, "", " user", "user ", "u\nser", "üser", new('x', 65) }) {
            Assert.Throws<ArgumentException>(() => ApiIdentityRules.UserName(invalid));
        }
        var password = " " + Convert.ToBase64String(RandomNumberGenerator.GetBytes(24)) + " ";
        var service = new ApiPasswordService();
        var hash = service.Hash(password);
        Assert.Equal(PasswordVerificationResult.Success, service.Verify(hash, password));
        Assert.Equal(PasswordVerificationResult.Failed, service.Verify(hash, password.Trim()));
        Assert.Throws<ArgumentException>(() => service.Hash(new string('x', 257)));
    }

    [Fact]
    public void User_grants_cannot_include_machine_or_administrative_authority() {
        Assert.Empty(ApiScopeCatalog.ValidateGrants([], forUser: true));
        Assert.Equal([ApiAccessScopeNames.ReadWorkflows], ApiScopeCatalog.ValidateGrants(
            [ApiAccessScopeNames.ReadWorkflows.ToUpperInvariant(), ApiAccessScopeNames.ReadWorkflows], forUser: true));
        foreach (var value in new[] { ApiAccessScopeNames.Api, ApiAccessScopeNames.IssueTokens, ApiAccessScopeNames.ManageAccess,
            ApiAccessScopeNames.Session, "api.unknown", "" }) {
            Assert.Throws<InvalidOperationException>(() => ApiScopeCatalog.ValidateGrants([value], forUser: true));
        }
        Assert.Throws<InvalidOperationException>(() => ApiScopeCatalog.ValidateGrants([ApiAccessScopeNames.ManageAccess], forUser: false));
    }

    [Fact]
    public void Configuration_rejects_insecure_combinations_and_preserves_machine_only_lifetimes() {
        var hash = new ApiPasswordService().Hash(Convert.ToBase64String(RandomNumberGenerator.GetBytes(24)));
        foreach (var apiEnabled in new[] { false, true }) {
            foreach (var jwtEnabled in new[] { false, true }) {
                foreach (var loginEnabled in new[] { false, true }) {
                    foreach (var managementEnabled in new[] { false, true }) {
                        var options = SecureOptions(hash);
                        options.Enabled = apiEnabled;
                        options.Authorization.Enabled = jwtEnabled;
                        options.UserAuthentication.Enabled = loginEnabled;
                        options.AccessManagement.Enabled = managementEnabled;
                        var valid = (!loginEnabled || apiEnabled && jwtEnabled) && (!managementEnabled || loginEnabled);
                        Assert.Equal(valid, ApiAccessOptions.Validate(options).Count == 0);
                    }
                }
            }
        }
        var machine = SecureOptions(hash);
        machine.UserAuthentication.Enabled = false;
        machine.Authorization.DefaultTokenLifetimeMinutes = 5;
        machine.Authorization.MaxTokenLifetimeMinutes = 10;
        Assert.Empty(ApiAccessOptions.Validate(machine));
        var missingHash = SecureOptions("");
        Assert.NotEmpty(ApiAccessOptions.Validate(missingHash));
        var invalidLifetime = SecureOptions(hash);
        invalidLifetime.UserAuthentication.TokenLifetimeMinutes = 1441;
        Assert.NotEmpty(ApiAccessOptions.Validate(invalidLifetime));
        invalidLifetime.UserAuthentication.TokenLifetimeMinutes = 0;
        Assert.NotEmpty(ApiAccessOptions.Validate(invalidLifetime));
        Assert.Equal(TimeSpan.FromSeconds(15), new ApiServerSentEventsOptions { HeartbeatIntervalSeconds = 3600 }.HeartbeatInterval);
    }

    [Fact]
    public async Task Concurrent_account_creation_and_updates_preserve_uniqueness_and_revisions_across_reopen() {
        await using var environment = CanDoItAllTestEnvironment.Create("api-account-concurrency");
        var store = CreateStore(environment);
        var now = DateTimeOffset.UtcNow;
        var record = new ApiUserRecord(Guid.NewGuid(), "Example", "EXAMPLE", "Example", true,
            new ApiPasswordService().Hash(Convert.ToBase64String(RandomNumberGenerator.GetBytes(24))), [], 1, 1, now, now);
        var attempts = await Task.WhenAll(Enumerable.Range(0, 4).Select(async _ => {
            try {
                await CreateStore(environment).SaveAsync(record with { Id = Guid.NewGuid() }, null);
                return true;
            } catch (ApiUserConflictException) {
                return false;
            }
        }));
        Assert.Single(attempts, success => success);
        var saved = Assert.Single(await store.ReadAsync());
        var updates = await Task.WhenAll(new[] { "First", "Second" }.Select(async name => {
            try {
                await CreateStore(environment).SaveAsync(saved with { DisplayName = name, Version = 2, AuthenticationRevision = 2 }, 1);
                return true;
            } catch (ApiUserConflictException) {
                return false;
            }
        }));
        Assert.Single(updates, success => success);
        var reopened = Assert.Single(await CreateStore(environment).ReadAsync());
        Assert.Equal(2, reopened.Version);
        Assert.Equal(2, reopened.AuthenticationRevision);
        await Assert.ThrowsAsync<ApiUserConflictException>(() => store.DeleteAsync(reopened.Id, 1));
        Assert.Equal(reopened.Id, Assert.Single(await store.ReadAsync()).Id);
    }

    [Fact]
    public async Task Unsupported_store_schema_never_becomes_an_empty_writable_store() {
        await using var environment = CanDoItAllTestEnvironment.Create("api-account-schema");
        var store = CreateStore(environment);
        Assert.Empty(await store.ReadAsync());
        var path = Path.Combine(environment.ControlPlaneRootPath, "api-users", "accounts.json");
        const string unsupported = "{\"schemaVersion\":99,\"users\":[]}";
        await File.WriteAllTextAsync(path, unsupported);
        await Assert.ThrowsAsync<InvalidDataException>(() => store.ReadAsync());
        await Assert.ThrowsAsync<InvalidDataException>(() => store.DeleteAsync(Guid.NewGuid(), 1));
        Assert.Equal(unsupported, await File.ReadAllTextAsync(path));
    }

    private static ApiAccessOptions SecureOptions(string hash) => new() {
        Authorization = new() { Enabled = true, SigningKey = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)) },
        UserAuthentication = new() { Enabled = true },
        BootstrapAdmin = new() { PasswordHash = hash }
    };

    private static FileApiUserStore CreateStore(CanDoItAllTestEnvironment environment) {
        var writer = new DurableFileWriter(new PhysicalFileSystemPathPolicyFactory());
        return new(new ControlPlanePathResolver(Options.Create(new ControlPlaneOptions { RootPath = environment.ControlPlaneRootPath }),
            environment.CreateHostEnvironment(nameof(ApiCredentialRulesTests)), writer), writer);
    }
}
