using CanDoItAll.Memory.Abstractions;
using CanDoItAll.Memory.Application;

namespace CanDoItAll.Memory.Tests;

public sealed class MemoryProviderRegistryTests
{
    private static readonly MemoryCapabilityId SyncQuery = MemoryCapabilityId.Parse("context.query.sync");
    private static readonly MemoryCapabilityId AsyncQuery = MemoryCapabilityId.Parse("context.query.async");
    private static readonly MemoryCapabilityId SnapshotIngestion = MemoryCapabilityId.Parse("ingestion.snapshot");

    [Fact]
    public void SB02_PR001_Zero_provider_selection_returns_typed_no_provider_without_dispatch()
    {
        var registry = new InMemoryMemoryProviderRegistry([]);

        var result = registry.SelectProvider(MemoryProviderSelectionPolicy.RequireCapability(SyncQuery), MemoryProviderSelectionContext.None);

        Assert.Equal(MemoryProviderSelectionStatus.NoProviderConfigured, result.Status);
        Assert.False(result.DispatchAllowed);
        Assert.Null(result.SelectedProvider);
        Assert.Contains("No memory provider is configured", result.Diagnostic);
    }

    [Fact]
    public void SB02_PR002_One_enabled_provider_is_selected_when_capability_matches()
    {
        var provider = CreateProfile("programming-memory", [SyncQuery]);
        var registry = new InMemoryMemoryProviderRegistry([provider]);

        var result = registry.SelectProvider(MemoryProviderSelectionPolicy.RequireCapability(SyncQuery), MemoryProviderSelectionContext.None);

        Assert.Equal(MemoryProviderSelectionStatus.Selected, result.Status);
        Assert.True(result.DispatchAllowed);
        Assert.Equal(provider.InstanceId, result.SelectedProvider?.InstanceId);
        Assert.Equal(MemoryProviderSelectionReason.DefaultProvider, result.Reason);
    }

    [Fact]
    public void SB02_PR003_Two_role_specific_providers_do_not_cross_select()
    {
        var programming = CreateProfile("programming-memory", [SyncQuery], tags: ["programming"]);
        var business = CreateProfile("business-memory", [SyncQuery], tags: ["business"]);
        var registry = new InMemoryMemoryProviderRegistry([programming, business]);
        var policy = MemoryProviderSelectionPolicy.RequireCapability(SyncQuery) with
        {
            Assignments =
            [
                new MemoryProviderAssignment(MemoryProviderAssignmentScope.AgentRole, "developer", programming.InstanceId),
                new MemoryProviderAssignment(MemoryProviderAssignmentScope.AgentRole, "business-analyst", business.InstanceId)
            ]
        };

        var developerResult = registry.SelectProvider(policy, new MemoryProviderSelectionContext(AgentId: "agent-dev", AgentRole: "developer", WorkflowId: null, WorkflowNodeId: null, ProcessId: null));
        var analystResult = registry.SelectProvider(policy, new MemoryProviderSelectionContext(AgentId: "agent-ba", AgentRole: "business-analyst", WorkflowId: null, WorkflowNodeId: null, ProcessId: null));

        Assert.Equal(programming.InstanceId, developerResult.SelectedProvider?.InstanceId);
        Assert.Equal(business.InstanceId, analystResult.SelectedProvider?.InstanceId);
        Assert.Equal(MemoryProviderSelectionReason.AssignmentOverride, developerResult.Reason);
        Assert.Equal(MemoryProviderSelectionReason.AssignmentOverride, analystResult.Reason);
    }

    [Fact]
    public void SB02_PR004_Disabled_explicit_provider_does_not_fall_back_to_other_provider()
    {
        var disabled = CreateProfile("disabled-memory", [SyncQuery], isEnabled: false);
        var enabled = CreateProfile("enabled-memory", [SyncQuery]);
        var registry = new InMemoryMemoryProviderRegistry([disabled, enabled]);
        var policy = MemoryProviderSelectionPolicy.RequireCapability(SyncQuery) with
        {
            ExplicitProviderId = disabled.InstanceId
        };

        var result = registry.SelectProvider(policy, MemoryProviderSelectionContext.None);

        Assert.Equal(MemoryProviderSelectionStatus.ProviderDisabled, result.Status);
        Assert.False(result.DispatchAllowed);
        Assert.Null(result.SelectedProvider);
        Assert.DoesNotContain(enabled.InstanceId.Value, result.Diagnostic);
    }

    [Fact]
    public void SB02_PR005_Unsupported_capability_fails_before_dispatch()
    {
        var provider = CreateProfile("programming-memory", [SyncQuery]);
        var registry = new InMemoryMemoryProviderRegistry([provider]);

        var result = registry.SelectProvider(MemoryProviderSelectionPolicy.RequireCapability(SnapshotIngestion), MemoryProviderSelectionContext.None);

        Assert.Equal(MemoryProviderSelectionStatus.CapabilityUnavailable, result.Status);
        Assert.False(result.DispatchAllowed);
        Assert.Contains("ingestion.snapshot", result.Diagnostic);
    }

    [Fact]
    public void SB02_PR006_Denied_capability_policy_fails_before_provider_selection()
    {
        var provider = CreateProfile("programming-memory", [SyncQuery]);
        var registry = new InMemoryMemoryProviderRegistry([provider]);
        var policy = MemoryProviderSelectionPolicy.RequireCapability(SyncQuery) with
        {
            DeniedCapabilities = [SyncQuery]
        };

        var result = registry.SelectProvider(policy, MemoryProviderSelectionContext.None);

        Assert.Equal(MemoryProviderSelectionStatus.CapabilityDenied, result.Status);
        Assert.False(result.DispatchAllowed);
        Assert.Null(result.SelectedProvider);
        Assert.Empty(result.CandidateProviderIds);
    }

    [Fact]
    public void SB02_PR007_All_disabled_providers_return_no_enabled_provider_without_mock_fallback()
    {
        var registry = new InMemoryMemoryProviderRegistry(
        [
            CreateProfile("mock-memory", [SyncQuery, AsyncQuery], MemoryProviderDriverKind.Mock, isEnabled: false),
            CreateProfile("native-memory", [SyncQuery], MemoryProviderDriverKind.NativeRemote, isEnabled: false)
        ]);

        var result = registry.SelectProvider(MemoryProviderSelectionPolicy.RequireCapability(SyncQuery), MemoryProviderSelectionContext.None);

        Assert.Equal(MemoryProviderSelectionStatus.NoEnabledProvider, result.Status);
        Assert.False(result.DispatchAllowed);
        Assert.Null(result.SelectedProvider);
        Assert.Contains("No enabled memory provider", result.Diagnostic);
    }

    private static MemoryProviderProfile CreateProfile(
        string id,
        IReadOnlyList<MemoryCapabilityId> capabilities,
        MemoryProviderDriverKind driverKind = MemoryProviderDriverKind.Http,
        bool isEnabled = true,
        IReadOnlyList<string>? tags = null)
    {
        return new MemoryProviderProfile(
            InstanceId: MemoryProviderInstanceId.Parse(id),
            DisplayName: id,
            DriverKind: driverKind,
            IsEnabled: isEnabled,
            HealthState: MemoryProviderHealthState.Healthy,
            WorkspaceScope: MemoryProviderWorkspaceScope.AllWorkspaces,
            SelectionTags: tags ?? [],
            DefaultPolicy: MemoryProviderProfilePolicy.Default,
            Manifest: new MemoryProviderManifest(
                ProviderKind: ResolveProviderKind(driverKind),
                ProtocolVersion: MemoryProtocolVersion.Current,
                Capabilities: capabilities.Select(capability => new MemoryCapabilityDescriptor(capability, "1.0", Supported: true)).ToArray(),
                InteractionSupport: new MemoryProviderInteractionSupport(
                    SupportsSynchronousQueries: capabilities.Contains(SyncQuery),
                    SupportsAsynchronousOperations: capabilities.Contains(AsyncQuery),
                    SupportsSourceRequests: capabilities.Contains(SnapshotIngestion),
                    SupportsFeedback: capabilities.Any(capability => capability.Value.StartsWith("feedback.", StringComparison.Ordinal)),
                    SupportsProviderEvents: capabilities.Any(capability => capability.Value.StartsWith("events.", StringComparison.Ordinal))),
                UiSurfaces: [],
                Limits: MemoryProviderLimits.Default,
                Extensions: MemoryExtensionData.Empty));
    }

    private static MemoryProviderKind ResolveProviderKind(MemoryProviderDriverKind driverKind)
    {
        return driverKind switch
        {
            MemoryProviderDriverKind.Http => MemoryProviderKind.Parse("memory.http"),
            MemoryProviderDriverKind.Mcp => MemoryProviderKind.Parse("memory.mcp"),
            MemoryProviderDriverKind.NativeRemote => MemoryProviderKind.Parse("memory.native-remote"),
            MemoryProviderDriverKind.Mock => MemoryProviderKind.Parse("memory.mock"),
            MemoryProviderDriverKind.InProcessMigration => MemoryProviderKind.Parse("memory.in-process-migration"),
            _ => throw new ArgumentOutOfRangeException(nameof(driverKind), driverKind, "Unsupported memory provider driver kind.")
        };
    }
}
