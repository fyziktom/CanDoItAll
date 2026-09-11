using CanDoItAll.Infrastructure.Storage;

namespace CanDoItAll.Tests.Unit.Storage;

public sealed class DefaultStorageRoutingServiceTests
{
    [Fact]
    public async Task RecommendAsync_prefers_a_matching_node_rule_over_workspace_scope()
    {
        var projectId = Guid.NewGuid();
        var workspaceStorageId = Guid.NewGuid();
        var nodeStorageId = Guid.NewGuid();
        var catalogService = new TestStorageCatalogService(
            [
                CreateStorage(
                    workspaceStorageId,
                    "Workspace file system",
                    StorageProviderKind.FileSystem,
                    StorageCapability.Write | StorageCapability.InlinePreview),
                CreateStorage(
                    nodeStorageId,
                    "Project evidence gateway",
                    StorageProviderKind.Ipfs,
                    StorageCapability.Write | StorageCapability.InlinePreview)
            ],
            [
                new StorageRoutingRule
                {
                    Name = "Workspace default",
                    ScopeKind = StorageRoutingScopeKind.Workspace,
                    PreferredStorageId = workspaceStorageId,
                    Reason = "Workspace fallback."
                },
                new StorageRoutingRule
                {
                    Name = "Node preview rule",
                    ScopeKind = StorageRoutingScopeKind.Node,
                    ProjectId = projectId,
                    NodeKey = "node-7",
                    PreviewRequired = true,
                    PreferredStorageId = nodeStorageId,
                    Reason = "Node-scoped preview content uses the evidence gateway."
                }
            ]);
        var sut = new DefaultStorageRoutingService(catalogService, catalogService.ReadRoutingRecordsAsync);

        var recommendation = await sut.RecommendAsync(new StorageSelectionContext(
            "evidence.pdf",
            "application/pdf",
            StorageUsagePurpose.Evidence,
            StorageContentKind.Pdf,
            projectId,
            "node-7",
            256,
            PreviewRequired: true));

        Assert.NotNull(recommendation.PrimaryCandidate);
        Assert.Equal(nodeStorageId, recommendation.PrimaryCandidate!.StorageId);
        Assert.Equal("Node-scoped preview content uses the evidence gateway.", recommendation.Reason);
    }

    [Fact]
    public async Task RecommendAsync_treats_publish_rules_as_opt_in_intent()
    {
        var ftpStorageId = Guid.NewGuid();
        var fileStorageId = Guid.NewGuid();
        var catalogService = new TestStorageCatalogService(
            [
                CreateStorage(
                    ftpStorageId,
                    "Release mirror",
                    StorageProviderKind.Ftp,
                    StorageCapability.Write),
                CreateStorage(
                    fileStorageId,
                    "Workspace file system",
                    StorageProviderKind.FileSystem,
                    StorageCapability.Write | StorageCapability.InlinePreview)
            ],
            [
                new StorageRoutingRule
                {
                    Name = "Publish only",
                    ScopeKind = StorageRoutingScopeKind.Workspace,
                    PublishIntent = true,
                    PreferredStorageId = ftpStorageId,
                    Reason = "Publish operations use the release mirror."
                }
            ]);
        var sut = new DefaultStorageRoutingService(catalogService, catalogService.ReadRoutingRecordsAsync);

        var recommendation = await sut.RecommendAsync(new StorageSelectionContext(
            "notes.md",
            "text/markdown",
            StorageUsagePurpose.ProjectAsset,
            StorageContentKind.Markdown,
            EditIntent: true,
            PreviewRequired: true,
            PublishIntent: false));

        Assert.NotNull(recommendation.PrimaryCandidate);
        Assert.Equal(StorageProviderKind.FileSystem, recommendation.PrimaryCandidate!.ProviderKind);
        Assert.DoesNotContain(recommendation.Warnings, warning => warning.Contains("configured default", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task RecommendAsync_uses_rule_alternatives_when_the_preferred_storage_cannot_satisfy_the_rule()
    {
        var preferredStorageId = Guid.NewGuid();
        var alternativeStorageId = Guid.NewGuid();
        var catalogService = new TestStorageCatalogService(
            [
                CreateStorage(
                    preferredStorageId,
                    "Read-only preview bucket",
                    StorageProviderKind.FileSystem,
                    StorageCapability.Write),
                CreateStorage(
                    alternativeStorageId,
                    "Shareable evidence store",
                    StorageProviderKind.Ipfs,
                    StorageCapability.Write | StorageCapability.InlinePreview)
            ],
            [
                new StorageRoutingRule
                {
                    Name = "Preview evidence",
                    ScopeKind = StorageRoutingScopeKind.Workspace,
                    PreviewRequired = true,
                    RequiredCapabilities = StorageCapability.InlinePreview,
                    PreferredStorageId = preferredStorageId,
                    AlternativeStorageIdsJson = StorageJson.SerializeGuidList([alternativeStorageId]),
                    Reason = "Previewable evidence prefers an inline-preview store."
                }
            ]);
        var sut = new DefaultStorageRoutingService(catalogService, catalogService.ReadRoutingRecordsAsync);

        var recommendation = await sut.RecommendAsync(new StorageSelectionContext(
            "proof.png",
            "image/png",
            StorageUsagePurpose.Evidence,
            StorageContentKind.Image,
            ContentLength: 1024,
            PreviewRequired: true));

        Assert.NotNull(recommendation.PrimaryCandidate);
        Assert.Equal(alternativeStorageId, recommendation.PrimaryCandidate!.StorageId);
        Assert.Contains(
            recommendation.Warnings,
            warning => warning.Contains("configured default storage", StringComparison.OrdinalIgnoreCase));
        Assert.StartsWith("Fallback applied for rule", recommendation.Reason, StringComparison.Ordinal);
    }

    private static StorageCatalogRecord CreateStorage(
        Guid id,
        string name,
        StorageProviderKind providerKind,
        StorageCapability capabilityMask,
        StorageHealthStatus healthStatus = StorageHealthStatus.Healthy)
    {
        return new StorageCatalogRecord
        {
            Id = id,
            Name = name,
            ProviderKind = providerKind,
            CapabilityMask = capabilityMask,
            HealthStatus = healthStatus,
            IsEnabled = true
        };
    }

    private sealed class TestStorageCatalogService(
        IReadOnlyList<StorageCatalogRecord> storages,
        IReadOnlyList<StorageRoutingRule> rules) : IStorageCatalogService
    {
        private Task<IReadOnlyList<StorageCatalogRecord>> ReadRecordsAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(storages);

        private Task<StorageCatalogRecord?> ReadRecordAsync(Guid id, CancellationToken cancellationToken = default)
            => Task.FromResult(storages.FirstOrDefault(item => item.Id == id));

        private Task<StorageCatalogRecord> ReadBootstrapRecordAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(storages.First());

        private Task<StorageCatalogRecord> SaveRecordAsync(StorageCatalogRecord record, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        internal Task<IReadOnlyList<StorageRoutingRule>> ReadRoutingRecordsAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(rules);

        private Task<StorageRoutingRule> SaveRoutingRecordAsync(StorageRoutingRule rule, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();
        public async Task<IReadOnlyList<StorageCatalogSnapshot>> ListAsync(CancellationToken cancellationToken = default) =>
            (await ReadRecordsAsync(cancellationToken)).Select(StorageCatalogMapping.ToSnapshot).ToArray();

        public async Task<StorageCatalogSnapshot?> GetAsync(Guid id, CancellationToken cancellationToken = default) =>
            (await ReadRecordAsync(id, cancellationToken))?.ToSnapshot();

        public async Task<StorageDriverInput?> GetDriverAsync(Guid id, CancellationToken cancellationToken = default) =>
            (await ReadRecordAsync(id, cancellationToken))?.ToDriverInput();

        public async Task<StorageCatalogEditorSnapshot?> GetEditorAsync(Guid id, CancellationToken cancellationToken = default) {
            var row = await ReadRecordAsync(id, cancellationToken);
            return row is null ? null : new(row.ToSnapshot(), StorageJson.ParseProviderConfiguration(row.ConfigJson));
        }

        public async Task<StorageDriverInput> EnsureBootstrapFileSystemStorageAsync(CancellationToken cancellationToken = default) =>
            (await ReadBootstrapRecordAsync(cancellationToken)).ToDriverInput();

        public async Task<StorageCatalogSnapshot> SaveAsync(StorageCatalogSaveRequest request, CancellationToken cancellationToken = default) =>
            (await SaveRecordAsync(StorageCatalogMapping.CreateDraft(request), cancellationToken)).ToSnapshot();

        public async Task<IReadOnlyList<StorageRoutingRuleSnapshot>> ListRulesAsync(CancellationToken cancellationToken = default) =>
            (await ReadRoutingRecordsAsync(cancellationToken)).Select(StorageCatalogMapping.ToSnapshot).ToArray();

        public async Task<StorageRoutingRuleSnapshot> SaveRuleAsync(StorageRoutingRuleSaveRequest request, CancellationToken cancellationToken = default) =>
            (await SaveRoutingRecordAsync(StorageCatalogMapping.CreateDraft(request), cancellationToken)).ToSnapshot();

        public Task ApplyDefaultPurposesAsync(Guid storageId, IReadOnlyCollection<StorageUsagePurpose> defaultPurposes,
            CancellationToken cancellationToken = default) => throw new NotSupportedException();

    }
}
