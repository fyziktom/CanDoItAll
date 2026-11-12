namespace CanDoItAll.Workbench.Content.UI;

public enum ContentTextKind { Text, Json, Markdown, Mermaid, Log }
public enum ContentTextSource { CreateNew, UploadExisting }
public enum ContentTextOutcomeKind { Prepared, Committed, Rejected, PartialCommit, Unconfirmed }

public sealed record ContentTextDefinition(
    ContentTextKind Kind,
    string Label,
    string TitleLabel,
    string TitlePlaceholder,
    string SubtitleLabel,
    string SubtitlePlaceholder,
    string NotesLabel,
    string NotesPlaceholder,
    string FileNamePlaceholder,
    string ContentPlaceholder,
    string AcceptedFileTypes,
    string FilePrompt,
    string SubmitLabel,
    int MaximumUploadBytes);

public sealed record ContentTextInitialDraft(string Title, string Subtitle, string Notes);
public sealed record ContentTextUpload(string FileName, string ContentType, long DeclaredSize, ReadOnlyMemory<byte> Bytes);
public sealed record ContentTextSubmission(Guid SubmissionId, ContentTextSource Source, string Title, string Subtitle,
    string Notes, string FileName, string Content, ContentTextUpload? Upload);
public sealed record ContentAssetReceipt(string NodeId, Guid? RecordId, string FileName);
public sealed record ContentTextOutcome(ContentTextOutcomeKind Kind, string Message, ContentAssetReceipt? Receipt = null) {
    public bool IsAccepted => Kind is ContentTextOutcomeKind.Prepared or ContentTextOutcomeKind.Committed or ContentTextOutcomeKind.PartialCommit;
    public bool RequiresObservation => Kind is ContentTextOutcomeKind.Committed or ContentTextOutcomeKind.PartialCommit or ContentTextOutcomeKind.Unconfirmed;
}
