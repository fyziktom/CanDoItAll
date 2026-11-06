using System.Globalization;

namespace CanDoItAll.Prompts.UI.Editor;

public enum PromptDraftField {
    Title, Summary, Kind, Phase, Content, Tags, Models, Consumers, Temperature, MaxOutputTokens, TopP, PendingModel
}

public sealed record PromptGalleryDraftCapture(PromptGalleryEditorSubmission Values, long Revision,
    IReadOnlyList<long> FieldRevisions, string Temperature, string MaxOutputTokens, string TopP,
    string PendingProvider, string PendingModel, string PendingTag = "") {
    public static string Format<T>(T? value) where T : struct, IFormattable
        => value?.ToString(null, CultureInfo.InvariantCulture) ?? string.Empty;
}
