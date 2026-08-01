using CanDoItAll.Memory.Abstractions;

namespace CanDoItAll.Modules.Memory.Services;

public sealed class MemoryQueryEditorModel
{
    public string Query { get; set; } = "payment integration";

    public bool UseAsyncQuery { get; set; }

    public string SourceModule { get; set; } = nameof(MemorySourceKind.Project);

    public string SourceRecordId { get; set; } = "project-1";

    public string Citation { get; set; } = "Project 1";
}

public sealed class MemoryFeedbackEditorModel
{
    public string ContextPackId { get; set; } = string.Empty;

    public MemoryFeedbackOutcome Outcome { get; set; } = MemoryFeedbackOutcome.Useful;

    public MemoryFeedbackStage Stage { get; set; } = MemoryFeedbackStage.ContextUsed;

    public string Comment { get; set; } = string.Empty;
}

public sealed class MemoryManualIngestionEditorModel
{
    public string Title { get; set; } = "Manual memory note";

    public string ContentText { get; set; } = "Payment integration context from the generic memory UI.";

    public string SourceCategory { get; set; } = "Manual";

    public string Tags { get; set; } = "memory-ui";
}
