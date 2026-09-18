using CanDoItAll.Modules.Prompts;
using CanDoItAll.Prompts.UI.Editor;

namespace CanDoItAll.Tests.Unit.Prompts;

public sealed class PromptGalleryEditorFormTests
{
    [Fact]
    public void Submission_snapshot_is_independent_of_later_edits_including_nested_collections()
    {
        var form = PromptGalleryEditorForm.FromSource(new PromptGalleryEditorSource(
            "Title",
            "Summary",
            PromptGalleryItemKind.Part,
            "phase",
            "Content",
            ["one"],
            [new PromptProviderModel("OpenAI", "gpt-5.4-mini", IsPreferred: true)],
            [PromptGalleryConsumer.Chat],
            new PromptModelRecommendations(0.5, 100, 0.7)));

        var submission = form.ToSubmission();
        form.Title = "Changed";
        form.Tags = ["one", "two"];
        form.SetConsumer(PromptGalleryConsumer.Workflow, enabled: true);
        Assert.Null(form.TryAddSupportedModel("Ollama", "llama3"));
        form.Temperature = 0.9;

        Assert.Equal("Title", submission.Title);
        Assert.Equal(["one"], submission.Tags);
        Assert.Equal([PromptGalleryConsumer.Chat], submission.SupportedConsumers);
        Assert.Single(submission.SupportedModels);
        Assert.Equal(0.5, submission.Recommendations.Temperature);
        Assert.Equal(["one", "two"], form.Tags);
        Assert.Equal(2, form.SupportedModels.Count);
    }

    [Fact]
    public void Supported_models_are_deduplicated_ordered_and_keep_one_preferred_pair()
    {
        var form = new PromptGalleryEditorForm();

        Assert.NotNull(form.TryAddSupportedModel(" ", "model"));
        Assert.Null(form.TryAddSupportedModel(" OpenAI ", " gpt-5.4-mini "));
        Assert.NotNull(form.TryAddSupportedModel("openai", "GPT-5.4-MINI"));
        Assert.Null(form.TryAddSupportedModel("Anthropic", "claude"));

        Assert.Equal(2, form.SupportedModels.Count);
        Assert.Equal(new PromptProviderModel("OpenAI", "gpt-5.4-mini", IsPreferred: true), form.SupportedModels[0]);
        Assert.False(form.SupportedModels[1].IsPreferred);

        form.SetPreferredModel(form.SupportedModels[1], preferred: true);
        Assert.Equal("Anthropic", form.SupportedModels[0].Provider);
        Assert.True(form.SupportedModels[0].IsPreferred);
        Assert.Single(form.SupportedModels, model => model.IsPreferred);

        form.RemoveSupportedModel(form.SupportedModels[0]);
        Assert.Equal("OpenAI", Assert.Single(form.SupportedModels).Provider);
    }

    [Fact]
    public void Required_values_need_a_name_and_content()
    {
        var form = new PromptGalleryEditorForm { Title = "Name" };
        Assert.False(form.HasRequiredValues);

        form.Content = "   ";
        Assert.False(form.HasRequiredValues);

        form.Content = "Instruction";
        Assert.True(form.HasRequiredValues);
    }

    [Fact]
    public void Consumers_are_kept_distinct_and_ordered()
    {
        var form = new PromptGalleryEditorForm();

        form.SetConsumer(PromptGalleryConsumer.Chat, enabled: true);
        form.SetConsumer(PromptGalleryConsumer.Workflow, enabled: true);
        form.SetConsumer(PromptGalleryConsumer.Chat, enabled: true);

        Assert.Equal([PromptGalleryConsumer.Workflow, PromptGalleryConsumer.Chat], form.SupportedConsumers);

        form.SetConsumer(PromptGalleryConsumer.Workflow, enabled: false);
        Assert.Equal([PromptGalleryConsumer.Chat], form.SupportedConsumers);
    }
}
