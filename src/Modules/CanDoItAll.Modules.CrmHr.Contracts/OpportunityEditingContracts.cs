using CanDoItAll.SharedKernel;
using CanDoItAll.Modules.Projects;

namespace CanDoItAll.Modules.CrmHr;

public sealed record CrmOpportunitySaveReceipt(Guid OpportunityId, CrmOpportunityEditorModel Accepted);
public sealed record CrmOpportunityStageAdvance(Guid OpportunityId, Guid AccountPartyId, OpportunityStage DisplayedStage, DateTimeOffset ExpectedUpdatedAtUtc) {
    public OpportunityStage NextStage => DisplayedStage switch {
        OpportunityStage.Identified => OpportunityStage.Qualified,
        OpportunityStage.Qualified => OpportunityStage.Proposal,
        OpportunityStage.Proposal => OpportunityStage.Negotiation,
        OpportunityStage.Negotiation => OpportunityStage.Won,
        _ => throw new InvalidOperationException("A closed opportunity cannot advance.")
    };
}
public sealed record CrmOpportunityStageReceipt(Guid OpportunityId, OpportunityStage Stage, DateTimeOffset UpdatedAtUtc);

public sealed record CrmOpportunityConversionProjectBasis(Guid OpportunityId, DateTimeOffset OpportunityUpdatedAtUtc,
    ProjectWriteAdmission Project, IReadOnlyList<CrmOpportunityAssignmentOriginal> Assignments) {
    public string ProjectName { get; init; } = string.Empty;
    public IReadOnlyDictionary<Guid, string> PartyNames { get; init; } = new Dictionary<Guid, string>();
}

public sealed record CrmOpportunityAssignmentOriginal(Guid Id, Guid ProjectId, Guid? ProjectLifetimeId, Guid PartyId,
    Guid? PartyOrganizationAffiliationId, ProjectPartyAssignmentKind AssignmentKind, string NodeKey, string PhaseName,
    Guid? OpportunityId, decimal? AllocationPercent, DateTimeOffset? StartsAtUtc, DateTimeOffset? EndsAtUtc,
    bool IsPrimary, string Source, string Notes);

public interface ICrmOpportunityEditorService {
    Task<CrmOpportunityDetailModel?> GetOpportunityForEditingAsync(Guid opportunityId, CancellationToken cancellationToken = default,
        int? maximumPartyLinks = null);
    Task<Result<CrmOpportunitySaveReceipt>> SaveOpportunityWithReceiptAsync(CrmOpportunityEditorModel model,
        CancellationToken cancellationToken = default, Action<CrmOpportunitySaveReceipt>? onCommitted = null);
}

public interface ICrmOpportunityConversionService {
    Task<CrmOpportunityDetailModel?> GetOpportunityForEditingAsync(Guid opportunityId, CancellationToken cancellationToken = default,
        int? maximumPartyLinks = null);
    Task<Result<CrmOpportunityConversionProjectBasis>> ObserveOpportunityProjectBasisAsync(Guid opportunityId,
        DateTimeOffset expectedOpportunityUpdatedAtUtc, ProjectWriteAdmission project, CancellationToken cancellationToken = default);
    Task<Result<CrmOpportunityConversionResult>> ConvertOpportunityToProjectAsync(CrmOpportunityConversionEditorModel model,
        CancellationToken cancellationToken = default, Action<CrmOpportunityConversionResult>? onCommitted = null);
}

public sealed class OpportunityReadLimitExceededException(int maximumRows)
    : InvalidOperationException("This opportunity exceeds the automatic linked-party read budget. Request a complete comparison explicitly.") {
    public int MaximumRows { get; } = maximumRows;
}
