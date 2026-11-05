using CanDoItAll.Modules.Prompts;
using CanDoItAll.Prompts.UI.Editor;

namespace CanDoItAll.Prompts.UiSandbox;

internal static class PromptMergeSandbox {
    public static PromptMergeReview Create(PromptGallerySandboxScenario scenario, PromptGalleryEditorSource original) {
        original = original with { Content = "First line\nMiddle line\nLast line\n", SupportedModels = [] };
        var form = PromptGalleryEditorForm.FromSource(original);
        form.Summary = "My local summary";
        form.Content = "First line\nMiddle line\nMy last line\n";
        var models = new List<PromptProviderModel>();
        if (scenario == PromptGallerySandboxScenario.EditorReviewPreferred) {
            form.SupportedModels = [new("Local provider", "local model", true)];
            models.Add(new("Remote provider", "remote model", true));
        }
        var local = form.Capture(scenario == PromptGallerySandboxScenario.EditorReviewRaw ? "-" : "0.2", "800", "0.9", "Pending provider", "");
        var remote = new PromptGalleryItemDetails(PromptGallerySandboxFixture.FirstItemId, null, null,
            "Current saved name", scenario == PromptGallerySandboxScenario.EditorReviewConflict ? "Another summary" : original.Summary,
            original.Kind, original.Phase, PromptArtifactStatus.Draft, false, "Saved first line\nMiddle line\nLast line\n", 0,
            original.Tags.Append("remote-tag").ToArray(), [], models, original.SupportedConsumers, [], original.Recommendations,
            new(PromptArtifactProvenance.User, null, null, null, null, null, null), [], DateTimeOffset.UnixEpoch, DateTimeOffset.UnixEpoch.AddSeconds(1));
        return new(original, local, remote);
    }
}
