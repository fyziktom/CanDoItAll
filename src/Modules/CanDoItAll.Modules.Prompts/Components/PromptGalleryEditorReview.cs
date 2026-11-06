using CanDoItAll.Prompts.UI.Editor;

namespace CanDoItAll.Modules.Prompts.Components;

public sealed record PromptGalleryEditorReview(long Generation, long HintRevision, PromptMergeReview Fields,
    PromptGalleryDraftCapture OriginalLocal);
