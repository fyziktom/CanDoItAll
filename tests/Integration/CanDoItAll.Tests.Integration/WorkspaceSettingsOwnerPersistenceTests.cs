using System.Text.Json;
using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.Modules.AgentFramework.ProviderManagement;
using CanDoItAll.Modules.Workspace;
using CanDoItAll.Tests.Support;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.Extensions.DependencyInjection;
using ProviderProfile = CanDoItAll.Modules.AgentFramework.ProviderManagement.ProviderProfile;

namespace CanDoItAll.Tests.Integration;

public sealed class WorkspaceSettingsOwnerPersistenceTests {
    [Fact]
    public async Task Settings_owner_preserves_the_complete_mapping_and_cannot_query_connector_records() {
        await using var application = await TestApplication.CreateAsync();
        await using var canonical = await application.Services.GetRequiredService<IDbContextFactory<AppDbContext>>().CreateDbContextAsync();
        await using var owner = await application.Services.GetRequiredService<IDbContextFactory<WorkspaceSettingsDbContext>>().CreateDbContextAsync();
        var mapping = Assert.Single(owner.GetService<IDesignTimeModel>().Model.GetEntityTypes());
        var complete = Assert.IsAssignableFrom<IEntityType>(canonical.GetService<IDesignTimeModel>().Model.FindEntityType(typeof(WorkspaceSettings)));
        Assert.Equal(complete.ToDebugString(MetadataDebugStringOptions.LongDefault), mapping.ToDebugString(MetadataDebugStringOptions.LongDefault));
        Assert.Throws<InvalidOperationException>(() => owner.Set<ConnectorCommandRecord>().ToQueryString());
    }

    [Fact]
    public async Task Explicit_current_defaults_survive_owner_service_update_restart_and_profile_isolation() {
        await using var environment = CanDoItAllTestEnvironment.Create("workspace-settings-owner");
        var profile = environment.CreatePostgreSqlProfile("original");
        var otherProfile = environment.CreatePostgreSqlProfile("other");
        var harness = new TestHarnessOptions { TestEnvironment = environment, ActiveProfile = profile };
        var legacy = new WorkspaceSettings {
            WorkspaceName = "Legacy workspace", DefaultProviderProfileId = Guid.NewGuid(), DefaultPromptOutputFormat = "Markdown",
            CurrencyCode = "USD", CurrencyCultureName = "en-US", Notes = "Retain exact legacy notes.",
            UpdatedAtUtc = new DateTimeOffset(2100, 1, 2, 3, 4, 5, TimeSpan.Zero), IsCurrent = true
        };
        await using (var original = await TestApplication.CreateAsync(harness)) {
            await using var canonical = await original.Services.GetRequiredService<IDbContextFactory<AppDbContext>>().CreateDbContextAsync();
            await using var transaction = await canonical.Database.BeginTransactionAsync();
            await canonical.Database.ExecuteSqlRawAsync("LOCK TABLE \"Workspace_Settings\" IN SHARE ROW EXCLUSIVE MODE");
            await canonical.Set<WorkspaceSettings>().Where(item => item.IsCurrent).ExecuteUpdateAsync(update => update.SetProperty(item => item.IsCurrent, false));
            canonical.Add(new ProviderProfile {
                Id = legacy.DefaultProviderProfileId!.Value, Name = "Retained custom provider",
                ConnectorPluginKey = ProviderConnectorKeys.ScenarioHarness, ConfigSchemaVersion = "1.0",
                BaseUrl = ProviderConnectorDefaults.ScenarioHarnessBaseUrl, DefaultModel = ProviderConnectorDefaults.ScenarioHarnessModel
            });
            canonical.Add(legacy);
            await canonical.SaveChangesAsync();
            await transaction.CommitAsync();
        }

        await using (var restarted = await TestApplication.CreateAsync(harness)) {
            await using var owner = await restarted.Services.GetRequiredService<IDbContextFactory<WorkspaceSettingsDbContext>>().CreateDbContextAsync();
            Assert.Equal(JsonSerializer.Serialize(legacy), JsonSerializer.Serialize(await owner.Set<WorkspaceSettings>().SingleAsync(item => item.Id == legacy.Id)));
            await using var scope = restarted.Services.CreateAsyncScope();
            var service = scope.ServiceProvider.GetRequiredService<WorkspaceService>();
            var original = await service.ReadSettingsSnapshotAsync();
            var settings = original.Values.ToEditorModel();
            Assert.Equal(legacy.WorkspaceName, settings.WorkspaceName);
            Assert.Equal(legacy.DefaultProviderProfileId, settings.DefaultProviderProfileId);
            Assert.Equal(legacy.Notes, settings.Notes);
            settings.WorkspaceName = "Owner saved workspace";
            settings.Notes = "Human edit retained after restart.";
            Assert.IsType<WorkspaceSettingsSaveOutcome.Accepted>(await service.SaveSettingsAsync(
                new(original.Expectation, WorkspaceSettingsValues.Capture(settings))));
        }

        await using (var restarted = await TestApplication.CreateAsync(harness)) {
            await using var scope = restarted.Services.CreateAsyncScope();
            var settings = await scope.ServiceProvider.GetRequiredService<WorkspaceService>().GetSettingsAsync();
            Assert.Equal("Owner saved workspace", settings.WorkspaceName);
            Assert.Equal("Human edit retained after restart.", settings.Notes);
            Assert.Equal(legacy.DefaultProviderProfileId, settings.DefaultProviderProfileId);
            await using var canonical = await restarted.Services.GetRequiredService<IDbContextFactory<AppDbContext>>().CreateDbContextAsync();
            Assert.Equal("Owner saved workspace", (await canonical.Set<WorkspaceSettings>().SingleAsync(item => item.Id == legacy.Id)).WorkspaceName);
        }

        await using var other = await TestApplication.CreateAsync(new TestHarnessOptions { TestEnvironment = environment, ActiveProfile = otherProfile });
        await using var otherOwner = await other.Services.GetRequiredService<IDbContextFactory<WorkspaceSettingsDbContext>>().CreateDbContextAsync();
        Assert.False(await otherOwner.Set<WorkspaceSettings>().AnyAsync(item => item.Id == legacy.Id));
    }
}
