namespace CanDoItAll.Modules.CrmHr;

public sealed record CrmAccountConnectionOriginal(Guid Id, Guid RelatedPartyId, CrmAccountConnectionRole Role,
    bool IsPrimary, string Notes, DateTimeOffset CreatedAtUtc, DateTimeOffset UpdatedAtUtc);

public sealed record CrmAccountConnectionProjectOriginal(Guid Id, Guid AccountConnectionId, Guid ProjectId, DateTimeOffset CreatedAtUtc);

public sealed record CrmAccountConnectionsObservation(Guid AccountPartyId,
    IReadOnlyList<CrmAccountConnectionOriginal> Rows, IReadOnlyList<CrmAccountConnectionProjectOriginal> Projects) {
    public CrmAccountConnectionsObservation Snapshot() => this with { Rows = Rows.ToArray(), Projects = Projects.ToArray() };
}

public sealed record CrmAccountConnectionsSaveReceipt(Guid AccountPartyId,
    IReadOnlyList<CrmAccountConnectionEditorModel> Accepted, CrmAccountConnectionsObservation Original);
