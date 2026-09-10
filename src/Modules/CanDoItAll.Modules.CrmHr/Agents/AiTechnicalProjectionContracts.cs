using System.Collections.Immutable;
using CanDoItAll.AgentFramework.Models;

namespace CanDoItAll.Modules.CrmHr;

public sealed record AiTechnicalProjectionSource {
    public AiTechnicalProjectionSource(Guid databaseProfileId, WorkspaceScopeDescriptor scope) {
        if (databaseProfileId == Guid.Empty) {
            throw new ArgumentException("A pinned database profile is required.", nameof(databaseProfileId));
        }

        ArgumentNullException.ThrowIfNull(scope);
        if (scope.Kind != WorkspaceScopeKind.Organization || string.IsNullOrWhiteSpace(scope.Key) || scope.Key.Length > 200) {
            throw new ArgumentException("An organization catalog scope is required.", nameof(scope));
        }

        DatabaseProfileId = databaseProfileId;
        Scope = new(scope.Kind, scope.Key);
    }

    public Guid DatabaseProfileId { get; }
    public WorkspaceScopeDescriptor Scope { get; }
}

public enum AiTechnicalProjectionAvailability {
    Unknown,
    Present,
    Missing,
    Superseded
}

public enum AiTechnicalProjectionApplyDisposition {
    Applied,
    Replayed,
    Stale
}

public sealed record AiTechnicalProjectionCapability(string Name, string Scope, string ToolAccess, string Limitations, string Notes);

public sealed record AiTechnicalProjectionEntry(
    Guid TechnicalAgentId,
    Guid? PreferredPartyId,
    string DisplayName,
    string Summary,
    AgentLifecycleStatus LifecycleStatus,
    AiExecutionMode? ExecutionMode,
    string ProviderName,
    string DefaultModel,
    string RoleTitle,
    string Instructions,
    string TemplateKey,
    ImmutableArray<string> Tags,
    ImmutableArray<AiTechnicalProjectionCapability> Capabilities);

public sealed record AiTechnicalCatalogProjection(
    AiTechnicalProjectionSource Source,
    CatalogDataRevision Revision,
    ImmutableArray<AiTechnicalProjectionEntry> Agents);

public sealed record AiTechnicalProjectionApplyResult(AiTechnicalProjectionApplyDisposition Disposition, CatalogDataRevision CurrentRevision);

public sealed record AiTechnicalProjectionProvenance(
    AiTechnicalProjectionSource Source,
    CatalogDataRevision Revision,
    AiTechnicalProjectionAvailability Availability,
    string DisplayName,
    string Summary,
    AgentLifecycleStatus? LifecycleStatus);

public sealed record AiTechnicalProjectionResource(
    Guid PartyId,
    PartyType? PartyType,
    string DisplayName,
    string Summary,
    AiTechnicalAgentDirectorySummary Directory,
    AiAgentStaffingFactModel Staffing);

public sealed record AiTechnicalBindingReference(Guid PartyId, Guid TechnicalAgentId);

public sealed record AiTechnicalCatalogRepairFacts(ImmutableArray<Guid> AiPartyIds, ImmutableArray<AiTechnicalBindingReference> Bindings);

public class AiTechnicalAgentCommittedSaveException(Guid technicalAgentId, Guid? partyId, Exception innerException)
    : Exception($"Technical Agent '{technicalAgentId:D}' was saved, but a subsequent CRM operation failed.", innerException) {
    public Guid TechnicalAgentId { get; } = technicalAgentId;
    public Guid? PartyId { get; } = partyId;
}

public interface IAiTechnicalAgentProjectionStore {
    Task<AiTechnicalProjectionApplyResult> ApplyAsync(AiTechnicalCatalogProjection projection, CancellationToken cancellationToken = default);
    Task<IReadOnlyDictionary<Guid, AiTechnicalProjectionResource>> ReadAsync(IReadOnlyList<Guid> partyIds, CancellationToken cancellationToken = default);
    Task<int> CountBoundAsync(CancellationToken cancellationToken = default);
    Task<AiTechnicalCatalogRepairFacts> ReadCatalogRepairFactsAsync(CancellationToken cancellationToken = default);
}
