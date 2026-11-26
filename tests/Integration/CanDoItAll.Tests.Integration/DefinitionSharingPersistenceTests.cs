using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.Modules.AgentFramework;
using CanDoItAll.RemoteInstances;
using CanDoItAll.Tests.Support;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace CanDoItAll.Tests.Integration;

public sealed class DefinitionSharingPersistenceTests {
    private const string PreviousMigration = "20261121161719_AddRemoteTemplatePublications";
    private const RemoteTemplateKind Chat = RemoteTemplateKind.SimpleChat;

    [Fact]
    public async Task Upgrade_preserves_the_old_catalog_and_downgrade_refuses_to_expose_a_private_definition() {
        await using var application = await TestApplication.CreateAsync();
        await using var scope = application.Services.CreateAsyncScope();
        var store = scope.ServiceProvider.GetRequiredService<RemoteTemplatePublicationStore>();
        await using var db = await scope.ServiceProvider.GetRequiredService<IDbContextFactory<AppDbContext>>().CreateDbContextAsync();
        var migrations = db.GetService<IMigrator>();
        await migrations.MigrateAsync(PreviousMigration);
        var id = Guid.NewGuid();
        await db.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO "Workspace_RemoteTemplatePublications" ("Kind", "DefinitionId", "CreatedUtcTicks")
            VALUES ({(int)Chat}, {id}, {0L})
            """);
        await migrations.MigrateAsync();

        var published = new DefinitionSharingSettings(true, true);
        Assert.Equal(published, await store.GetSharingAsync(Chat, id, default));
        Assert.Equal(DefinitionSharingSettings.Default, await store.GetSharingAsync(Chat, Guid.NewGuid(), default));
        var privateChat = new DefinitionSharingSettings(false, false);
        await store.SetSharingAsync(Chat, id, published, privateChat, null, default);
        Assert.DoesNotContain(id, await store.ListAsync(Chat, default));
        Assert.Contains(id, await store.ListApiUnavailableIdsAsync(Chat, default));
        Assert.False(await store.IsApiAvailableAsync(Chat, id, default));

        var refusal = await Assert.ThrowsAsync<PostgresException>(() => migrations.MigrateAsync(PreviousMigration));
        Assert.Equal(PostgresErrorCodes.RaiseException, refusal.SqlState);
        Assert.Equal(privateChat, await store.GetSharingAsync(Chat, id, default));
        await store.SetSharingAsync(Chat, id, privateChat, published, null, default);
        await migrations.MigrateAsync(PreviousMigration);
        await migrations.MigrateAsync();
        Assert.Equal(published, await store.GetSharingAsync(Chat, id, default));
    }

    [Fact]
    public async Task Concurrent_editors_preserve_the_accepted_settings_and_legacy_template_writes_preserve_api_access() {
        await using var application = await TestApplication.CreateAsync();
        await using var scope = application.Services.CreateAsyncScope();
        var store = scope.ServiceProvider.GetRequiredService<RemoteTemplatePublicationStore>();
        var id = Guid.NewGuid();
        var original = DefinitionSharingSettings.Default;
        var onlyTemplate = new DefinitionSharingSettings(false, true);
        await store.SetSharingAsync(Chat, id, original, onlyTemplate, null, default);
        var accepted = onlyTemplate with { ApiAvailable = true };

        await Assert.ThrowsAsync<RemoteInstanceReadException>(() => store.SetSharingAsync(Chat, id, onlyTemplate, original,
            ct => new ValueTask(store.SetSharingAsync(Chat, id, onlyTemplate, accepted, null, ct)), default));
        Assert.Equal(accepted, await store.GetSharingAsync(Chat, id, default));
        await store.SetSharingAsync(Chat, id, accepted, onlyTemplate, null, default);
        await store.SetAsync(Chat, id, false, default);
        Assert.Equal(new DefinitionSharingSettings(false, false), await store.GetSharingAsync(Chat, id, default));
        await store.SetAsync(Chat, id, true, default);
        Assert.Equal(onlyTemplate, await store.GetSharingAsync(Chat, id, default));
    }
}
