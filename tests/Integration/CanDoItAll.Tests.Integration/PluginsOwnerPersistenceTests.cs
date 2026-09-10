using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.Modules.Plugins;
using CanDoItAll.Modules.Projects;
using CanDoItAll.Plugins.Abstractions;
using CanDoItAll.SharedKernel;
using CanDoItAll.Tests.Support;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.Extensions.DependencyInjection;

namespace CanDoItAll.Tests.Integration;

public sealed class PluginsOwnerPersistenceTests {
    [Fact]
    public async Task Runtime_model_preserves_all_six_schema_mappings_and_rejects_foreign_entities() {
        await using var application = await TestApplication.CreateAsync();
        await using var owner = await application.Services.GetRequiredService<IDbContextFactory<PluginsDbContext>>()
            .CreateDbContextAsync();
        await using var schema = await application.Services.GetRequiredService<IDbContextFactory<AppDbContext>>()
            .CreateDbContextAsync();
        var entities = owner.GetService<IDesignTimeModel>().Model.GetEntityTypes().ToArray();
        Type[] ownedTypes = [typeof(PluginInstallationRecord), typeof(PluginCapabilityGrantRecord),
            typeof(PluginConnectionRecord), typeof(PluginOAuthConnectionRecord),
            typeof(PluginOAuthSessionRecord), typeof(PluginLogRecord)];
        Assert.Equal(ownedTypes.OrderBy(type => type.Name), entities.Select(entity => entity.ClrType).OrderBy(type => type.Name));
        foreach (var entity in entities) {
            var completeEntity = Assert.IsAssignableFrom<IEntityType>(
                schema.GetService<IDesignTimeModel>().Model.FindEntityType(entity.ClrType));
            Assert.Equal(completeEntity.ToDebugString(MetadataDebugStringOptions.LongDefault),
                entity.ToDebugString(MetadataDebugStringOptions.LongDefault));
        }

        Assert.Throws<InvalidOperationException>(() => owner.Set<Project>().ToQueryString());
    }

    [Fact]
    public async Task Legacy_records_survive_restart_owner_writes_and_profile_isolation() {
        await using var environment = CanDoItAllTestEnvironment.Create("plugins-owner-restart");
        var profile = environment.CreatePostgreSqlProfile("original");
        var otherProfile = environment.CreatePostgreSqlProfile("other");
        var options = new TestHarnessOptions { TestEnvironment = environment, ActiveProfile = profile };
        var pluginId = new PluginId("owner.persistence");
        var packageId = new PluginPackageId("owner.persistence.package");
        var connectionKey = new PluginConnectionKey("mail");
        var createdAt = new DateTimeOffset(2026, 1, 2, 3, 4, 5, TimeSpan.Zero);
        var descriptor = new PluginDescriptor(
            pluginId, "Legacy plugin", "Saved manifest description", "1.2.3", "CanDoItAll",
            PluginSourceKind.LocalPackage, PluginTrustLevel.LocalPackage, "1.0.0", PluginCapabilityKind.OAuth2,
            [], PluginSettingsDescriptor.Empty, [],
            new PluginPackageDescriptor(packageId, "1.2.3", "1.0.0", "historical-checksum", "historical-signature"));
        var installation = new PluginInstallationRecord {
            PluginId = pluginId.Value,
            PackageId = packageId.Value,
            DisplayNameSnapshot = descriptor.DisplayName,
            Version = descriptor.Version,
            Vendor = descriptor.Vendor,
            ManifestSnapshotJson = JsonSerializer.Serialize(descriptor, new JsonSerializerOptions(JsonSerializerDefaults.Web)),
            IsEnabled = true,
            InstalledBy = "legacy-import",
            InstalledAtUtc = createdAt,
            UpdatedAtUtc = createdAt,
            ConcurrencyToken = Guid.NewGuid()
        };
        var grant = new PluginCapabilityGrantRecord {
            PluginId = pluginId.Value,
            Capability = (int)PluginCapabilityKind.OAuth2,
            ScopeKind = nameof(PluginGrantScopeKind.Plugin),
            State = nameof(PluginGrantState.Granted),
            RiskKind = nameof(PluginGrantRiskKind.High),
            Reason = "Saved explicit permission",
            UpdatedBy = "legacy-import",
            CreatedAtUtc = createdAt,
            UpdatedAtUtc = createdAt,
            ConcurrencyToken = Guid.NewGuid()
        };
        var connection = new PluginConnectionRecord {
            PluginId = pluginId.Value,
            ConnectionKey = connectionKey.Value,
            DisplayName = "Legacy connection",
            SettingsJson = """{"clientId":"legacy-client","nested":{"retained":true},"extra":[1,2]}""",
            IsEnabled = true,
            HealthStatus = "Saved health",
            UpdatedBy = "legacy-import",
            CreatedAtUtc = createdAt,
            UpdatedAtUtc = createdAt,
            ConcurrencyToken = Guid.NewGuid()
        };
        var oauthConnection = new PluginOAuthConnectionRecord {
            ConnectionId = connection.Id,
            PluginId = pluginId.Value,
            ConnectionKey = connectionKey.Value,
            ProviderKey = "owner.persistence/mail",
            TokenVaultKey = "plugins/oauth/legacy/token-reference",
            Status = nameof(PluginOAuthConnectionStatusKind.Connected),
            AccountDisplay = "legacy@example.invalid",
            GrantedScopesJson = "[\"mail.read\",\"offline_access\"]",
            AccessTokenExpiresAtUtc = createdAt.AddHours(1),
            RefreshTokenExpiresAtUtc = createdAt.AddDays(1),
            LastErrorCode = "historical-error",
            LastErrorDescription = "Saved diagnostic",
            CreatedAtUtc = createdAt,
            UpdatedAtUtc = createdAt,
            ConcurrencyToken = Guid.NewGuid()
        };
        var session = new PluginOAuthSessionRecord {
            StateHash = "historical-state-hash",
            PluginId = pluginId.Value,
            ConnectionId = connection.Id,
            ConnectionKey = connectionKey.Value,
            ProviderKey = oauthConnection.ProviderKey,
            CodeVerifierVaultKey = "plugins/oauth/legacy/pkce-reference",
            RedirectUri = "https://example.invalid/oauth/callback",
            ReturnPath = "/plugins?tab=connections",
            RequestedScopesJson = oauthConnection.GrantedScopesJson,
            CreatedAtUtc = createdAt,
            ExpiresAtUtc = createdAt.AddMinutes(10),
            CompletedAtUtc = createdAt.AddMinutes(1),
            Status = "Completed",
            ErrorCode = "historical-error",
            ErrorDescription = "Saved session diagnostic",
            ConcurrencyToken = Guid.NewGuid()
        };
        var log = new PluginLogRecord {
            PluginId = pluginId.Value,
            PackageId = packageId.Value,
            WorkflowExecutorId = "owner.persistence.executor",
            StreamKind = nameof(PluginLogStreamKind.Runtime),
            OperationKind = nameof(PluginLogOperationKind.ExecutorCompleted),
            Severity = nameof(PluginLogSeverity.Warning),
            Status = "Completed",
            Message = "Saved runtime event",
            DetailsJson = """{"schemaVersion":1,"legacyField":"retained"}""",
            CorrelationId = "historical-correlation",
            CreatedAtUtc = createdAt,
            ConcurrencyToken = Guid.NewGuid()
        };
        await using (var original = await TestApplication.CreateAsync(options)) {
            await using var schema = await original.Services.GetRequiredService<IDbContextFactory<AppDbContext>>()
                .CreateDbContextAsync();
            schema.AddRange(installation, grant, connection, oauthConnection, session, log);
            await schema.SaveChangesAsync();
        }

        await using var restarted = await TestApplication.CreateAsync(options);
        await using var scope = restarted.Services.CreateAsyncScope();
        await using var owner = await restarted.Services.GetRequiredService<IDbContextFactory<PluginsDbContext>>()
            .CreateDbContextAsync();
        owner.ChangeTracker.QueryTrackingBehavior = QueryTrackingBehavior.NoTracking;
        AssertSnapshot(installation, await owner.Set<PluginInstallationRecord>().SingleAsync(item => item.Id == installation.Id));
        AssertSnapshot(grant, await owner.Set<PluginCapabilityGrantRecord>().SingleAsync(item => item.Id == grant.Id));
        AssertSnapshot(connection, await owner.Set<PluginConnectionRecord>().SingleAsync(item => item.Id == connection.Id));
        AssertSnapshot(oauthConnection, await owner.Set<PluginOAuthConnectionRecord>().SingleAsync(item => item.Id == oauthConnection.Id));
        AssertSnapshot(session, await owner.Set<PluginOAuthSessionRecord>().SingleAsync(item => item.Id == session.Id));
        AssertSnapshot(log, await owner.Set<PluginLogRecord>().SingleAsync(item => item.Id == log.Id));

        var installations = scope.ServiceProvider.GetRequiredService<PluginInstallationStore>();
        var grants = scope.ServiceProvider.GetRequiredService<PluginGrantStore>();
        var connections = scope.ServiceProvider.GetRequiredService<PluginConnectionStore>();
        var logs = scope.ServiceProvider.GetRequiredService<PluginLogStore>();
        var oauth = scope.ServiceProvider.GetRequiredService<PluginOAuthService>();
        AssertSnapshot(installation, await installations.FindAsync(pluginId));
        AssertSnapshot(installation, installations.Find(pluginId));
        Assert.Equal(grant.ConcurrencyToken, Assert.Single(grants.List(pluginId)).ConcurrencyToken);
        Assert.Equal(grant.ConcurrencyToken, Assert.Single(await grants.ListAsync(pluginId)).ConcurrencyToken);
        Assert.Equal(connection.Id, (await connections.FindFirstByKeyAsync(pluginId, connectionKey))!.Id.Value);
        Assert.Equal(log.Id, Assert.Single(await logs.ListAsync(new PluginLogQuery(
            PluginLogStreamKind.Runtime, pluginId, packageId, PluginLogSeverity.Warning, Take: 1))).Id);
        var status = Assert.Single(await oauth.ListStatusesAsync(pluginId));
        Assert.Equal(connection.Id, status.ConnectionId.Value);
        Assert.Equal(PluginOAuthConnectionStatusKind.Connected, status.Status);
        Assert.Equal(new[] { "mail.read", "offline_access" }, status.GrantedScopes);
        var catalog = scope.ServiceProvider.GetRequiredService<PluginCatalogService>();
        var unavailable = Assert.Single(await catalog.ListCatalogAsync(), item => item.PluginId == pluginId);
        Assert.Equal(PluginCatalogAvailabilityKind.Unavailable, unavailable.Availability);
        Assert.Equal(descriptor.Description, unavailable.Descriptor.Description);
        Assert.Equal(packageId, unavailable.Descriptor.Package!.PackageId);

        var installationRevision = installations.Revision;
        var disabled = await installations.SetEnabledAsync(pluginId, false, "owner-write");
        Assert.True(disabled.IsSuccess);
        Assert.Equal(installationRevision + 1, installations.Revision);
        Assert.Equal(installation.Id, disabled.Value!.Id);
        Assert.Equal(installation.ManifestSnapshotJson, disabled.Value.ManifestSnapshotJson);
        Assert.NotEqual(installation.ConcurrencyToken, disabled.Value.ConcurrencyToken);
        var grantRevision = grants.Revision;
        var denied = await grants.SetAsync(pluginId,
            new PluginGrantUpdateRequest(PluginCapabilityKind.OAuth2, PluginGrantState.Denied, Reason: "Owner denial"), "owner-write");
        Assert.True(denied.IsSuccess);
        Assert.Equal(grantRevision + 1, grants.Revision);
        Assert.NotEqual(grant.ConcurrencyToken, denied.Value!.ConcurrencyToken);
        var renamed = await connections.SaveAsync(pluginId, new PluginConnectionSaveRequest(
            new PluginConnectionId(connection.Id), connectionKey, "Edited connection", connection.SettingsJson), "owner-write");
        Assert.True(renamed.IsSuccess);
        Assert.NotEqual(connection.ConcurrencyToken, renamed.Value!.ConcurrencyToken);
        var writtenLog = await logs.WriteAsync(new PluginLogWriteRequest(
            PluginLogStreamKind.Runtime, PluginLogOperationKind.PluginEvent, PluginLogSeverity.Warning,
            "Recorded", "Owner runtime event", PluginId: pluginId, PackageId: packageId));
        Assert.NotEqual(Guid.Empty, (await owner.Set<PluginLogRecord>().SingleAsync(item => item.Id == writtenLog.Id)).ConcurrencyToken);
        var savedConnection = await owner.Set<PluginConnectionRecord>().SingleAsync(item => item.Id == connection.Id);
        Assert.Equal(connection.SettingsJson, savedConnection.SettingsJson);
        Assert.Equal(connection.CreatedAtUtc, savedConnection.CreatedAtUtc);
        Assert.Equal(connection.HealthStatus, savedConnection.HealthStatus);
        Assert.Equal("Edited connection", savedConnection.DisplayName);
        Assert.Equal(nameof(PluginGrantState.Denied), (await owner.Set<PluginCapabilityGrantRecord>()
            .SingleAsync(item => item.Id == grant.Id)).State);
        AssertSnapshot(oauthConnection, await owner.Set<PluginOAuthConnectionRecord>().SingleAsync(item => item.Id == oauthConnection.Id));
        AssertSnapshot(session, await owner.Set<PluginOAuthSessionRecord>().SingleAsync(item => item.Id == session.Id));

        await using var other = await TestApplication.CreateAsync(new TestHarnessOptions {
            TestEnvironment = environment,
            ActiveProfile = otherProfile
        });
        await using var otherOwner = await other.Services.GetRequiredService<IDbContextFactory<PluginsDbContext>>()
            .CreateDbContextAsync();
        Assert.Empty(await otherOwner.Set<PluginInstallationRecord>().Where(item => item.PluginId == pluginId.Value).ToArrayAsync());
        Assert.Empty(await otherOwner.Set<PluginCapabilityGrantRecord>().Where(item => item.PluginId == pluginId.Value).ToArrayAsync());
        Assert.Empty(await otherOwner.Set<PluginConnectionRecord>().Where(item => item.PluginId == pluginId.Value).ToArrayAsync());
        Assert.Empty(await otherOwner.Set<PluginOAuthConnectionRecord>().Where(item => item.PluginId == pluginId.Value).ToArrayAsync());
        Assert.Empty(await otherOwner.Set<PluginOAuthSessionRecord>().Where(item => item.PluginId == pluginId.Value).ToArrayAsync());
        Assert.Empty(await otherOwner.Set<PluginLogRecord>().Where(item => item.PluginId == pluginId.Value).ToArrayAsync());
        Assert.False((await installations.FindAsync(pluginId))!.IsEnabled);
        Assert.Equal(connection.Id, (await connections.FindFirstByKeyAsync(pluginId, connectionKey))!.Id.Value);
    }

    [Fact]
    public async Task Callback_denial_persists_session_and_connection_and_rejects_replay_after_restart() {
        await using var environment = CanDoItAllTestEnvironment.Create("plugins-owner-callback");
        var profile = environment.CreatePostgreSqlProfile("original");
        var options = new TestHarnessOptions { TestEnvironment = environment, ActiveProfile = profile };
        const string state = "plugins-owner-callback-state";
        var session = new PluginOAuthSessionRecord {
            StateHash = Convert.ToBase64String(SHA256.HashData(Encoding.ASCII.GetBytes(state)))
                .TrimEnd('=').Replace('+', '-').Replace('/', '_'),
            PluginId = "owner.callback",
            ConnectionId = Guid.NewGuid(),
            ConnectionKey = "mail",
            ProviderKey = "owner.callback/mail",
            ReturnPath = "/plugins?tab=connections",
            RedirectUri = "https://example.invalid/oauth/callback",
            RequestedScopesJson = "[\"mail.read\"]",
            ConcurrencyToken = Guid.NewGuid()
        };
        await using (var original = await TestApplication.CreateAsync(options)) {
            session.CreatedAtUtc = original.Services.GetRequiredService<IClock>().GetUtcNow();
            session.ExpiresAtUtc = session.CreatedAtUtc.AddHours(1);
            await using var schema = await original.Services.GetRequiredService<IDbContextFactory<AppDbContext>>()
                .CreateDbContextAsync();
            schema.Add(session);
            await schema.SaveChangesAsync();
        }

        PluginOAuthSessionRecord failedSession;
        PluginOAuthConnectionRecord failedConnection;
        await using (var restarted = await TestApplication.CreateAsync(options)) {
            await using var scope = restarted.Services.CreateAsyncScope();
            var oauth = scope.ServiceProvider.GetRequiredService<PluginOAuthService>();
            var result = await oauth.CompleteCallbackAsync(state, null, "access_denied", "Operator denied consent");
            Assert.Contains("oauth=failed", result.OriginalString, StringComparison.Ordinal);
            Assert.Contains("reason=access_denied", result.OriginalString, StringComparison.Ordinal);
            await using var owner = await restarted.Services.GetRequiredService<IDbContextFactory<PluginsDbContext>>()
                .CreateDbContextAsync();
            failedSession = await owner.Set<PluginOAuthSessionRecord>().SingleAsync(item => item.Id == session.Id);
            failedConnection = await owner.Set<PluginOAuthConnectionRecord>().SingleAsync(item => item.ConnectionId == session.ConnectionId);
            Assert.Equal("Failed", failedSession.Status);
            Assert.Equal("access_denied", failedSession.ErrorCode);
            Assert.Equal("Operator denied consent", failedSession.ErrorDescription);
            Assert.NotNull(failedSession.CompletedAtUtc);
            Assert.NotEqual(session.ConcurrencyToken, failedSession.ConcurrencyToken);
            Assert.Equal(session.StateHash, failedSession.StateHash);
            Assert.Equal(session.RequestedScopesJson, failedSession.RequestedScopesJson);
            Assert.Equal(session.ReturnPath, failedSession.ReturnPath);
            Assert.Equal(session.PluginId, failedConnection.PluginId);
            Assert.Equal(session.ConnectionKey, failedConnection.ConnectionKey);
            Assert.Equal(session.ProviderKey, failedConnection.ProviderKey);
            Assert.Equal(nameof(PluginOAuthConnectionStatusKind.Error), failedConnection.Status);
            Assert.Equal("access_denied", failedConnection.LastErrorCode);
            Assert.Equal("Operator denied consent", failedConnection.LastErrorDescription);
            Assert.NotEqual(Guid.Empty, failedConnection.ConcurrencyToken);
            Assert.Empty(failedConnection.TokenVaultKey);
        }

        await using var replayed = await TestApplication.CreateAsync(options);
        await using var replayScope = replayed.Services.CreateAsyncScope();
        var replay = await replayScope.ServiceProvider.GetRequiredService<PluginOAuthService>()
            .CompleteCallbackAsync(state, null, "different-error", "Should not overwrite saved state");
        Assert.Contains("reason=state-used", replay.OriginalString, StringComparison.Ordinal);
        await using var replayOwner = await replayed.Services.GetRequiredService<IDbContextFactory<PluginsDbContext>>()
            .CreateDbContextAsync();
        AssertSnapshot(failedSession, await replayOwner.Set<PluginOAuthSessionRecord>().SingleAsync(item => item.Id == session.Id));
        AssertSnapshot(failedConnection, await replayOwner.Set<PluginOAuthConnectionRecord>()
            .SingleAsync(item => item.ConnectionId == session.ConnectionId));
    }

    private static void AssertSnapshot<T>(T expected, T actual) {
        Assert.Equal(JsonSerializer.Serialize(expected), JsonSerializer.Serialize(actual));
    }
}
