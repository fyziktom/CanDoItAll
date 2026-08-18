using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.Memory.Abstractions;
using CanDoItAll.Memory.Application;
using CanDoItAll.Memory.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace CanDoItAll.Memory.Tests.Persistence;

public sealed class ManualMemorySourceIngestionTests
{
    private static readonly DateTimeOffset Now = DateTimeOffset.Parse("2026-07-05T18:30:00Z");
    private static readonly MemoryProviderInstanceId ProviderId = MemoryProviderInstanceId.Parse("provider.manual-test");

    [Fact]
    public async Task Manual_text_ingestion_captures_snapshot_source_job_and_operation_identity()
    {
        using var serviceProvider = CreateServiceProvider();
        using var scope = serviceProvider.CreateScope();
        var scopedProvider = scope.ServiceProvider;
        await SeedProviderAsync(scopedProvider);
        var service = scopedProvider.GetRequiredService<ManualMemorySourceIngestionService>();

        var result = await service.EnqueueAsync(new ManualMemorySourceIngestionRequest(
            ProviderId,
            ManualMemorySourcePayload.Text(
                "Release notes",
                "Memory providers can ingest this approved manual note.",
                "release",
                ["release", "manual"]),
            RequestedBy: "user-42",
            CreateRequester(),
            CreateRetentionPolicy()));

        var sourceStore = scopedProvider.GetRequiredService<IMemorySourceRequestLedgerStore>();
        var operationStore = scopedProvider.GetRequiredService<IMemoryOperationLedgerStore>();
        var sourceRecord = Assert.Single(await sourceStore.ListByProviderAsync(ProviderId));
        var operation = await operationStore.GetAsync(result.OperationId);

        Assert.Equal(MemorySourceIngestionJobStatus.SnapshotCaptured, sourceRecord.Status);
        Assert.Equal(result.CapturedSnapshotId, sourceRecord.CapturedSnapshotId);
        Assert.Equal(result.OperationId, sourceRecord.OperationId);
        Assert.NotNull(operation);
        Assert.Equal(MemoryOperationCallerKind.ManualIngestion, operation!.Extensions.GetMemoryOperationCaller()?.Kind);
        Assert.Contains(
            operation.SourceSnapshotIds,
            snapshotId => snapshotId.Value == result.CapturedSnapshotId.Value);
        Assert.Contains(MemorySourcePayloadForm.TextSection, result.PayloadForms);
    }

    [Fact]
    public async Task Manual_file_and_link_sources_expose_references_without_copying_payload_bytes()
    {
        using var serviceProvider = CreateServiceProvider();
        using var scope = serviceProvider.CreateScope();
        var scopedProvider = scope.ServiceProvider;
        await SeedProviderAsync(scopedProvider);
        var service = scopedProvider.GetRequiredService<ManualMemorySourceIngestionService>();

        var fileResult = await service.EnqueueAsync(new ManualMemorySourceIngestionRequest(
            ProviderId,
            ManualMemorySourcePayload.FileReference(
                "Architecture note",
                "C:\\workspace\\architecture\\memory-plan.pdf",
                "application/pdf",
                "architecture",
                ["file"]),
            RequestedBy: "user-42",
            CreateRequester(),
            CreateRetentionPolicy()));
        var linkResult = await service.EnqueueAsync(new ManualMemorySourceIngestionRequest(
            ProviderId,
            ManualMemorySourcePayload.LinkReference(
                "Design page",
                "https://docs.example.test/memory/provider-model",
                "architecture",
                ["link"]),
            RequestedBy: "user-42",
            CreateRequester(),
            CreateRetentionPolicy()));

        var sourceStore = scopedProvider.GetRequiredService<IMemorySourceRequestLedgerStore>();
        var sourceRecords = await sourceStore.ListByProviderAsync(ProviderId);

        Assert.Equal(2, sourceRecords.Count);
        Assert.Contains(MemorySourcePayloadForm.FileReference, fileResult.PayloadForms);
        Assert.Contains(MemorySourcePayloadForm.BinaryOrExternalReference, fileResult.PayloadForms);
        Assert.Contains(MemorySourcePayloadForm.LinkReference, linkResult.PayloadForms);
        Assert.All(sourceRecords, record => Assert.NotNull(record.CapturedSnapshotId));
    }

    [Fact]
    public async Task Manual_link_with_sensitive_query_is_rejected_before_ledger_enqueue()
    {
        using var serviceProvider = CreateServiceProvider();
        using var scope = serviceProvider.CreateScope();
        var scopedProvider = scope.ServiceProvider;
        await SeedProviderAsync(scopedProvider);
        var service = scopedProvider.GetRequiredService<ManualMemorySourceIngestionService>();

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.EnqueueAsync(new ManualMemorySourceIngestionRequest(
                ProviderId,
                ManualMemorySourcePayload.LinkReference(
                    "Unsafe page",
                    "https://docs.example.test/memory?token=secret-value",
                    "architecture"),
                RequestedBy: "user-42",
                CreateRequester(),
                CreateRetentionPolicy())));
        var sourceStore = scopedProvider.GetRequiredService<IMemorySourceRequestLedgerStore>();

        Assert.Contains("sensitive query parameter", exception.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Empty(await sourceStore.ListByProviderAsync(ProviderId));
    }

    private static ServiceProvider CreateServiceProvider()
    {
        var services = new ServiceCollection();
        services.AddDbContextFactory<AppDbContext>(options =>
            options.UseInMemoryDatabase($"manual-memory-source-{Guid.NewGuid():N}"));
        services.AddGenericMemoryModule();
        services.AddSingleton<TimeProvider>(new FixedTimeProvider(Now));
        return services.BuildServiceProvider(validateScopes: true);
    }

    private static async Task SeedProviderAsync(IServiceProvider serviceProvider)
    {
        var store = serviceProvider.GetRequiredService<IMemoryProviderProfileStore>();
        await store.UpsertAsync(
            new MemoryProviderProfile(
                ProviderId,
                DisplayName: "Manual ingestion provider",
                MemoryProviderDriverKind.Mock,
                IsEnabled: true,
                MemoryProviderHealthState.Healthy,
                MemoryProviderWorkspaceScope.AllWorkspaces,
                SelectionTags: ["manual"],
                MemoryProviderProfilePolicy.Default,
                new MemoryProviderManifest(
                    MemoryProviderKind.Parse("provider.manual"),
                    MemoryProtocolVersion.Current,
                    [new MemoryCapabilityDescriptor(MemoryCapabilityIds.IngestionSnapshot, Version: "1", Supported: true)],
                    MemoryProviderInteractionSupport.SyncQueryOnly,
                    UiSurfaces: [],
                    MemoryProviderLimits.Default,
                    MemoryExtensionData.Empty)),
            Now);
    }

    private static MemoryLedgerRequester CreateRequester()
    {
        return new MemoryLedgerRequester(
            RequesterId: "user-42",
            AgentId: null,
            AgentRole: null,
            SessionId: "manual-session",
            WorkflowId: null,
            WorkflowNodeId: null,
            ProcessId: null,
            ProcessStepId: null);
    }

    private static MemoryLedgerRetentionPolicy CreateRetentionPolicy()
    {
        return MemoryLedgerRetentionPolicy.Expiring(Now.AddDays(7), Now.AddDays(30));
    }

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}
