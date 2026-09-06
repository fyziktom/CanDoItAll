using System.Collections.Immutable;
using CanDoItAll.AgentFramework.Models;
using AccessEffect = CanDoItAll.AgentFramework.Capabilities.Abstractions.CapabilityAccessEffect;
using AccessScope = CanDoItAll.AgentFramework.Capabilities.Abstractions.CapabilityAccessScope;
using SelectorKind = CanDoItAll.AgentFramework.Capabilities.Abstractions.CapabilitySelectorKind;

namespace CanDoItAll.Modules.AgentFramework.Pages.Components;

public enum AgentCapabilitiesLoadState {
    Loading,
    Ready,
    Failed
}

public sealed record AgentCapabilitiesSelection(Guid? AgentId);

public sealed record AgentCapabilitiesAgent(Guid Id, string Name, string RoleTitle, string Model, int AssignedCount);

public sealed record AgentCapabilitiesCurator(string Name, string AvatarImageUrl, bool CanLaunch);

public sealed record AgentCapabilityAccessDraft(
    AccessEffect Effect = AccessEffect.Deny,
    AccessScope Scope = AccessScope.UiPreview,
    SelectorKind Selector = SelectorKind.OperationClassification,
    string Value = "externalAction",
    string ServerKey = "",
    string Reason = "UI preview denies matching capabilities.");

public sealed record AgentCapabilityNotice(string Label, string Message, string RepairHint);

public sealed record AgentCapabilityPreview(
    bool IsValid, int AllowedCount, int SuppressedCount,
    ImmutableArray<AgentCapabilityNotice> ValidationIssues,
    ImmutableArray<AgentCapabilityNotice> Diagnostics);

public sealed record AgentCapabilitiesSnapshot(
    ImmutableArray<AgentCapabilitiesAgent> Agents,
    ImmutableArray<CapabilityCatalogItem> Capabilities,
    ImmutableArray<Guid> SelectedCapabilityIds,
    AgentCapabilitiesCurator Curator) {
    public static AgentCapabilitiesSnapshot Empty { get; } = new([], [], [], new(
        CapabilityCuratorAgentIdentity.DefaultDisplayName, CapabilityCuratorAgentIdentity.DefaultAvatarImageUrl, false));

    public string? LoadError { get; init; }
    public AgentCapabilityOperationState? Operation { get; init; }
    public AgentCapabilityPreview? Preview { get; init; }
    public bool IsBusy { get; init; }
    public bool IsAccessPreviewBusy { get; init; }
    public bool IsOpeningCurator { get; init; }
    public CapabilityCuratorLaunchStatus CuratorLaunchStatus { get; init; }
    public bool IsOpeningWizard { get; init; }
}

public abstract record AgentCapabilitiesIntent {
    private AgentCapabilitiesIntent() { }

    public sealed record SelectAgent(Guid AgentId) : AgentCapabilitiesIntent;
    public sealed record ToggleAssignment(Guid CapabilityId) : AgentCapabilitiesIntent;
    public sealed record VerifyCapability(Guid CapabilityId) : AgentCapabilitiesIntent;
    public sealed record OpenDetails(Guid CapabilityId) : AgentCapabilitiesIntent;
    public sealed record CreateCapability(CapabilityKind Kind) : AgentCapabilitiesIntent;
    public sealed record PreviewAccess(AgentCapabilityAccessDraft Draft) : AgentCapabilitiesIntent;
    public sealed record OpenCurator : AgentCapabilitiesIntent;
    public sealed record RetryLoad : AgentCapabilitiesIntent;
    public sealed record RecoverOperation : AgentCapabilitiesIntent;
    public sealed record RetryAssignment : AgentCapabilitiesIntent;
    public sealed record AdoptCurrent : AgentCapabilitiesIntent;
}
