using CanDoItAll.AgentFramework.Models;
using CanDoItAll.SharedKernel;
using System.Collections.Immutable;

namespace CanDoItAll.Modules.CrmHr;

/// <summary>
/// State of the link between an AI agent party in CRM/HR and a technical agent definition of the Agents module, as a
/// JSON integer: 0 Unbound (no technical agent is linked), 1 PendingBackfill (a legacy CRM/HR technical profile waits
/// to be linked to an agent definition), 2 Bound (linked to a technical agent definition), 3 Error (the linked
/// technical agent is not available to CRM/HR).
/// </summary>
public enum AiResourceBindingStatus
{
    Unbound,
    PendingBackfill,
    Bound,
    Error
}

public static class AiAgentDirectoryQueryLimits
{
    public const int DefaultPageSize = 12;
    public const int MaximumPageSize = 100;
    public const int MaximumSearchLength = 200;
}

public sealed record AiAgentDirectoryQuery(
    string SearchText = "",
    AiValidationStatus? ValidationStatus = null,
    int PageIndex = 0,
    int PageSize = AiAgentDirectoryQueryLimits.DefaultPageSize);

public sealed record AiAgentDirectoryGovernanceModel(
    PartyLifecycleStatus LifecycleStatus,
    bool IsSensitive,
    AiResourceBindingStatus BindingStatus,
    string BindingReason,
    AiExecutionMode? ExecutionMode,
    bool HasProfile,
    AiValidationStatus ValidationStatus,
    string OwnerName,
    DateTimeOffset UpdatedAtUtc);

public sealed record AiAgentDirectoryItemModel(
    Guid PartyId,
    AgentDefinition Agent,
    AiAgentDirectoryGovernanceModel Governance,
    ProviderProfile? Provider,
    DateTimeOffset? ProjectionUpdatedAtUtc)
{
    public string ProviderName => Provider?.Name ?? string.Empty;

    public bool IsPrivateProvider => Provider?.IsPrivateProvider == true;

    public AiValidationStatus ValidationStatus => Governance.ValidationStatus;

    public int CapabilityCount => Agent.Capabilities.Count;
}

public sealed record AiAgentDirectoryPage(
    IReadOnlyList<AiAgentDirectoryItemModel> Items,
    int PageIndex,
    int PageSize,
    int TotalCount)
{
    public int TotalPages => TotalCount == 0
        ? 0
        : (int)Math.Ceiling(TotalCount / (double)PageSize);
}

public interface IAiAgentDirectoryQueryService
{
    Task RefreshProjectionAsync(CancellationToken cancellationToken = default);

    Task<AiAgentDirectoryPage> SearchAsync(
        AiAgentDirectoryQuery query,
        CancellationToken cancellationToken = default);

    Task<AiAgentDirectoryItemModel?> GetByPartyIdAsync(
        Guid partyId,
        CancellationToken cancellationToken = default);
}
