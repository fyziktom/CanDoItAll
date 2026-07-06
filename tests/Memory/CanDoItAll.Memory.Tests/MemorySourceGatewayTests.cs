using AgentCore = CanDoItAll.AgentFramework.Core;
using CanDoItAll.Memory.Abstractions;
using CanDoItAll.Memory.Application;

namespace CanDoItAll.Memory.Tests;

public sealed class MemorySourceGatewayTests
{
    private static readonly Guid ProjectId = Guid.Parse("4a7f8bf0-b2a5-4cc7-8229-322729fb9168");

    [Fact]
    public async Task SB04_SG001_Source_gateway_returns_existing_snapshot_contract_with_redaction_and_provenance()
    {
        var snapshot = CreateWorkbenchSnapshot(redacted: true);
        var gateway = new MemorySourceGateway(
            [new FakeSourceGatewayAdapter(snapshot)],
            [AgentCore.MemorySourceKind.WorkbenchProjectStructure]);
        var request = CreateGatewayRequest(
            AgentCore.MemorySourceKind.WorkbenchProjectStructure,
            MemorySourceGatewayPolicy.Allow([AgentCore.MemorySourceKind.WorkbenchProjectStructure]));

        var result = await gateway.ReadSnapshotAsync(request);

        Assert.Equal(MemorySourceGatewayStatus.Succeeded, result.Status);
        Assert.Same(snapshot, result.Snapshot);
        Assert.Equal(AgentCore.MemorySourceKind.WorkbenchProjectStructure, result.Snapshot?.Manifest.SourceKind);
        Assert.Equal(AgentCore.MemorySourceAccessMode.Redacted, result.Snapshot?.Items[0].Permission.AccessMode);
        Assert.Equal(AgentCore.MemorySourceSensitivity.Sensitive, result.Snapshot?.Items[0].Permission.Sensitivity);
        Assert.Equal(ProjectId, result.Snapshot?.Items[0].Provenance.ScopeId);
        Assert.Equal(MemorySourceModuleId.Parse("workbench.project-structure"), result.AdapterModuleId);
        Assert.Contains(MemorySourcePayloadForm.StructuredJsonFacts, result.PayloadForms);
    }

    [Fact]
    public async Task SB04_SG002_Missing_adapter_fails_closed_without_snapshot()
    {
        var gateway = new MemorySourceGateway(
            adapters: [],
            supportedSourceKinds: [AgentCore.MemorySourceKind.WorkbenchProjectStructure]);
        var request = CreateGatewayRequest(
            AgentCore.MemorySourceKind.WorkbenchProjectStructure,
            MemorySourceGatewayPolicy.Allow([AgentCore.MemorySourceKind.WorkbenchProjectStructure]));

        var result = await gateway.ReadSnapshotAsync(request);

        Assert.Equal(MemorySourceGatewayStatus.MissingAdapter, result.Status);
        Assert.Null(result.Snapshot);
        Assert.False(result.DispatchAllowed);
    }

    [Fact]
    public async Task SB04_SG003_Denied_source_scope_fails_before_adapter_call()
    {
        var adapter = new FakeSourceGatewayAdapter(CreateWorkbenchSnapshot(redacted: true));
        var gateway = new MemorySourceGateway(
            [adapter],
            [AgentCore.MemorySourceKind.WorkbenchProjectStructure]);
        var request = CreateGatewayRequest(
            AgentCore.MemorySourceKind.WorkbenchProjectStructure,
            MemorySourceGatewayPolicy.Allow([AgentCore.MemorySourceKind.WorkflowRuntime]));

        var result = await gateway.ReadSnapshotAsync(request);

        Assert.Equal(MemorySourceGatewayStatus.DeniedSourceScope, result.Status);
        Assert.False(result.DispatchAllowed);
        Assert.Equal(0, adapter.ReadCount);
    }

    [Fact]
    public async Task SB04_SG004_Unsupported_source_kind_fails_closed_before_adapter_discovery()
    {
        var gateway = new MemorySourceGateway(
            adapters: [],
            supportedSourceKinds: [AgentCore.MemorySourceKind.WorkbenchProjectStructure]);
        var request = CreateGatewayRequest(
            AgentCore.MemorySourceKind.WorkflowRuntime,
            MemorySourceGatewayPolicy.Allow([AgentCore.MemorySourceKind.WorkflowRuntime]));

        var result = await gateway.ReadSnapshotAsync(request);

        Assert.Equal(MemorySourceGatewayStatus.UnsupportedSourceKind, result.Status);
        Assert.Null(result.Snapshot);
    }

    [Fact]
    public async Task SB11_SG001_Denied_requested_scope_fails_before_adapter_call()
    {
        var adapter = new FakeSourceGatewayAdapter(CreateWorkbenchSnapshot(redacted: true));
        var gateway = new MemorySourceGateway(
            [adapter],
            [AgentCore.MemorySourceKind.WorkbenchProjectStructure]);
        var request = new MemorySourceGatewayRequest(
            AgentCore.MemorySourceKind.WorkbenchProjectStructure,
            ScopeId: ProjectId,
            RequestedScope: MemorySourceScope.Process,
            Cursor: null,
            Take: null,
            MemorySourceGatewayPolicy.AllowScopes(
                [AgentCore.MemorySourceKind.WorkbenchProjectStructure],
                [MemorySourceScope.Project]),
            RequesterId: "user-42");

        var result = await gateway.ReadSnapshotAsync(request);

        Assert.Equal(MemorySourceGatewayStatus.DeniedSourceScope, result.Status);
        Assert.False(result.DispatchAllowed);
        Assert.Equal(0, adapter.ReadCount);
    }

    [Fact]
    public async Task SB04_SG005_Sensitive_payload_without_redaction_is_rejected()
    {
        var gateway = new MemorySourceGateway(
            [new FakeSourceGatewayAdapter(CreateWorkbenchSnapshot(redacted: false, containsSensitivePayload: true))],
            [AgentCore.MemorySourceKind.WorkbenchProjectStructure]);
        var request = CreateGatewayRequest(
            AgentCore.MemorySourceKind.WorkbenchProjectStructure,
            MemorySourceGatewayPolicy.Allow([AgentCore.MemorySourceKind.WorkbenchProjectStructure]));

        var result = await gateway.ReadSnapshotAsync(request);

        Assert.Equal(MemorySourceGatewayStatus.RedactionRequired, result.Status);
        Assert.False(result.DispatchAllowed);
        Assert.Null(result.Snapshot);
    }

    [Fact]
    public async Task SB14_SG001_Adapter_required_scope_mismatch_fails_before_adapter_call()
    {
        var adapter = new FakeSourceGatewayAdapter(CreateWorkbenchSnapshot(redacted: true));
        var gateway = new MemorySourceGateway(
            [adapter],
            [AgentCore.MemorySourceKind.WorkbenchProjectStructure]);
        var request = new MemorySourceGatewayRequest(
            AgentCore.MemorySourceKind.WorkbenchProjectStructure,
            ScopeId: ProjectId,
            RequestedScope: MemorySourceScope.Process,
            Cursor: null,
            Take: null,
            MemorySourceGatewayPolicy.AllowScopes(
                [AgentCore.MemorySourceKind.WorkbenchProjectStructure],
                [MemorySourceScope.Process]),
            RequesterId: "user-42");

        var result = await gateway.ReadSnapshotAsync(request);

        Assert.Equal(MemorySourceGatewayStatus.DeniedSourceScope, result.Status);
        Assert.False(result.DispatchAllowed);
        Assert.Equal(0, adapter.ReadCount);
    }

    [Fact]
    public async Task SB14_SG002_Snapshot_with_stale_scope_id_is_rejected()
    {
        var staleScopeId = Guid.Parse("bf14aab7-8409-4266-9347-01fc0c9b542c");
        var snapshot = CreateWorkbenchSnapshot(redacted: true) with
        {
            Manifest = CreateWorkbenchSnapshot(redacted: true).Manifest with
            {
                ScopeId = staleScopeId
            }
        };
        var gateway = new MemorySourceGateway(
            [new FakeSourceGatewayAdapter(snapshot)],
            [AgentCore.MemorySourceKind.WorkbenchProjectStructure]);
        var request = CreateGatewayRequest(
            AgentCore.MemorySourceKind.WorkbenchProjectStructure,
            MemorySourceGatewayPolicy.Allow([AgentCore.MemorySourceKind.WorkbenchProjectStructure]));

        var result = await gateway.ReadSnapshotAsync(request);

        Assert.Equal(MemorySourceGatewayStatus.InvalidSnapshot, result.Status);
        Assert.False(result.DispatchAllowed);
        Assert.Null(result.Snapshot);
        Assert.Contains(staleScopeId.ToString("D"), result.Diagnostic, StringComparison.Ordinal);
    }

    [Fact]
    public async Task SB14_SG003_Item_with_stale_provenance_scope_is_rejected()
    {
        var staleScopeId = Guid.Parse("8a0aacd3-2790-47ac-b765-e82633453a91");
        var snapshot = CreateWorkbenchSnapshot(redacted: true);
        var item = snapshot.Items[0] with
        {
            Provenance = snapshot.Items[0].Provenance with
            {
                ScopeId = staleScopeId
            }
        };
        var gateway = new MemorySourceGateway(
            [new FakeSourceGatewayAdapter(snapshot with { Items = [item] })],
            [AgentCore.MemorySourceKind.WorkbenchProjectStructure]);
        var request = CreateGatewayRequest(
            AgentCore.MemorySourceKind.WorkbenchProjectStructure,
            MemorySourceGatewayPolicy.Allow([AgentCore.MemorySourceKind.WorkbenchProjectStructure]));

        var result = await gateway.ReadSnapshotAsync(request);

        Assert.Equal(MemorySourceGatewayStatus.InvalidSnapshot, result.Status);
        Assert.False(result.DispatchAllowed);
        Assert.Null(result.Snapshot);
        Assert.Contains("provenance scope", result.Diagnostic, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task SB14_SG004_Item_without_redaction_policy_is_rejected()
    {
        var snapshot = CreateWorkbenchSnapshot(redacted: true);
        var item = snapshot.Items[0] with
        {
            Permission = snapshot.Items[0].Permission with
            {
                RedactionPolicy = string.Empty
            }
        };
        var gateway = new MemorySourceGateway(
            [new FakeSourceGatewayAdapter(snapshot with { Items = [item] })],
            [AgentCore.MemorySourceKind.WorkbenchProjectStructure]);
        var request = CreateGatewayRequest(
            AgentCore.MemorySourceKind.WorkbenchProjectStructure,
            MemorySourceGatewayPolicy.Allow([AgentCore.MemorySourceKind.WorkbenchProjectStructure]));

        var result = await gateway.ReadSnapshotAsync(request);

        Assert.Equal(MemorySourceGatewayStatus.InvalidSnapshot, result.Status);
        Assert.False(result.DispatchAllowed);
        Assert.Null(result.Snapshot);
        Assert.Contains("redaction policy", result.Diagnostic, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void SB04_SG006_Provider_source_requests_and_user_ingestion_jobs_share_gateway_request_shape()
    {
        var protocolSourceRequest = new MemorySourceRequest(
            MemorySourceRequestId.Parse("provider-source-request-1"),
            [MemorySourceScope.Project],
            Purpose: "hydrate context pack",
            ProviderVisibleReason: "provider asked for project facts");
        var policy = MemorySourceGatewayPolicy.Allow([AgentCore.MemorySourceKind.WorkbenchProjectStructure]);

        var providerRequest = MemoryProviderSourceGatewayRequest.Create(
            MemoryProviderInstanceId.Parse("provider.programming"),
            protocolSourceRequest,
            AgentCore.MemorySourceKind.WorkbenchProjectStructure,
            ProjectId,
            policy);
        var ingestionJob = MemorySourceIngestionJobRequest.Create(
            MemoryProviderInstanceId.Parse("provider.programming"),
            AgentCore.MemorySourceKind.WorkbenchProjectStructure,
            ProjectId,
            MemorySourceScope.Project,
            policy,
            requestedBy: "user-42");

        Assert.Equal(providerRequest.SourceGatewayRequest.SourceKind, ingestionJob.SourceGatewayRequest.SourceKind);
        Assert.Equal(providerRequest.SourceGatewayRequest.ScopeId, ingestionJob.SourceGatewayRequest.ScopeId);
        Assert.Equal(typeof(AgentCore.MemorySourceSnapshot), providerRequest.ExpectedSnapshotContractType);
        Assert.Equal(typeof(AgentCore.MemorySourceSnapshot), ingestionJob.ExpectedSnapshotContractType);
    }

    private static MemorySourceGatewayRequest CreateGatewayRequest(
        AgentCore.MemorySourceKind sourceKind,
        MemorySourceGatewayPolicy policy)
    {
        return new MemorySourceGatewayRequest(
            sourceKind,
            ScopeId: ProjectId,
            RequestedScope: MemorySourceScope.Project,
            Cursor: null,
            Take: null,
            policy,
            RequesterId: "user-42");
    }

    private static AgentCore.MemorySourceSnapshot CreateWorkbenchSnapshot(bool redacted)
    {
        return CreateWorkbenchSnapshot(redacted, containsSensitivePayload: redacted);
    }

    private static AgentCore.MemorySourceSnapshot CreateWorkbenchSnapshot(
        bool redacted,
        bool containsSensitivePayload)
    {
        var itemId = AgentCore.MemorySourceItemId.Create(
            AgentCore.MemorySourceKind.WorkbenchProjectStructure,
            ProjectId,
            AgentCore.MemorySourceEntityKind.ProjectNode,
            "node-1");
        var content = """{"title":"Payment integration","secret":"[redacted]"}""";
        var contentHash = AgentCore.MemorySourceSnapshotHasher.Compute(content);
        var item = new AgentCore.MemorySourceItem(
            itemId,
            AgentCore.MemorySourceKind.WorkbenchProjectStructure,
            AgentCore.MemorySourceEntityKind.ProjectNode,
            Title: "Payment integration",
            Content: content,
            ContentHash: contentHash,
            CreatedAtUtc: DateTimeOffset.Parse("2026-07-05T12:00:00Z"),
            UpdatedAtUtc: DateTimeOffset.Parse("2026-07-05T12:00:00Z"),
            new AgentCore.MemorySourceProvenance(
                AgentCore.MemorySourceKind.WorkbenchProjectStructure,
                ProjectId,
                AgentCore.MemorySourceEntityKind.ProjectNode,
                SourceEntityId: "node-1",
                SourceRoute: $"/projects/{ProjectId:D}/structure"),
            new AgentCore.MemorySourcePermissionContext(
                redacted ? AgentCore.MemorySourceAccessMode.Redacted : AgentCore.MemorySourceAccessMode.ReadOnly,
                containsSensitivePayload ? AgentCore.MemorySourceSensitivity.Sensitive : AgentCore.MemorySourceSensitivity.Internal,
                ContainsSensitivePayload: containsSensitivePayload,
                RedactionPolicy: redacted ? "secret fields redacted" : "metadata only",
                AllowedFutureUsageSummary: "Source-grounded project structure evidence."),
            Layout: null,
            Links: [],
            References: [],
            StorageReference: null,
            Metadata: new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["sourceModule"] = "workbench"
            });
        var snapshotHash = AgentCore.MemorySourceSnapshotHasher.Compute(item.ContentHash);
        return new AgentCore.MemorySourceSnapshot(
            new AgentCore.MemorySourceSnapshotManifest(
                AgentCore.MemorySourceSnapshotId.Create(
                    AgentCore.MemorySourceKind.WorkbenchProjectStructure,
                    ProjectId,
                    snapshotHash),
                AgentCore.MemorySourceKind.WorkbenchProjectStructure,
                ProjectId,
                DateTimeOffset.Parse("2026-07-05T12:00:00Z"),
                TotalItemCount: 1,
                NextCursor: null,
                HasMore: false,
                AgentCore.MemorySourceSnapshotPageStatus.EndOfSource,
                AgentCore.MemorySourceSnapshotHashScope.FullSnapshot,
                AgentCore.MemorySourceSnapshotProviderVersions.WorkbenchProjectStructure),
            [item]);
    }

    private sealed class FakeSourceGatewayAdapter(AgentCore.MemorySourceSnapshot snapshot) : IMemorySourceGatewayAdapter
    {
        public MemorySourceGatewayAdapterDescriptor Descriptor { get; } = new(
            MemorySourceModuleId.Parse("workbench.project-structure"),
            AgentCore.MemorySourceKind.WorkbenchProjectStructure,
            AgentCore.MemorySourceSnapshotProviderVersions.WorkbenchProjectStructure,
            MemorySourceScope.Project,
            RequiresPermissionCheck: true);

        public int ReadCount { get; private set; }

        public Task<AgentCore.MemorySourceSnapshot> ReadSnapshotAsync(
            MemorySourceGatewayRequest request,
            CancellationToken cancellationToken = default)
        {
            ReadCount++;
            return Task.FromResult(snapshot);
        }
    }
}
