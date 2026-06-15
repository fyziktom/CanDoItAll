namespace CanDoItAll.Processes.Drivers.OfficeEvidence;

public sealed record OfficeEvidenceItem(
    OfficeEvidenceItemKind Kind,
    string ItemId,
    string SubjectOrTitle,
    string SenderOrAuthor,
    IReadOnlyList<string> Recipients,
    DateTimeOffset? ObservedAt,
    string Text);

public enum OfficeEvidenceItemKind
{
    EmailMessage = 1,
    Document = 2
}
