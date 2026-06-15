namespace CanDoItAll.Processes.Drivers.BusinessAnalysis;

public sealed record BusinessAnalysisEvidenceItem(
    BusinessAnalysisEvidenceItemKind Kind,
    string ItemId,
    string Title,
    string Text,
    DateTimeOffset? ObservedAt);

public enum BusinessAnalysisEvidenceItemKind
{
    Deliverable = 1,
    SupportingEvidence = 2
}
