using CanDoItAll.Memory.Abstractions;
using CanDoItAll.Memory.Application;

namespace CanDoItAll.Memory.Tests.Providers;

public sealed class MemoryProviderRegistryTests
{
    private static readonly MemoryCapabilityId SyncQuery = MemoryCapabilityId.Parse("context.query.sync");
    private static readonly MemoryCapabilityId AsyncQuery = MemoryCapabilityId.Parse("context.query.async");
    private static readonly MemoryCapabilityId SnapshotIngestion = MemoryCapabilityId.Parse("ingestion.snapshot");

    [Fact]
    public void PR001_Zero_provider_selection_returns_typed_no_provider_without_dispatch()
    {
        var registry = new InMemoryMemoryProviderRegistry([]);

        var result = registry.SelectProvider(MemoryProviderSelectionPolicy.RequireCapability(SyncQuery), MemoryProviderSelectionContext.None);

        Assert.Equal(MemoryProviderSelectionStatus.NoProviderConfigured, result.Status);
        Assert.False(result.DispatchAllowed);
        Assert.Null(result.SelectedProvider);
        Assert.Contains("No memory provider is configured", result.Diagnostic);
    }

    [Fact]
    public void PR002_One_enabled_provider_is_selected_only_when_explicitly_requested()
    {
        var provider = CreateProfile("programming-memory", [SyncQuery]);
        var registry = new InMemoryMemoryProviderRegistry([provider]);

        var policy = MemoryProviderSelectionPolicy.RequireCapability(SyncQuery) with
        {
            ExplicitProviderId = provider.InstanceId
        };

        var result = registry.SelectProvider(policy, MemoryProviderSelectionContext.None);

        Assert.Equal(MemoryProviderSelectionStatus.Selected, result.Status);
        Assert.True(result.DispatchAllowed);
        Assert.Equal(provider.InstanceId, result.SelectedProvider?.InstanceId);
        Assert.Equal(MemoryProviderSelectionReason.ExplicitProvider, result.Reason);
    }

    [Fact]
    public void PR003_Two_role_specific_providers_do_not_cross_select()
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
    public void PR004_Disabled_explicit_provider_does_not_fall_back_to_other_provider()
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
    public void PR005_Unsupported_capability_fails_before_dispatch()
    {
        var provider = CreateProfile("programming-memory", [SyncQuery]);
        var registry = new InMemoryMemoryProviderRegistry([provider]);

        var policy = MemoryProviderSelectionPolicy.RequireCapability(SnapshotIngestion) with
        {
            ExplicitProviderId = provider.InstanceId
        };

        var result = registry.SelectProvider(policy, MemoryProviderSelectionContext.None);

        Assert.Equal(MemoryProviderSelectionStatus.CapabilityUnavailable, result.Status);
        Assert.False(result.DispatchAllowed);
        Assert.Contains("ingestion.snapshot", result.Diagnostic);
    }

    [Fact]
    public void PR006_Denied_capability_policy_fails_before_provider_selection()
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
    public void PR007_All_disabled_providers_return_no_enabled_provider_without_mock_fallback()
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

    [Fact]
    public void PR008_Deny_implicit_fallback_never_selects_an_unassigned_compatible_provider()
    {
        var first = CreateProfile("first-memory", [SyncQuery]);
        var second = CreateProfile("second-memory", [SyncQuery]);
        var registry = new InMemoryMemoryProviderRegistry([first, second]);
        var policy = MemoryProviderSelectionPolicy.RequireCapability(SyncQuery) with
        {
            FallbackBehavior = MemoryProviderFallbackBehavior.DenyImplicitFallback
        };

        var result = registry.SelectProvider(policy, MemoryProviderSelectionContext.None);

        Assert.False(result.DispatchAllowed);
        Assert.Null(result.SelectedProvider);
        Assert.Equal(MemoryProviderSelectionStatus.ProviderSelectionRequired, result.Status);
        Assert.Equal([first.InstanceId, second.InstanceId], result.CandidateProviderIds);
    }

    [Fact]
    public void PR009_Explicit_provider_outside_allowed_provider_ids_is_denied()
    {
        var allowed = CreateProfile("allowed-memory", [SyncQuery]);
        var denied = CreateProfile("denied-memory", [SyncQuery]);
        var registry = new InMemoryMemoryProviderRegistry([allowed, denied]);
        var policy = MemoryProviderSelectionPolicy.RequireCapability(SyncQuery) with
        {
            ExplicitProviderId = denied.InstanceId,
            AllowedProviderIds = [allowed.InstanceId]
        };

        var result = registry.SelectProvider(policy, MemoryProviderSelectionContext.None);

        Assert.Equal(MemoryProviderSelectionStatus.ProviderDenied, result.Status);
        Assert.False(result.DispatchAllowed);
        Assert.Null(result.SelectedProvider);
        Assert.Equal([denied.InstanceId], result.CandidateProviderIds);
    }

    [Fact]
    public void PR010_Assignment_outside_allowed_provider_ids_is_denied()
    {
        var allowed = CreateProfile("allowed-memory", [SyncQuery]);
        var denied = CreateProfile("denied-memory", [SyncQuery]);
        var registry = new InMemoryMemoryProviderRegistry([allowed, denied]);
        var policy = MemoryProviderSelectionPolicy.RequireCapability(SyncQuery) with
        {
            AllowedProviderIds = [allowed.InstanceId],
            Assignments =
            [
                new MemoryProviderAssignment(
                    MemoryProviderAssignmentScope.Agent,
                    "agent-dev",
                    denied.InstanceId)
            ]
        };

        var result = registry.SelectProvider(
            policy,
            new MemoryProviderSelectionContext(
                AgentId: "agent-dev",
                AgentRole: null,
                WorkflowId: null,
                WorkflowNodeId: null,
                ProcessId: null));

        Assert.Equal(MemoryProviderSelectionStatus.ProviderDenied, result.Status);
        Assert.False(result.DispatchAllowed);
        Assert.Null(result.SelectedProvider);
    }

    [Fact]
    public void PR011_Allowed_provider_ids_do_not_create_an_implicit_selection()
    {
        var allowed = CreateProfile("allowed-memory", [SyncQuery]);
        var excluded = CreateProfile("excluded-memory", [SyncQuery]);
        var registry = new InMemoryMemoryProviderRegistry([excluded, allowed]);
        var policy = MemoryProviderSelectionPolicy.RequireCapability(SyncQuery) with
        {
            AllowedProviderIds = [allowed.InstanceId]
        };

        var result = registry.SelectProvider(policy, MemoryProviderSelectionContext.None);

        Assert.Equal(MemoryProviderSelectionStatus.ProviderSelectionRequired, result.Status);
        Assert.False(result.DispatchAllowed);
        Assert.Null(result.SelectedProvider);
        Assert.Equal([allowed.InstanceId], result.CandidateProviderIds);
    }

    [Fact]
    public void PR012_Default_provider_is_selected_only_when_named_by_policy()
    {
        var first = CreateProfile("first-memory", [SyncQuery]);
        var second = CreateProfile("second-memory", [SyncQuery]) with
        {
            DefaultPolicy = new MemoryProviderProfilePolicy(
                MemoryProviderFallbackBehavior.AllowDefaultProviderWhenNoAssignment)
        };
        var registry = new InMemoryMemoryProviderRegistry([first, second]);
        var policy = MemoryProviderSelectionPolicy.RequireCapability(SyncQuery) with
        {
            DefaultProviderId = second.InstanceId,
            FallbackBehavior = MemoryProviderFallbackBehavior.AllowDefaultProviderWhenNoAssignment
        };

        var result = registry.SelectProvider(policy, MemoryProviderSelectionContext.None);

        Assert.Equal(MemoryProviderSelectionStatus.Selected, result.Status);
        Assert.Equal(second.InstanceId, result.SelectedProvider?.InstanceId);
        Assert.Equal(MemoryProviderSelectionReason.DefaultProvider, result.Reason);
    }

    [Fact]
    public void PR013_Default_provider_is_rejected_when_implicit_fallback_is_denied()
    {
        var provider = CreateProfile("default-memory", [SyncQuery]);
        var registry = new InMemoryMemoryProviderRegistry([provider]);
        var policy = MemoryProviderSelectionPolicy.RequireCapability(SyncQuery) with
        {
            DefaultProviderId = provider.InstanceId,
            FallbackBehavior = MemoryProviderFallbackBehavior.DenyImplicitFallback
        };

        var result = registry.SelectProvider(policy, MemoryProviderSelectionContext.None);

        Assert.Equal(MemoryProviderSelectionStatus.ProviderSelectionRequired, result.Status);
        Assert.False(result.DispatchAllowed);
        Assert.Null(result.SelectedProvider);
        Assert.Contains("fallback is denied", result.Diagnostic, StringComparison.Ordinal);
    }

    [Fact]
    public void PR014_Single_workspace_provider_is_rejected_without_a_scope_key()
    {
        var provider = CreateProfile("scoped-memory", [SyncQuery]) with
        {
            WorkspaceScope = MemoryProviderWorkspaceScope.SingleWorkspace
        };
        var registry = new InMemoryMemoryProviderRegistry([provider]);
        var policy = MemoryProviderSelectionPolicy.RequireCapability(SyncQuery) with
        {
            ExplicitProviderId = provider.InstanceId
        };

        var result = registry.SelectProvider(policy, MemoryProviderSelectionContext.None);

        Assert.Equal(MemoryProviderSelectionStatus.ProviderDenied, result.Status);
        Assert.False(result.DispatchAllowed);
        Assert.Null(result.SelectedProvider);
        Assert.Empty(registry.GetProvidersForCapability(SyncQuery));
        Assert.Contains("cannot be validated", result.Diagnostic, StringComparison.Ordinal);
    }

    [Fact]
    public void PR015_Default_provider_requires_provider_profile_opt_in()
    {
        var provider = CreateProfile("default-memory", [SyncQuery]);
        var registry = new InMemoryMemoryProviderRegistry([provider]);
        var policy = MemoryProviderSelectionPolicy.RequireCapability(SyncQuery) with
        {
            DefaultProviderId = provider.InstanceId,
            FallbackBehavior = MemoryProviderFallbackBehavior.AllowDefaultProviderWhenNoAssignment
        };

        var result = registry.SelectProvider(policy, MemoryProviderSelectionContext.None);

        Assert.Equal(MemoryProviderSelectionStatus.ProviderDenied, result.Status);
        Assert.False(result.DispatchAllowed);
        Assert.Contains("does not allow", result.Diagnostic, StringComparison.Ordinal);
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
