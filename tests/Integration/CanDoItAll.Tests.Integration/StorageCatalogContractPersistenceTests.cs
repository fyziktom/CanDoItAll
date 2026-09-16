using System.Text.Json;
using CanDoItAll.Infrastructure.Storage;
using CanDoItAll.Modules.Workspace;
using CanDoItAll.Tests.Support;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace CanDoItAll.Tests.Integration;

public sealed class StorageCatalogContractPersistenceTests {
    [Fact]
    public async Task Metadata_reads_and_omitted_configuration_preserve_exact_owner_bytes_across_restart() {
        await using var environment = CanDoItAllTestEnvironment.Create("storage-contract-restart");
        var profile = environment.CreatePostgreSqlProfile("original");
        var harness = new TestHarnessOptions { TestEnvironment = environment, ActiveProfile = profile };
        StorageCatalogRecord[] rows = [Row("Unknown members", " {\n \"port\":2121, \"unknown\": {\"keep\":true}\n } "),
            Row("Whitespace", " \r\n\t "), Row("Malformed unrelated", "{unparseable")];
        Guid newId;
        await using (var application = await TestApplication.CreateAsync(harness)) {
            await using var database = await OpenAsync(application);
            database.AddRange(rows);
            await database.SaveChangesAsync();
            var service = application.Services.GetRequiredService<StorageCatalogService>();
            var listed = await service.ListAsync();
            Assert.Equal(rows.Select(row => row.Id).Order(), listed.Where(row => rows.Select(saved => saved.Id).Contains(row.Id)).Select(row => row.Id).Order());
            foreach (var original in rows.Take(2)) {
                var snapshot = await service.GetAsync(original.Id);
                Assert.NotNull(snapshot);
                await service.SaveAsync(StorageCatalogSaveRequest.FromSnapshot(snapshot) with { Name = original.Name + " edited" });
            }
            Assert.NotNull(await service.GetDriverAsync(rows[2].Id));
            await Assert.ThrowsAsync<JsonException>(() => service.GetEditorAsync(rows[2].Id));
            var created = await service.SaveAsync(new() { Name = "Omitted new configuration", ProviderKind = StorageProviderKind.Ftp });
            newId = created.Id;
            await using var verify = await OpenAsync(application);
            foreach (var original in rows) {
                var stored = await verify.Set<StorageCatalogRecord>().SingleAsync(row => row.Id == original.Id);
                Assert.Equal(original.ConfigJson, stored.ConfigJson);
                Assert.Equal(original.CreatedAtUtc, stored.CreatedAtUtc);
                Assert.Equal(original.CredentialSecretId, stored.CredentialSecretId);
            }
            Assert.Equal("{}", (await verify.Set<StorageCatalogRecord>().SingleAsync(row => row.Id == newId)).ConfigJson);
        }
        await using var restarted = await TestApplication.CreateAsync(harness);
        var catalog = restarted.Services.GetRequiredService<StorageCatalogService>();
        var editor = await catalog.GetEditorAsync(rows[0].Id);
        Assert.NotNull(editor);
        Assert.Equal(2121, editor.Configuration.Port);
        var replacement = new StorageProviderConfiguration { Port = 2021, BasePath = "explicit-replacement" };
        await catalog.SaveAsync(StorageCatalogSaveRequest.FromSnapshot(editor.Catalog) with { Configuration = replacement });
        await using var final = await OpenAsync(restarted);
        Assert.Equal(StorageJson.SerializeProviderConfiguration(replacement),
            (await final.Set<StorageCatalogRecord>().SingleAsync(row => row.Id == rows[0].Id)).ConfigJson);
        Assert.Equal(rows[1].ConfigJson, (await final.Set<StorageCatalogRecord>().SingleAsync(row => row.Id == rows[1].Id)).ConfigJson);
        Assert.Equal(rows[2].ConfigJson, (await final.Set<StorageCatalogRecord>().SingleAsync(row => row.Id == rows[2].Id)).ConfigJson);
        Assert.Equal("{}", (await final.Set<StorageCatalogRecord>().SingleAsync(row => row.Id == newId)).ConfigJson);
    }

    [Fact]
    public async Task Routing_parses_only_matched_alternatives_and_disabled_defaults_retain_all_other_fields() {
        await using var application = await TestApplication.CreateAsync();
        var catalog = application.Services.GetRequiredService<StorageCatalogService>();
        var storage = Row("Route target", "{unrelated-provider-config");
        var firstProject = Guid.NewGuid();
        var secondProject = Guid.NewGuid();
        var matched = new StorageRoutingRule {
            Name = "Exact selected route", Priority = -100, ScopeKind = StorageRoutingScopeKind.Project,
            ProjectId = firstProject, UsagePurpose = StorageUsagePurpose.Evidence, PreferredStorageId = storage.Id,
            AlternativeStorageIdsJson = $" [ \"{storage.Id:D}\", \"{storage.Id:D}\" ] ", Reason = "exact reason"
        };
        var malformed = new StorageRoutingRule {
            Name = "Only other project", Priority = -100, ScopeKind = StorageRoutingScopeKind.Project,
            ProjectId = secondProject, UsagePurpose = StorageUsagePurpose.Evidence, PreferredStorageId = storage.Id,
            AlternativeStorageIdsJson = "{bad-alternatives"
        };
        var workspace = new StorageRoutingRule {
            Name = "Retained default", Priority = -90, UsagePurpose = StorageUsagePurpose.ProjectAsset,
            PreferredStorageId = storage.Id, AlternativeStorageIdsJson = "{preserve-disabled-alternatives",
            MinimumContentLength = 7, MaximumContentLength = 700, MimePattern = "text/*", EditIntent = true,
            PreviewRequired = true, PublishIntent = true, RequiredCapabilities = StorageCapability.Write,
            Reason = "Retain this routing decision"
        };
        await using (var database = await OpenAsync(application)) {
            database.AddRange(storage, matched, malformed, workspace);
            await database.SaveChangesAsync();
        }
        Assert.Contains(await catalog.ListRulesAsync(), row => row.Id == malformed.Id);
        var routing = application.Services.GetRequiredService<IStorageRoutingService>();
        var context = new StorageSelectionContext("proof.txt", "text/plain", StorageUsagePurpose.Evidence, ProjectId: firstProject);
        Assert.Equal(storage.Id, (await routing.RecommendAsync(context)).PrimaryCandidate!.StorageId);
        await Assert.ThrowsAsync<JsonException>(() => routing.RecommendAsync(context with { ProjectId = secondProject }));
        var metadata = Assert.Single(await catalog.ListRulesAsync(), row => row.Id == matched.Id);
        await catalog.SaveRuleAsync(StorageRoutingRuleSaveRequest.FromSnapshot(metadata) with { Name = "Renamed only" });
        await catalog.ApplyDefaultPurposesAsync(storage.Id, []);
        await using var verify = await OpenAsync(application);
        var disabled = await verify.Set<StorageRoutingRule>().SingleAsync(row => row.Id == workspace.Id);
        Assert.False(disabled.IsEnabled);
        workspace.IsEnabled = false;
        workspace.UpdatedAtUtc = disabled.UpdatedAtUtc;
        Assert.Equal(JsonSerializer.Serialize(workspace), JsonSerializer.Serialize(disabled));
        Assert.Equal(matched.AlternativeStorageIdsJson,
            (await verify.Set<StorageRoutingRule>().SingleAsync(row => row.Id == matched.Id)).AlternativeStorageIdsJson);
        Assert.Equal(malformed.AlternativeStorageIdsJson,
            (await verify.Set<StorageRoutingRule>().SingleAsync(row => row.Id == malformed.Id)).AlternativeStorageIdsJson);
        Assert.Equal(storage.ConfigJson, (await verify.Set<StorageCatalogRecord>().SingleAsync(row => row.Id == storage.Id)).ConfigJson);
    }

    [Fact]
    public async Task Connection_health_success_and_failure_retain_original_configuration_and_capability_semantics() {
        await using var application = await TestApplication.CreateAsync();
        var storage = Row("Connection target", " {\"port\":2021,\"unknown\":\"retained\"} ");
        await using (var database = await OpenAsync(application)) {
            database.AddRange(storage, Row("Malformed unrelated", "{unparseable"));
            await database.SaveChangesAsync();
        }
        var driver = new ConnectionProbe(storage.ConfigJson);
        var secret = new CredentialProbe();
        var service = new StorageConnectionTestService(application.Services.GetRequiredService<StorageCatalogService>(),
            new StorageDriverRegistry([driver]), secret, NullLogger<StorageConnectionTestService>.Instance);
        var success = await service.TestAsync(storage.Id);
        Assert.True(success.IsSuccess);
        driver.Fail = true;
        var failure = await service.TestAsync(storage.Id);
        Assert.False(failure.IsSuccess);
        Assert.Equal([storage.CredentialSecretId, storage.CredentialSecretId], secret.Requests);
        await using var verify = await OpenAsync(application);
        var saved = await verify.Set<StorageCatalogRecord>().SingleAsync(row => row.Id == storage.Id);
        Assert.Equal(storage.ConfigJson, saved.ConfigJson);
        Assert.Equal(success.CapabilityMask, saved.CapabilityMask);
        Assert.Equal(StorageHealthStatus.Unavailable, saved.HealthStatus);
        Assert.Equal(failure.TestedAtUtc.ToUniversalTime().Ticks / TimeSpan.TicksPerMicrosecond,
            saved.LastTestedAtUtc!.Value.Ticks / TimeSpan.TicksPerMicrosecond);
        Assert.Equal(failure.Message, saved.LastHealthMessage);
    }

    [Fact]
    public async Task Actual_workspace_editor_uses_typed_configuration_and_owner_default_routes() {
        await using var application = await TestApplication.CreateAsync();
        await using var scope = application.Services.CreateAsyncScope();
        var workspace = scope.ServiceProvider.GetRequiredService<WorkspaceService>();
        var draft = workspace.CreateStorageDraft(StorageProviderKind.Ftp);
        draft.Name = "Typed editor storage";
        draft.EndpointOrRoot = "ftp://files.example.test/editor";
        draft.Username = "editor-user";
        draft.Port = 2021;
        draft.BasePath = "editor-root";
        draft.CredentialSecretId = Guid.NewGuid();
        draft.DefaultPurposes = [StorageUsagePurpose.ProjectAsset, StorageUsagePurpose.DeploymentMirror];
        var saved = await workspace.SaveStorageAsync(draft);
        Assert.True(saved.IsSuccess);
        var reloaded = await workspace.GetStorageAsync(saved.Value);
        Assert.Equal(draft.Username, reloaded.Username);
        Assert.Equal(draft.Port, reloaded.Port);
        Assert.Equal(draft.BasePath, reloaded.BasePath);
        Assert.Equal(draft.CredentialSecretId, reloaded.CredentialSecretId);
        Assert.Equal(draft.DefaultPurposes, reloaded.DefaultPurposes);
        reloaded.DefaultPurposes = [];
        Assert.True((await workspace.SaveStorageAsync(reloaded)).IsSuccess);
        Assert.Empty((await workspace.GetStorageAsync(saved.Value)).DefaultPurposes);
        await using var database = await OpenAsync(application);
        var routes = await database.Set<StorageRoutingRule>().Where(row => row.PreferredStorageId == saved.Value).OrderBy(row => row.Priority).ToArrayAsync();
        Assert.Equal([100, 170], routes.Select(row => row.Priority));
        Assert.All(routes, row => Assert.False(row.IsEnabled));
    }

    private static Task<StorageDbContext> OpenAsync(TestApplication application) =>
        application.Services.GetRequiredService<IDbContextFactory<StorageDbContext>>().CreateDbContextAsync();

    private static StorageCatalogRecord Row(string name, string configuration) => new() {
        Name = name, ProviderKind = StorageProviderKind.Ftp, ConnectionMode = StorageConnectionMode.Remote,
        EndpointOrRoot = "ftp://files.example.test/root", ConfigJson = configuration,
        CapabilityMask = StorageCapability.Read | StorageCapability.Write, HealthStatus = StorageHealthStatus.Healthy,
        CredentialSecretId = Guid.NewGuid(), CreatedAtUtc = new(2026, 1, 2, 3, 4, 5, TimeSpan.Zero),
        UpdatedAtUtc = new(2026, 1, 2, 3, 4, 5, TimeSpan.Zero)
    };

    private sealed class CredentialProbe : IStorageSecretResolver {
        public List<Guid?> Requests { get; } = [];
        public Task<string?> ResolveCredentialAsync(Guid? secretId, CancellationToken cancellationToken = default) {
            Requests.Add(secretId);
            return Task.FromResult<string?>("private-credential");
        }
    }

    private sealed class ConnectionProbe(string configuration) : IStorageDriver {
        public bool Fail { get; set; }
        public StorageProviderKind ProviderKind => StorageProviderKind.Ftp;
        public StorageCapability SupportedCapabilities => StorageCapability.Read | StorageCapability.ConnectionTest;
        public Task<StorageConnectionTestResult> TestConnectionAsync(StorageDriverInput storage, string? secretValue,
            CancellationToken cancellationToken = default) {
            Assert.Equal(configuration, storage.OriginalConfigurationJson);
            Assert.Equal("private-credential", secretValue);
            if (Fail) {
                throw new IOException("Controlled connection failure");
            }
            return Task.FromResult(new StorageConnectionTestResult(true, "Connection observed", StorageHealthStatus.Healthy,
                SupportedCapabilities, DateTimeOffset.UtcNow));
        }
        public Task<StorageWriteResult> SaveAsync(StorageDriverInput storage, StorageWriteRequest request,
            CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<Stream> OpenReadAsync(StorageDriverInput storage, StorageObjectReference reference,
            CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task DeleteAsync(StorageDriverInput storage, StorageObjectReference reference,
            CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }
}
